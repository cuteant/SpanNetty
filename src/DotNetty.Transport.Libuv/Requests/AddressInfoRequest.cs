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
    using System.Collections.Generic;
    using System.Net;
    using System.Runtime.CompilerServices;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Native;

    public readonly struct AddressInfo
    {
        internal AddressInfo(IPHostEntry hostEntry, Exception error)
        {
            HostEntry = hostEntry;
            Error = error;
        }

        public IPHostEntry HostEntry { get; }

        public Exception Error { get; }
    }

    public sealed class AddressInfoRequest : ScheduleRequest
    {
        internal static readonly uv_getaddrinfo_cb AddressInfoCallback = OnAddressInfoCallback;
        private readonly RequestContext _handle;
        private Action<AddressInfoRequest, AddressInfo> _requestCallback;

        internal unsafe AddressInfoRequest(LoopContext loop)
            : base(uv_req_type.UV_GETADDRINFO)
        {
            if (loop is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.loop); }

            int size = NativeMethods.GetSize(uv_req_type.UV_GETADDRINFO);
            if ((uint)(size - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(size, ExceptionArgument.size); }

            _handle = new RequestContext(RequestType, size, this);

            // Loop handle
            ((uv_getaddrinfo_t*)_handle.Handle)->loop = loop.Handle;
        }

        internal override IntPtr InternalHandle => _handle.Handle;

        public unsafe AddressInfoRequest Start(string node, string service, Action<AddressInfoRequest, AddressInfo> callback)
        {
            if (string.IsNullOrEmpty(node)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.node); }
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            _handle.Validate();
            _requestCallback = callback;

            IntPtr internalHandle = InternalHandle;
            IntPtr loopHandle = ((uv_getaddrinfo_t*)internalHandle)->loop;
            NativeMethods.GetAddressInfo(
                loopHandle,
                internalHandle,
                node,
                service,
                AddressInfoCallback);

            return this;
        }

        public bool TryCancel() => Cancel();

        private void OnAddressInfoCallback(int status, ref addrinfo res)
        {
            OperationException error = null;
            IPHostEntry hostEntry = null;
            if (SharedConstants.TooBigOrNegative >= (uint)status)
            {
                hostEntry = GetHostEntry(ref res);
            }
            else
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }

            var addressInfo = new AddressInfo(hostEntry, error);
            _requestCallback?.Invoke(this, addressInfo);
        }

        private static unsafe IPHostEntry GetHostEntry(ref addrinfo res)
        {
            var hostEntry = new IPHostEntry();

            try
            {
                hostEntry.HostName = res.GetCanonName();
                var addressList = new List<IPAddress>();

                addrinfo info = res;
                while (true)
                {
                    IPAddress address = info.GetAddress();
                    if (address is object)
                    {
                        addressList.Add(address);
                    }

                    IntPtr next = info.ai_next;
                    if (next == IntPtr.Zero)
                    {
                        break;
                    }

                    info = Unsafe.Read<addrinfo>((void*)next);
                }

                hostEntry.AddressList = addressList.ToArray();
            }
            finally
            {
                NativeMethods.FreeAddressInfo(ref res);
            }

            return hostEntry;
        }

        private static void OnAddressInfoCallback(IntPtr req, int status, ref addrinfo res)
        {
            var addressInfo = RequestContext.GetTarget<AddressInfoRequest>(req);
            addressInfo?.OnAddressInfoCallback(status, ref res);
        }

        protected override void Close()
        {
            _requestCallback = null;
            _handle.Dispose();
        }
    }
}
