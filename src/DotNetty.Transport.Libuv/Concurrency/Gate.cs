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

namespace DotNetty.Transport.Libuv.Concurrency
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    internal sealed class Gate
    {
        private const int Busy = 1;
        private const int Free = 0;

        private readonly Guard _guard;
        private long _state;

        internal Gate()
        {
            _state = Free;
            _guard = new Guard(this);
        }

        internal IDisposable TryAquire()
        {
            if (Free >= (uint)Interlocked.CompareExchange(ref _state, Busy, Free))
            {
                return _guard;
            }
            return default;
        }

        internal IDisposable Aquire()
        {
            IDisposable disposable;
            while ((disposable = TryAquire()) is null) { /* Aquire */ }
            return disposable;
        }

        private void Release()
        {
            long previousState = Interlocked.CompareExchange(ref _state, Free, Busy);
            Debug.Assert(previousState == Busy);
        }

        private readonly struct Guard : IDisposable
        {
            private readonly Gate _gate;

            internal Guard(Gate gate)
            {
                _gate = gate;
            }

            public void Dispose() => _gate.Release();
        }
    }
}
