// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
// ReSharper disable RedundantAssignment
// ReSharper disable NotAccessedVariable
#pragma warning disable 414
#pragma warning disable 169
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;

    //https://github.com/aspnet/KestrelHttpServer/blob/dev/src/Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv/Internal/ListenerPrimary.cs
    sealed class WindowsApi : IDisposable
    {
        IntPtr fileCompletionInfoPtr;
        bool tryDetachFromIOCP = PlatformApi.IsWindows;

        public WindowsApi()
        {
            var fileCompletionInfo = new FILE_COMPLETION_INFORMATION { Key = IntPtr.Zero, Port = IntPtr.Zero };
            this.fileCompletionInfoPtr = NativeMethods.Allocate(Marshal.SizeOf(fileCompletionInfo));
            Marshal.StructureToPtr(fileCompletionInfo, this.fileCompletionInfoPtr, false);
        }

        public void DetachFromIOCP(NativeHandle handle)
        {
            if (!this.tryDetachFromIOCP)
            {
                return;
            }

            // https://msdn.microsoft.com/en-us/library/windows/hardware/ff728840(v=vs.85).aspx
            const int FileReplaceCompletionInformation = 61;
            // https://msdn.microsoft.com/en-us/library/cc704588.aspx
            const uint STATUS_INVALID_INFO_CLASS = 0xC0000003;

#pragma warning disable IDE0059 // 不需要赋值
            var statusBlock = new IO_STATUS_BLOCK();
#pragma warning restore IDE0059 // 不需要赋值
            IntPtr socket = IntPtr.Zero;
            NativeMethods.uv_fileno(handle.Handle, ref socket);

            uint len = (uint)Marshal.SizeOf<FILE_COMPLETION_INFORMATION>();
            if (NtSetInformationFile(socket,
#pragma warning disable IDE0059 // 不需要赋值
                out statusBlock, this.fileCompletionInfoPtr, len,
#pragma warning restore IDE0059 // 不需要赋值
                FileReplaceCompletionInformation) == STATUS_INVALID_INFO_CLASS)
            {
                // Replacing IOCP information is only supported on Windows 8.1 or newer
                this.tryDetachFromIOCP = false;
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
            IntPtr handle = this.fileCompletionInfoPtr;
            if (handle != IntPtr.Zero)
            {
                NativeMethods.FreeMemory(handle);
            }
            this.fileCompletionInfoPtr = IntPtr.Zero;
        }
    }
}
