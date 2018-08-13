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
        internal static T ThrowGetException_ShouldNotReachHere<T>()
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("should not reach here");
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
        internal static void ThrowArgumentException_Sum()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException($"The sum of offset and count is larger than the output length");
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
        internal static void ThrowArgumentOutOfRangeException_Index(int cIndex, int count)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException($"cIndex: {cIndex} (expected: >= 0 && <= numComponents({count}))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Index(int cIndex, int numComponents, int count)
        {
            throw GetArgumentOutOfRangeException();

            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException($"cIndex: {cIndex}, numComponents: {numComponents} " + $"(expected: cIndex >= 0 && cIndex + numComponents <= totalNumComponents({count}))");
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
    }
}
