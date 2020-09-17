// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Requests;
    using Xunit;

    public sealed class GetAddrInfoTests : IDisposable
    {
        const string Name = "localhost";
        const int RequestCount = 10;

        Loop loop;
        int callbackCount;

        public GetAddrInfoTests()
        {
            this.loop = new Loop();
        }

        [Fact]
        public void Fail()
        {
            this.callbackCount = 0;

            this.loop
                .CreateAddressInfoRequest()
                .Start("xyzzy.xyzzy.xyzzy.", null, this.OnAddressInfoFail);

            this.loop.RunDefault();
            Assert.Equal(1, this.callbackCount);
        }

        void OnAddressInfoFail(AddressInfoRequest request, AddressInfo info)
        {
            if (info.Error != null
                && info.HostEntry == null)
            {
                this.callbackCount++;
            }

            request.Dispose();
        }

        [Fact]
        public void Basic()
        {
            this.callbackCount = 0;

            this.loop
                .CreateAddressInfoRequest()
                .Start(Name, null, this.OnAddressInfoOk);

            this.loop.RunDefault();
            Assert.Equal(1, this.callbackCount);
        }

        void OnAddressInfoOk(AddressInfoRequest request, AddressInfo info)
        {
            if (info.Error == null 
                && info.HostEntry != null)
            {
                this.callbackCount++;
            }

            request.Dispose();
        }

        [Fact]
        public void Concurrent()
        {
            for (int i = 0; i < RequestCount; i++)
            {
                AddressInfoRequest request = this.loop.CreateAddressInfoRequest();
                request.Start(Name, null, this.OnAddressInfoOk);
            }

            this.loop.RunDefault();
            Assert.Equal(RequestCount, this.callbackCount);
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
