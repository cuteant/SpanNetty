// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Buffers
{
    using System;
    using BenchmarkDotNet.Attributes;
    using DotNetty.Buffers;
    using DotNetty.Common;
#if DESKTOPCLR
    using BenchmarkDotNet.Diagnostics.Windows.Configs;
#endif

#if !DESKTOPCLR
    [CoreJob]
#else
    [ClrJob]
    [InliningDiagnoser]
#endif
    [BenchmarkCategory("ByteBuffer")]
    public class ByteBufferBenchmark
    {
        const string PropMode = "io.netty.buffer.bytebuf.checkAccessible";

        static ByteBufferBenchmark()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
            System.Environment.SetEnvironmentVariable("io.netty.buffer.checkAccessible", "false");
            System.Environment.SetEnvironmentVariable("io.netty.buffer.checkBounds", "false");
            Environment.SetEnvironmentVariable(PropMode, "false");
        }

        readonly IByteBuffer unpooledBuffer;
        readonly IByteBuffer pooledBuffer;

        public ByteBufferBenchmark()
        {
            var unpooled = new UnpooledByteBufferAllocator(true);
            this.unpooledBuffer = unpooled.Buffer(8);
            this.pooledBuffer = PooledByteBufferAllocator.Default.Buffer(8);
        }

        [Benchmark]
        public void SetByteUnpooled() => this.unpooledBuffer.SetByte(0, 0);

        [Benchmark]
        public void SetBytePooled() =>this.pooledBuffer.SetByte(0, 0);

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.unpooledBuffer.Release();
            this.pooledBuffer.Release();
        }
    }
}
