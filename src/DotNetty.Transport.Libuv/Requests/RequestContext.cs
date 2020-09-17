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

namespace DotNetty.Transport.Libuv.Requests
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Transport.Libuv.Native;

    internal sealed unsafe class RequestContext : NativeHandle
    {
        private readonly uv_req_type _requestType;
        private readonly int _handleSize;

        internal RequestContext(uv_req_type requestType, int size, ScheduleRequest target)
        {
            Debug.Assert(size >= 0);
            Debug.Assert(target is object);

            _handleSize = NativeMethods.GetSize(requestType);
            int totalSize = _handleSize + size;
            IntPtr handle = NativeMethods.Allocate(totalSize);

            GCHandle gcHandle = GCHandle.Alloc(target, GCHandleType.Normal);
            *(IntPtr*)handle = GCHandle.ToIntPtr(gcHandle);

            Handle = handle;
            _requestType = requestType;
#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} {} allocated.", requestType, handle);
            }
#endif
        }

        internal RequestContext(uv_req_type requestType, Action<IntPtr> initializer, ScheduleRequest target)
        {
            Debug.Assert(initializer is object);
            Debug.Assert(target is object);

            _handleSize = NativeMethods.GetSize(requestType);
            IntPtr handle = NativeMethods.Allocate(_handleSize);

            try
            {
                initializer(handle);
            }
            catch
            {
                NativeMethods.FreeMemory(handle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(target, GCHandleType.Normal);
            *(IntPtr*)handle = GCHandle.ToIntPtr(gcHandle);

            Handle = handle;
            _requestType = requestType;
#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} {} allocated.", requestType, handle);
            }
#endif
        }

        internal int HandleSize
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _handleSize;
        }

        internal static T GetTarget<T>(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            IntPtr internalHandle = ((uv_req_t*)handle)->data;
            if (internalHandle != IntPtr.Zero)
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(internalHandle);
                if (gcHandle.IsAllocated)
                {
                    return (T)gcHandle.Target;
                }
            }

            return default;
        }

        protected override void CloseHandle()
        {
            IntPtr handle = Handle;
            if (handle == IntPtr.Zero) { return; }

            IntPtr pHandle = ((uv_req_t*)handle)->data;

            // Free GCHandle
            if (pHandle != IntPtr.Zero)
            {
                GCHandle nativeHandle = GCHandle.FromIntPtr(pHandle);
                if (nativeHandle.IsAllocated)
                {
                    nativeHandle.Free();
                    ((uv_req_t*)handle)->data = IntPtr.Zero;
#if DEBUG
                    if (Log.DebugEnabled)
                    {
                        Log.Debug("{} {} GCHandle released.", _requestType, handle);
                    }
#endif
                }
            }

            // Release memory
            NativeMethods.FreeMemory(handle);
            Handle = IntPtr.Zero;

#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} {} memory released.", _requestType, handle);
            }
#endif
        }
    }
}
