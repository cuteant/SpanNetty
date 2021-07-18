/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) Microsoft. All rights reserved.
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;

    /// <summary>Expose metrics for an arena.</summary>
    public interface IPoolArenaMetric : ISizeClassesMetric
    {
        /// <summary>Returns the number of thread caches backed by this arena.</summary>
        int NumThreadCaches { get; }

        /// <summary>Returns the number of tiny sub-pages for the arena.</summary>
        [Obsolete("Tiny sub-pages have been merged into small sub-pages.")]
        int NumTinySubpages { get; }

        /// <summary>Returns the number of small sub-pages for the arena.</summary>
        int NumSmallSubpages { get; }

        /// <summary>Returns the number of chunk lists for the arena.</summary>
        int NumChunkLists { get; }

        /// <summary>Returns an unmodifiable <see cref="IReadOnlyList{T}"/> which holds <see cref="IPoolSubpageMetric"/>s for tiny sub-pages.</summary>
        [Obsolete("Tiny sub-pages have been merged into small sub-pages.")]
        IReadOnlyList<IPoolSubpageMetric> TinySubpages { get; }

        /// <summary>Returns an unmodifiable <see cref="IReadOnlyList{T}"/> which holds <see cref="IPoolSubpageMetric"/>s for small sub-pages.</summary>
        IReadOnlyList<IPoolSubpageMetric> SmallSubpages { get; }

        /// <summary>Returns an unmodifiable <see cref="IReadOnlyList{T}"/> which holds <see cref="IPoolChunkListMetric"/>s.</summary>
        IReadOnlyList<IPoolChunkListMetric> ChunkLists { get; }

        /// <summary>Return the number of allocations done via the arena. This includes all sizes.</summary>
        long NumAllocations { get; }

        /// <summary>Return the number of tiny allocations done via the arena.</summary>
        [Obsolete("Tiny allocations have been merged into small allocations.")]
        long NumTinyAllocations { get; }

        /// <summary>Return the number of small allocations done via the arena.</summary>
        long NumSmallAllocations { get; }

        /// <summary>Return the number of normal allocations done via the arena.</summary>
        long NumNormalAllocations { get; }

        /// <summary>Return the number of huge allocations done via the arena.</summary>
        long NumHugeAllocations { get; }

        /// <summary>Return the number of deallocations done via the arena. This includes all sizes.</summary>
        long NumDeallocations { get; }

        /// <summary>Return the number of tiny deallocations done via the arena.</summary>
        [Obsolete("Tiny deallocations have been merged into small deallocations.")]
        long NumTinyDeallocations { get; }

        /// <summary>Return the number of small deallocations done via the arena.</summary>
        long NumSmallDeallocations { get; }

        /// <summary>Return the number of normal deallocations done via the arena.</summary>
        long NumNormalDeallocations { get; }

        /// <summary>Return the number of huge deallocations done via the arena.</summary>
        long NumHugeDeallocations { get; }

        /// <summary>Return the number of currently active allocations.</summary>
        long NumActiveAllocations { get; }

        /// <summary>Return the number of currently active tiny allocations.</summary>
        [Obsolete("Tiny allocations have been merged into small allocations.")]
        long NumActiveTinyAllocations { get; }

        /// <summary>Return the number of currently active small allocations.</summary>
        long NumActiveSmallAllocations { get; }

        /// <summary>Return the number of currently active normal allocations.</summary>
        long NumActiveNormalAllocations { get; }

        /// <summary>Return the number of currently active huge allocations.</summary>
        long NumActiveHugeAllocations { get; }

        /// <summary>Return the number of active bytes that are currently allocated by the arena.</summary>
        long NumActiveBytes { get; }
    }
}