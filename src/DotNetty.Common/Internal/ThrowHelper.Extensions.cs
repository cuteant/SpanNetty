using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNetty.Common
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
        internal static int ThrowException_Compare()
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("failed to compare two different constants");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowArgumentException(string name)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"'{name}' is already in use");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Slice(int length, int totalLength)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(nameof(length), $"length({length}) cannot be longer than Array.length({totalLength})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_Slice(int index, int length, int totalLength)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(nameof(length), $"index: ({index}), length({length}) index + length cannot be longer than Array.length({totalLength})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_SetRange_Index(int index, int srcLength, int totalLength)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(nameof(srcLength), $"index: ({index}), srcLength({srcLength}) index + length cannot be longer than Array.length({totalLength})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_SetRange_SrcIndex(int srcIndex, int srcLength, int totalLength)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(nameof(srcLength), $"index: ({srcIndex}), srcLength({srcLength}) index + length cannot be longer than src.length({totalLength})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_LogLevel(LogLevel logLevel)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_TooMany()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("too many thread-local indexed variables");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_RecycledAlready()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("recycled already");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ReleasedAlready()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("released already");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_RecycledMultiTimes()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("recycled multiple times");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidCastException<T>()
        {
            throw GetException();
            InvalidCastException GetException()
            {
                var msg = $"Type of policy requires pooled object policy of type {typeof(IThreadLocalPooledObjectPolicy<T>)}.";
                return new InvalidCastException(msg);
            }
        }
    }
}
