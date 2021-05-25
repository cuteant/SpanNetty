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
    using DotNetty.Transport.Libuv.Native;

    public interface IScheduleHandle : IDisposable
    {
        bool IsActive { get; }

        bool IsClosing { get; }

        bool IsValid { get; }

        object UserToken { get; set; }

        bool TryGetLoop(out Loop loop);

        void AddReference();

        void RemoveReference();

        bool HasReference();

        void CloseHandle(Action<IScheduleHandle> onClosed);
    }

    internal interface IInternalScheduleHandle : IScheduleHandle
    {
        uv_handle_type HandleType { get; }

        IntPtr InternalHandle { get; }

        IntPtr LoopHandle();

        void Validate();

        void OnHandleClosed();
    }
}
