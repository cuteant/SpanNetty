// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Buffers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    public partial class CompositeByteBuffer : AbstractReferenceCountedByteBuffer, IEnumerable<IByteBuffer>
    {
        static readonly IList<IByteBuffer> EmptyList = new ReadOnlyCollection<IByteBuffer>(new IByteBuffer[0]);

        class ComponentEntry
        {
            public readonly IByteBuffer Buffer;
            public int Adjustment;
            public int Offset;
            public int EndOffset;
            internal IByteBuffer slice;

            public ComponentEntry(IByteBuffer buffer, int srcOffset, int offset, int len, IByteBuffer slice)
            {
                this.Buffer = buffer;
                this.Offset = offset;
                this.EndOffset = offset + len;
                this.Adjustment = srcOffset - offset;
                this.slice = slice;
            }

            [MethodImpl(InlineMethod.Value)]
            public int Idx(int index)
            {
                return index + this.Adjustment;
            }

            public int Length()
            {
                return this.EndOffset - this.Offset;
            }

            public void Reposition(int newOffset)
            {
                int move = newOffset - this.Offset;
                this.EndOffset += move;
                this.Adjustment -= move;
                this.Offset = newOffset;
            }

            // copy then release
            public void TransferTo(IByteBuffer dst)
            {
                dst.WriteBytes(this.Buffer, this.Idx(this.Offset), this.Length());
                this.FreeIfNecessary();
            }

            public IByteBuffer Slice()
            {
                return this.slice != null ? this.slice : (this.slice = this.Buffer.Slice(this.Idx(this.Offset), this.Length()));
            }

            public IByteBuffer Duplicate()
            {
                return this.Buffer.Duplicate().SetIndex(this.Idx(this.Offset), this.Idx(this.EndOffset));
            }

            public void FreeIfNecessary()
            {
                // Release the slice if present since it may have a different
                // refcount to the unwrapped buf if it is a PooledSlicedByteBuf
                IByteBuffer buffer = this.slice;
                if (buffer != null)
                {
                    buffer.Release();
                }
                else
                {
                    this.Buffer.Release();
                }
                // null out in either case since it could be racy
                this.slice = null;
            }
        }

        static readonly ArraySegment<byte> EmptyNioBuffer = Unpooled.Empty.GetIoBuffer();
        static readonly IByteBuffer[] Empty = { Unpooled.Empty };

        readonly IByteBufferAllocator allocator;
        readonly bool direct;
        readonly int maxNumComponents;

        int componentCount;
        ComponentEntry[] components;

        bool freed;

        private CompositeByteBuffer(IByteBufferAllocator allocator, bool direct, int maxNumComponents, int initSize)
            : base(AbstractByteBufferAllocator.DefaultMaxCapacity)
        {
            if (null == allocator) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.allocator); }
            if (maxNumComponents < 1) { ThrowHelper.ThrowArgumentException_CheckMaxNumComponents(maxNumComponents); }

            this.allocator = allocator;
            this.direct = direct;
            this.maxNumComponents = maxNumComponents;
            this.components = NewCompArray(initSize, maxNumComponents);
        }

        public CompositeByteBuffer(IByteBufferAllocator alloc, bool direct, int maxNumComponents)
            : this(alloc, direct, maxNumComponents, 0)
        {
        }

        public CompositeByteBuffer(IByteBufferAllocator allocator, bool direct, int maxNumComponents, params IByteBuffer[] buffers)
            : this(allocator, direct, maxNumComponents, buffers ?? Empty, 0)
        {
        }

        internal CompositeByteBuffer(IByteBufferAllocator alloc, bool direct, int maxNumComponents, IByteBuffer[] buffers, int offset)
            : this(alloc, direct, maxNumComponents, buffers.Length - offset)
        {
            this.AddComponents0(false, 0, buffers, offset);
            this.ConsolidateIfNeeded();
            this.SetIndex0(0, this.Capacity);
        }

        public CompositeByteBuffer(IByteBufferAllocator allocator, bool direct, int maxNumComponents, IEnumerable<IByteBuffer> buffers)
            : this(allocator, direct, maxNumComponents, buffers is ICollection<IByteBuffer> bufCol ? bufCol.Count : 0)
        {
            this.AddComponents0(false, 0, buffers);
            this.ConsolidateIfNeeded();
            this.SetIndex(0, this.Capacity);
        }

        static ComponentEntry[] NewCompArray(int initComponents, int maxNumComponents)
        {
            int capacityGuess = Math.Min(AbstractByteBufferAllocator.DefaultMaxComponents, maxNumComponents);
            return new ComponentEntry[Math.Max(initComponents, capacityGuess)];
        }

        // Special constructor used by WrappedCompositeByteBuf
        internal CompositeByteBuffer(IByteBufferAllocator allocator) : base(int.MaxValue)
        {
            this.allocator = allocator;
            this.direct = false;
            this.maxNumComponents = 0;
            this.components = null;
        }

        /// <summary>
        ///     Add the given {@link IByteBuffer}.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param buffer the {@link IByteBuffer} to add
        /// </summary>
        public virtual CompositeByteBuffer AddComponent(IByteBuffer buffer) => this.AddComponent(false, buffer);

        /// <summary>
        ///     Add the given {@link IByteBuffer}s.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public virtual CompositeByteBuffer AddComponents(params IByteBuffer[] buffers) => this.AddComponents(false, buffers);

        /// <summary>
        ///     Add the given {@link IByteBuffer}s.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public virtual CompositeByteBuffer AddComponents(IEnumerable<IByteBuffer> buffers) => this.AddComponents(false, buffers);

        /// <summary>
        ///     Add the given {@link IByteBuffer} on the specific index.
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param cIndex the index on which the {@link IByteBuffer} will be added
        ///     @param buffer the {@link IByteBuffer} to add
        /// </summary>
        public virtual CompositeByteBuffer AddComponent(int cIndex, IByteBuffer buffer) => this.AddComponent(false, cIndex, buffer);

        public virtual CompositeByteBuffer AddComponent(bool increaseWriterIndex, IByteBuffer buffer)
        {
            if (null == buffer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            this.AddComponent0(increaseWriterIndex, this.componentCount, buffer);
            this.ConsolidateIfNeeded();
            return this;
        }

        public virtual CompositeByteBuffer AddComponents(bool increaseWriterIndex, params IByteBuffer[] buffers)
        {
            if (null == buffers) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffers); }
            this.AddComponents0(increaseWriterIndex, this.componentCount, buffers, 0);
            this.ConsolidateIfNeeded();
            return this;
        }

        public virtual CompositeByteBuffer AddComponents(bool increaseWriterIndex, IEnumerable<IByteBuffer> buffers)
        {
            this.AddComponents0(increaseWriterIndex, this.componentCount, buffers);
            this.ConsolidateIfNeeded();
            return this;
        }

        public virtual CompositeByteBuffer AddComponent(bool increaseWriterIndex, int cIndex, IByteBuffer buffer)
        {
            if (null == buffer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            this.AddComponent0(increaseWriterIndex, cIndex, buffer);
            this.ConsolidateIfNeeded();
            return this;
        }

        int AddComponent0(bool increaseWriterIndex, int cIndex, IByteBuffer buffer)
        {
            bool wasAdded = false;
            try
            {
                this.CheckComponentIndex(cIndex);

                // No need to consolidate - just add a component to the list.
                ComponentEntry c = this.NewComponent(buffer, 0);
                int readableBytes = c.Length();

                this.AddComp(cIndex, c);
                wasAdded = true;
                if (readableBytes > 0 && cIndex < componentCount - 1)
                {
                    this.UpdateComponentOffsets(cIndex);
                }
                else if (cIndex > 0)
                {
                    c.Reposition(components[cIndex - 1].EndOffset);
                }
                if (increaseWriterIndex)
                {
                    this.SetWriterIndex(this.WriterIndex + readableBytes);
                }
                return cIndex;
            }
            finally
            {
                if (!wasAdded)
                {
                    buffer.Release();
                }
            }
        }

        ComponentEntry NewComponent(IByteBuffer buf, int offset)
        {
            if (CheckAccessible && buf.ReferenceCount == 0)
            {
                ThrowHelper.ThrowIllegalReferenceCountException(0);
            }
            int srcIndex = buf.ReaderIndex, len = buf.ReadableBytes;
            IByteBuffer slice = null;
            if (buf is AbstractUnpooledSlicedByteBuffer unpooledSliceBuf)
            {
                srcIndex += unpooledSliceBuf.Idx(0);
                slice = buf;
                buf = buf.Unwrap();
            }
            else if (buf is PooledSlicedByteBuffer pooledSlicedBuf)
            {
                srcIndex += pooledSlicedBuf.adjustment;
                slice = buf;
                buf = buf.Unwrap();
            }
            return new ComponentEntry(buf, srcIndex, offset, len, slice);
        }

        /// <summary>
        ///     Add the given {@link IByteBuffer}s on the specific index
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param cIndex the index on which the {@link IByteBuffer} will be added.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public virtual CompositeByteBuffer AddComponents(int cIndex, params IByteBuffer[] buffers)
        {
            if (null == buffers) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffers); }
            this.AddComponents0(false, cIndex, buffers, 0);
            this.ConsolidateIfNeeded();
            return this;
        }

        int AddComponents0(bool increaseWriterIndex, int cIndex, IByteBuffer[] buffers, int arrOffset)
        {
            int len = buffers.Length, count = len - arrOffset;
            int ci = int.MaxValue;
            try
            {
                this.CheckComponentIndex(cIndex);
                this.ShiftComps(cIndex, count); // will increase componentCount
                ci = cIndex; // only set this after we've shifted so that finally block logic is always correct
                int nextOffset = cIndex > 0 ? this.components[cIndex - 1].EndOffset : 0;
                for (IByteBuffer b; arrOffset < len && (b = buffers[arrOffset]) != null; arrOffset++, ci++)
                {
                    ComponentEntry c = this.NewComponent(b, nextOffset);
                    this.components[ci] = c;
                    nextOffset = c.EndOffset;
                }
                return ci;
            }
            finally
            {
                // ci is now the index following the last successfully added component
                if (ci < componentCount)
                {
                    if (ci < cIndex + count)
                    {
                        // we bailed early
                        this.RemoveCompRange(ci, cIndex + count);
                        for (; arrOffset < len; ++arrOffset)
                        {
                            ReferenceCountUtil.SafeRelease(buffers[arrOffset]);
                        }
                    }
                    this.UpdateComponentOffsets(ci); // only need to do this here for components after the added ones
                }
                if (increaseWriterIndex && ci > cIndex && ci <= componentCount)
                {
                    this.SetWriterIndex(this.WriterIndex + this.components[ci - 1].EndOffset - this.components[cIndex].Offset);
                }
            }
        }

        /// <summary>
        ///     Add the given {@link ByteBuf}s on the specific index
        ///     Be aware that this method does not increase the {@code writerIndex} of the {@link CompositeByteBuffer}.
        ///     If you need to have it increased you need to handle it by your own.
        ///     @param cIndex the index on which the {@link IByteBuffer} will be added.
        ///     @param buffers the {@link IByteBuffer}s to add
        /// </summary>
        public virtual CompositeByteBuffer AddComponents(int cIndex, IEnumerable<IByteBuffer> buffers)
        {
            this.AddComponents0(false, cIndex, buffers);
            this.ConsolidateIfNeeded();
            return this;
        }

        // TODO optimize further, similar to ByteBuf[] version
        // (difference here is that we don't know *always* know precise size increase in advance,
        // but we do in the most common case that the Iterable is a Collection)
        int AddComponents0(bool increaseIndex, int cIndex, IEnumerable<IByteBuffer> buffers)
        {
            if (null == buffers) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffers); }

            if (buffers is IByteBuffer buffer)
            {
                // If buffers also implements ByteBuf (e.g. CompositeByteBuf), it has to go to addComponent(ByteBuf).
                return this.AddComponent0(increaseIndex, cIndex, buffer);
            }

            IByteBuffer[] array = buffers.ToArray();
            return this.AddComponents0(increaseIndex, cIndex, array, 0);
        }

        /// <summary>
        ///     This should only be called as last operation from a method as this may adjust the underlying
        ///     array of components and so affect the index etc.
        /// </summary>
        void ConsolidateIfNeeded()
        {
            // Consolidate if the number of components will exceed the allowed maximum by the current
            // operation.
            int size = this.componentCount;
            if (size > this.maxNumComponents)
            {
                int capacity = this.components[size - 1].EndOffset;

                IByteBuffer consolidated = this.AllocateBuffer(capacity);
                this.lastAccessed = null;

                // We're not using foreach to avoid creating an iterator.
                for (int i = 0; i < size; i++)
                {
                    this.components[i].TransferTo(consolidated);
                }

                this.components[0] = new ComponentEntry(consolidated, 0, 0, capacity, consolidated);
                this.RemoveCompRange(1, size);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        void CheckComponentIndex(int cIndex)
        {
            this.EnsureAccessible();
            if (cIndex < 0 || cIndex > this.componentCount)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_Index(cIndex, this.componentCount);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        void CheckComponentIndex(int cIndex, int numComponents)
        {
            this.EnsureAccessible();
            if (cIndex < 0 || cIndex + numComponents > this.componentCount)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_Index(cIndex, numComponents, this.componentCount);
            }
        }

        void UpdateComponentOffsets(int cIndex)
        {
            int size = this.componentCount;
            if (size <= cIndex)
            {
                return;
            }

            int nextIndex = cIndex > 0 ? this.components[cIndex].EndOffset : 0;
            for (; cIndex < size; cIndex++)
            {
                ComponentEntry c = this.components[cIndex];
                c.Reposition(nextIndex);
                nextIndex = c.EndOffset;
            }
        }

        /// <summary>
        ///     Remove the {@link IByteBuffer} from the given index.
        ///     @param cIndex the index on from which the {@link IByteBuffer} will be remove
        /// </summary>
        public virtual CompositeByteBuffer RemoveComponent(int cIndex)
        {
            this.CheckComponentIndex(cIndex);
            ComponentEntry comp = this.components[cIndex];
            if (this.lastAccessed == comp)
            {
                this.lastAccessed = null;
            }
            comp.FreeIfNecessary();
            this.RemoveComp(cIndex);
            if (comp.Length() > 0)
            {
                // Only need to call updateComponentOffsets if the length was > 0
                this.UpdateComponentOffsets(cIndex);
            }
            return this;
        }

        /// <summary>
        ///     Remove the number of {@link IByteBuffer}s starting from the given index.
        ///     @param cIndex the index on which the {@link IByteBuffer}s will be started to removed
        ///     @param numComponents the number of components to remove
        /// </summary>
        public virtual CompositeByteBuffer RemoveComponents(int cIndex, int numComponents)
        {
            this.CheckComponentIndex(cIndex, numComponents);

            if (numComponents == 0)
            {
                return this;
            }
            int endIndex = cIndex + numComponents;
            bool needsUpdate = false;
            for (int i = cIndex; i < endIndex; ++i)
            {
                ComponentEntry c = this.components[i];
                needsUpdate |= c.Length() > 0;
                if (this.lastAccessed == c)
                {
                    this.lastAccessed = null;
                }
                c.FreeIfNecessary();
            }
            this.RemoveCompRange(cIndex, endIndex);

            if (needsUpdate)
            {
                // Only need to call updateComponentOffsets if the length was > 0
                this.UpdateComponentOffsets(cIndex);
            }
            return this;
        }

        public virtual IEnumerator<IByteBuffer> GetEnumerator()
        {
            this.EnsureAccessible();

            var size = this.componentCount;
            for (var idx = 0; idx < size; idx++)
            {
                yield return this.components[idx].Slice();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

#if NET40
        internal protected override int ForEachByteAsc0(int start, int end, IByteProcessor processor)
        {
            if (end <= start)
            {
                return -1;
            }
            for (int i = this.ToComponentIndex0(start), length = end - start; length > 0; i++)
            {
                ComponentEntry c = this.components[i];
                if (c.Offset == c.EndOffset)
                {
                    continue; // empty
                }
                IByteBuffer s = c.Buffer;
                int localStart = c.Idx(start);
                int localLength = Math.Min(length, c.EndOffset - start);
                // avoid additional checks in AbstractByteBuf case
                int result = s is AbstractByteBuffer buf
                    ? buf.ForEachByteAsc0(localStart, localStart + localLength, processor)
                    : s.ForEachByte(localStart, localLength, processor);
                if (result != -1)
                {
                    return result - c.Adjustment;
                }
                start += localLength;
                length -= localLength;
            }
            return -1;
        }

        internal protected override int ForEachByteDesc0(int rStart, int rEnd, IByteProcessor processor)
        {
            if (rEnd > rStart) // rStart *and* rEnd are inclusive
            {
                return -1;
            }
            for (int i = this.ToComponentIndex0(rStart), length = 1 + rStart - rEnd; length > 0; i--)
            {
                ComponentEntry c = this.components[i];
                if (c.Offset == c.EndOffset)
                {
                    continue; // empty
                }
                IByteBuffer s = c.Buffer;
                int localRStart = c.Idx(length + rEnd);
                int localLength = Math.Min(length, localRStart), localIndex = localRStart - localLength;
                // avoid additional checks in AbstractByteBuf case
                int result = s is AbstractByteBuffer buf
                    ? buf.ForEachByteDesc0(localRStart - 1, localIndex, processor)
                    : s.ForEachByteDesc(localIndex, localLength, processor);

                if (result != -1)
                {
                    return result - c.Adjustment;
                }
                length -= localLength;
            }
            return -1;
        }
#endif

        /// <summary>
        ///     Same with {@link #slice(int, int)} except that this method returns a list.
        /// </summary>
        public virtual IList<IByteBuffer> Decompose(int offset, int length)
        {
            this.CheckIndex(offset, length);
            if (0u >= (uint)length)
            {
                return EmptyList;
            }

            int componentId = this.ToComponentIndex0(offset);
            int bytesToSlice = length;
            // The first component
            ComponentEntry firstC = this.components[componentId];

            IByteBuffer slice = firstC.Buffer.Slice(firstC.Idx(offset), Math.Min(firstC.EndOffset - offset, bytesToSlice));
            bytesToSlice -= slice.ReadableBytes;

            if (bytesToSlice == 0)
            {
                return new List<IByteBuffer> { slice };
            }

            var sliceList = new List<IByteBuffer>(this.componentCount - componentId);
            sliceList.Add(slice);

            // Add all the slices until there is nothing more left and then return the List.
            do
            {
                var component = this.components[++componentId];
                slice = component.Buffer.Slice(component.Idx(component.Offset), Math.Min(component.Length(), bytesToSlice));
                bytesToSlice -= slice.ReadableBytes;
                sliceList.Add(slice);
            } while (bytesToSlice > 0);

            return sliceList;
        }

        public override int IoBufferCount
        {
            get
            {
                int size = this.componentCount;
                switch (size)
                {
                    case 0:
                        return 1;
                    case 1:
                        return this.components[0].Buffer.IoBufferCount;
                    default:
                        int count = 0;
                        for (int i = 0; i < size; i++)
                        {
                            count += this.components[i].Buffer.IoBufferCount;
                        }
                        return count;
                }
            }
        }

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            if (0u >= (uint)length) { return default; }

            switch (this.componentCount)
            {
                case 0:
                    return EmptyNioBuffer;
                case 1:
                    ComponentEntry c = this.components[0];
                    IByteBuffer buf = c.Buffer;
                    if (buf.IoBufferCount == 1)
                    {
                        return buf.GetIoBuffer(c.Idx(index), length);
                    }
                    break;
            }

            var merged = new byte[length];
#if NET40
            ArraySegment<byte>[] buffers = this.GetIoBuffers(index, length);

            int offset = 0;
            foreach (ArraySegment<byte> buf in buffers)
            {
                Debug.Assert(merged.Length - offset >= buf.Count);

                PlatformDependent.CopyMemory(buf.Array, buf.Offset, merged, offset, buf.Count);
                offset += buf.Count;
            }
#else
            var memory = new Memory<byte>(merged);
            var buffers = this.GetSequence(index, length);

            int offset = 0;
            foreach (ReadOnlyMemory<byte> buf in buffers)
            {
                Debug.Assert(merged.Length - offset >= buf.Length);

                buf.CopyTo(memory.Slice(offset));
                offset += buf.Length;
            }
#endif
            return new ArraySegment<byte>(merged);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex(index, length);
            if (0u >= (uint)length)
            {
                return new[] { EmptyNioBuffer };
            }

            var buffers = ThreadLocalList<ArraySegment<byte>>.NewInstance(this.componentCount);
            try
            {
                int i = this.ToComponentIndex0(index);
                while (length > 0)
                {
                    ComponentEntry c = this.components[i];
                    IByteBuffer s = c.Buffer;
                    int localLength = Math.Min(length, c.EndOffset - index);
                    switch (s.IoBufferCount)
                    {
                        case 0:
                            ThrowHelper.ThrowNotSupportedException();
                            break;
                        case 1:
                            buffers.Add(s.GetIoBuffer(c.Idx(index), localLength));
                            break;
                        default:
                            buffers.AddRange(s.GetIoBuffers(c.Idx(index), localLength));
                            break;
                    }

                    index += localLength;
                    length -= localLength;
                    i++;
                }

                return buffers.ToArray();
            }
            finally
            {
                buffers.Return();
            }
        }


        public override bool IsDirect
        {
            get
            {
                int size = this.componentCount;
                if (size == 0)
                {
                    return false;
                }
                for (int i = 0; i < size; i++)
                {
                    if (!this.components[i].Buffer.IsDirect)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public override bool HasArray
        {
            get
            {
                switch (this.componentCount)
                {
                    case 0:
                        return true;
                    case 1:
                        return this.components[0].Buffer.HasArray;
                    default:
                        return false;
                }
            }
        }

        public override byte[] Array
        {
            get
            {
                switch (this.componentCount)
                {
                    case 0:
                        return ArrayExtensions.ZeroBytes;
                    case 1:
                        return this.components[0].Buffer.Array;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public override int ArrayOffset
        {
            get
            {
                switch (this.componentCount)
                {
                    case 0:
                        return 0;
                    case 1:
                        ComponentEntry c = this.components[0];
                        return c.Idx(c.Buffer.ArrayOffset);
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public override bool HasMemoryAddress
        {
            get
            {
                switch (this.componentCount)
                {
                    case 1:
                        return this.components[0].Buffer.HasMemoryAddress;
                    default:
                        return false;
                }
            }
        }

        public override ref byte GetPinnableMemoryAddress()
        {
            switch (this.componentCount)
            {
                case 1:
                    return ref this.components[0].Buffer.GetPinnableMemoryAddress();
                default:
                    throw new NotSupportedException();
            }
        }

        public override IntPtr AddressOfPinnedMemory()
        {
            switch (this.componentCount)
            {
                case 1:
                    return this.components[0].Buffer.AddressOfPinnedMemory();
                default:
                    throw new NotSupportedException();
            }
        }

        public override int Capacity
        {
            get
            {
                int size = this.componentCount;
                return size > 0 ? this.components[size - 1].EndOffset : 0;
            }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.CheckNewCapacity(newCapacity);

            int size = this.componentCount, oldCapacity = this.Capacity;
            if (newCapacity > oldCapacity)
            {
                int paddingLength = newCapacity - oldCapacity;
                IByteBuffer padding = this.AllocateBuffer(paddingLength).SetIndex(0, paddingLength);
                this.AddComponent0(false, size, padding);
                if (this.componentCount >= this.maxNumComponents)
                {
                    // FIXME: No need to create a padding buffer and consolidate.
                    // Just create a big single buffer and put the current content there.
                    this.ConsolidateIfNeeded();
                }
            }
            else if (newCapacity < oldCapacity)
            {
                int i = size - 1;
                for (int bytesToTrim = oldCapacity - newCapacity; i >= 0; i--)
                {
                    ComponentEntry c = this.components[i];
                    int cLength = c.Length();
                    if (bytesToTrim < cLength)
                    {
                        // Trim the last component
                        c.EndOffset -= bytesToTrim;
                        c.slice = null;
                        break;
                    }
                    c.FreeIfNecessary();
                    bytesToTrim -= cLength;
                }
                this.RemoveCompRange(i + 1, size);

                if (this.ReaderIndex > newCapacity)
                {
                    this.SetIndex(newCapacity, newCapacity);
                }
                else if (this.WriterIndex > newCapacity)
                {
                    this.SetWriterIndex(newCapacity);
                }
            }
            return this;
        }

        public override IByteBufferAllocator Allocator => this.allocator;

        /// <summary>
        ///     Return the current number of {@link IByteBuffer}'s that are composed in this instance
        /// </summary>
        public virtual int NumComponents => this.componentCount;

        /// <summary>
        ///     Return the max number of {@link IByteBuffer}'s that are composed in this instance
        /// </summary>
        public virtual int MaxNumComponents => this.maxNumComponents;

        /// <summary>
        ///     Return the index for the given offset
        /// </summary>
        public virtual int ToComponentIndex(int offset)
        {
            this.CheckIndex(offset);
            return this.ToComponentIndex0(offset);
        }

        int ToComponentIndex0(int offset)
        {
            int size = this.componentCount;
            if (offset == 0) // fast-path zero offset
            {
                for (int i = 0; i < size; i++)
                {
                    if (this.components[i].EndOffset > 0)
                    {
                        return i;
                    }
                }
            }

            for (int low = 0, high = size; low <= high;)
            {
                int mid = (low + high).RightUShift(1);
                ComponentEntry c = this.components[mid];
                if (offset >= c.EndOffset)
                {
                    low = mid + 1;
                }
                else if (offset < c.Offset)
                {
                    high = mid - 1;
                }
                else
                {
                    return mid;
                }
            }

            return ThrowHelper.ThrowException_ShouldNotReachHere<int>();
        }

        public virtual int ToByteIndex(int cIndex)
        {
            this.CheckComponentIndex(cIndex);
            return this.components[cIndex].Offset;
        }

        public override byte GetByte(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            return c.Buffer.GetByte(c.Idx(index));
        }

        protected internal override byte _GetByte(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            return c.Buffer.GetByte(c.Idx(index));
        }

        protected internal override short _GetShort(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 2 <= c.EndOffset)
            {
                return c.Buffer.GetShort(c.Idx(index));
            }

            return (short)(this._GetByte(index) << 8 | this._GetByte(index + 1));
        }

        protected internal override short _GetShortLE(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 2 <= c.EndOffset)
            {
                return c.Buffer.GetShortLE(c.Idx(index));
            }

            return (short)(this._GetByte(index) << 8 | this._GetByte(index + 1));
        }

        protected internal override int _GetUnsignedMedium(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 3 <= c.EndOffset)
            {
                return c.Buffer.GetUnsignedMedium(c.Idx(index));
            }

            return (this._GetShort(index) & 0xffff) << 8 | this._GetByte(index + 2);
        }

        protected internal override int _GetUnsignedMediumLE(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 3 <= c.EndOffset)
            {
                return c.Buffer.GetUnsignedMediumLE(c.Idx(index));
            }

            return (this._GetShortLE(index) & 0xffff) << 8 | this._GetByte(index + 2);
        }

        protected internal override int _GetInt(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 4 <= c.EndOffset)
            {
                return c.Buffer.GetInt(c.Idx(index));
            }

            return this._GetShort(index) << 16 | (ushort)this._GetShort(index + 2);
        }

        protected internal override int _GetIntLE(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 4 <= c.EndOffset)
            {
                return c.Buffer.GetIntLE(c.Idx(index));
            }

            return (this._GetShortLE(index) << 16 | (ushort)this._GetShortLE(index + 2));
        }

        protected internal override long _GetLong(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 8 <= c.EndOffset)
            {
                return c.Buffer.GetLong(c.Idx(index));
            }

            return (long)this._GetInt(index) << 32 | (uint)this._GetInt(index + 4);
        }

        protected internal override long _GetLongLE(int index)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 8 <= c.EndOffset)
            {
                return c.Buffer.GetLongLE(c.Idx(index));
            }

            return (this._GetIntLE(index) << 32 | this._GetIntLE(index + 4));
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            if (0u >= (uint)length)
            {
                return this;
            }

            int i = this.ToComponentIndex0(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                int localLength = Math.Min(length, c.EndOffset - index);
                c.Buffer.GetBytes(c.Idx(index), dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            if (0u >= (uint)length)
            {
                return this;
            }

            int i = this.ToComponentIndex0(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                int localLength = Math.Min(length, c.EndOffset - index);
                c.Buffer.GetBytes(c.Idx(index), destination, localLength);
                index += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (0u >= (uint)length)
            {
                return this;
            }

            int i = this.ToComponentIndex0(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                int localLength = Math.Min(length, c.EndOffset - index);
                c.Buffer.GetBytes(c.Idx(index), dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            ComponentEntry c = this.FindComponent(index);
            c.Buffer.SetByte(c.Idx(index), value);
            return this;
        }

        protected internal override void _SetByte(int index, int value)
        {
            ComponentEntry c = this.FindComponent0(index);
            c.Buffer.SetByte(c.Idx(index), value);
        }

        protected internal override void _SetShort(int index, int value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 2 <= c.EndOffset)
            {
                c.Buffer.SetShort(c.Idx(index), value);
            }
            else
            {
                this._SetByte(index, (byte)((uint)value >> 8));
                this._SetByte(index + 1, (byte)value);
            }
        }

        protected internal override void _SetShortLE(int index, int value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 2 <= c.EndOffset)
            {
                c.Buffer.SetShortLE(c.Idx(index), value);
            }
            else
            {
                this._SetByte(index, (byte)(value.RightUShift(8)));
                this._SetByte(index + 1, (byte)value);
            }
        }

        protected internal override void _SetMedium(int index, int value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 3 <= c.EndOffset)
            {
                c.Buffer.SetMedium(c.Idx(index), value);
            }
            else
            {
                this._SetShort(index, (short)(value >> 8));
                this._SetByte(index + 2, (byte)value);
            }
        }

        protected internal override void _SetMediumLE(int index, int value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 3 <= c.EndOffset)
            {
                c.Buffer.SetMediumLE(c.Idx(index), value);
            }
            else
            {
                this._SetShortLE(index, (short)(value >> 8));
                this._SetByte(index + 2, (byte)value);
            }
        }

        protected internal override void _SetInt(int index, int value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 4 <= c.EndOffset)
            {
                c.Buffer.SetInt(c.Idx(index), value);
            }
            else
            {
                this._SetShort(index, (short)((uint)value >> 16));
                this._SetShort(index + 2, (short)value);
            }
        }

        protected internal override void _SetIntLE(int index, int value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 4 <= c.EndOffset)
            {
                c.Buffer.SetIntLE(c.Idx(index), value);
            }
            else
            {
                this._SetShortLE(index, (short)value.RightUShift(16));
                this._SetShortLE(index + 2, (short)value);
            }
        }

        protected internal override void _SetLong(int index, long value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 8 <= c.EndOffset)
            {
                c.Buffer.SetLong(c.Idx(index), value);
            }
            else
            {
                this._SetInt(index, (int)((ulong)value >> 32));
                this._SetInt(index + 4, (int)value);
            }
        }

        protected internal override void _SetLongLE(int index, long value)
        {
            ComponentEntry c = this.FindComponent0(index);
            if (index + 8 <= c.EndOffset)
            {
                c.Buffer.SetLongLE(c.Idx(index), value);
            }
            else
            {
                this._SetIntLE(index, (int)value.RightUShift(32));
                this._SetIntLE(index + 4, (int)value);
            }
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            if (0u >= (uint)length)
            {
                return this;
            }

            int i = this.ToComponentIndex0(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                int localLength = Math.Min(length, c.EndOffset - index);
                c.Buffer.SetBytes(c.Idx(index), src, srcIndex, localLength);
                index += localLength;
                srcIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override async Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            if (0u >= (uint)length)
            {
                return 0;
                //return src.Read(EmptyArrays.EMPTY_BYTES);
            }

            int i = this.ToComponentIndex0(index);
            int readBytes = 0;

            do
            {
                ComponentEntry c = this.components[i];
                int localLength = Math.Min(length, c.EndOffset - index);
                if (localLength == 0)
                {
                    // Skip empty buffer
                    i++;
                    continue;
                }
                int localReadBytes = await c.Buffer.SetBytesAsync(c.Idx(index), src, localLength, cancellationToken);
                if (localReadBytes < 0)
                {
                    if (readBytes == 0)
                    {
                        return -1;
                    }
                    else
                    {
                        break;
                    }
                }

                if (localReadBytes == localLength)
                {
                    index += localLength;
                    length -= localLength;
                    readBytes += localLength;
                    i++;
                }
                else
                {
                    index += localReadBytes;
                    length -= localReadBytes;
                    readBytes += localReadBytes;
                }
            }
            while (length > 0);

            return readBytes;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            if (0u >= (uint)length)
            {
                return this;
            }

            int i = this.ToComponentIndex0(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                int localLength = Math.Min(length, c.EndOffset - index);
                c.Buffer.SetBytes(c.Idx(index), src, srcIndex, localLength);
                index += localLength;
                srcIndex += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            if (0u >= (uint)length)
            {
                return this;
            }

            int i = this.ToComponentIndex(index);
            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                IByteBuffer s = c.Buffer;
                int adjustment = c.Offset;
                int localLength = Math.Min(length, s.Capacity - (index - adjustment));
                s.SetZero(index - adjustment, localLength);
                index += localLength;
                length -= localLength;
                i++;
            }
            return this;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            IByteBuffer dst = this.AllocateBuffer(length);
            if (length != 0)
            {
                this.CopyTo(index, length, this.ToComponentIndex0(index), dst);
            }
            return dst;
        }

        void CopyTo(int index, int length, int componentId, IByteBuffer dst)
        {
            int dstIndex = 0;
            int i = componentId;

            while (length > 0)
            {
                ComponentEntry c = this.components[i];
                int localLength = Math.Min(length, c.EndOffset - index);
                c.Buffer.GetBytes(c.Idx(index), dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                i++;
            }

            dst.SetWriterIndex(dst.Capacity);
        }

        /// <summary>
        ///     Return the {@link IByteBuffer} on the specified index
        ///     @param cIndex the index for which the {@link IByteBuffer} should be returned
        ///     @return buffer the {@link IByteBuffer} on the specified index
        /// </summary>
        public virtual IByteBuffer this[int cIndex]
        {
            get
            {
                this.CheckComponentIndex(cIndex);
                return this.components[cIndex].Duplicate();
            }
        }

        /// <summary>
        ///     Return the {@link IByteBuffer} on the specified index
        ///     @param offset the offset for which the {@link IByteBuffer} should be returned
        ///     @return the {@link IByteBuffer} on the specified index
        /// </summary>
        public virtual IByteBuffer ComponentAtOffset(int offset) => this.FindComponent(offset).Duplicate();

        /// <summary>
        ///     Return the internal {@link IByteBuffer} on the specified index. Note that updating the indexes of the returned
        ///     buffer will lead to an undefined behavior of this buffer.
        ///     @param cIndex the index for which the {@link IByteBuffer} should be returned
        /// </summary>
        public virtual IByteBuffer InternalComponent(int cIndex)
        {
            this.CheckComponentIndex(cIndex);
            return this.components[cIndex].Slice();
        }

        /// <summary>
        ///     Return the internal {@link IByteBuffer} on the specified offset. Note that updating the indexes of the returned
        ///     buffer will lead to an undefined behavior of this buffer.
        ///     @param offset the offset for which the {@link IByteBuffer} should be returned
        /// </summary>
        public virtual IByteBuffer InternalComponentAtOffset(int offset) => this.FindComponent(offset).Slice();

        // weak cache - check it first when looking for component
        ComponentEntry lastAccessed;

        ComponentEntry FindComponent(int offset)
        {
            var la = this.lastAccessed;
            if (la != null && offset >= la.Offset && offset < la.EndOffset)
            {
                this.EnsureAccessible();
                return la;
            }
            this.CheckIndex(offset);
            return this.FindIt(offset);
        }

        ComponentEntry FindComponent0(int offset)
        {
            var la = lastAccessed;
            if (la != null && offset >= la.Offset && offset < la.EndOffset)
            {
                return la;
            }
            return this.FindIt(offset);
        }

        ComponentEntry FindIt(int offset)
        {
            for (int low = 0, high = componentCount; low <= high;)
            {
                int mid = (low + high).RightUShift(1);
                ComponentEntry c = this.components[mid];
                if (offset >= c.EndOffset)
                {
                    low = mid + 1;
                }
                else if (offset < c.Offset)
                {
                    high = mid - 1;
                }
                else
                {
                    lastAccessed = c;
                    return c;
                }
            }

            return ThrowHelper.ThrowException_ShouldNotReachHere<ComponentEntry>();
        }

        /// <summary>
        ///     Consolidate the composed {@link IByteBuffer}s
        /// </summary>
        public virtual CompositeByteBuffer Consolidate()
        {
            this.EnsureAccessible();
            int numComponents = this.componentCount;
            if (numComponents <= 1)
            {
                return this;
            }

            int capacity = this.components[numComponents - 1].EndOffset;
            IByteBuffer consolidated = this.AllocateBuffer(capacity);

            for (int i = 0; i < numComponents; i++)
            {
                this.components[i].TransferTo(consolidated);
            }

            this.lastAccessed = null;
            this.components[0] = new ComponentEntry(consolidated, 0, 0, capacity, consolidated);
            this.RemoveCompRange(1, numComponents);
            return this;
        }

        /// <summary>
        ///     Consolidate the composed {@link IByteBuffer}s
        ///     @param cIndex the index on which to start to compose
        ///     @param numComponents the number of components to compose
        /// </summary>
        public virtual CompositeByteBuffer Consolidate(int cIndex, int numComponents)
        {
            this.CheckComponentIndex(cIndex, numComponents);
            if (numComponents <= 1)
            {
                return this;
            }

            int endCIndex = cIndex + numComponents;
            ComponentEntry last = this.components[endCIndex - 1];
            int capacity = last.EndOffset - this.components[cIndex].Offset;
            IByteBuffer consolidated = this.AllocateBuffer(capacity);

            for (int i = cIndex; i < endCIndex; i++)
            {
                this.components[i].TransferTo(consolidated);
            }
            this.lastAccessed = null;
            this.RemoveCompRange(cIndex + 1, endCIndex);
            this.components[cIndex] = new ComponentEntry(consolidated, 0, 0, capacity, consolidated);
            this.UpdateComponentOffsets(cIndex);
            return this;
        }

        /// <summary>
        ///     Discard all {@link IByteBuffer}s which are read.
        /// </summary>
        public virtual CompositeByteBuffer DiscardReadComponents()
        {
            this.EnsureAccessible();
            int readerIndex = this.ReaderIndex;
            if (readerIndex == 0)
            {
                return this;
            }

            // Discard everything if (readerIndex = writerIndex = capacity).
            int writerIndex = this.WriterIndex;
            if (readerIndex == writerIndex && writerIndex == this.Capacity)
            {
                var size = this.componentCount;
                for (var idx = 0; idx < size; idx++)
                {
                    this.components[idx].FreeIfNecessary();
                }
                this.lastAccessed = null;
                this.ClearComps();
                this.SetIndex(0, 0);
                this.AdjustMarkers(readerIndex);
                return this;
            }

            // Remove read components.
            int firstComponentId = this.ToComponentIndex0(readerIndex);
            for (int i = 0; i < firstComponentId; i++)
            {
                this.components[i].FreeIfNecessary();
            }
            this.lastAccessed = null;
            this.RemoveCompRange(0, firstComponentId);

            // Update indexes and markers.
            ComponentEntry first = this.components[0];
            int offset = first.Offset;
            this.UpdateComponentOffsets(0);
            this.SetIndex(readerIndex - offset, writerIndex - offset);
            this.AdjustMarkers(offset);
            return this;
        }

        public override IByteBuffer DiscardReadBytes()
        {
            this.EnsureAccessible();
            int readerIndex = this.ReaderIndex;
            if (readerIndex == 0)
            {
                return this;
            }

            // Discard everything if (readerIndex = writerIndex = capacity).
            int writerIndex = this.WriterIndex;
            if (readerIndex == writerIndex && writerIndex == this.Capacity)
            {
                var size = this.componentCount;
                for (var idx = 0; idx < size; idx++)
                {
                    this.components[idx].FreeIfNecessary();
                }
                this.lastAccessed = null;
                this.ClearComps();
                this.SetIndex(0, 0);
                this.AdjustMarkers(readerIndex);
                return this;
            }

            // Remove read components.
            int firstComponentId = this.ToComponentIndex0(readerIndex);
            for (int i = 0; i < firstComponentId; i++)
            {
                var item = this.components[i];
                item.FreeIfNecessary();
                if (this.lastAccessed == item)
                {
                    this.lastAccessed = null;
                }
            }

            // Remove or replace the first readable component with a new slice.
            ComponentEntry c = this.components[firstComponentId];
            if (readerIndex == c.EndOffset)
            {
                // new slice would be empty, so remove instead
                c.FreeIfNecessary();
                if (this.lastAccessed == c)
                {
                    this.lastAccessed = null;
                }
                firstComponentId++;
            }
            else
            {
                c.Offset = 0;
                c.EndOffset -= readerIndex;
                c.Adjustment += readerIndex;
                c.slice = null;
            }

            this.RemoveCompRange(0, firstComponentId);

            // Update indexes and markers.
            this.UpdateComponentOffsets(0);
            this.SetIndex(0, writerIndex - readerIndex);
            this.AdjustMarkers(readerIndex);
            return this;
        }

        IByteBuffer AllocateBuffer(int capacity) =>
            this.direct ? this.Allocator.DirectBuffer(capacity) : this.Allocator.HeapBuffer(capacity);

        public override string ToString()
        {
            string result = base.ToString();
            result = result.Substring(0, result.Length - 1);
            return $"{result}, components={this.componentCount})";
        }

        public override IReferenceCounted Touch() => this;

        public override IReferenceCounted Touch(object hint) => this;

        public override IByteBuffer DiscardSomeReadBytes() => this.DiscardReadComponents();

        protected internal override void Deallocate()
        {
            if (this.freed)
            {
                return;
            }

            this.freed = true;
            int size = this.componentCount;
            // We're not using foreach to avoid creating an iterator.
            // see https://github.com/netty/netty/issues/2642
            for (int i = 0; i < size; i++)
            {
                this.components[i].FreeIfNecessary();
            }
        }

        public override IByteBuffer Unwrap() => null;

        // Component array manipulation - range checking omitted

        void ClearComps()
        {
            this.RemoveCompRange(0, this.componentCount);
        }

        void RemoveComp(int i)
        {
            this.RemoveCompRange(i, i + 1);
        }

        void RemoveCompRange(int from, int to)
        {
            if (from >= to) { return; }

            int size = this.componentCount;
            Debug.Assert(from >= 0 && to <= size);
            if (to < size)
            {
                System.Array.Copy(this.components, to, this.components, from, size - to);
            }
            int newSize = size - to + from;
            for (int i = newSize; i < size; i++)
            {
                components[i] = null;
            }
            this.componentCount = newSize;
        }

        void AddComp(int i, ComponentEntry c)
        {
            this.ShiftComps(i, 1);
            this.components[i] = c;
        }

        void ShiftComps(int i, int count)
        {
            int size = this.componentCount, newSize = size + count;
            Debug.Assert(i >= 0 && i <= size && count > 0);
            if (newSize > this.components.Length)
            {
                // grow the array
                int newArrSize = Math.Max(size + (size >> 1), newSize);
                ComponentEntry[] newArr;
                newArr = new ComponentEntry[newArrSize];
                if (i == size)
                {
                    System.Array.Copy(this.components, 0, newArr, 0, Math.Min(this.components.Length, newArrSize));
                }
                else
                {
                    if (i > 0)
                    {
                        System.Array.Copy(this.components, 0, newArr, 0, i);
                    }
                    if (i < size)
                    {
                        System.Array.Copy(this.components, i, newArr, i + count, size - i);
                    }
                }
                this.components = newArr;
            }
            else if (i < size)
            {
                System.Array.Copy(this.components, i, this.components, i + count, size - i);
            }
            this.componentCount = newSize;
        }
    }
}