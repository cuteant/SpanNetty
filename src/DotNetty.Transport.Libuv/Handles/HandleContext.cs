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

namespace DotNetty.Transport.Libuv.Handles
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using DotNetty.Transport.Libuv.Native;

    internal sealed unsafe class HandleContext : NativeHandle
    {
        private static readonly uv_close_cb CloseCallback = h => OnCloseHandle(h);

        private readonly uv_handle_type _handleType;

        internal HandleContext(
            uv_handle_type handleType,
            Func<IntPtr, IntPtr, object[], int> initializer,
            IntPtr loopHandle,
            IInternalScheduleHandle target,
            params object[] args)
        {
            Debug.Assert(loopHandle != IntPtr.Zero);
            Debug.Assert(initializer is object);
            Debug.Assert(target is object);

            int size = NativeMethods.GetSize(handleType);
            IntPtr handle = NativeMethods.Allocate(size);

            try
            {
                int result = initializer(loopHandle, handle, args);
                NativeMethods.ThrowIfError(result);
            }
            catch (Exception)
            {
                NativeMethods.FreeMemory(handle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(target, GCHandleType.Normal);
            ((uv_handle_t*)handle)->data = GCHandle.ToIntPtr(gcHandle);

            Handle = handle;
            _handleType = handleType;

            if (Log.InfoEnabled) { Log.HandleAllocated(handleType, handle); }
        }

        internal bool IsActive => /*IsValid && */NativeMethods.IsHandleActive(Handle);

        internal bool IsClosing => /*IsValid && */NativeMethods.IsHandleClosing(Handle);

        internal void AddReference()
        {
            Validate();
            NativeMethods.AddReference(Handle);
        }

        internal void ReleaseReference()
        {
            Validate();
            NativeMethods.ReleaseReference(Handle);
        }

        internal bool HasReference()
        {
            Validate();
            return NativeMethods.HadReference(Handle);
        }

        protected override void CloseHandle()
        {
            IntPtr handle = Handle;
            if (handle == IntPtr.Zero) { return; }

            NativeMethods.CloseHandle(handle, CloseCallback);
            if (Log.InfoEnabled) { Log.HandleClosedReleasingResourcesPending(_handleType, handle); }
        }

        internal static T GetTarget<T>(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            IntPtr inernalHandle = ((uv_handle_t*)handle)->data;
            if (inernalHandle != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(inernalHandle);
                if (gcHandle.IsAllocated)
                {
                    return (T)gcHandle.Target;
                }
            }

            return default;
        }

        private static void OnCloseHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero) { return; }

            IInternalScheduleHandle scheduleHandle = null;

            // Get gc handle first
            IntPtr pHandle = ((uv_handle_t*)handle)->data;
            if (pHandle != IntPtr.Zero)
            {
                GCHandle nativeHandle = GCHandle.FromIntPtr(pHandle);
                if (nativeHandle.IsAllocated)
                {
                    scheduleHandle = nativeHandle.Target as IInternalScheduleHandle;
                    nativeHandle.Free();

                    ((uv_handle_t*)handle)->data = IntPtr.Zero;
#if DEBUG
                    if (Log.TraceEnabled)
                    {
                        Log.Trace("{} {} GCHandle released.", scheduleHandle?.HandleType, handle);
                    }
#endif
                }
            }

            // Release memory
            NativeMethods.FreeMemory(handle);
            scheduleHandle?.OnHandleClosed();
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} memory and GCHandle released.", scheduleHandle?.HandleType, handle);
            }
#endif
        }
    }
}
