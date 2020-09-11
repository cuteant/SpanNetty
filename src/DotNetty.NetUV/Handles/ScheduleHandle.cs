/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.NetUV.Handles
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.NetUV.Native;

    public abstract class ScheduleHandle : IDisposable
    {
        protected static readonly IInternalLogger Log = InternalLoggerFactory.GetInstance<ScheduleHandle>();

        private readonly HandleContext _handle;
        private Action<ScheduleHandle> _closeCallback;

        internal ScheduleHandle(
            LoopContext loop,
            uv_handle_type handleType,
            object[] args = null)
        {
            if (loop is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.loop); }

            HandleContext initialHandle = NativeMethods.Initialize(loop.Handle, handleType, this, args);
            Debug.Assert(initialHandle is object);

            _handle = initialHandle;
            HandleType = handleType;
        }

        public bool IsActive => _handle.IsActive;

        public bool IsClosing => _handle.IsClosing;

        public bool IsValid
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _handle.IsValid;
        }

        public object UserToken { get; set; }

        internal IntPtr InternalHandle
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _handle.Handle;
        }

        internal uv_handle_type HandleType { get; }

        internal void OnHandleClosed()
        {
            try
            {
                _handle.SetHandleAsInvalid();
                _closeCallback?.Invoke(this);
            }
            catch (Exception exception)
            {
                Log.Handle_close_handle_callback_error(HandleType, exception);
            }
            finally
            {
                _closeCallback = null;
                UserToken = null;
            }
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal void Validate() => _handle.Validate();

        public unsafe bool TryGetLoop(out Loop loop)
        {
            loop = null;
            try
            {
                IntPtr nativeHandle = InternalHandle;
                if (nativeHandle == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr loopHandle = ((uv_handle_t*)nativeHandle)->loop;
                if (loopHandle != IntPtr.Zero)
                {
                    loop = HandleContext.GetTarget<Loop>(loopHandle);
                }

                return loop is object;
            }
            catch (Exception exception)
            {
                Log.Failed_to_get_loop(HandleType, exception);
                return false;
            }
        }

        protected internal void CloseHandle(Action<ScheduleHandle> handler = null)
        {
            try
            {
                ScheduleClose(handler);
            }
            catch (Exception exception)
            {
                Log.Failed_to_close_handle(HandleType, exception);
                throw;
            }
        }

        protected virtual void ScheduleClose(Action<ScheduleHandle> handler = null)
        {
            if (!IsValid) { return; }

            _closeCallback = handler;
            Close();
            _handle.Dispose();
        }

        protected abstract void Close();

        protected void StopHandle()
        {
            if (!IsValid) { return; }

            NativeMethods.Stop(HandleType, _handle.Handle);
        }

        public void AddReference()
        {
            if (!IsValid) { return; }

            _handle.AddReference();
        }

        public void RemoveReference()
        {
            if (!IsValid) { return; }

            _handle.ReleaseReference();
        }

        public bool HasReference() => IsValid && _handle.HasReference();

        public void Dispose()
        {
            try
            {
                CloseHandle();
            }
            catch (Exception exception)
            {
                Log.Failed_to_close_and_releasing_resources(_handle, exception);
            }
        }
    }
}
