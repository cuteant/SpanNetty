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

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal.Logging;

    internal abstract class NativeHandle : IDisposable
    {
        protected static readonly IInternalLogger Log = InternalLoggerFactory.GetInstance<NativeHandle>();
        private IntPtr _handle;

        protected NativeHandle()
        {
            _handle = IntPtr.Zero;
        }

        protected internal IntPtr Handle
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _handle;
            protected set => _handle = value;
        }

        internal bool IsValid
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _handle != IntPtr.Zero;
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        protected internal void Validate()
        {
            if (!IsValid) { ThrowObjectDisposedException(); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowObjectDisposedException()
        {
            throw GetObjectDisposedException();

            ObjectDisposedException GetObjectDisposedException()
            {
                return new ObjectDisposedException(GetType().FullName);
            }
        }

        internal void SetHandleAsInvalid() => _handle = IntPtr.Zero;

        protected abstract void CloseHandle();

        private void Dispose(bool disposing)
        {
            try
            {
                if (!IsValid) { return; }
#if DEBUG
                if (Log.DebugEnabled)
                {
                    Log.Debug("Disposing {} (Finalizer {})", _handle, !disposing);
                }
#endif
                CloseHandle();
            }
            catch (Exception exception)
            {
                Log.NativeHandle_error_whilst_closing_handle(_handle, exception);

                // For finalizer, we cannot allow this to escape.
                if (disposing) { throw; }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NativeHandle()
        {
            Dispose(false);
        }
    }
}
