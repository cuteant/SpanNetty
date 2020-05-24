// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Buffers;
    using System.Buffers.Text;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
#if !(NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER)
    using System.Runtime.CompilerServices;
#endif

    /// <summary>
    /// A utility class mainly for use by web sockets
    /// </summary>
    static class WebSocketUtil
    {
        static readonly Random Random = PlatformDependent.GetThreadLocalRandom();

        static readonly ThreadLocalMD5 LocalMd5 = new ThreadLocalMD5();

        static readonly ThreadLocalRNG LocalRNG = new ThreadLocalRNG();

        sealed class ThreadLocalMD5 : FastThreadLocal<MD5>
        {
            protected override MD5 GetInitialValue() => MD5.Create();
        }

        static readonly ThreadLocalSha1 LocalSha1 = new ThreadLocalSha1();

        sealed class ThreadLocalSha1 : FastThreadLocal<SHA1>
        {
            protected override SHA1 GetInitialValue() => SHA1.Create();
        }

        sealed class ThreadLocalRNG : FastThreadLocal<RNGCryptoServiceProvider>
        {
            protected override RNGCryptoServiceProvider GetInitialValue() => new RNGCryptoServiceProvider();
        }

        internal static byte[] Md5(byte[] data)
        {
            MD5 md5 = LocalMd5.Value;
            md5.Initialize();
            return md5.ComputeHash(data);
        }

        internal static byte[] Sha1(byte[] data)
        {
            SHA1 sha1 = LocalSha1.Value;
            sha1.Initialize();
            return sha1.ComputeHash(data);
        }

        internal static string Base64String(byte[] data)
        {
            var maxLen = Base64.GetMaxEncodedToUtf8Length(data.Length);
            byte[] utf8Array = null;
            char[] utf16Array = null;
            try
            {
                Span<byte> utf8Bytes = (uint)maxLen <= SharedConstants.uStackallocThreshold ?
                    stackalloc byte[maxLen] :
                    (utf8Array = ArrayPool<byte>.Shared.Rent(maxLen));
                var result = Base64.EncodeToUtf8(data, utf8Bytes, out _, out int bytesWritten);
                Debug.Assert(result == OperationStatus.Done);
#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
                ReadOnlySpan<byte> base64Bytes = MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(utf8Bytes), bytesWritten);
#else
                ReadOnlySpan<byte> base64Bytes;
                unsafe
                {
                    base64Bytes = new ReadOnlySpan<byte>(Unsafe.AsPointer(ref MemoryMarshal.GetReference(utf8Bytes)), bytesWritten);
                }
#endif
                var charCount = TextEncodings.Utf8.GetCharCount(base64Bytes);
                Span<char> utf16Chars = (uint)charCount <= SharedConstants.uStackallocThreshold ?
                    stackalloc char[charCount] :
                    (utf16Array = ArrayPool<char>.Shared.Rent(charCount));
                TextEncodings.Utf8.GetChars(base64Bytes, utf16Chars);
                return utf16Chars.ToString();
            }
            finally
            {
                if (utf8Array is object) { ArrayPool<byte>.Shared.Return(utf8Array); }
                if (utf16Array is object) { ArrayPool<char>.Shared.Return(utf16Array); }
            }
        }

        internal static byte[] RandomBytes(int size)
        {
            var bytes = new byte[size];
            var rng = LocalRNG.Value;
            rng.GetBytes(bytes);
            return bytes;
        }

        internal static int RandomNumber(int minimum, int maximum) => unchecked((int)(Random.NextDouble() * maximum + minimum));

        // Math.Random()
        internal static double RandomNext() => Random.NextDouble();
    }
}
