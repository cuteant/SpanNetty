/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Handles;

    static class PlatformApis
    {
        const int AF_INET6_LINUX = 10;
        const int AF_INET6_OSX = 30;

#if NETCOREAPP || NETSTANDARD
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static IntPtr Allocate(int size) => Marshal.AllocCoTaskMem(size);

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static void FreeMemory(IntPtr ptr) => Marshal.FreeCoTaskMem(ptr);
#else
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static IntPtr Allocate(int size) => Marshal.AllocHGlobal(size);

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static void FreeMemory(IntPtr ptr) => Marshal.FreeHGlobal(ptr);
#endif
        internal static string GetPipeName()
        {
            return GetPipeName("SpanNetty_" + Guid.NewGuid().ToString("n"));
        }

        internal static string GetPipeName(string pipeName)
        {
            if (string.IsNullOrEmpty(pipeName)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.pipeName); }

            if (Platform.IsWindows)
            {
                return @"\\.\pipe\" + pipeName;
            }

            return "/tmp/" + pipeName;
        }

        internal static uint GetAddressFamily(AddressFamily addressFamily)
        {
            // AF_INET 2
            if (addressFamily == AddressFamily.InterNetwork || Platform.IsWindows)
            {
                return (uint)addressFamily;
            }

            if (Platform.IsLinux)
            {
                return AF_INET6_LINUX;
            }

            if (Platform.IsDarwin)
            {
                return AF_INET6_OSX;
            }

            return ThrowHelper.FromInvalidOperationException_Dispatch(addressFamily);
        }

        internal static bool GetReuseAddress(IStreamHandle handle)
        {
            IntPtr socketHandle = GetSocketHandle(handle);

            if (Platform.IsWindows)
            {
                return WindowsApi.GetReuseAddress(socketHandle);
            }
            return UnixApi.GetReuseAddress(socketHandle);
        }

        internal static void SetReuseAddress(IStreamHandle handle, int value)
        {
            IntPtr socketHandle = GetSocketHandle(handle);
            if (Platform.IsWindows)
            {
                WindowsApi.SetReuseAddress(socketHandle, value);
            }
            else
            {
                UnixApi.SetReuseAddress(socketHandle, value);
            }
        }

        internal static bool GetReusePort(IStreamHandle handle)
        {
            if (Platform.IsWindows) { return GetReuseAddress(handle); }

            IntPtr socketHandle = GetSocketHandle(handle);
            return UnixApi.GetReusePort(socketHandle);
        }

        internal static void SetReusePort(IStreamHandle handle, int value)
        {
            IntPtr socketHandle = GetSocketHandle(handle);
            // Ignore SO_REUSEPORT on Windows because it is controlled
            // by SO_REUSEADDR
            if (Platform.IsWindows) { return; }

            UnixApi.SetReusePort(socketHandle, value);
        }

        static IntPtr GetSocketHandle(IStreamHandle handle)
        {
            IntPtr socket = IntPtr.Zero;
            handle.GetFileDescriptor(ref socket);
            return socket;
        }
    }
}
