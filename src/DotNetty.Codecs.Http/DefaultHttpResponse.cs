// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http
{
    using DotNetty.Common.Internal;

    public class DefaultHttpResponse : DefaultHttpMessage, IHttpResponse
    {
        const int HashCodePrime = 31;
        HttpResponseStatus status;

        public DefaultHttpResponse(HttpVersion version, HttpResponseStatus status, bool validateHeaders = true, bool singleFieldHeaders = false)
            : base(version, validateHeaders, singleFieldHeaders)
        {
            if (status is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.status); }

            this.status = status;
        }

        public DefaultHttpResponse(HttpVersion version, HttpResponseStatus status, HttpHeaders headers)
            : base(version, headers)
        {
            if (status is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.status); }

            this.status = status;
        }

        public HttpResponseStatus Status => this.status;

        public IHttpResponse SetStatus(HttpResponseStatus value)
        {
            if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
            this.status = value;
            return this;
        }

        public override string ToString() => StringBuilderManager.ReturnAndFree(HttpMessageUtil.AppendResponse(StringBuilderManager.Allocate(256), this));

        public override int GetHashCode()
        {
            int result = 1;
            result = HashCodePrime * result + this.status.GetHashCode();
            result = HashCodePrime * result + base.GetHashCode();
            return result;
        }

        public override bool Equals(object obj)
        {
            if(obj is DefaultHttpResponse other)
            {
                return this.status.Equals(other.status) && base.Equals(obj);
            }
            return false;
        }
    }
}
