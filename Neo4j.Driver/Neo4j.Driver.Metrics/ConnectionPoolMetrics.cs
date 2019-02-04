// Copyright (c) 2002-2019 "Neo4j,"
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
using System.Threading;

namespace Neo4j.Driver.Internal.Metrics
{
    internal class ConnectionPoolMetrics : IConnectionPoolMetrics, IConnectionPoolListener
    {
        private int _creating;
        private long _created;
        private long _failedToCreate;

        private int _closing;
        private long _closed;

        private int _acquiring;
        private long _acquired;
        private long _timedOutToAcquire;

        private long _totalAcquisitionTime;
        private long _totalConnectionTime;
        private long _totalInUseTime;
        private long _totalInUseCount;

        public int Creating => _creating;
        public long Created => Interlocked.Read(ref _created);
        public long FailedToCreate => Interlocked.Read(ref _failedToCreate);

        public int Closing => _closing;
        public long Closed => Interlocked.Read(ref _closed);

        public int Acquiring => _acquiring;
        public long Acquired => Interlocked.Read(ref _acquired);
        public long TimedOutToAcquire => Interlocked.Read(ref _timedOutToAcquire);
        public long TotalAcquisitionTime => Interlocked.Read(ref _totalAcquisitionTime);
        public long TotalConnectionTime => Interlocked.Read(ref _totalConnectionTime);
        public long TotalInUseTime => Interlocked.Read(ref _totalInUseTime);
        public long TotalInUseCount => Interlocked.Read(ref _totalInUseCount);
        public IMetrics Snapshot()
        {
            throw new NotImplementedException();
        }

        public string Id { get; }

        private IConnectionPool _pool;
        public int InUse => _pool?.NumberOfInUseConnections ?? 0;
        public int Idle => _pool?.NumberOfIdleConnections ?? 0;
        public PoolStatus PoolStatus => _pool?.Status.Code ?? PoolStatus.Closed;

        public ConnectionPoolMetrics(Uri uri, IConnectionPool pool)
        {
            Id = uri.ToString();
            _pool = pool;
        }

        public void BeforeCreating(IListenerEvent connEvent)
        {
            Interlocked.Increment(ref _creating);
            connEvent.Start();
        }

        public void AfterCreated(IListenerEvent connEvent)
        {
            Interlocked.Decrement(ref _creating);
            Interlocked.Increment(ref _created);
            Interlocked.Add(ref _totalConnectionTime, connEvent.GetElapsed());
        }

        public void AfterFailedToCreate()
        {
            Interlocked.Increment(ref _failedToCreate);
            Interlocked.Decrement(ref _creating);
        }

        public void BeforeClosing()
        {
            Interlocked.Increment(ref _closing);
        }

        public void AfterClosed()
        {
            Interlocked.Increment(ref _closed);
            Interlocked.Decrement(ref _closing);
        }

        public void BeforeAcquiring(IListenerEvent acquireEvent)
        {
            Interlocked.Increment(ref _acquiring);
            acquireEvent.Start();
        }

        public void AfterAcquired(IListenerEvent acquireEvent)
        {
            Interlocked.Decrement(ref _acquiring);
            Interlocked.Increment(ref _acquired);
            Interlocked.Add(ref _totalAcquisitionTime, acquireEvent.GetElapsed());
        }

        public void AfterFailedToAcquire()
        {
            Interlocked.Decrement(ref _acquiring);
        }

        public void AfterTimedOutToAcquire()
        {
            Interlocked.Increment(ref _timedOutToAcquire);
        }

        public void ConnectionAcquired(IListenerEvent inUseEvent)
        {
            inUseEvent.Start();
        }

        public void ConnectionReleased(IListenerEvent inUseEvent)
        {
            Interlocked.Increment(ref _totalInUseCount);
            Interlocked.Add(ref _totalInUseTime, inUseEvent.GetElapsed());
        }

        public void Dispose()
        {
            _pool = null;
        }

        public override string ToString()
        {
            return this.ToDictionary().ToContentString();
        }
    }
}
