/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Libuv.Handles
{
    using System;
    using DotNetty.Transport.Libuv.Native;

    public sealed class FileStatus
    {
        internal FileStatus(uv_stat_t stat)
        {
            Device = stat.st_dev;
            Mode = stat.st_mode;
            LinkCount = stat.st_nlink;

            UserIdentifier = stat.st_uid;
            GroupIdentifier = stat.st_gid;

            DeviceType = stat.st_rdev;
            Inode = stat.st_ino;

            Size = stat.st_size;
            BlockSize = stat.st_blksize;
            Blocks = stat.st_blocks;

            Flags = stat.st_flags;
            FileGeneration = stat.st_gen;

            LastAccessTime = (DateTime)stat.st_atim;
            LastModifyTime = (DateTime)stat.st_mtim;
            LastChangeTime = (DateTime)stat.st_ctim;
            CreateTime = (DateTime)stat.st_birthtim;
        }

        public long Device { get; }

        public long Mode { get; }

        public long LinkCount { get; }

        public long UserIdentifier { get; }

        public long GroupIdentifier { get; }

        public long DeviceType { get; }

        public long Inode { get; }

        public long Size { get; }

        public long BlockSize { get; }

        public long Blocks { get; }

        public long Flags { get; }

        public long FileGeneration { get; }

        public DateTime LastAccessTime { get; }

        public DateTime LastModifyTime { get; }

        public DateTime LastChangeTime { get; }

        public DateTime CreateTime { get; }
    }
}
