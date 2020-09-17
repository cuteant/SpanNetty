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
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Libuv.Native;

    public abstract class ScheduleRequest : IDisposable
    {
        internal static readonly IInternalLogger Log = InternalLoggerFactory.GetInstance<ScheduleRequest>();

        internal ScheduleRequest(uv_req_type requestType)
        {
            RequestType = requestType;
        }

        public bool IsValid => InternalHandle != IntPtr.Zero;

        public object UserToken { get; set; }

        internal abstract IntPtr InternalHandle { get; }

        internal uv_req_type RequestType { get; }

        protected bool Cancel() => 
            IsValid && NativeMethods.Cancel(InternalHandle);

        protected abstract void Close();

        public override string ToString() =>
            $"{RequestType} {InternalHandle}";

        public void Dispose()
        {
            if (!IsValid)
            {
                return;
            }

            UserToken = null;
            Close();
        }
    }
}
