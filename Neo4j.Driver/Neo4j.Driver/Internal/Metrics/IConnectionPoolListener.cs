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
using System.Diagnostics;

namespace Neo4j.Driver.Internal.Metrics
{
    internal interface IConnectionPoolListener: IDisposable
    {
        void BeforeCreating(IListenerEvent connEvent);
        void AfterCreated(IListenerEvent connEvent);
        void AfterFailedToCreate();
        void BeforeClosing();
        void AfterClosed();
        void BeforeAcquiring(IListenerEvent acquireEvent);
        void AfterAcquired(IListenerEvent acquireEvent);
        void AfterFailedToAcquire();
        void AfterTimedOutToAcquire();

        void ConnectionAcquired(IListenerEvent inUseEvent);
        void ConnectionReleased(IListenerEvent inUseEvent);
    }

    internal interface IListenerEvent
    {
        void Start();
        long GetElapsed();
    }

    /// <summary>
    /// A very simple impl of <see cref="IListenerEvent"/> without much error checks.
    /// </summary>
    internal class SimpleTimerEvent : IListenerEvent
    {
        private readonly Stopwatch _timer;
        private long _startTime;

        public SimpleTimerEvent(Stopwatch timer)
        {
            _timer = timer;
        }

        public void Start()
        {
            _startTime = _timer.ElapsedMilliseconds;
        }

        public long GetElapsed()
        {
            return _timer.ElapsedMilliseconds - _startTime;
        }
    }
}
