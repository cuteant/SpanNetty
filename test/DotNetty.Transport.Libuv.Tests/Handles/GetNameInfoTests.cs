// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using System.Net;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Requests;
    using Xunit;

    public sealed class GetNameInfoTests : IDisposable
    {
        const int Port = 80;

        Loop loop;
        int callbackCount;

        public GetNameInfoTests()
        {
            this.loop = new Loop();
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("::1")]
        public void Basic(string ipAddress)
        {
            this.callbackCount = 0;

            IPAddress address = IPAddress.Parse(ipAddress);
            var endPoint = new IPEndPoint(address, Port);
            this.loop
                .CreateNameInfoRequest().
                Start(endPoint, this.OnNameInfo);

            this.loop.RunDefault();
            Assert.Equal(1, this.callbackCount);
        }

        void OnNameInfo(NameInfoRequest request, NameInfo nameInfo)
        {
            if (nameInfo.Error == null 
                && !string.IsNullOrEmpty(nameInfo.HostName))
            {
                this.callbackCount++;
            }

            request.Dispose();
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
