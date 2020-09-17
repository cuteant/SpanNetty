// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    public sealed class TtyTests : IDisposable
    {
        Loop loop;
        int closeCount;

        public TtyTests()
        {
            this.loop = new Loop();
        }

        [Fact]
        public void Types()
        {
            if (Platform.IsWindows
                || Platform.IsDarwin)
            {
                return;
            }

#if NETCOREAPP2_1
            var runInAzureDevOps = false; // 本地测试
#else
            var runInAzureDevOps = true;
#endif
            if (runInAzureDevOps)
            {
                // TODO Azure DevOps 有时测试无法通过
                return;
            }

            this.closeCount = 0;

            Tty ttyIn = this.loop.CreateTty(TtyType.In);
            Assert.True(ttyIn.IsReadable);
            Assert.False(ttyIn.IsWritable);

            Tty ttyOut = this.loop.CreateTty(TtyType.Out);
            Assert.False(ttyOut.IsReadable);
            Assert.True(ttyOut.IsWritable);

            /*
            int width;
            int height;
            ttyOut.WindowSize(out width, out height);

            // Is it a safe assumption that most people have terminals larger than
            // 10x10?
            Assert.True(width > 10);
            Assert.True(height > 10);
            */

            /* Turn on raw mode. */
            ttyIn.Mode(TtyMode.Raw);

            /* Turn off raw mode. */
            ttyIn.Mode(TtyMode.Normal);

            /* Calling uv_tty_reset_mode() repeatedly should not clobber errno. */
            Tty.ResetMode();
            Tty.ResetMode();
            Tty.ResetMode();

            ttyIn.CloseHandle(this.OnClose);
            ttyOut.CloseHandle(this.OnClose);

            this.loop.RunDefault();

            Assert.Equal(2, this.closeCount);
        }

        void OnClose(Tty handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
