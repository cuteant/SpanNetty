// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;
    using Xunit.Abstractions;

    public class TlsHandlerTest : TestBase
    {
        static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        public TlsHandlerTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> GetTlsReadTestData()
        {
            var random = new Random(Environment.TickCount);
            var lengthVariations =
                new[]
                {
                    new[] { 1 },
                    new[] { 2, 8000, 300 },
                    new[] { 100, 0, 1000 },
                    new[] { 4 * 1024 - 10, 1, 0, 1 },
                    new[] { 0, 24000, 0, 1000 },
                    new[] { 0, 4000, 0 },
                    new[] { 16 * 1024 - 100 },
                    Enumerable.Repeat(0, 30).Select(_ => random.Next(0, 17000)).ToArray()
                };
            var boolToggle = new[] { false, true };
            var protocols = new (SslProtocols serverProtocol, SslProtocols clientProtocol)[] { (SslProtocols.None, SslProtocols.None) };

            var writeStrategyFactories = new Func<IWriteStrategy>[]
            {
                () => new AsIsWriteStrategy(),
                () => new BatchingWriteStrategy(1, TimeSpan.FromMilliseconds(20), true),
                () => new BatchingWriteStrategy(4096, TimeSpan.FromMilliseconds(20), true),
                () => new BatchingWriteStrategy(32 * 1024, TimeSpan.FromMilliseconds(20), false)
            };

            return
                from frameLengths in lengthVariations
                from isClient in boolToggle
                from writeStrategyFactory in writeStrategyFactories
                from protocol in protocols
                select new object[] { frameLengths, isClient, writeStrategyFactory(), protocol.serverProtocol, protocol.clientProtocol };
        }

        public static IEnumerable<object[]> GetTlsReadTestProtocol()
        {
            var lengthVariations =
                new[]
                {
                    new[] { 1 },
                };
            var boolToggle = new[] { false, true };
            var protocols = GetTlsTestProtocol();

            var writeStrategyFactories = new Func<IWriteStrategy>[]
            {
                () => new AsIsWriteStrategy()
            };

            return
                from frameLengths in lengthVariations
                from isClient in boolToggle
                from writeStrategyFactory in writeStrategyFactories
                from protocol in protocols
                select new object[] { frameLengths, isClient, writeStrategyFactory(), protocol.serverProtocol, protocol.clientProtocol };
        }

        static List<(SslProtocols serverProtocol, SslProtocols clientProtocol)> GetTlsTestProtocol()
        {
            var protocols = new List<(SslProtocols serverProtocol, SslProtocols clientProtocol)>();
            var supportedProtocolList = Platform.SupportedSslProtocolList;
            foreach (var cur in supportedProtocolList)
            {
                protocols.Add((cur, cur));
            }
            int handShakeTestCnt = 0;
            var supportedProtocols = Platform.AllSupportedSslProtocols;
            foreach (var cur in supportedProtocolList)
            {
                protocols.Add((cur, supportedProtocols));
                protocols.Add((supportedProtocols, cur));

                handShakeTestCnt++;
                if (handShakeTestCnt >= 2)
                    break;
            }

            protocols = FilterPlatformAvailableProtocols(protocols);
            return protocols;
        }

        [Theory]
        [MemberData(nameof(GetTlsReadTestData))]
        [MemberData(nameof(GetTlsReadTestProtocol))]
        public async Task TlsRead(int[] frameLengths, bool isClient, IWriteStrategy writeStrategy, SslProtocols serverProtocol, SslProtocols clientProtocol)
        {
            this.Output.WriteLine($"frameLengths: {string.Join(", ", frameLengths)}");
            this.Output.WriteLine($"isClient: {isClient}");
            this.Output.WriteLine($"writeStrategy: {writeStrategy}");
            this.Output.WriteLine($"serverProtocol: {serverProtocol}");
            this.Output.WriteLine($"clientProtocol: {clientProtocol}");

            var executor = new DefaultEventExecutor();

            try
            {
                var writeTasks = new List<Task>();
                var pair = await SetupStreamAndChannelAsync(isClient, executor, writeStrategy, serverProtocol, clientProtocol, writeTasks).WithTimeout(TimeSpan.FromSeconds(10));
                EmbeddedChannel ch = pair.Item1;
                SslStream driverStream = pair.Item2;

                int randomSeed = Environment.TickCount;
                var random = new Random(randomSeed);
                IByteBuffer expectedBuffer = Unpooled.Buffer(16 * 1024);
                foreach (int len in frameLengths)
                {
                    var data = new byte[len];
                    random.NextBytes(data);
                    expectedBuffer.WriteBytes(data);
                    await driverStream.WriteAsync(data, 0, data.Length).WithTimeout(TimeSpan.FromSeconds(5));
                }
                await Task.WhenAll(writeTasks).WithTimeout(TimeSpan.FromSeconds(5));
                IByteBuffer finalReadBuffer = Unpooled.Buffer(16 * 1024);
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
                await ReadOutboundAsync(async () => ch.ReadInbound<IByteBuffer>(), expectedBuffer.ReadableBytes, finalReadBuffer, TestTimeout);
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
                bool isEqual = ByteBufferUtil.Equals(expectedBuffer, finalReadBuffer);
                if (!isEqual)
                {
                    Assert.True(isEqual, $"---Expected:\n{ByteBufferUtil.PrettyHexDump(expectedBuffer)}\n---Actual:\n{ByteBufferUtil.PrettyHexDump(finalReadBuffer)}");
                }
                driverStream.Dispose();
                Assert.False(ch.Finish());
            }
            finally
            {
                await executor.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);
            }
        }

        public static IEnumerable<object[]> GetTlsWriteTestData()
        {
            var random = new Random(Environment.TickCount);
            var lengthVariations =
                new[]
                {
                    new[] { 1 },
                    new[] { 2, 8000, 300 },
                    new[] { 100, 0, 1000 },
                    new[] { 4 * 1024 - 10, 1, -1, 0, -1, 1 },
                    new[] { 0, 24000, 0, -1, 1000 },
                    new[] { 0, 4000, 0 },
                    new[] { 16 * 1024 - 100 },
                    Enumerable.Repeat(0, 30).Select(_ => random.Next(0, 10) < 2 ? -1 : random.Next(0, 17000)).ToArray()
                };
            var boolToggle = new[] { false, true };
            var protocols = new (SslProtocols serverProtocol, SslProtocols clientProtocol)[] { (SslProtocols.None, SslProtocols.None) };

            return
                from frameLengths in lengthVariations
                from isClient in boolToggle
                from protocol in protocols
                select new object[] { frameLengths, isClient, protocol.serverProtocol, protocol.clientProtocol };
        }

        public static IEnumerable<object[]> GetTlsWriteTestProtocol()
        {
            var lengthVariations =
                new[]
                {
                    new[] { 1 }
                };
            var boolToggle = new[] { false, true };
            var protocols = GetTlsTestProtocol();

            return
                from frameLengths in lengthVariations
                from isClient in boolToggle
                from protocol in protocols
                select new object[] { frameLengths, isClient, protocol.serverProtocol, protocol.clientProtocol };
        }

        [Theory]
        [MemberData(nameof(GetTlsWriteTestData))]
        [MemberData(nameof(GetTlsWriteTestProtocol))]
        public async Task TlsWrite(int[] frameLengths, bool isClient, SslProtocols serverProtocol, SslProtocols clientProtocol)
        {
            this.Output.WriteLine($"frameLengths: {string.Join(", ", frameLengths)}");
            this.Output.WriteLine($"isClient: {isClient}");
            this.Output.WriteLine($"serverProtocol: {serverProtocol}");
            this.Output.WriteLine($"clientProtocol: {clientProtocol}");

            var writeStrategy = new AsIsWriteStrategy();
            this.Output.WriteLine($"writeStrategy: {writeStrategy}");

            var executor = new DefaultEventExecutor();

            try
            {
                var writeTasks = new List<Task>();
                var pair = await SetupStreamAndChannelAsync(isClient, executor, writeStrategy, serverProtocol, clientProtocol, writeTasks);
                EmbeddedChannel ch = pair.Item1;
                SslStream driverStream = pair.Item2;

                int randomSeed = Environment.TickCount;
                var random = new Random(randomSeed);
                IByteBuffer expectedBuffer = Unpooled.Buffer(16 * 1024);
                foreach (IEnumerable<int> lengths in frameLengths.Split(x => x < 0))
                {
                    ch.WriteOutbound(lengths.Select(len =>
                    {
                        var data = new byte[len];
                        random.NextBytes(data);
                        expectedBuffer.WriteBytes(data);
                        return (object)Unpooled.WrappedBuffer(data);
                    }).ToArray());
                }

                IByteBuffer finalReadBuffer = Unpooled.Buffer(16 * 1024);
                var readBuffer = new byte[16 * 1024 * 10];
                await ReadOutboundAsync(
                    async () =>
                    {
                        int read = await driverStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                        return Unpooled.WrappedBuffer(readBuffer, 0, read);
                    },
                    expectedBuffer.ReadableBytes, finalReadBuffer, TestTimeout);
                bool isEqual = ByteBufferUtil.Equals(expectedBuffer, finalReadBuffer);
                if (!isEqual)
                {
                    Assert.True(isEqual, $"---Expected:\n{ByteBufferUtil.PrettyHexDump(expectedBuffer)}\n---Actual:\n{ByteBufferUtil.PrettyHexDump(finalReadBuffer)}");
                }
                driverStream.Dispose();
                Assert.False(ch.Finish());
            }
            finally
            {
                await executor.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);
            }
        }

        static List<(SslProtocols serverProtocol, SslProtocols clientProtocol)> FilterPlatformAvailableProtocols(List<(SslProtocols serverProtocol, SslProtocols clientProtocol)> protocols)
        {
            var set = new HashSet<(SslProtocols serverProtocol, SslProtocols clientProtocol)>();
            var list = new List<(SslProtocols serverProtocol, SslProtocols clientProtocol)>(protocols.Count + 1);

            //Ensure there is at least one test available(SslProtocols.None: Allows the operating system to choose the best protocol to use, and to block protocols that are not secure.)
            list.Add((SslProtocols.None, SslProtocols.None));

            var supportedSslProtocols = Platform.AllSupportedSslProtocols;
            if (supportedSslProtocols != SslProtocols.None)
            {
                foreach (var cur in protocols)
                {
                    var (serverProtocol, clientProtocol) = cur;
                    serverProtocol &= supportedSslProtocols;
                    clientProtocol &= supportedSslProtocols;
                    if ((serverProtocol & clientProtocol) == SslProtocols.None)
                        continue;
                    if (set.Add((serverProtocol, clientProtocol)))
                        list.Add((serverProtocol, clientProtocol));
                }
            }
            return list;
        }

        static async Task<Tuple<EmbeddedChannel, SslStream>> SetupStreamAndChannelAsync(bool isClient, IEventExecutor executor, IWriteStrategy writeStrategy, SslProtocols serverProtocol, SslProtocols clientProtocol, List<Task> writeTasks)
        {
            X509Certificate2 tlsCertificate = TestResourceHelper.GetTestCertificate();
            string targetHost = tlsCertificate.GetNameInfo(X509NameType.DnsName, false);
            TlsHandler tlsHandler = isClient ?
                new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(clientProtocol, false, new List<X509Certificate>(), targetHost)) :
                new TlsHandler(new ServerTlsSettings(tlsCertificate, false, false, serverProtocol));
            //var ch = new EmbeddedChannel(new LoggingHandler("BEFORE"), tlsHandler, new LoggingHandler("AFTER"));
            var ch = new EmbeddedChannel(tlsHandler);

            IByteBuffer readResultBuffer = Unpooled.Buffer(4 * 1024);
            Func<ArraySegment<byte>, Task<int>> readDataFunc = async output =>
            {
                if (writeTasks.Count > 0)
                {
                    await Task.WhenAll(writeTasks).WithTimeout(TestTimeout);
                    writeTasks.Clear();
                }

                if (readResultBuffer.ReadableBytes < output.Count)
                {
                    if (ch.IsActive)
                    {
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
                        await ReadOutboundAsync(async () => ch.ReadOutbound<IByteBuffer>(), output.Count - readResultBuffer.ReadableBytes, readResultBuffer, TestTimeout, readResultBuffer.ReadableBytes != 0 ? 0 : 1);
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
                    }
                }
                int read = Math.Min(output.Count, readResultBuffer.ReadableBytes);
                readResultBuffer.ReadBytes(output.Array, output.Offset, read);
                return read;
            };
            var mediationStream = new MediationStream(readDataFunc, input =>
            {
                Task task = executor.SubmitAsync(() => writeStrategy.WriteToChannelAsync(ch, input)).Unwrap();
                writeTasks.Add(task);
                return task;
            }, () =>
            {
                ch.CloseAsync();
            });

            var driverStream = new SslStream(mediationStream, true, (_1, _2, _3, _4) => true);
            if (isClient)
            {
                await Task.Run(() => driverStream.AuthenticateAsServerAsync(tlsCertificate, false, serverProtocol, false)).WithTimeout(TimeSpan.FromSeconds(5));
            }
            else
            {
                await Task.Run(() => driverStream.AuthenticateAsClientAsync(targetHost, null, clientProtocol, false)).WithTimeout(TimeSpan.FromSeconds(5));
            }
            if ((clientProtocol & serverProtocol) != SslProtocols.None)
                Assert.True((clientProtocol & serverProtocol & driverStream.SslProtocol) != SslProtocols.None, "Unexpected ssl handshake protocol: " + driverStream.SslProtocol);

            writeTasks.Clear();

            return Tuple.Create(ch, driverStream);
        }

        static Task ReadOutboundAsync(Func<Task<IByteBuffer>> readFunc, int expectedBytes, IByteBuffer result, TimeSpan timeout, int minBytes = -1)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int remaining = expectedBytes;
            if (minBytes < 0) minBytes = expectedBytes;
            if (minBytes > expectedBytes) throw new ArgumentOutOfRangeException("minBytes can not greater than expectedBytes");
            return AssertEx.EventuallyAsync(
                async () =>
                {
                    TimeSpan readTimeout = timeout - stopwatch.Elapsed;
                    if (readTimeout <= TimeSpan.Zero)
                    {
                        return false;
                    }

                    IByteBuffer output;
                    while (true)
                    {
                        output = await readFunc().WithTimeout(readTimeout);//inbound ? ch.ReadInbound<IByteBuffer>() : ch.ReadOutbound<IByteBuffer>();
                        if (output == null)
                            break;

                        if (!output.IsReadable())
                        {
                            output.Release();
                            return true;
                        }

                        remaining -= output.ReadableBytes;
                        minBytes -= output.ReadableBytes;
                        result.WriteBytes(output);
                        output.Release();

                        if (remaining <= 0)
                            return true;
                    }
                    return minBytes <= 0;
                },
                TimeSpan.FromMilliseconds(10),
                timeout);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NoAutoReadHandshakeProgresses(bool dropChannelActive)
        {
            var readHandler = new ReadRegisterHandler();
            var ch = new EmbeddedChannel(EmbeddedChannelId.Instance, false, false,
               readHandler,
               TlsHandler.Client("dotnetty.com"),
               new ActivatingHandler(dropChannelActive)
            );

            ch.Configuration.IsAutoRead = false;
            ch.Register();
            Assert.False(ch.Configuration.IsAutoRead);
            Assert.True(ch.WriteOutbound(Unpooled.Empty));
            Assert.True(readHandler.ReadIssued);
            ch.CloseAsync();
        }

        class ReadRegisterHandler : ChannelHandlerAdapter
        {
            public bool ReadIssued { get; private set; }

            public override void Read(IChannelHandlerContext context)
            {
                this.ReadIssued = true;
                base.Read(context);
            }
        }

        class ActivatingHandler : ChannelHandlerAdapter
        {
            bool dropChannelActive;

            public ActivatingHandler(bool dropChannelActive)
            {
                this.dropChannelActive = dropChannelActive;
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                if (!dropChannelActive)
                {
                    context.FireChannelActive();
                }
            }
        }

        public static class Platform
        {
            public static readonly SslProtocols AllSupportedSslProtocols;
            public static readonly IReadOnlyList<SslProtocols> SupportedSslProtocolList;
            static Platform()
            {
                var allProtocol = new[]
                {
                    SslProtocols.Tls,
                    SslProtocols.Tls11,
                    SslProtocols.Tls12,
#if NETCOREAPP_3_0_GREATER
                    SslProtocols.Tls13,
#endif
                };
                var protocols = SslProtocols.None;
                var list = new List<SslProtocols>();
                foreach (var cur in allProtocol)
                {
                    if (CheckSslProtocol(cur))
                    {
                        protocols |= cur;
                        list.Add(cur);
                    }
                }
                AllSupportedSslProtocols = protocols;
                SupportedSslProtocolList = list.AsReadOnly();
            }

            private static bool CheckSslProtocol(SslProtocols protocol)
            {
                X509Certificate2 tlsCertificate = TestResourceHelper.GetTestCertificate();
                string targetHost = tlsCertificate.GetNameInfo(X509NameType.SimpleName, false);
                try
                {
                    using (var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        server.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                        server.Listen(1);
                        using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                        {
                            Task.Run(async () =>
                            {
                                client.Connect(server.LocalEndPoint);
                                using (var a = new SslStream(new NetworkStream(server.Accept(), ownsSocket: true)))
                                using (var b = new SslStream(new NetworkStream(client, ownsSocket: true), false, (sender, certificate, chain, sslPolicyErrors) => true))
                                {
                                    await Task.WhenAll(
                                        Task.Run(async () =>
                                        {
                                            using (b)
                                            {
                                                await b.AuthenticateAsClientAsync(targetHost, null, protocol, false);
                                                Debug.Assert(b.SslProtocol == protocol);
                                            }
                                        }),
                                        Task.Run(async () =>
                                        {
                                            using (a)
                                            {
                                                await a.AuthenticateAsServerAsync(tlsCertificate, false, protocol, false);
                                                Debug.Assert(a.SslProtocol == protocol);
                                            }
                                        }));
                                }
                            }).WithTimeout(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
                        }
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
