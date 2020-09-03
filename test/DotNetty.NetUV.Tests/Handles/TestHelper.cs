// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;

    static class TestHelper
    {
        internal const int TestPort = 9123;
        internal const int TestPort2 = 9124;

        // ReSharper disable once InconsistentNaming
        internal static readonly IPEndPoint IPv6AnyEndPoint = new IPEndPoint(IPAddress.IPv6Any, IPEndPoint.MinPort);

        public static string RootSystemDirectory() => Path.GetPathRoot(Path.GetTempPath());

        public static void DeleteDirectory(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) 
                || !Directory.Exists(fullPath))
            {
                return;
            }
            
            Directory.Delete(fullPath);
        }

        public static void DeleteFile(string fullName)
        {
            if (string.IsNullOrEmpty(fullName) 
                || !File.Exists(fullName))
            {
                return;
            }

            File.Delete(fullName);
        }

        public static void DeleteFiles(IReadOnlyList<string> files)
        {
            if (files == null 
                || files.Count == 0)
            {
                return;
            }

            foreach (string fileName in files)
            {
                try
                {
                    DeleteFile(fileName);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }
        }

        public static void DeleteDirectories(IReadOnlyList<string> directories)
        {
            if (directories == null 
                || directories.Count == 0)
            {
                return;
            }

            foreach (string directory in directories)
            {
                try
                {
                    string[] files = GetFiles(directory);
                    DeleteFiles(files);
                    DeleteDirectory(directory);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }
        }

        public static string CreateTempDirectory()
        {
            string tempDirectory = GetRandomTempFileName();
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static string CreateRandomDirectory(string path)
        {
            string directory = Path.Combine(path, Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            return directory;
        }

        public static string GetRandomTempFileName() => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        public static string CreateTempFile(string directory, int count = 0)
        {
            string fullName = Path.Combine(directory, Path.GetRandomFileName());
            using (FileStream stream = File.Open(fullName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                if (count > 0)
                {
                    stream.WriteByte(1);
                    for (int i = 1; i < count; i++)
                    {
                        stream.WriteByte(1);
                    }
                }
            }
            
            return fullName;
        }

        public static FileStream OpenTempFile()
        {
            string directory = CreateTempDirectory();
            string fileName = Path.Combine(directory, Path.GetRandomFileName());
            FileStream file = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            return file;
        }

        public static void CreateFile(string fullName)
        {
            if (File.Exists(fullName))
            {
                return;
            }
            using (File.Create(fullName))
            {
                // NOP
            }
        }

        public static string[] GetFiles(string directory) => 
            Directory.Exists(directory) ? Directory.GetFiles(directory) : new string[0];

        public static void TouchFile(string fullName, int count = 1)
        {
            using (FileStream stream = File.Open(fullName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                if (count > 0)
                {
                    stream.WriteByte(1);

                    for (int i = 1; i < count; i++)
                    {
                        stream.WriteByte(1);
                    }
                }
            }
        }

        public static IntPtr GetHandle(Socket socket) => socket.Handle;
    }
}
