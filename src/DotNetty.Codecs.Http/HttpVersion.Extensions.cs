// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System.Text;

    partial class HttpVersion
    {
        static readonly byte[] Http11Bytes = Encoding.ASCII.GetBytes("HTTP/1.1");
        const byte OneByte = (byte)'1';
        const byte ZeroByte = (byte)'0';
    }
}
