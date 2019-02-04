﻿// Copyright (c) 2002-2019 "Neo4j,"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Neo4j.Driver.Internal.IO;
using Neo4j.Driver.Internal.Messaging;
using Neo4j.Driver.Internal.Metrics;
using Neo4j.Driver.Internal.Protocol;
using Neo4j.Driver.Internal.Routing;
using Neo4j.Driver;

namespace Neo4j.Driver.Internal.Connector
{
    internal class SocketClient : ISocketClient
    {
        private const string MessagePattern = "C: {0}";
        private readonly Uri _uri;
        private readonly BufferSettings _bufferSettings;

        public IMessageReader Reader { get; private set; }
        public IMessageWriter Writer { get; private set; }
        private readonly ITcpSocketClient _tcpSocketClient;

        private int _closedMarker = -1;

        private readonly IDriverLogger _logger;
        private readonly IConnectionPoolListener _connMetricsListener;
        private readonly IListenerEvent _connEvent;

        public SocketClient(Uri uri, SocketSettings socketSettings, BufferSettings bufferSettings,
            IConnectionPoolListener connMetricsListener = null, IDriverLogger logger = null, ITcpSocketClient socketClient = null)
        {
            _uri = uri;
            _logger = logger;
            _bufferSettings = bufferSettings;
            _tcpSocketClient = socketClient ?? new TcpSocketClient(socketSettings, _logger);

            _connMetricsListener = connMetricsListener;
            if (_connMetricsListener != null)
            {
                _connEvent = new SimpleTimerEvent(Driver);
            }
        }

        // For testing only
        internal SocketClient(IMessageReader reader, IMessageWriter writer, ITcpSocketClient socketClient = null)
        {
            Reader = reader;
            Writer = writer;
            _tcpSocketClient = socketClient;
        }

        public bool IsOpen => _closedMarker == 0;
        private bool IsClosed => _closedMarker > 0;

        public IBoltProtocol Connect()
        {
            _connMetricsListener?.BeforeCreating(_connEvent);
            _tcpSocketClient.Connect(_uri);

            SetOpened();
            _logger?.Debug($"~~ [CONNECT] {_uri}");
            _connMetricsListener?.AfterCreated(_connEvent);

            var version = DoHandshake();
            return SelectBoltProtocol(version);
        }

