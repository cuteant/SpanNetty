using System;
using System.Runtime.CompilerServices;
using DotNetty.Common.Utilities;

namespace DotNetty.Buffers
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        array,
        bufferSize,
        output,
        maxCachedBufferCapacity,
        freeSweepAllocationThreshold,
        nHeapArena,
        nDirectArena,
        assembly,
        buffer,
        destination,
        key,
        obj,
        s,
        str,
        source,
        type,
        types,
        value,
        values,
        valueFactory,
        name,
        reqCapacity,
        item,
        options,
        list,
        ts,
        other,
        pool,
        inner,
        policy,
        offset,
        count,
        path,
        typeInfo,
        method,
        qualifiedTypeName,
        fullName,
        feature,
        manager,
        directories,
        dirEnumArgs,
        asm,
        includedAssemblies,
        func,
        defaultFn,
        returnType,
        propertyInfo,
        parameterTypes,
        fieldInfo,
        memberInfo,
        attributeType,
        pi,
        fi,
        invoker,
        instanceType,
        target,
        member,
        typeName,
        predicate,
        assemblyPredicate,
        collection,
        capacity,
        match,
        index,
        length,
        startIndex,
        newSize,
        expression,
        data,
        maxCapacity,
        newMaxCapacity,
        minWritableBytes,
        src,
        increment,
        decrement,
        buf,
        encoding,
        allocator,
        buffers,
        trackedByteBuf,
        leak,
        alloc,
        initialArray,
        initialBuffer,
        initialCapacity,
        dst,
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
            Exception GetException()
            {
                return new Exception("should not reach here");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowInvalidOperationException_ShouldNotReachHere<T>()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("should not reach here");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Positive(int value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: > 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Positive(long value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: > 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PositiveOrZero(int value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PositiveOrZero(long value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InitialCapacityMaxCapacity(int initialCapacity, int maxCapacity)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException($"initialCapacity({initialCapacity}) > maxCapacity({maxCapacity})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ExpectedPowerOf2()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Expected power of 2", "pageSize");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckMaxOrder30(int maxOrder)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("maxOrder should be < 30, but is: " + maxOrder, nameof(maxOrder));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckMaxOrder14(int maxOrder)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("maxOrder: " + maxOrder + " (expected: 0-14)", nameof(maxOrder));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckMaxNumComponents(int maxNumComponents)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("maxNumComponents: " + maxNumComponents + " (expected: >= 2)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckIndex(int length)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("length: " + length);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CheckLength(int length)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("length: " + length + " (expected: >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_LenIsTooBig()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("The total length of the specified buffers is too big.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NonNegative()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("All indexes and lengths must be non-negative");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CopyArray()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("The total length of the specified arrays is too big.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CopyBuffer()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("The total length of the specified buffers is too big.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InitialCapacity(int initialCapacity, int maxCapacity)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException($"initialCapacity({initialCapacity}) > maxCapacity({maxCapacity})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PageSize(int pageSize, int maxOrder, int maxChunkSize)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException($"pageSize ({pageSize}) << maxOrder ({maxOrder}) must not exceed {maxChunkSize}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidOffLen()
        {
            throw GetArgumentOutOfRangeException();

            ArgumentException GetArgumentOutOfRangeException()
            {
                return new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_IsText(int index, int length)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException($"index: {index}length: {length}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_CheckSliceOutOfBounds(int index, int length, IByteBuffer buffer)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException($"{buffer}.Slice({index}, {length})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_Expected(int offset, int length, int capacity)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException($"expected: 0 <= offset({offset}) <= offset + length({length}) <= buf.capacity({capacity}{')'}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument argument)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), $"The {GetArgumentName(argument)} cannot be negative.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index(int cIndex, int count)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(nameof(cIndex), $"cIndex: {cIndex} (expected: >= 0 && <= numComponents({count}))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index(int cIndex, int numComponents, int count)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(nameof(cIndex), $"cIndex: {cIndex}, numComponents: {numComponents} " + $"(expected: cIndex >= 0 && cIndex + numComponents <= totalNumComponents({count}))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_CheckIndexBounds(int readerIndex, int writerIndex, int capacity)
        {
            throw GetIndexOutOfRangeException();
            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                throw new IndexOutOfRangeException(
                    $"readerIndex: {readerIndex}, writerIndex: {writerIndex} (expected: 0 <= readerIndex <= writerIndex <= capacity({capacity}))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIllegalReferenceCountException(int refCnt, int increment)
        {
            throw GetIllegalReferenceCountException();

            IllegalReferenceCountException GetIllegalReferenceCountException()
            {
                return new IllegalReferenceCountException(refCnt, increment);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException_StreamIsClosed()
        {
            throw GetObjectDisposedException();

            ObjectDisposedException GetObjectDisposedException()
            {
                return new ObjectDisposedException(null, "Cannot access a closed Stream.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException_StreamIsClosed(ExceptionArgument argument)
        {
            throw GetObjectDisposedException();

            ObjectDisposedException GetObjectDisposedException()
            {
                return new ObjectDisposedException(GetArgumentName(argument), "Cannot access a closed Stream.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_NeedPosNum(ExceptionArgument argument, int value)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), value, "Positive number required.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException()
        {
            throw GetNotSupportedException();

            NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_UnreadableStream()
        {
            throw GetNotSupportedException();

            NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException("Stream does not support reading.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_UnseekableStream()
        {
            throw GetNotSupportedException();

            NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException("Stream does not support seeking.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_UnwritableStream()
        {
            throw GetNotSupportedException();

            NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException("Stream does not support writing.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowReadOnlyBufferException()
        {
            throw GetException();

            ReadOnlyBufferException GetException()
            {
                return new ReadOnlyBufferException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowReadOnlyBufferException<T>()
        {
            throw GetException();

            ReadOnlyBufferException GetException()
            {
                return new ReadOnlyBufferException();
            }
        }
    }
}
