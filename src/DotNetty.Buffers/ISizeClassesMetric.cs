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
 *   https://github.com/cuteant/spannetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Buffers
{
    /// <summary>Expose metrics for an SizeClasses.</summary>
    public interface ISizeClassesMetric
    {
        /// <summary>Computes size from lookup table according to sizeIdx.</summary>
        /// <param name="sizeIdx">TBD</param>
        /// <returns>size</returns>
        int SizeIdx2Size(int sizeIdx);

        /// <summary>Computes size according to sizeIdx.</summary>
        /// <param name="sizeIdx">TBD</param>
        /// <returns>size</returns>
        int SizeIdx2SizeCompute(int sizeIdx);

        /// <summary>Computes size from lookup table according to pageIdx.</summary>
        /// <param name="pageIdx">TBD</param>
        /// <returns>size which is multiples of pageSize.</returns>
        long PageIdx2Size(int pageIdx);

        /// <summary>Computes size according to pageIdx.</summary>
        /// <param name="pageIdx">TBD</param>
        /// <returns>size which is multiples of pageSize</returns>
        long PageIdx2SizeCompute(int pageIdx);

        /// <summary>Normalizes request size up to the nearest size class.</summary>
        /// <param name="size">request size</param>
        /// <returns>sizeIdx of the size class</returns>
        int Size2SizeIdx(int size);

        /// <summary>Normalizes request size up to the nearest pageSize class.</summary>
        /// <param name="pages">multiples of pageSizes</param>
        /// <returns>pageIdx of the pageSize class</returns>
        int Pages2PageIdx(int pages);

        /// <summary>Normalizes request size down to the nearest pageSize class.</summary>
        /// <param name="pages">multiples of pageSizes</param>
        /// <returns>pageIdx of the pageSize class</returns>
        int Pages2PageIdxFloor(int pages);

        /// <summary>Normalizes usable size that would result from allocating an object with the
        /// specified size and alignment.</summary>
        /// <param name="size">request size</param>
        /// <returns>normalized size</returns>
        int NormalizeSize(int size);
    }
}