        public Task<IBoltProtocol> ConnectAsync()
        {
            TaskCompletionSource<IBoltProtocol> tcs = new TaskCompletionSource<IBoltProtocol>();

            _connMetricsListener?.BeforeCreating(_connEvent);
            _tcpSocketClient.ConnectAsync(_uri)
                .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            tcs.SetException(t.Exception.GetBaseException());
                        }
                        else if (t.IsCanceled)
                        {
                            tcs.SetCanceled();
                        }
                        else
                        {
                            SetOpened();
                            _logger?.Debug($"~~ [CONNECT] {_uri}");
                            _connMetricsListener?.AfterCreated(_connEvent);
                            return DoHandshakeAsync();
                        }
                        return Task.FromResult(-1);
                    }, TaskContinuationOptions.ExecuteSynchronously).Unwrap()
                .ContinueWith(t =>
                {
                    int version = t.Result;

                    if (version != -1)
                    {
                        try
                        {
                            tcs.SetResult(SelectBoltProtocol(version));
                        }
                        catch (AggregateException exc)
                        {
                            tcs.SetException(exc.GetBaseException());
                        }
                        catch (Exception exc)
                        {
                            tcs.SetException(exc);
                        }
                    }   
                }, TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }

        public void Send(IEnumerable<IRequestMessage> messages)
        {
            try
            {
                foreach (var message in messages)
                {
                    Writer.Write(message);
                    LogDebug(MessagePattern, message);
                }
                Writer.Flush();
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Unable to send message to server {_uri}, connection will be terminated.");
                Stop();
                throw;
            }
        }

        public async Task SendAsync(IEnumerable<IRequestMessage> messages)
        {
            try
            {
                foreach (var message in messages)
                {
                    Writer.Write(message);
                    LogDebug(MessagePattern, message);
                }
                await Writer.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, $"Unable to send message to server {_uri}, connection will be terminated.");
                await StopAsync().ConfigureAwait(false);
                throw;
            }
        }

        public void Receive(IMessageResponseHandler responseHandler)
        {
            while(responseHandler.UnhandledMessageSize > 0)
            {
                ReceiveOne(responseHandler);
            }
        }

        public async Task ReceiveAsync(IMessageResponseHandler responseHandler)
        {
            while (responseHandler.UnhandledMessageSize > 0)
            {
                await ReceiveOneAsync(responseHandler).ConfigureAwait(false);
            }
        }

        public void ReceiveOne(IMessageResponseHandler responseHandler)
        {
            try
            {
                Reader.Read(responseHandler);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, $"Unable to read message from server {_uri}, connection will be terminated.");
                Stop();
                throw;
            }
            if (responseHandler.HasProtocolViolationError)
            {
                _logger?.Warn(responseHandler.Error, $"Received bolt protocol error from server {_uri}, connection will be terminated.");
                Stop();
                throw responseHandler.Error;
            }
        }

        public async Task ReceiveOneAsync(IMessageResponseHandler responseHandler)
        {
            try
            {
                await Reader.ReadAsync(responseHandler).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, $"Unable to read message from server {_uri}, connection will be terminated.");
                await StopAsync().ConfigureAwait(false);
                throw;
            }
            if (responseHandler.HasProtocolViolationError)
            {
                _logger?.Warn(responseHandler.Error, $"Received bolt protocol error from server {_uri}, connection will be terminated.");
                await StopAsync().ConfigureAwait(false);
                throw responseHandler.Error;
            }
       }

        internal void SetOpened()
        {
            Interlocked.CompareExchange(ref _closedMarker, 0, -1);
        }


        public void Stop()
        {
            if (Interlocked.CompareExchange(ref _closedMarker, 1, 0) == 0)
            {
                _tcpSocketClient.Disconnect();
            }
        }

        public Task StopAsync()
        {
            if (Interlocked.CompareExchange(ref _closedMarker, 1, 0) == 0)
            {
                return _tcpSocketClient.DisconnectAsync();
            }

            return TaskHelper.GetCompletedTask();
        }

        public void ResetMessageReaderAndWriterForServerV3_1(IBoltProtocol boltProtocol)
        {
            Reader = boltProtocol.NewReader(_tcpSocketClient.ReadStream, _bufferSettings, _logger, false);
            Writer = boltProtocol.NewWriter(_tcpSocketClient.WriteStream, _bufferSettings, _logger, false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsClosed)
                return;

            if (disposing)
            {
                Stop();
            }
        }

        private int DoHandshake()
        {
            var data = BoltProtocolFactory.PackSupportedVersions();
            _tcpSocketClient.WriteStream.Write(data, 0, data.Length);
            _tcpSocketClient.WriteStream.Flush();
            _logger?.Debug("C: [HANDSHAKE] {0}", data.ToHexString());

            data = new byte[4];
            _tcpSocketClient.ReadStream.Read(data, 0, data.Length);

            var agreedVersion = BoltProtocolFactory.UnpackAgreedVersion(data);
            _logger?.Debug("S: [HANDSHAKE] {0}", agreedVersion);
            return agreedVersion;
        }

        private async Task<int> DoHandshakeAsync()
        {
            var data = BoltProtocolFactory.PackSupportedVersions();
            await _tcpSocketClient.WriteStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
            await _tcpSocketClient.WriteStream.FlushAsync().ConfigureAwait(false);
            _logger?.Debug("C: [HANDSHAKE] {0}", data.ToHexString());

            data = new byte[4];
            await _tcpSocketClient.ReadStream.ReadAsync(data, 0, data.Length).ConfigureAwait(false);

            var agreedVersion = BoltProtocolFactory.UnpackAgreedVersion(data);
            _logger?.Debug("S: [HANDSHAKE] {0}", agreedVersion);
            return agreedVersion;
        }

        private IBoltProtocol SelectBoltProtocol(int version)
        {
            var boltProtocol = BoltProtocolFactory.ForVersion(version);
            Reader = boltProtocol.NewReader(_tcpSocketClient.ReadStream, _bufferSettings, _logger);
            Writer = boltProtocol.NewWriter(_tcpSocketClient.WriteStream, _bufferSettings, _logger);
            return boltProtocol;
        }

        private void LogDebug(string message, params object[] args)
        {
            if (_logger != null && _logger.IsDebugEnabled())
            {
                _logger?.Debug(message, args);
            }
        }
    }
}
