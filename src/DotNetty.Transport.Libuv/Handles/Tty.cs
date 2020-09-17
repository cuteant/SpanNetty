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
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Native;

    public enum TtyType
    {
        In = 0,  // stdin  - readable
        Out = 1,  // stdout - not readable
        Error = 2   // stderr
    }

    public enum TtyMode
    {
        Normal = 0,

        /* Raw input mode (On Windows, ENABLE_WINDOW_INPUT is also enabled) */
        Raw = 1,

        /* Binary-safe I/O mode for IPC (Unix-only) */
        IO
    }

    public sealed class Tty : StreamHandle<Tty>
    {
        private readonly TtyType _ttyType;

        internal Tty(LoopContext loop, TtyType ttyType)
            : base(loop, uv_handle_type.UV_TTY, ttyType)
        {
            _ttyType = ttyType;
        }

        public override void OnRead(
            Action<Tty, ReadableBuffer> onAccept,
            Action<Tty, Exception> onError,
            Action<Tty> onCompleted = null)
        {
            //if (onAccept is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onAccept); }
            //if (onError is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onError); }

            if (_ttyType != TtyType.In)
            {
                ThrowHelper.ThrowInvalidOperationException_uv_handle_type_is_not_readable(HandleType, InternalHandle, _ttyType);
            }

            base.OnRead(
                (stream, buffer) => onAccept((Tty)stream, buffer),
                (stream, error) => onError((Tty)stream, error),
                stream => onCompleted?.Invoke((Tty)stream));
        }

        public override void OnRead(Action<Tty, IStreamReadCompletion> onRead)
        {
            //if (onRead is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onRead); }

            if (_ttyType != TtyType.In)
            {
                ThrowHelper.ThrowInvalidOperationException_uv_handle_type_is_not_readable(HandleType, InternalHandle, _ttyType);
            }

            base.OnRead((stream, completion) => onRead((Tty)stream, completion));
        }

        public Tty Mode(TtyMode mode)
        {
            if (mode == TtyMode.IO && !Platform.IsUnix)
            {
                ThrowHelper.ThrowArgumentException_TtyMode_is_Unix_only(mode);
            }

            Validate();
            NativeMethods.TtySetMode(InternalHandle, mode);

            return this;
        }

        public Tty WindowSize(out int width, out int height)
        {
            Validate();
            NativeMethods.TtyWindowSize(InternalHandle, out width, out height);

            return this;
        }

        public static void ResetMode() => NativeMethods.TtyResetMode();
    }
}
