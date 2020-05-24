using System;
using System.Runtime.CompilerServices;
using DotNetty.Common.Utilities;

namespace DotNetty.Buffers
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        s,

        pi,
        fi,
        ts,

        asm,
        buf,
        dst,
        key,
        obj,
        src,
        str,

        data,
        leak,
        func,
        path,
        type,
        name,
        item,
        list,
        pool,

        alloc,
        array,
        count,
        types,
        value,
        other,
        match,
        index,
        inner,

        buffer,
        output,
        values,
        source,
        policy,
        offset,
        method,
        target,
        length,
        member,

        buffers,
        options,
        feature,
        manager,
        invoker,
        newSize,

        assembly,
        capacity,
        dstIndex,
        encoding,
        fullName,
        srcIndex,
        typeInfo,
        typeName,

        allocator,
        decrement,
        defaultFn,
        fieldInfo,
        increment,
        predicate,

        returnType,
        memberInfo,
        bufferSize,
        byteBuffer,
        collection,
        startIndex,
        expression,
        nHeapArena,

        maxCapacity,
        destination,
        reqCapacity,
        directories,
        dirEnumArgs,
        frameLength,

        nDirectArena,
        propertyInfo,
        elementIndex,
        initialArray,
        instanceType,
        valueFactory,

        attributeType,
        initialBuffer,

        parameterTypes,
        maxFrameLength,
        newMaxCapacity,
        trackedByteBuf,
        minNewCapacity,

        initialCapacity,

        minWritableBytes,

        assemblyPredicate,
        lengthFieldOffset,
        qualifiedTypeName,

        includedAssemblies,

        initialBytesToStrip,

        maxCachedBufferCapacity,

        freeSweepAllocationThreshold,

    }

    #endregion

    #region -- ExceptionResource --

    /// <summary>The convention for this enum is using the resource name as the enum name</summary>
    internal enum ExceptionResource
    {
    }

    #endregion

    partial class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowException_ShouldNotReachHere<T>()
        {
            throw GetException();

            static Exception GetException()
            {
                return new Exception("should not reach here");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException(int capacity)
        {
            throw GetException(capacity);

            static InvalidOperationException GetException(int capacity)
            {
                return new InvalidOperationException($"Cannot advance past the end of the buffer, which has a size of {capacity}.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EndPositionNotReached() { throw CreateInvalidOperationException_EndPositionNotReached(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Exception CreateInvalidOperationException_EndPositionNotReached()
        {
            return new InvalidOperationException("EndPositionNotReached");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowInvalidOperationException_ShouldNotReachHere<T>()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("should not reach here");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NeedMoreData()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Destination is too short.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_DestinationTooShort()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Destination is too short.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Positive(int value, ExceptionArgument argument)
        {
            throw GetException(value, argument);

            static ArgumentOutOfRangeException GetException(int value, ExceptionArgument argument)
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), $"{GetArgumentName(argument)}: {value} (expected: > 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Positive(long value, ExceptionArgument argument)
        {
            throw GetException(value, argument);

            static ArgumentOutOfRangeException GetException(long value, ExceptionArgument argument)
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), $"{GetArgumentName(argument)}: {value} (expected: > 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PositiveOrZero(int value, ExceptionArgument argument)
        {
            throw GetException(value, argument);

            static ArgumentOutOfRangeException GetException(int value, ExceptionArgument argument)
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), $"{GetArgumentName(argument)}: {value} (expected: >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PositiveOrZero(long value, ExceptionArgument argument)
        {
            throw GetException(value, argument);

            static ArgumentOutOfRangeException GetException(long value, ExceptionArgument argument)
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), $"{GetArgumentName(argument)}: {value} (expected: >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InitialCapacityMaxCapacity(int initialCapacity, int maxCapacity)
        {
            throw GetArgumentException(initialCapacity, maxCapacity);

            static ArgumentException GetArgumentException(int initialCapacity, int maxCapacity)
            {
                return new ArgumentException($"initialCapacity({initialCapacity}) > maxCapacity({maxCapacity})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ExpectedPowerOf2()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("Expected power of 2", "pageSize");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckMaxOrder30(int maxOrder)
        {
            throw GetArgumentException(maxOrder);

            static ArgumentException GetArgumentException(int maxOrder)
            {
                return new ArgumentException("maxOrder should be < 30, but is: " + maxOrder, nameof(maxOrder));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckMaxOrder14(int maxOrder)
        {
            throw GetArgumentException(maxOrder);

            static ArgumentException GetArgumentException(int maxOrder)
            {
                return new ArgumentException("maxOrder: " + maxOrder + " (expected: 0-14)", nameof(maxOrder));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckMaxNumComponents(int maxNumComponents)
        {
            throw GetArgumentException(maxNumComponents);

            static ArgumentException GetArgumentException(int maxNumComponents)
            {
                return new ArgumentException("maxNumComponents: " + maxNumComponents + " (expected: >= 1)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_LenIsTooBig()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("The total length of the specified buffers is too big.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NonNegative()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("All indexes and lengths must be non-negative");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CopyArray()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("The total length of the specified arrays is too big.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CopyBuffer()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("The total length of the specified buffers is too big.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InitialCapacity(int initialCapacity, int maxCapacity)
        {
            throw GetArgumentException(initialCapacity, maxCapacity);

            static ArgumentException GetArgumentException(int initialCapacity, int maxCapacity)
            {
                return new ArgumentException($"initialCapacity({initialCapacity}) > maxCapacity({maxCapacity})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PageSize(int pageSize, int maxOrder, int maxChunkSize)
        {
            throw GetArgumentException(pageSize, maxOrder, maxChunkSize);

            static ArgumentException GetArgumentException(int pageSize, int maxOrder, int maxChunkSize)
            {
                return new ArgumentException($"pageSize ({pageSize}) << maxOrder ({maxOrder}) must not exceed {maxChunkSize}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidOffLen()
        {
            throw GetArgumentOutOfRangeException();

            static ArgumentException GetArgumentOutOfRangeException()
            {
                return new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_FailedToGetLargerSpan()
        {
            throw GetArgumentOutOfRangeException();

            static ArgumentException GetArgumentOutOfRangeException()
            {
                return new ArgumentException("The 'IByteBuffer' could not provide an output buffer that is large enough to continue writing.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_FailedToGetMinimumSizeSpan(int minimumSize)
        {
            throw GetArgumentOutOfRangeException(minimumSize);

            static ArgumentException GetArgumentOutOfRangeException(int minimumSize)
            {
                return new ArgumentException($"The 'IByteBuffer' could not provide an output buffer that is large enough to continue writing. Need at least {minimumSize} bytes.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_IsText(int index, int length)
        {
            throw GetIndexOutOfRangeException(index, length);

            static IndexOutOfRangeException GetIndexOutOfRangeException(int index, int length)
            {
                return new IndexOutOfRangeException($"index: {index}length: {length}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_CheckSliceOutOfBounds(int index, int length, IByteBuffer buffer)
        {
            throw GetIndexOutOfRangeException(index, length, buffer);

            static IndexOutOfRangeException GetIndexOutOfRangeException(int index, int length, IByteBuffer buffer)
            {
                return new IndexOutOfRangeException($"{buffer}.Slice({index}, {length})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_Expected(int offset, int length, int capacity)
        {
            throw GetIndexOutOfRangeException(offset, length, capacity);

            static IndexOutOfRangeException GetIndexOutOfRangeException(int offset, int length, int capacity)
            {
                return new IndexOutOfRangeException($"expected: 0 <= offset({offset}) <= offset + length({length}) <= buf.capacity({capacity}{')'}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_Expected_Seq(int start, int seqCount, int end)
        {
            throw GetIndexOutOfRangeException(start, seqCount, end);

            static IndexOutOfRangeException GetIndexOutOfRangeException(int start, int seqCount, int end)
            {
                return new IndexOutOfRangeException("expected: 0 <= start(" + start + ") <= end (" + end
                        + ") <= seq.length(" + seqCount + ')');
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument argument)
        {
            throw GetArgumentOutOfRangeException(argument);

            static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument)
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), $"The {GetArgumentName(argument)} cannot be negative.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index(int cIndex, int count)
        {
            throw GetArgumentOutOfRangeException(cIndex, count);

            static ArgumentOutOfRangeException GetArgumentOutOfRangeException(int cIndex, int count)
            {
                return new ArgumentOutOfRangeException(nameof(cIndex), $"cIndex: {cIndex} (expected: >= 0 && <= numComponents({count}))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index(int cIndex, int numComponents, int count)
        {
            throw GetArgumentOutOfRangeException(cIndex, numComponents, count);

            static ArgumentOutOfRangeException GetArgumentOutOfRangeException(int cIndex, int numComponents, int count)
            {
                return new ArgumentOutOfRangeException(nameof(cIndex), $"cIndex: {cIndex}, numComponents: {numComponents} " + $"(expected: cIndex >= 0 && cIndex + numComponents <= totalNumComponents({count}))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_CheckIndexBounds(int readerIndex, int writerIndex, int capacity)
        {
            throw GetIndexOutOfRangeException(readerIndex, writerIndex, capacity);

            static IndexOutOfRangeException GetIndexOutOfRangeException(int readerIndex, int writerIndex, int capacity)
            {
                throw new IndexOutOfRangeException(
                    $"readerIndex: {readerIndex}, writerIndex: {writerIndex} (expected: 0 <= readerIndex <= writerIndex <= capacity({capacity}))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIllegalReferenceCountException(int refCnt, int increment)
        {
            throw GetIllegalReferenceCountException(refCnt, increment);

            static IllegalReferenceCountException GetIllegalReferenceCountException(int refCnt, int increment)
            {
                return new IllegalReferenceCountException(refCnt, increment);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException_StreamIsClosed()
        {
            throw GetObjectDisposedException();

            static ObjectDisposedException GetObjectDisposedException()
            {
                return new ObjectDisposedException(null, "Cannot access a closed Stream.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException_StreamIsClosed(ExceptionArgument argument)
        {
            throw GetObjectDisposedException(argument);

            static ObjectDisposedException GetObjectDisposedException(ExceptionArgument argument)
            {
                return new ObjectDisposedException(GetArgumentName(argument), "Cannot access a closed Stream.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_NeedPosNum(ExceptionArgument argument, int value)
        {
            throw GetArgumentOutOfRangeException(argument, value);

            static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument, int value)
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), value, "Positive number required.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException()
        {
            throw GetNotSupportedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotSupportedException GetNotSupportedException()
        {
            return new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_UncompositeBuffer()
        {
            throw GetNotSupportedException();

            static NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException("ByteBufferWriter does not support composite buffer.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_UnreadableStream()
        {
            throw GetNotSupportedException();

            static NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException("Stream does not support reading.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_UnseekableStream()
        {
            throw GetNotSupportedException();

            static NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException("Stream does not support seeking.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_UnwritableStream()
        {
            throw GetNotSupportedException();

            static NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException("Stream does not support writing.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowReadOnlyBufferException()
        {
            throw GetReadOnlyBufferException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowReadOnlyBufferException<T>()
        {
            throw GetReadOnlyBufferException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ReadOnlyBufferException GetReadOnlyBufferException()
        {
            return new ReadOnlyBufferException();
        }
    }
}
