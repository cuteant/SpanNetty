// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Collections.Generic;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class FSPollTests : IDisposable
    {
        Loop loop;
        Timer timer;

        string file;
        int callbackCount;
        int closeCount;
        int timerCount;
        List<string> directoryList;

        public FSPollTests()
        {
            this.loop = new Loop();
            this.directoryList = new List<string>();
        }

        [Fact]
        public void Poll()
        {
            this.file = TestHelper.GetRandomTempFileName();

            this.loop
                .CreateFSPoll()
                .Start(this.file, 100, this.OnFSPollCount);

            this.timer = this.loop.CreateTimer();

            this.loop.RunDefault();
            Assert.Equal(5, this.callbackCount);
            Assert.Equal(2, this.timerCount);
            Assert.Equal(1, this.closeCount);
        }

        void OnFSPollCount(FSPoll fsPoll, FSPollStatus fsPollStatus)
        {
            bool checkFailed = false;
            if (this.callbackCount == 0)
            {
                if (fsPollStatus.Error is OperationException error
                    && error.ErrorCode == ErrorCode.ENOENT)
                {
                    TestHelper.CreateFile(this.file);
                }
                else
                {
                    checkFailed = true;
                }
            }
            else if (this.callbackCount == 1)
            {
                if (fsPollStatus.Error == null)
                {
                    this.timer.Start(this.OnTimer, 20, 0);
                }
                else
                {
                    checkFailed = true;
                }
            }
            else if (this.callbackCount == 2)
            {
                if (fsPollStatus.Error == null)
                {
                    this.timer.Start(this.OnTimer, 200, 0);
                }
                else
                {
                    checkFailed = true;
                }
            }
            else if (this.callbackCount == 3)
            {
                if (fsPollStatus.Error == null)
                {
                    TestHelper.DeleteFile(this.file);
                }
                else
                {
                    checkFailed = true;
                }
            }
            else if (this.callbackCount == 4)
            {
                if (fsPollStatus.Error is OperationException error
                    && error.ErrorCode == ErrorCode.ENOENT)
                {
                    fsPoll.CloseHandle(this.OnClose);
                }
                else
                {
                    checkFailed = true;
                }
            }
            else
            {
                checkFailed = true;
            }

            if (checkFailed)
            {
                fsPoll.CloseHandle(this.OnClose);
                this.timer.CloseHandle(this.OnClose);
            }
            else
            {
                this.callbackCount++;
            }
        }

        void OnTimer(Timer handle)
        {
            // Need to change the file size because the poller may not pick up
            // sub-second mtime changes.
            TestHelper.TouchFile(this.file, this.callbackCount * 10);
            this.timerCount++;
        }

        [Fact]
        public void GetPath()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            this.file = TestHelper.CreateTempFile(directory);

            FSPoll fsPoll = this.loop.CreateFSPoll();
            var error = Assert.Throws<OperationException>(() => fsPoll.GetPath());
            Assert.Equal(ErrorCode.EINVAL, error.ErrorCode);

            fsPoll.Start(this.file, 100, this.OnFSPoll);
            string path = fsPoll.GetPath();
            Assert.Equal(this.file, path);

            fsPoll.CloseHandle(this.OnClose);

            this.loop.RunDefault();
            Assert.Equal(1, this.closeCount);
            Assert.Equal(0, this.callbackCount);
        }

        void OnFSPoll(FSPoll fsPoll, FSPollStatus fsPollStatus) => this.callbackCount++;

        void OnClose(ScheduleHandle handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            TestHelper.DeleteDirectories(this.directoryList);
            this.directoryList = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
