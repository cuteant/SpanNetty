using System;
using System.Runtime.CompilerServices;

namespace DotNetty.Common.Internal
{
    partial class QueueX<T>
    {
        public bool IsEmpty
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get { return 0u >= (uint)_size; }
        }

        public bool NonEmpty
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get { return _size > 0; }
        }

        #region -- TryDequeueIf --

        public bool TryDequeueIf(Predicate<T> predicate, out T result)
        {
            if (0u >= (uint)_size)
            {
                result = default;
                return false;
            }

            int head = _head;
            T[] array = _array;

            result = array[head];
            if (!predicate(result)) { return false; }

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                array[head] = default;
            }
#else
            array[head] = default;
#endif
            MoveNext(ref _head);
            _size--;
            _version++;
            return true;
        }

        #endregion

        #region -- ForEach --

        public void ForEach(Action<T> action)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = 0;
            while (idx < _size)
            {
                action(DoGetItem(idx));
                idx++;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void ForEach<TArg>(Action<T, TArg> action, TArg arg)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = 0;
            while (idx < _size)
            {
                action(DoGetItem(idx), arg);
                idx++;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void ForEach<TArg1, TArg2>(Action<T, TArg1, TArg2> action, TArg1 arg1, TArg2 arg2)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = 0;
            while (idx < _size)
            {
                action(DoGetItem(idx), arg1, arg2);
                idx++;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }


        public void ForEach(Action<T, int> action)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = 0;
            while (idx < _size)
            {
                action(DoGetItem(idx), idx);
                idx++;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void ForEach<TArg>(Action<T, int, TArg> action, TArg arg)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = 0;
            while (idx < _size)
            {
                action(DoGetItem(idx), idx, arg);
                idx++;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void ForEach<TArg1, TArg2>(Action<T, int, TArg1, TArg2> action, TArg1 arg1, TArg2 arg2)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = 0;
            while (idx < _size)
            {
                action(DoGetItem(idx), idx, arg1, arg2);
                idx++;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        #endregion

        #region -- Reverse --

        public void Reverse(Action<T> action)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = _size - 1;
            while (idx >= 0)
            {
                action(DoGetItem(idx));
                idx--;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void Reverse<TArg>(Action<T, TArg> action, TArg arg)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = _size - 1;
            while (idx >= 0)
            {
                action(DoGetItem(idx), arg);
                idx--;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void Reverse<TArg1, TArg2>(Action<T, TArg1, TArg2> action, TArg1 arg1, TArg2 arg2)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = _size - 1;
            while (idx >= 0)
            {
                action(DoGetItem(idx), arg1, arg2);
                idx--;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void Reverse(Action<T, int> action)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = _size - 1;
            while (idx >= 0)
            {
                action(DoGetItem(idx), idx);
                idx--;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void Reverse<TArg>(Action<T, int, TArg> action, TArg arg)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = _size - 1;
            while (idx >= 0)
            {
                action(DoGetItem(idx), idx, arg);
                idx--;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        public void Reverse<TArg1, TArg2>(Action<T, int, TArg1, TArg2> action, TArg1 arg1, TArg2 arg2)
        {
            if (null == action) { ThrowArgumentNullException_Action(); }
            if (IsEmpty) { return; }

            var version = _version;

            var idx = _size - 1;
            while (idx >= 0)
            {
                action(DoGetItem(idx), idx, arg1, arg2);
                idx--;
            }

            if (version != _version) { ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion(); }
        }

        #endregion

        #region -- TrueForAll --

        public bool TrueForAll(Predicate<T> match)
        {
            if (match == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            }

            if (IsEmpty) { return false; }

            var idx = 0;
            while (idx < _size)
            {
                if (!match(DoGetItem(idx))) { return false; }
                idx++;
            }
            return true;
        }

        #endregion

        #region ** DoGetItem **

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private T DoGetItem(int index)
        {
            int capacity = _array.Length;

            // _index represents the 0-based index into the queue, however the queue
            // doesn't have to start from 0 and it may not even be stored contiguously in memory.

            int arrayIndex = _head + index; // this is the actual index into the queue's backing array
            if (arrayIndex >= capacity)
            {
                // NOTE: Originally we were using the modulo operator here, however
                // on Intel processors it has a very high instruction latency which
                // was slowing down the loop quite a bit.
                // Replacing it with simple comparison/subtraction operations sped up
                // the average foreach loop by 2x.

                arrayIndex -= capacity; // wrap around if needed
            }
            return _array[arrayIndex];
        }

        #endregion

        #region ** ThrowHelper **

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentNullException_Action()
        {
            throw GetArgumentNullException();

            ArgumentNullException GetArgumentNullException()
            {
                return new ArgumentNullException("action");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion()
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion); ;
            }
        }

        #endregion

        #region ** SR **

        static class SR
        {
            internal const string InvalidOperation_EmptyQueue = "Queue empty.";

            internal const string InvalidOperation_EnumFailedVersion = "Collection was modified; enumeration operation may not execute.";

            internal const string InvalidOperation_EnumNotStarted = "Enumeration has not started. Call MoveNext.";

            internal const string InvalidOperation_EnumEnded = "Enumeration already finished.";

            internal const string Argument_InvalidArrayType = "Target array type is not compatible with the type of items in the collection.";

            internal const string Argument_InvalidOffLen = "Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.";

            internal const string ArgumentOutOfRange_Index = "Index was out of range. Must be non-negative and less than the size of the collection.";

            internal const string Arg_NonZeroLowerBound = "The lower bound of target array must be zero.";

            internal const string Arg_RankMultiDimNotSupported = "Only single dimensional arrays are supported for the requested action.";

            internal const string ArgumentOutOfRange_NeedNonNegNum = "Non-negative number required.";
        }

        #endregion
    }
}
