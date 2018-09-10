// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Threading;

    public static class PlatformProvider
    {
        static IPlatform defaultPlatform;

        public static IPlatform Platform
        {
            get
            {
                IPlatform platform = Volatile.Read(ref defaultPlatform);
                if(platform == null)
                {
                    platform = new DefaultPlatform();
                    IPlatform current = Interlocked.CompareExchange(ref defaultPlatform, platform, null);
                    if (current != null)
                    {
                        return current;
                    }
                }
                return platform;
            }

            set
            {
                if (null == value) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                Interlocked.Exchange(ref defaultPlatform, value);
            }
        }
    }
}