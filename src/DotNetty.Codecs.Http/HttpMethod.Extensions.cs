// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using CuteAnt.Collections;

    partial class HttpMethod : IEquatable<HttpMethod>
    {
        const byte CByte = (byte)'C';
        const byte DByte = (byte)'D';
        const byte GByte = (byte)'G';
        const byte HByte = (byte)'H';
        const byte OByte = (byte)'O';
        const byte PByte = (byte)'P';
        const byte UByte = (byte)'U';
        const byte AByte = (byte)'A';
        const byte TByte = (byte)'T';

        static readonly CachedReadConcurrentDictionary<string, HttpMethod> s_methodCache = 
            new CachedReadConcurrentDictionary<string, HttpMethod>(StringComparer.Ordinal);
        static readonly Func<string, HttpMethod> s_convertToHttpMethodFunc = ConvertToHttpMethod;

        private static HttpMethod ConvertToHttpMethod(string name)
        {
            var methodName = name.ToUpperInvariant();

            if (MethodMap.TryGetValue(methodName, out var result))
            {
                return result;
            }

            return new HttpMethod(methodName);
        }

        public bool Equals(HttpMethod other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            return other is object && this.name.Equals(other.name);
        }
    }
}
