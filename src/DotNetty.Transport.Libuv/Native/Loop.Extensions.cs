// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Runtime.CompilerServices;

    partial class Loop
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException()
        {
            throw GetObjectDisposedException();
            ObjectDisposedException GetObjectDisposedException()
            {
                return new ObjectDisposedException($"{typeof(Loop)}");
            }
        }
    }
}
