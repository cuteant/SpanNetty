// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Runtime.CompilerServices;

    partial class Utf8Validator
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowCorruptedFrameException()
        {
            throw GetCorruptedFrameException();
            CorruptedFrameException GetCorruptedFrameException()
            {
                return new CorruptedFrameException("bytes are not UTF-8");
            }
        }
    }
}
