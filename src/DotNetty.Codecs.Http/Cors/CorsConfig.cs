// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.Cors
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    // Configuration for Cross-Origin Resource Sharing (CORS).
    public sealed class CorsConfig
    {
        readonly ISet<ICharSequence> origins;
        readonly bool anyOrigin;
        readonly bool enabled;
        readonly ISet<ICharSequence> exposeHeaders;
        readonly bool allowCredentials;
        readonly long maxAge;
        readonly ISet<HttpMethod> allowedRequestMethods;
        readonly ISet<AsciiString> allowedRequestHeaders;
        readonly bool allowNullOrigin;
        readonly IDictionary<AsciiString, ICallable<object>> preflightHeaders;
        readonly bool shortCircuit;

        internal CorsConfig(CorsConfigBuilder builder)
        {
            this.origins = new HashSet<ICharSequence>(builder.origins, AsciiString.CaseSensitiveHasher);
            this.anyOrigin = builder.anyOrigin;
            this.enabled = builder.enabled;
            this.exposeHeaders = builder.exposeHeaders;
            this.allowCredentials = builder.allowCredentials;
            this.maxAge = builder.maxAge;
            this.allowedRequestMethods = builder.requestMethods;
            this.allowedRequestHeaders = builder.requestHeaders;
            this.allowNullOrigin = builder.allowNullOrigin;
            this.preflightHeaders = builder.preflightHeaders;
            this.shortCircuit = builder.shortCircuit;
        }

        public bool IsCorsSupportEnabled => this.enabled;

        public bool IsAnyOriginSupported => this.anyOrigin;

        public ICharSequence Origin => 0u >= (uint)this.origins.Count ? CorsHandler.AnyOrigin : this.origins.First();

        public ISet<ICharSequence> Origins => this.origins;

        public bool IsNullOriginAllowed => this.allowNullOrigin;

        public ISet<ICharSequence> ExposedHeaders() => this.exposeHeaders.ToImmutableHashSet();

        public bool IsCredentialsAllowed => this.allowCredentials;

        public long MaxAge => this.maxAge;

        public ISet<HttpMethod> AllowedRequestMethods() => this.allowedRequestMethods.ToImmutableHashSet();

        public ISet<AsciiString> AllowedRequestHeaders() => this.allowedRequestHeaders.ToImmutableHashSet();

        public HttpHeaders PreflightResponseHeaders()
        {
            if (0u >= (uint)this.preflightHeaders.Count)
            {
                return EmptyHttpHeaders.Default;
            }
            HttpHeaders headers = new DefaultHttpHeaders();
            foreach (KeyValuePair<AsciiString, ICallable<object>> entry in this.preflightHeaders)
            {
                object value = GetValue(entry.Value);
                if (value is IEnumerable<object> values)
                {
                    headers.Add(entry.Key, values);
                }
                else
                {
                    headers.Add(entry.Key, value);
                }
            }
            return headers;
        }

        public bool IsShortCircuit => this.shortCircuit;

        static object GetValue(ICallable<object> callable)
        {
            try
            {
                return callable.Call();
            }
            catch (Exception exception)
            {
                return ThrowHelper.ThrowInvalidOperationException_Cqrs(callable, exception);
            }
        }

        public override string ToString()
        {
            var builder = StringBuilderManager.Allocate();
            builder.Append($"{StringUtil.SimpleClassName(this)}")
                .Append($"[enabled = {this.enabled}");

            builder.Append(", origins=");
            if (0u >= (uint)this.Origins.Count)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (ICharSequence value in this.Origins)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append(", exposedHeaders=");
            if (0u >= (uint)this.exposeHeaders.Count)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (ICharSequence value in this.exposeHeaders)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append($", isCredentialsAllowed={this.allowCredentials}");
            builder.Append($", maxAge={this.maxAge}");

            builder.Append(", allowedRequestMethods=");
            if (0u >= (uint)this.allowedRequestMethods.Count)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (HttpMethod value in this.allowedRequestMethods)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append(", allowedRequestHeaders=");
            if (0u >= (uint)this.allowedRequestHeaders.Count)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (AsciiString value in this.allowedRequestHeaders)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append(", preflightHeaders=");
            if (0u >= (uint)this.preflightHeaders.Count)
            {
                builder.Append("*");
            }
            else
            {
                builder.Append("(");
                foreach (AsciiString value in this.preflightHeaders.Keys)
                {
                    builder.Append($"'{value}'");
                }
                builder.Append(")");
            }

            builder.Append("]");
            return StringBuilderManager.ReturnAndFree(builder);
        }
    }
}
