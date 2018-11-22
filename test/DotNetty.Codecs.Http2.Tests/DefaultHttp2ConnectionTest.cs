
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    public class EventLoopGroupFixture : IDisposable
    {
        public readonly MultithreadEventLoopGroup Group;

        public EventLoopGroupFixture()
        {
            Group = new MultithreadEventLoopGroup(2);
        }

        public void Dispose()
        {
            Group.ShutdownGracefullyAsync();
        }
    }

    /**
     * Tests for {@link DefaultHttp2Connection}.
     */
    public class DefaultHttp2ConnectionTest : IClassFixture<EventLoopGroupFixture>
    {
        private readonly EventLoopGroupFixture fixture;
        private DefaultHttp2Connection server;
        private DefaultHttp2Connection client;

        private Mock<IHttp2ConnectionListener> clientListener;
        private Mock<IHttp2ConnectionListener> clientListener2;

        public DefaultHttp2ConnectionTest(EventLoopGroupFixture fixture)
        {
            this.fixture = fixture;

            this.clientListener = new Mock<IHttp2ConnectionListener>();
            this.clientListener
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(stream => Assert.True(stream.Id > 0));
            this.clientListener
                .Setup(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(stream => Assert.True(stream.Id > 0));
            this.clientListener2 = new Mock<IHttp2ConnectionListener>();

            this.server = new DefaultHttp2Connection(true);
            this.client = new DefaultHttp2Connection(false);
            this.client.AddListener(this.clientListener.Object);

        }

        [Fact]
        public void GetStreamWithoutStreamShouldReturnNull()
        {
            Assert.Null(server.Stream(100));
        }

        [Fact]
        public void RemoveAllStreamsWithEmptyStreams()
        {
            this.TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveAllStreamsWithJustOneLocalStream()
        {
            client.Local.CreateStream(3, false);
            this.TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveAllStreamsWithJustOneRemoveStream()
        {
            client.Remote.CreateStream(2, false);
            this.TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveAllStreamsWithManyActiveStreams()
        {
            var remote = client.Remote;
            var local = client.Local;
            for (int c = 3, s = 2; c < 5000; c += 2, s += 2)
            {
                local.CreateStream(c, false);
                remote.CreateStream(s, false);
            }
            this.TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveIndividualStreamsWhileCloseDoesNotNPE()
        {
            IHttp2Stream streamA = client.Local.CreateStream(3, false);
            IHttp2Stream streamB = client.Remote.CreateStream(2, false);
            this.clientListener2
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(stream =>
                {
                    streamA.Close();
                    streamB.Close();
                });
            try
            {
                client.AddListener(this.clientListener2.Object);
                this.TestRemoveAllStreams();
            }
            finally
            {
                client.RemoveListener(this.clientListener2.Object);
            }
        }

        [Fact]
        public void RemoveAllStreamsWhileIteratingActiveStreams()
        {
            var remote = client.Remote;
            var local = client.Local;
            for (int c = 3, s = 2; c < 5000; c += 2, s += 2)
            {
                local.CreateStream(c, false);
                remote.CreateStream(s, false);
            }
            var promise = this.fixture.Group.GetNext().NewPromise();
            var latch = new CountdownEvent(client.NumActiveStreams);
            bool localVisit(IHttp2Stream stream)
            {
                var closeFuture = client.CloseAsync(promise);
                closeFuture.ContinueWith(t =>
                {
                    Assert.True(t.IsCompleted);
                    latch.SafeSignal();
                }, TaskContinuationOptions.ExecuteSynchronously);
                return true;
            }
            client.ForEachActiveStream(localVisit);
            Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void RemoveAllStreamsWhileIteratingActiveStreamsAndExceptionOccurs()
        {
            var remote = client.Remote;
            var local = client.Local;
            for (int c = 3, s = 2; c < 5000; c += 2, s += 2)
            {
                local.CreateStream(c, false);
                remote.CreateStream(s, false);
            }
            var promise = this.fixture.Group.GetNext().NewPromise();
            var latch = new CountdownEvent(1);
            try
            {
                bool localVisit(IHttp2Stream stream)
                {
                    // This close call is basically a noop, because the following statement will throw an exception.
                    client.CloseAsync(promise);
                    // Do an invalid operation while iterating.
                    remote.CreateStream(3, false);
                    return true;
                }
                client.ForEachActiveStream(localVisit);
            }
            catch (Http2Exception)
            {
                var closeFuture = client.CloseAsync(promise);
                closeFuture.ContinueWith(t =>
                {
                    Assert.True(t.IsCompleted);
                    latch.SafeSignal();
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
            Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void GoAwayReceivedShouldCloseStreamsGreaterThanLastStream()
        {
            IHttp2Stream stream1 = client.Local.CreateStream(3, false);
            IHttp2Stream stream2 = client.Local.CreateStream(5, false);
            IHttp2Stream remoteStream = client.Remote.CreateStream(4, false);

            Assert.Equal(Http2StreamState.Open, stream1.State);
            Assert.Equal(Http2StreamState.Open, stream2.State);

            client.GoAwayReceived(3, (Http2Error)8, null);

            Assert.Equal(Http2StreamState.Open, stream1.State);
            Assert.Equal(Http2StreamState.Closed, stream2.State);
            Assert.Equal(Http2StreamState.Open, remoteStream.State);
            Assert.Equal(3, client.Local.LastStreamKnownByPeer());
            Assert.Equal(5, client.Local.LastStreamCreated);
            // The remote endpoint must not be affected by a received GOAWAY frame.
            Assert.Equal(-1, client.Remote.LastStreamKnownByPeer());
            Assert.Equal(Http2StreamState.Open, remoteStream.State);
        }

        [Fact]
        public void GoAwaySentShouldCloseStreamsGreaterThanLastStream()
        {
            IHttp2Stream stream1 = server.Remote.CreateStream(3, false);
            IHttp2Stream stream2 = server.Remote.CreateStream(5, false);
            IHttp2Stream localStream = server.Local.CreateStream(4, false);

            server.GoAwaySent(3, (Http2Error)8, null);

            Assert.Equal(Http2StreamState.Open, stream1.State);
            Assert.Equal(Http2StreamState.Closed, stream2.State);

            Assert.Equal(3, server.Remote.LastStreamKnownByPeer());
            Assert.Equal(5, server.Remote.LastStreamCreated);
            // The local endpoint must not be affected by a sent GOAWAY frame.
            Assert.Equal(-1, server.Local.LastStreamKnownByPeer());
            Assert.Equal(Http2StreamState.Open, localStream.State);
        }

        [Fact]
        public void ServerCreateStreamShouldSucceed()
        {
            IHttp2Stream stream = server.Local.CreateStream(2, false);
            Assert.Equal(2, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(1, server.NumActiveStreams);
            Assert.Equal(2, server.Local.LastStreamCreated);

            stream = server.Local.CreateStream(4, true);
            Assert.Equal(4, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedLocal, stream.State);
            Assert.Equal(2, server.NumActiveStreams);
            Assert.Equal(4, server.Local.LastStreamCreated);

            stream = server.Remote.CreateStream(3, true);
            Assert.Equal(3, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.Equal(3, server.NumActiveStreams);
            Assert.Equal(3, server.Remote.LastStreamCreated);

            stream = server.Remote.CreateStream(5, false);
            Assert.Equal(5, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(4, server.NumActiveStreams);
            Assert.Equal(5, server.Remote.LastStreamCreated);
        }

        [Fact]
        public void ClientCreateStreamShouldSucceed()
        {
            IHttp2Stream stream = client.Remote.CreateStream(2, false);
            Assert.Equal(2, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(1, client.NumActiveStreams);
            Assert.Equal(2, client.Remote.LastStreamCreated);

            stream = client.Remote.CreateStream(4, true);
            Assert.Equal(4, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.Equal(2, client.NumActiveStreams);
            Assert.Equal(4, client.Remote.LastStreamCreated);
            Assert.True(stream.IsHeadersReceived);

            stream = client.Local.CreateStream(3, true);
            Assert.Equal(3, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedLocal, stream.State);
            Assert.Equal(3, client.NumActiveStreams);
            Assert.Equal(3, client.Local.LastStreamCreated);
            Assert.True(stream.IsHeadersSent);

            stream = client.Local.CreateStream(5, false);
            Assert.Equal(5, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(4, client.NumActiveStreams);
            Assert.Equal(5, client.Local.LastStreamCreated);
        }

        [Fact]
        public void ServerReservePushStreamShouldSucceed()
        {
            IHttp2Stream stream = server.Remote.CreateStream(3, true);
            IHttp2Stream pushStream = server.Local.ReservePushStream(2, stream);
            Assert.Equal(2, pushStream.Id);
            Assert.Equal(Http2StreamState.ReservedLocal, pushStream.State);
            Assert.Equal(1, server.NumActiveStreams);
            Assert.Equal(2, server.Local.LastStreamCreated);
        }

        [Fact]
        public void ClientReservePushStreamShouldSucceed()
        {
            IHttp2Stream stream = server.Remote.CreateStream(3, true);
            IHttp2Stream pushStream = server.Local.ReservePushStream(4, stream);
            Assert.Equal(4, pushStream.Id);
            Assert.Equal(Http2StreamState.ReservedLocal, pushStream.State);
            Assert.Equal(1, server.NumActiveStreams);
            Assert.Equal(4, server.Local.LastStreamCreated);
        }

        [Fact]
        public void ServerRemoteIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(server.Remote);
        }

        [Fact]
        public void ServerLocalIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(server.Local);
        }

        [Fact]
        public void ClientRemoteIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(client.Remote);
        }

        [Fact]
        public void ClientLocalIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(client.Local);
        }

        [Fact]
        public void ServerRemoteIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(server.Remote, int.MaxValue));
        }

        [Fact]
        public void ServerLocalIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(server.Local, int.MaxValue - 1));
        }

        [Fact]
        public void ClientRemoteIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(client.Remote, int.MaxValue - 1));
        }

        [Fact]
        public void ClientLocalIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(client.Local, int.MaxValue));
        }

        [Fact]
        public void NewStreamBehindExpectedShouldThrow()
        {
            Assert.Throws<Http2Exception>(() => server.Local.CreateStream(0, true));
        }

        [Fact]
        public void NewStreamNotForServerShouldThrow()
        {
            Assert.Throws<Http2Exception>(() => server.Local.CreateStream(11, true));
        }

        [Fact]
        public void NewStreamNotForClientShouldThrow()
        {
            Assert.Throws<Http2Exception>(() => client.Local.CreateStream(10, true));
        }

        [Fact]
        public void CreateShouldThrowWhenMaxAllowedStreamsOpenExceeded()
        {
            Assert.Throws<StreamException>(() =>
            {
                server.Local.SetMaxActiveStreams(0);
                server.Local.CreateStream(2, true);
            });
        }

        [Fact]
        public void ServerCreatePushShouldFailOnRemoteEndpointWhenMaxAllowedStreamsExceeded()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                server = new DefaultHttp2Connection(true, 0);
                server.Remote.SetMaxActiveStreams(1);
                IHttp2Stream requestStream = server.Remote.CreateStream(3, false);
                server.Remote.ReservePushStream(2, requestStream);
            });
        }

        [Fact]
        public void ClientCreatePushShouldFailOnRemoteEndpointWhenMaxAllowedStreamsExceeded()
        {
            Assert.Throws<StreamException>(() =>
            {
                client = new DefaultHttp2Connection(false, 0);
                client.Remote.SetMaxActiveStreams(1);
                IHttp2Stream requestStream = client.Remote.CreateStream(2, false);
                client.Remote.ReservePushStream(4, requestStream);
            });
        }

        [Fact]
        public void ServerCreatePushShouldSucceedOnLocalEndpointWhenMaxAllowedStreamsExceeded()
        {
            server = new DefaultHttp2Connection(true, 0);
            server.Local.SetMaxActiveStreams(1);
            IHttp2Stream requestStream = server.Remote.CreateStream(3, false);
            Assert.NotNull(server.Local.ReservePushStream(2, requestStream));
        }

        [Fact]
        public void ReserveWithPushDisallowedShouldThrow()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                IHttp2Stream stream = server.Remote.CreateStream(3, true);
                server.Remote.AllowPushTo(false);
                server.Local.ReservePushStream(2, stream);
            });
        }

        [Fact]
        public void GoAwayReceivedShouldDisallowLocalCreation()
        {
            Assert.Throws<StreamException>(() =>
            {
                server.GoAwayReceived(0, (Http2Error)1L, Unpooled.Empty);
                server.Local.CreateStream(3, true);
            });
        }

        [Fact]
        public void GoAwayReceivedShouldAllowRemoteCreation()
        {
            server.GoAwayReceived(0, (Http2Error)1L, Unpooled.Empty);
            server.Remote.CreateStream(3, true);
        }

        [Fact]
        public void GoAwaySentShouldDisallowRemoteCreation()
        {
            Assert.Throws<StreamException>(() =>
            {
                server.GoAwaySent(0, (Http2Error)1L, Unpooled.Empty);
                server.Remote.CreateStream(2, true);
            });
        }

        [Fact]
        public void GoAwaySentShouldAllowLocalCreation()
        {
            server.GoAwaySent(0, (Http2Error)1L, Unpooled.Empty);
            server.Local.CreateStream(2, true);
        }

        [Fact]
        public void CloseShouldSucceed()
        {
            IHttp2Stream stream = server.Remote.CreateStream(3, true);
            stream.Close();
            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.Equal(0, server.NumActiveStreams);
        }

        [Fact]
        public void CloseLocalWhenOpenShouldSucceed()
        {
            IHttp2Stream stream = server.Remote.CreateStream(3, false);
            stream.CloseLocalSide();
            Assert.Equal(Http2StreamState.HalfClosedLocal, stream.State);
            Assert.Equal(1, server.NumActiveStreams);
        }

        [Fact]
        public void CloseRemoteWhenOpenShouldSucceed()
        {
            IHttp2Stream stream = server.Remote.CreateStream(3, false);
            stream.CloseRemoteSide();
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.Equal(1, server.NumActiveStreams);
        }

        [Fact]
        public void CloseOnlyOpenSideShouldClose()
        {
            IHttp2Stream stream = server.Remote.CreateStream(3, true);
            stream.CloseLocalSide();
            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.Equal(0, server.NumActiveStreams);
        }

        ////@SuppressWarnings("NumericOverflow")
        //[Fact] 
        //public void localStreamInvalidStreamIdShouldThrow()
        //{
        //    client.Local.CreateStream(int.MaxValue + 2L, false);
        //}

        ////@SuppressWarnings("NumericOverflow")
        //[Fact] 
        //public void remoteStreamInvalidStreamIdShouldThrow()
        //{
        //    client.Remote.CreateStream(int.MaxValue + 1, false);
        //}

        /**
         * We force {@link #clientListener} methods to all throw a {@link RuntimeException} and verify the following:
         * <ol>
         * <li>all listener methods are called for both {@link #clientListener} and {@link #clientListener2}</li>
         * <li>{@link #clientListener2} is notified after {@link #clientListener}</li>
         * <li>{@link #clientListener2} methods are all still called despite {@link #clientListener}'s
         * method throwing a {@link RuntimeException}</li>
         * </ol>
         */
        [Fact]
        public void ListenerThrowShouldNotPreventOtherListenersFromBeingNotified()
        {
            var calledArray = new bool[128];
            // The following setup will ensure that clientListener throws exceptions, and marks a value in an array
            // such that clientListener2 will verify that is is set or fail the test.
            int methodIndex = 0;
            this.clientListener
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            this.clientListener
                .Setup(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            this.clientListener
                .Setup(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            this.clientListener
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            this.clientListener
                .Setup(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            this.clientListener
                .Setup(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            this.clientListener
                .Setup(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            this.clientListener
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            this.clientListener2
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            // Now we add clientListener2 and exercise all listener functionality
            try
            {
                this.client.AddListener(this.clientListener2.Object);
                IHttp2Stream stream = client.Local.CreateStream(3, false);
                this.clientListener.Verify(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()));
                this.clientListener2.Verify(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()));
                this.clientListener.Verify(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()));
                this.clientListener2.Verify(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()));

                IHttp2Stream reservedStream = client.Remote.ReservePushStream(2, stream);
                this.clientListener.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))), Times.Never());
                this.clientListener2.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))), Times.Never());

                reservedStream.Open(false);
                this.clientListener.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))));
                this.clientListener2.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))));

                stream.CloseLocalSide();
                this.clientListener.Verify(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()));
                this.clientListener2.Verify(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()));

                stream.Close();
                this.clientListener.Verify(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()));
                this.clientListener2.Verify(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()));
                this.clientListener.Verify(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()));
                this.clientListener2.Verify(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()));

                this.client.GoAwaySent(this.client.ConnectionStream.Id, Http2Error.InternalError, Unpooled.Empty);
                this.clientListener.Verify(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));
                this.clientListener2.Verify(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));

                this.client.GoAwayReceived(this.client.ConnectionStream.Id, Http2Error.InternalError, Unpooled.Empty);
                this.clientListener.Verify(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));
                this.clientListener2.Verify(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));
            }
            finally
            {
                this.client.RemoveListener(this.clientListener2.Object);
            }
        }

        void TestRemoveAllStreams()
        {
            var latch = new CountdownEvent(1);
            var promise = this.fixture.Group.GetNext().NewPromise();
            var closeFuture = this.client.CloseAsync(promise);
            closeFuture.ContinueWith(t =>
            {
                Assert.True(t.IsCompleted);
                latch.SafeSignal();
            }, TaskContinuationOptions.ExecuteSynchronously);
            Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));
        }

        static void IncrementAndGetStreamShouldRespectOverflow(IHttp2ConnectionEndpoint endpoint, int streamId)
        {
            Assert.True(streamId > 0);
            try
            {
                endpoint.CreateStream(streamId, true);
                streamId = endpoint.IncrementAndGetNextStreamId;
            }
            catch (Exception t)
            {
                Assert.False(true, t.Message);
            }
            Assert.True(streamId < 0);
            endpoint.CreateStream(streamId, true);
        }

        static void IncrementAndGetStreamShouldSucceed(IHttp2ConnectionEndpoint endpoint)
        {
            IHttp2Stream streamA = endpoint.CreateStream(endpoint.IncrementAndGetNextStreamId, true);
            IHttp2Stream streamB = endpoint.CreateStream(streamA.Id + 2, true);
            IHttp2Stream streamC = endpoint.CreateStream(endpoint.IncrementAndGetNextStreamId, true);
            Assert.Equal(streamB.Id + 2, streamC.Id);
            endpoint.CreateStream(streamC.Id + 2, true);
        }

        static readonly Http2RuntimeException FAKE_EXCEPTION = new Http2RuntimeException("Fake Exception");
        static void ListenerExceptionThrower(bool[] array, int index)
        {
            array[index] = true;
            throw FAKE_EXCEPTION;
        }

        static void ListenerVerifyCallAnswer(bool[] array, int index)
        {
            Assert.True(array[index]);
        }

        static bool StreamEq(IHttp2Stream arg, IHttp2Stream stream)
        {
            return stream == null ? arg == null : ReferenceEquals(arg, stream);
        }
    }
}
