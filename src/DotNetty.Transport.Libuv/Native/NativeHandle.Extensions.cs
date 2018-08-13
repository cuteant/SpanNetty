// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using DotNetty.Common.Internal.Logging;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    partial class NativeHandle
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void ThrowObjectDisposedException()
        {
            throw GetObjectDisposedException();
            ObjectDisposedException GetObjectDisposedException()
            {
                return new ObjectDisposedException($"{this.GetType()}");
            }
        }
    }
}
