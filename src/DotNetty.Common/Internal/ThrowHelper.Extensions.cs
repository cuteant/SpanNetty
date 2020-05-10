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
        chars,
        assembly,
        argArray,
        buffer,
        increment,
        decrement,
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
        action,
        dst,
        e,
        src,
        seq,
        charSequence,
        sequence,
        delimiters,
        firstNameComponent,
        secondNameComponent,
        comparer,
        builder,
        start,
        end,
        resourceType,
        samplingInterval,
        thread,
        stack,
        handle,
        maxCapacity,
        aggregatePromise
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

            static Exception GetException()
            {
                return new Exception("failed to compare two different constants");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowException_CompareSignal()
        {
            throw GetException();

            static Exception GetException()
            {
                return new Exception("failed to compare two different signal constants");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_InvalidCodePoint()
        {
            throw GetException();

            static Exception GetException()
            {
                return new Exception("Invalid code point!");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_CodepointIsDecodedButNumberOfCharactersReadIs0OrNegative()
        {
            throw GetException();

            static Exception GetException()
            {
                return new Exception("Internal error: CodePoint is decoded but number of characters read is 0 or negative");
            }
        }

        #endregion

        #region -- ArgumentException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException(string name)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"'{name}' is already in use");
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
        internal static void ThrowArgumentException_MustBeGreaterThanZero(TimeSpan tickInterval)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{nameof(tickInterval)} must be greater than 0: {tickInterval}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MustBeGreaterThanZero(int ticksPerWheel)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{nameof(ticksPerWheel)} must be greater than 0: {ticksPerWheel}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MustBeGreaterThanOrEquelToZero(TimeSpan quietPeriod)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{nameof(quietPeriod)} must be greater than 0: {quietPeriod}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MustBeGreaterThanQuietPeriod(TimeSpan timeout, TimeSpan quietPeriod)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("timeout: " + timeout + " (expected >= quietPeriod (" + quietPeriod + "))");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ValueDiffers()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("value differs from one backed by this handle.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MustBeLessThanOrEqualTo()
        {
            throw GetException();

            static ArgumentException GetException()
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PriorityQueueIndex<T>(int index, T item)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"item.priorityQueueIndex(): {index} (expected: -1) + item: {item}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NotLongEnoughToHoldOutputValueUtf16()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException(
                    message: "Argument is not long enough to hold output value.",
                    paramName: "utf16");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NotLongEnoughToHoldOutputValueUtf8()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException(
                  message: "Argument is not long enough to hold output value.",
                  paramName: "utf8");
            }
        }

        #endregion

        #region -- ArgumentOutOfRangeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_MustBeGreaterThan(int ticksPerWheel)
        {
            throw GetException();
            ArgumentOutOfRangeException GetException()
            {
                return new ArgumentOutOfRangeException(nameof(ticksPerWheel),
                    $"{nameof(ticksPerWheel)} may not be greater than 2^30: {ticksPerWheel}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_StartIndex(ExceptionArgument argument)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), "StartIndex cannot be less than zero.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_EndIndexLessThanStartIndex()
        {
            throw GetArgumentOutOfRangeException();

            static ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("end", "EndIndex cannot be less than StartIndex.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_IndexLargerThanLength(ExceptionArgument argument)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(GetArgumentName(argument), $"{GetArgumentName(argument)} must be less than length of char sequence.");
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
        internal static void ArgumentOutOfRangeException_InvalidUnicodeChar()
        {
            throw GetNullReferenceException();

            static ArgumentOutOfRangeException GetNullReferenceException()
            {
                return new ArgumentOutOfRangeException(
                   message: "Value must be between U+0000 and U+D7FF, inclusive; or value must be between U+E000 and U+FFFF, inclusive.",
                   paramName: "char");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ArgumentOutOfRangeException_InvalidUnicodeValue()
        {
            throw GetNullReferenceException();

            static ArgumentOutOfRangeException GetNullReferenceException()
            {
                return new ArgumentOutOfRangeException(
                    message: "Value must be between U+0000 and U+D7FF, inclusive; or value must be between U+E000 and U+10FFFF, inclusive.",
                    paramName: "value");
            }
        }

        #endregion

        #region -- InvalidOperationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CannotBeCalledFromTimerTask()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException($"{nameof(HashedWheelTimer)}.stop() cannot be called from timer task.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_InvalidWorkerState()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("Invalid WorkerState");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CannotBeStartedOnceStopped()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("cannot be started once stopped");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EnumeratorNotInit()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("Enumerator not initialized.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EnumeratorAlreadyCompleted()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("Eumerator already completed.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_TooMany()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("too many thread-local indexed variables");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_RecycledAlready()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("recycled already");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ReleasedAlready()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("released already");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_AlreadyFinished()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("Already finished");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_AddingPromisesIsNotAllowedAfterFinishedAdding()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("Adding promises is not allowed after finished adding");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_RecycledMultiTimes()
        {
            throw GetException();

            static InvalidOperationException GetException()
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
                    string.Format("timeout.deadline {0} > deadline {1}", timeoutDeadline, deadline));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EnumeratorIsOnInvalidPosition()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("Enumerator is on invalid position");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_InvalidCharactersInTheString()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("Invalid characters in the string");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_MovenextNeedsToBeCalledAtLeastOnce()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("MoveNext() needs to be called at least once");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CurrentDoesNotExist()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("Current does not exist");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_InvalidUtf8CharacterBadlyEncoded()
        {
            throw GetException();

            static Exception GetException()
            {
                return new Exception("Invalid UTF-8 character (badly encoded)");
            }
        }

        #endregion

        #region -- InvalidCastException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidCastException<T>()
        {
            throw GetException();

            static InvalidCastException GetException()
            {
                var msg = $"Type of policy requires pooled object policy of type {typeof(IThreadLocalPooledObjectPolicy<T>)}.";
                return new InvalidCastException(msg);
            }
        }

        #endregion

        #region -- IndexOutOfRangeException --

        internal static void ThrowIndexOutOfRangeException_Start(int start, int length, int count)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= start({0}) <= start + length({1}) <= value.length({2})", start, length, count));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException_Index(int index, int length, int capacity)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("index: {0}, length: {1} (expected: range(0, {2}))", index, length, capacity));
            }
        }

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

            static FormatException GetException()
            {
                return new FormatException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException_Radix()
        {
            throw GetException();

            static FormatException GetException()
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

            static RejectedExecutionException GetException()
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
