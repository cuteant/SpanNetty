using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;

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
        task,
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
        #region -- Exception --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowException_CompareConstant()
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("failed to compare two different constants");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowException_CompareSignal()
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("failed to compare two different signal constants");
            }
        }

        #endregion

        #region -- ArgumentException --

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
        internal static int ThrowArgumentException_MustBeGreaterThanZero(TimeSpan tickInterval)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{nameof(tickInterval)} must be greater than 0: {tickInterval}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowArgumentException_MustBeGreaterThanZero(int ticksPerWheel)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{nameof(ticksPerWheel)} must be greater than 0: {ticksPerWheel}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowArgumentException_MustBeLessThanOrEqualTo()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"tickInterval must be less than or equal to ${int.MaxValue} ms.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_DecodeHexByte(string s, int pos)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"invalid hex byte '{s.Substring(pos, 2)}' at index {pos} of '{s}'");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidLen(int length)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"length: {length}");
            }
        }

        #endregion

        #region -- ArgumentOutOfRangeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowArgumentOutOfRangeException_MustBeGreaterThan(int ticksPerWheel)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                return new ArgumentOutOfRangeException(
                    $"{nameof(ticksPerWheel)} may not be greater than 2^30: {ticksPerWheel}");
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

        #endregion

        #region -- InvalidOperationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CannotBeCalledFromTimerTask()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"{nameof(HashedWheelTimer)}.stop() cannot be called from timer task.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_InvalidWorkerState()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Invalid WorkerState");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CannotBeStartedOnceStopped()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("cannot be started once stopped");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EnumeratorNotInit()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Enumerator not initialized.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EnumeratorAlreadyCompleted()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Eumerator already completed.");
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
        internal static void ThrowInvalidOperationException_CapacityMustBePositive(int newCapacity)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"New capacity {newCapacity} must be positive");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Unexpected(Signal signal)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"unexpected signal: {signal}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Deadline(TimeSpan timeoutDeadline, TimeSpan deadline)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException(
                    string.Format("timeout.deadline (%d) > deadline (%d)", timeoutDeadline, deadline));
            }
        }

        #endregion

        #region -- InvalidCastException --

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

        #endregion

        #region -- IndexOutOfRangeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_ParseChar(int start)
        {
            throw GetArgumentOutOfRangeException();
            IndexOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new IndexOutOfRangeException($"2 bytes required to convert to character. index {start} would go out of bounds.");
            }
        }

        #endregion

        #region -- FormatException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException()
        {
            throw GetException();
            FormatException GetException()
            {
                return new FormatException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException_Radix()
        {
            throw GetException();
            FormatException GetException()
            {
                return new FormatException($"Radix must be from {CharUtil.MinRadix} to {CharUtil.MaxRadix}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException(ICharSequence seq, int start, int end)
        {
            throw GetException();
            FormatException GetException()
            {
                return new FormatException(seq.SubSequence(start, end).ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException(int start, int end)
        {
            throw GetException();
            FormatException GetException()
            {
                return new FormatException($"Content is empty because {start} and {end} are the same.");
            }
        }

        #endregion

        #region -- RejectedExecutionException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowRejectedExecutionException_TimerStopped()
        {
            throw GetException();
            RejectedExecutionException GetException()
            {
                return new RejectedExecutionException("Timer has been stopped and cannot process new operations.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowRejectedExecutionException_NumOfPendingTimeouts(long pendingTimeoutsCount, long maxPendingTimeouts)
        {
            throw GetException();
            RejectedExecutionException GetException()
            {
                return new RejectedExecutionException($"Number of pending timeouts ({pendingTimeoutsCount}) is greater than or equal to maximum allowed pending timeouts ({maxPendingTimeouts})");
            }
        }

        #endregion
    }
}
