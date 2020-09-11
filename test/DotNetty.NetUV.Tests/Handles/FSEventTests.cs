// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Collections.Generic;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class FSEventTests : IDisposable
    {
        const int FileEventCount = 16;

        Loop loop;

        FSEvent fsEventCurrent;
        FSEvent fsEventCurrent1;
        string currentFileName;
        string currentFileName1;

        int callbackCount;
        int closeCount;
        int touchCount;

        Timer timer;
        int fileCreated;
        int fileRemoved;

        List<string> directoryList;

        public FSEventTests()
        {
            this.loop = new Loop();
            this.directoryList = new List<string>();
        }

        [Fact]
        public void WatchDirRecursive()
        {
            if (!Platform.IsWindows 
                && !Platform.IsDarwin)
            {
                // Recursive directory watching not supported on this platform.
                return;
            }

            string directory = TestHelper.CreateTempDirectory();
            this.currentFileName = TestHelper.CreateRandomDirectory(directory);
            this.directoryList.Add(this.currentFileName);

            this.fsEventCurrent = this.loop
                .CreateFSEvent()
                .Start(this.currentFileName, this.OnFSEventDirMultipleFile, FSEventMask.Recursive);

            this.timer = this.loop
                .CreateTimer()
                .Start(this.OnTimerCreateFile, 100, 0);

            this.loop.RunDefault();

            Assert.True(this.fileCreated + this.fileRemoved == this.callbackCount,
                $"{nameof(this.fileCreated)}={this.fileCreated} + {nameof(this.fileRemoved)}={this.fileRemoved} [{nameof(this.callbackCount)}={this.callbackCount}]");
            Assert.Equal(2, this.closeCount);
        }

        [Fact]
        public void WatchDir()
        {
            this.currentFileName = TestHelper.CreateTempDirectory();
            this.directoryList.Add(this.currentFileName);

            this.fsEventCurrent = this.loop
                .CreateFSEvent()
                .Start(this.currentFileName, this.OnFSEventDirMultipleFile);

            this.timer = this.loop
                .CreateTimer()
                .Start(this.OnTimerCreateFile, 100, 0);

            this.loop.RunDefault();
            Assert.True(this.fileCreated + this.fileRemoved == this.callbackCount, 
                $"{nameof(this.fileCreated)}={this.fileCreated} + {nameof(this.fileRemoved)}={this.fileRemoved} [{nameof(this.callbackCount)}={this.callbackCount}]");
            Assert.Equal(2, this.closeCount);
        }

        void OnFSEventDirMultipleFile(FSEvent fsEvent, FileSystemEvent fileSystemEvent)
        {
            this.callbackCount++;

            if (this.fileCreated + this.fileRemoved == FileEventCount)
            {
                // Once we've processed all create events, delete all files
                this.timer.Start(this.OnTimerDeleteFile, 1, 0);
            }
            else if (this.callbackCount == 2 * FileEventCount)
            {
                // Once we've processed all create and delete events, stop watching
                this.timer.CloseHandle(this.OnClose);
                fsEvent.CloseHandle(this.OnClose);
            }
        }

        void OnTimerDeleteFile(Timer handle)
        {
            if (this.fileRemoved < FileEventCount)
            {
                // Remove the file
                string[] files = TestHelper.GetFiles(this.currentFileName);
                if (files != null && files.Length > 0)
                {
                    TestHelper.DeleteFile(files[0]);
                    this.fileRemoved++;
                }

                if (this.fileRemoved < FileEventCount)
                {
                    // Remove another file on a different event loop tick.  We do it this way
                    // to avoid fs events coalescing into one fs event.
                    handle.Start(this.OnTimerDeleteFile, 10, 0);
                }
            }
        }

        void OnTimerCreateFile(Timer handle)
        {
            // Make sure we're not attempting to create files we do not intend
            if (this.fileCreated < FileEventCount)
            {
                // Create the file
                TestHelper.CreateTempFile(this.currentFileName);
                this.fileCreated++;

                if (this.fileCreated < FileEventCount)
                {
                    // Create another file on a different event loop tick.  We do it this way
                    // to avoid fs events coalescing into one fs event.
                    handle.Start(this.OnTimerCreateFile, 1, 0);
                }
            }
        }

        [Fact]
        public void WatchFile()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            this.currentFileName = TestHelper.CreateTempFile(directory);
            this.currentFileName1 = TestHelper.CreateTempFile(directory);

            this.fsEventCurrent = this.loop
                .CreateFSEvent()
                .Start(this.currentFileName1, this.OnFSEventFile);

            this.loop.CreateTimer()
                .Start(this.OnTimerFile, 100, 100);

            this.loop.RunDefault();

            Assert.Equal(1, this.callbackCount);
            Assert.Equal(2, this.touchCount);
            Assert.Equal(2, this.closeCount);
        }

        void OnTimerFile(Timer handle)
        {
            this.touchCount++;

            if (this.touchCount == 1)
            {
                TestHelper.TouchFile(this.currentFileName, 10);
            }
            else
            {
                TestHelper.TouchFile(this.currentFileName1, 10);
                handle.CloseHandle(this.OnClose);
            }
        }

        [Fact]
        public void WatchFileTwice()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            string file = TestHelper.CreateTempFile(directory);

            this.fsEventCurrent = this.loop
                .CreateFSEvent()
                .Start(file, this.OnFSEventFile);

            this.fsEventCurrent1 = this.loop
                .CreateFSEvent()
                .Start(file, this.OnFSEventFile);

            this.loop.CreateTimer()
                .Start(this.OnTimerWatchTwice, 10, 0);

            this.loop.RunDefault();
            Assert.Equal(0, this.callbackCount);
            Assert.Equal(3, this.closeCount);
        }

        void OnTimerWatchTwice(Timer handle)
        {
            this.fsEventCurrent?.CloseHandle(this.OnClose);
            this.fsEventCurrent1?.CloseHandle(this.OnClose);
            handle.CloseHandle(this.OnClose);
        }

        [Fact]
        public void WatchFileCurrentDir()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            this.currentFileName = TestHelper.CreateTempFile(directory);

            this.fsEventCurrent = this.loop
                .CreateFSEvent()
                .Start(this.currentFileName, this.OnFSEventFileCurrentDir);

            this.loop.CreateTimer()
                .Start(this.OnTimerTouch, 100, 0);

            this.loop.RunDefault();
            Assert.Equal(1, this.touchCount);
            Assert.Equal(1, this.callbackCount);
            Assert.Equal(3, this.closeCount);
        }

        void OnTimerTouch(Timer handle)
        {
            this.touchCount++;
            TestHelper.TouchFile(this.currentFileName, 10);
            handle.CloseHandle(this.OnClose);
        }

        void OnFSEventFileCurrentDir(FSEvent fsEvent, FileSystemEvent fileSystemEvent)
        {
            if (this.callbackCount == 0 
                && fileSystemEvent.EventType == FSEventType.Change 
                && !string.IsNullOrEmpty(fileSystemEvent.FileName))
            {
                this.callbackCount++;
            }

            this.loop.CreateTimer()
                .Start(this.OnTimerClose, 250, 0);
        }

        void OnTimerClose(Timer handle)
        {
            handle.CloseHandle(this.OnClose);
            this.fsEventCurrent?.CloseHandle(this.OnClose);
        }

        [Fact]
        public void WatchFileRootDir()
        {
            string root = TestHelper.RootSystemDirectory();
            FSEvent fsEvent = this.loop
                .CreateFSEvent()
                .Start(root, this.OnFSEvent);

            fsEvent.CloseHandle(this.OnClose);

            this.loop.RunDefault();
            Assert.Equal(0, this.callbackCount);
            Assert.Equal(1, this.closeCount);
        }

        [Fact]
        public void NoCallbackAfterClose()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            string file = TestHelper.CreateTempFile(directory);

            FSEvent fsEvent = this.loop
                .CreateFSEvent()
                .Start(file, this.OnFSEventFile);
            fsEvent.CloseHandle(this.OnClose);

            TestHelper.TouchFile(file, 10);

            this.loop.RunDefault();
            Assert.Equal(0, this.callbackCount);
            Assert.Equal(1, this.closeCount);
        }

        [Fact]
        public void NoCallbackOnClose()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            string file = TestHelper.CreateTempFile(directory);

            FSEvent fsEvent = this.loop
                .CreateFSEvent()
                .Start(file, this.OnFSEventFile);

            fsEvent.CloseHandle(this.OnClose);

            this.loop.RunDefault();
            Assert.Equal(0, this.callbackCount);
            Assert.Equal(1, this.closeCount);
        }

        void OnFSEventFile(FSEvent fsEvent, FileSystemEvent fileSystemEvent)
        {
            if (fileSystemEvent.EventType == FSEventType.Change)
            {
                this.callbackCount++;
            }

            fsEvent.Stop();
            fsEvent.CloseHandle(this.OnClose);
        }

        [Fact]
        public void ImmediateClose()
        {
            this.loop.CreateTimer()
                .Start(this.OnTimer, 1, 0);

            this.loop.RunDefault();

            Assert.Equal(2, this.closeCount);
            Assert.Equal(0, this.callbackCount);
        }

        void OnTimer(Timer handle)
        {
            FSEvent fsEvent = this.loop
                .CreateFSEvent()
                .Start(".", this.OnFSEvent);
            fsEvent.CloseHandle(this.OnClose);
            handle.CloseHandle(this.OnClose);
        }

        [Fact]
        public void CloseWithPendingEvent()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            string file = TestHelper.CreateTempFile(directory);

            FSEvent fsEvent = this.loop
                .CreateFSEvent()
                .Start(file, this.OnFSEvent);

            TestHelper.TouchFile(file, 10);
            fsEvent.CloseHandle(this.OnClose);

            this.loop.RunDefault();

            Assert.Equal(0, this.callbackCount);
            Assert.Equal(1, this.closeCount);
        }

        [Fact]
        public void CloseInCallback()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            string file1 = TestHelper.CreateTempFile(directory);
            string file2 = TestHelper.CreateTempFile(directory);
            string file3 = TestHelper.CreateTempFile(directory);
            string file4 = TestHelper.CreateTempFile(directory);
            string file5 = TestHelper.CreateTempFile(directory);

            FSEvent fsEvent = this.loop.CreateFSEvent();
            fsEvent.Start(directory, this.OnFSeventClose);

            /* Generate a couple of fs events. */
            TestHelper.TouchFile(file1, 10);
            TestHelper.TouchFile(file2, 20);
            TestHelper.TouchFile(file3, 30);
            TestHelper.TouchFile(file4, 40);
            TestHelper.TouchFile(file5, 50);

            this.loop.RunDefault();

            Assert.Equal(1, this.closeCount);
            Assert.Equal(3, this.callbackCount);
        }

        void OnFSeventClose(FSEvent fsEvent, FileSystemEvent fileSystemEvent)
        {
            this.callbackCount++;

            if (this.callbackCount == 3)
            {
                fsEvent.CloseHandle(this.OnClose);
            }
        }

        [Fact]
        public void StartAndClose()
        {
            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            FSEvent fsEvent1 = this.loop.CreateFSEvent();
            fsEvent1.Start(directory, this.OnFSEventDirectory);

            FSEvent fsEvent2 = this.loop.CreateFSEvent();
            fsEvent2.Start(directory, this.OnFSEventDirectory);

            fsEvent1.CloseHandle(this.OnClose);
            fsEvent2.CloseHandle(this.OnClose);

            this.loop.RunDefault();

            Assert.Equal(2, this.closeCount);
            Assert.Equal(0, this.callbackCount);
        }

        void OnFSEventDirectory(FSEvent fsEvent, FileSystemEvent fileSystemEvent)
        {
            if (fileSystemEvent.EventType == FSEventType.Rename)
            {
                this.callbackCount++;
            }

            fsEvent.Stop();
            fsEvent.CloseHandle(this.OnClose);
        }

        [Fact]
        public void GetPath()
        {
            FSEvent fsEvent = this.loop.CreateFSEvent();
            var error = Assert.Throws<OperationException>(() => fsEvent.GetPath());
            Assert.Equal(ErrorCode.EINVAL, error.ErrorCode);

            string directory = TestHelper.CreateTempDirectory();
            this.directoryList.Add(directory);

            fsEvent.Start(directory, this.OnFSEvent);
            string path = fsEvent.GetPath();
            Assert.Equal(directory, path);

            fsEvent.Stop();
            fsEvent.CloseHandle(this.OnClose);

            this.loop.RunDefault();
            Assert.Equal(0, this.callbackCount);
            Assert.Equal(1, this.closeCount);
        }

        void OnClose(ScheduleHandle handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        void OnFSEvent(FSEvent fsEvent, FileSystemEvent fileSystemEvent) => this.callbackCount++;

        public void Dispose()
        {
            this.fsEventCurrent?.Dispose();
            this.fsEventCurrent = null;

            this.fsEventCurrent1?.Dispose();
            this.fsEventCurrent1 = null;

            TestHelper.DeleteDirectories(this.directoryList);
            this.directoryList = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
