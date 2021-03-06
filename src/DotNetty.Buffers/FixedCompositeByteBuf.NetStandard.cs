﻿/*
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
    using System.Buffers;
    using System.Diagnostics;
    using DotNetty.Common;

    partial class FixedCompositeByteBuf
    {
        protected internal override ReadOnlyMemory<byte> _GetReadableMemory(int index, int count)
        {
            if (0u >= (uint)count) { return ReadOnlyMemory<byte>.Empty; }

            if (_buffers.Length == 1)
            {
                var buf = Buffer(0);
                if (buf.IsSingleIoBuffer)
                {
                    return buf.GetReadableMemory(index, count);
                }
            }

            var merged = new Memory<byte>(new byte[count]);
            var bufs = GetSequence(index, count);

            int offset = 0;
            foreach (var buf in bufs)
            {
                Debug.Assert(merged.Length - offset >= buf.Length);

                buf.CopyTo(merged.Slice(offset));
                offset += buf.Length;
            }

            return merged;
        }

        protected internal override ReadOnlySpan<byte> _GetReadableSpan(int index, int count)
        {
            if (0u >= (uint)count) { return ReadOnlySpan<byte>.Empty; }

            if (_buffers.Length == 1)
            {
                var buf = Buffer(0);
                if (buf.IsSingleIoBuffer)
                {
                    return buf.GetReadableSpan(index, count);
                }
            }

            var merged = new Memory<byte>(new byte[count]);
            var bufs = GetSequence(index, count);

            int offset = 0;
            foreach (var buf in bufs)
            {
                Debug.Assert(merged.Length - offset >= buf.Length);

                buf.CopyTo(merged.Slice(offset));
                offset += buf.Length;
            }

            return merged.Span;
        }

        protected internal override ReadOnlySequence<byte> _GetSequence(int index, int count)
        {
            if (0u >= (uint)count) { return ReadOnlySequence<byte>.Empty; }

            var array = ThreadLocalList<ReadOnlyMemory<byte>>.NewInstance(_nioBufferCount);
            try
            {
                var c = FindComponent(index);
                int i = c.Index;
                int adjustment = c.Offset;
                var s = c.Buf;
                for (; ; )
                {
                    int localLength = Math.Min(count, s.ReadableBytes - (index - adjustment));
                    switch (s.IoBufferCount)
                    {
                        case 0:
                            ThrowHelper.ThrowNotSupportedException();
                            break;
                        case 1:
                            array.Add(s.GetReadableMemory(index - adjustment, localLength));
                            break;
                        default:
                            var sequence = s.GetSequence(index - adjustment, localLength);
                            foreach (var memory in sequence)
                            {
                                array.Add(memory);
                            }
                            break;
                    }

                    index += localLength;
                    count -= localLength;
                    adjustment += s.ReadableBytes;
                    if ((uint)(count - 1) > SharedConstants.TooBigOrNegative) // count <= 0
                    {
                        break;
                    }
                    s = Buffer(++i);
                }

                return ReadOnlyBufferSegment.Create(array);
            }
            finally
            {
                array.Return();
            }
        }

        public override Memory<byte> GetMemory(int sizeHintt = 0)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }

        protected internal override Memory<byte> _GetMemory(int index, int count)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }

        public override Span<byte> GetSpan(int sizeHintt = 0)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }

        protected internal override Span<byte> _GetSpan(int index, int count)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }

        public override IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }
        public override IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }

        public override IByteBuffer WriteBytes(in ReadOnlySpan<byte> src)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }
        public override IByteBuffer WriteBytes(in ReadOnlyMemory<byte> src)
        {
            throw ThrowHelper.GetReadOnlyBufferException();
        }
    }
}
