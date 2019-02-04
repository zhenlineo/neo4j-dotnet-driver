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
using System.Collections.Generic;
using Neo4j.Driver;

namespace Neo4j.Driver.Internal.Metrics
{

    /// <summary>
    /// The factory interface to create <see cref="IMetrics"/> instances
    /// </summary>
    internal interface IMetricsFactory
    {
        /// <summary>
        /// Creates an <see cref="IMetrics"/> instance for the driver with the specified <see cref="Config"/>
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        IMetrics CreateMetrics(Config config);
    }

    /// <summary>
    /// The driver metrics
    /// </summary>
    internal interface IMetrics
    {
        /// <summary>
        /// The connection pool metrics.
        /// </summary>
        IDictionary<string, IConnectionPoolMetrics> ConnectionPoolMetrics { get; }

        IConnectionListener CreateConnectionListener(Uri uri);

        IConnectionPoolListener CreateConnectionPoolListener(Uri uri, IConnectionPool pool);
    }

    /// <summary>
    /// The connection pool metrics
    /// </summary>
    internal interface IConnectionPoolMetrics
    {
        /// <summary>
        /// The unique name of this metrics, used as an unique identifier among all <see cref="IConnectionPoolMetrics"/> instances.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The pool status
        /// </summary>
        PoolStatus PoolStatus { get; }

        /// <summary>
        /// The amount of the connections that are used by user's application
        /// </summary>
        int InUse { get; }

        /// <summary>
        /// The amount of connections that are buffered by the pool
        /// </summary>
        int Idle { get; }

        /// <summary>
        /// The amount of connections that are waiting to be created.
        /// </summary>
        int Creating { get; }

        /// <summary>
        /// The amount of connections that have been created by this driver
        /// </summary>
        long Created { get; }

        /// <summary>
        /// The amount of connections that are failed to be created.
        /// The cause of the error could be pool is full for example.
        /// </summary>
        long FailedToCreate { get; }

        /// <summary>
        /// The amount of connections that are waiting to be closed.
        /// </summary>
        int Closing { get; }

        /// <summary>
        /// The amount of connections that have been closed by this driver.
        /// </summary>
        long Closed { get; }

        /// <summary>
        /// The amount of requests trying to acquire a connection from the pool.
        /// </summary>
        int Acquiring { get; }

        /// <summary>
        /// The amount of requests that have acquired a connection out of the pool.
        /// </summary>
        long Acquired { get; }

        /// <summary>
        /// The amount of requests to acquire a connection from pool but failed due to acquisition timeout.
        /// </summary>
        long TimedOutToAcquire { get; }

        /// <summary>
        /// The total acquisition time in milliseconds of all connection acquisition requests since the pool is created.
        /// See <see cref="Acquired"/> for the total amount of connections that have been acquired since the pool is created.
        /// </summary>
        long TotalAcquisitionTime { get; }

        /// <summary>
        /// The total time in milliseconds spent to establishing new socket connections since the pool is created.
        /// See <see cref="Created"/> for all the amount of connections that have been created since the pool is created.
        /// </summary>
        long TotalConnectionTime { get; }

        /// <summary>
        /// The total time in milliseconds connections are borrowed out of the pool,
        /// such as the time spent in user's application code to run cypher queries.
        /// </summary>
        long TotalInUseTime { get; }

        /// <summary>
        /// The total amount of connections that are borrowed outside the pool since the pool is created.
        /// </summary>
        long TotalInUseCount { get; }

        /// <summary>
        /// Returns a snapshot of this connection pool metrics.
        /// </summary>
        /// <returns>Returns a snapshot of this connection pool metrics.</returns>
        IMetrics Snapshot();
    }

    internal enum PoolStatus
    {
        Open, Closed, Inactive
    }
}
