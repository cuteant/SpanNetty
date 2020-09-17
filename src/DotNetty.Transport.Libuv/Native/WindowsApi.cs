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
    using System.Runtime.InteropServices;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Handles;

    //https://github.com/aspnet/KestrelHttpServer/blob/dev/src/Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv/Internal/ListenerPrimary.cs
    sealed class WindowsApi : IDisposable
    {
        private IntPtr _fileCompletionInfoPtr;
        private bool _tryDetachFromIOCP = Platform.IsWindows;

        public WindowsApi()
        {
            var fileCompletionInfo = new FILE_COMPLETION_INFORMATION { Key = IntPtr.Zero, Port = IntPtr.Zero };
            _fileCompletionInfoPtr = PlatformApis.Allocate(Marshal.SizeOf(fileCompletionInfo));
            Marshal.StructureToPtr(fileCompletionInfo, _fileCompletionInfoPtr, false);
        }

        public void DetachFromIOCP(IStreamHandle handle)
        {
            if (!_tryDetachFromIOCP)
            {
                return;
            }

            // https://msdn.microsoft.com/en-us/library/windows/hardware/ff728840(v=vs.85).aspx
            const int FileReplaceCompletionInformation = 61;
            // https://msdn.microsoft.com/en-us/library/cc704588.aspx
            const uint STATUS_INVALID_INFO_CLASS = 0xC0000003;

            var statusBlock = new IO_STATUS_BLOCK();
            IntPtr socket = IntPtr.Zero;
            handle.GetFileDescriptor(ref socket);

            uint len = (uint)Marshal.SizeOf<FILE_COMPLETION_INFORMATION>();
            if (NtSetInformationFile(socket,
                out statusBlock, _fileCompletionInfoPtr, len,
                FileReplaceCompletionInformation) == STATUS_INVALID_INFO_CLASS)
            {
                // Replacing IOCP information is only supported on Windows 8.1 or newer
                _tryDetachFromIOCP = false;
            }
        }

        struct IO_STATUS_BLOCK
        {
#pragma warning disable IDE0044 // 添加只读修饰符
#pragma warning disable IDE0051 // 删除未使用的私有成员
            uint status;
            ulong information;
#pragma warning restore IDE0051 // 删除未使用的私有成员
#pragma warning restore IDE0044 // 添加只读修饰符
        }

        struct FILE_COMPLETION_INFORMATION
        {
            public IntPtr Port;
            public IntPtr Key;
        }

        [DllImport("NtDll.dll")]
        static extern uint NtSetInformationFile(IntPtr FileHandle, out IO_STATUS_BLOCK IoStatusBlock, IntPtr FileInformation, uint Length, int FileInformationClass);

#pragma warning disable IDE1006 // 命名样式
        [DllImport("ws2_32.dll", SetLastError = true)]
        static extern SocketError setsockopt(IntPtr socketHandle, SocketOptionLevel level, SocketOptionName optionName, ref int optionValue, uint optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        static extern SocketError getsockopt(IntPtr socketHandle, SocketOptionLevel level, SocketOptionName optionName, ref int optionValue, ref int optionLength);
#pragma warning restore IDE1006 // 命名样式

        internal static bool GetReuseAddress(IntPtr socket)
        {
            int value = GetSocketOption(socket, SocketOptionName.ReuseAddress);
            return value != 0;
        }

        internal static void SetReuseAddress(IntPtr socket, int value) => SetSocketOption(socket, SocketOptionName.ReuseAddress, value);

        static int GetSocketOption(IntPtr socket, SocketOptionName optionName)
        {
            int optLen = 4;
            int value = 0;
            SocketError status = getsockopt(socket, SocketOptionLevel.Socket, optionName, ref value, ref optLen);
            if (status == SocketError.SocketError)
            {
                ThrowHelper.ThrowSocketException(Marshal.GetLastWin32Error());
            }
            return value;
        }

        static void SetSocketOption(IntPtr socket, SocketOptionName optionName, int value)
        {
            SocketError status = setsockopt(socket, SocketOptionLevel.Socket, optionName, ref value, 4);
            if (status == SocketError.SocketError)
            {
                ThrowHelper.ThrowSocketException(Marshal.GetLastWin32Error());
            }
        }

        public void Dispose()
        {
            IntPtr handle = _fileCompletionInfoPtr;
            if (handle != IntPtr.Zero)
            {
                PlatformApis.FreeMemory(handle);
            }
            _fileCompletionInfoPtr = IntPtr.Zero;
        }
    }
}
