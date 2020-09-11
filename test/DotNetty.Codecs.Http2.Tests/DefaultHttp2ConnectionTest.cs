
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Moq;
    using Xunit;

    /**
     * Tests for {@link DefaultHttp2Connection}.
     */
    public class DefaultHttp2ConnectionTest : IDisposable
    {
        private readonly DefaultEventLoopGroup _group;
        private DefaultHttp2Connection _server;
        private DefaultHttp2Connection _client;

        private Mock<IHttp2ConnectionListener> _clientListener;
        private Mock<IHttp2ConnectionListener> _clientListener2;

        public DefaultHttp2ConnectionTest()
        {
            _group = new DefaultEventLoopGroup(2);

            _clientListener = new Mock<IHttp2ConnectionListener>();
            _clientListener
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(stream => Assert.True(stream.Id > 0));
            _clientListener
                .Setup(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(stream => Assert.True(stream.Id > 0));
            _clientListener2 = new Mock<IHttp2ConnectionListener>();

            _server = new DefaultHttp2Connection(true);
            _client = new DefaultHttp2Connection(false);
            _client.AddListener(_clientListener.Object);
        }

        public void Dispose()
        {
            try
            {
                _group.ShutdownGracefullyAsync();
            }
            catch
            {
                // Ignore RejectedExecutionException(on Azure DevOps)
            }
        }

        [Fact]
        public void GetStreamWithoutStreamShouldReturnNull()
        {
            Assert.Null(_server.Stream(100));
        }

        [Fact]
        public void RemoveAllStreamsWithEmptyStreams()
        {
            TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveAllStreamsWithJustOneLocalStream()
        {
            _client.Local.CreateStream(3, false);
            TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveAllStreamsWithJustOneRemoveStream()
        {
            _client.Remote.CreateStream(2, false);
            TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveAllStreamsWithManyActiveStreams()
        {
            var remote = _client.Remote;
            var local = _client.Local;
            for (int c = 3, s = 2; c < 5000; c += 2, s += 2)
            {
                local.CreateStream(c, false);
                remote.CreateStream(s, false);
            }
            TestRemoveAllStreams();
        }

        [Fact]
        public void RemoveIndividualStreamsWhileCloseDoesNotNPE()
        {
            IHttp2Stream streamA = _client.Local.CreateStream(3, false);
            IHttp2Stream streamB = _client.Remote.CreateStream(2, false);
            _clientListener2
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(stream =>
                {
                    streamA.Close();
                    streamB.Close();
                });
            try
            {
                _client.AddListener(_clientListener2.Object);
                TestRemoveAllStreams();
            }
            finally
            {
                _client.RemoveListener(_clientListener2.Object);
            }
        }

        [Fact]
        public void RemoveAllStreamsWhileIteratingActiveStreams()
        {
            var remote = _client.Remote;
            var local = _client.Local;
            for (int c = 3, s = 2; c < 5000; c += 2, s += 2)
            {
                local.CreateStream(c, false);
                remote.CreateStream(s, false);
            }
            var promise = _group.GetNext().NewPromise();
            var latch = new CountdownEvent(_client.NumActiveStreams);
            bool localVisit(IHttp2Stream stream)
            {
                var closeFuture = _client.CloseAsync(promise);
                closeFuture.ContinueWith(t =>
                {
                    Assert.True(t.IsCompleted);
                    latch.SafeSignal();
                }, TaskContinuationOptions.ExecuteSynchronously);
                return true;
            }
            _client.ForEachActiveStream(localVisit);
            Assert.True(latch.Wait(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void RemoveAllStreamsWhileIteratingActiveStreamsAndExceptionOccurs()
        {
            var remote = _client.Remote;
            var local = _client.Local;
            for (int c = 3, s = 2; c < 5000; c += 2, s += 2)
            {
                local.CreateStream(c, false);
                remote.CreateStream(s, false);
            }
            var promise = _group.GetNext().NewPromise();
            var latch = new CountdownEvent(1);
            try
            {
                bool localVisit(IHttp2Stream stream)
                {
                    // This close call is basically a noop, because the following statement will throw an exception.
                    _client.CloseAsync(promise);
                    // Do an invalid operation while iterating.
                    remote.CreateStream(3, false);
                    return true;
                }
                _client.ForEachActiveStream(localVisit);
            }
            catch (Http2Exception)
            {
                var closeFuture = _client.CloseAsync(promise);
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
            IHttp2Stream stream1 = _client.Local.CreateStream(3, false);
            IHttp2Stream stream2 = _client.Local.CreateStream(5, false);
            IHttp2Stream remoteStream = _client.Remote.CreateStream(4, false);

            Assert.Equal(Http2StreamState.Open, stream1.State);
            Assert.Equal(Http2StreamState.Open, stream2.State);

            _client.GoAwayReceived(3, (Http2Error)8, null);

            Assert.Equal(Http2StreamState.Open, stream1.State);
            Assert.Equal(Http2StreamState.Closed, stream2.State);
            Assert.Equal(Http2StreamState.Open, remoteStream.State);
            Assert.Equal(3, _client.Local.LastStreamKnownByPeer());
            Assert.Equal(5, _client.Local.LastStreamCreated);
            // The remote endpoint must not be affected by a received GOAWAY frame.
            Assert.Equal(-1, _client.Remote.LastStreamKnownByPeer());
            Assert.Equal(Http2StreamState.Open, remoteStream.State);
        }

        [Fact]
        public void GoAwaySentShouldCloseStreamsGreaterThanLastStream()
        {
            IHttp2Stream stream1 = _server.Remote.CreateStream(3, false);
            IHttp2Stream stream2 = _server.Remote.CreateStream(5, false);
            IHttp2Stream localStream = _server.Local.CreateStream(4, false);

            _server.GoAwaySent(3, (Http2Error)8, null);

            Assert.Equal(Http2StreamState.Open, stream1.State);
            Assert.Equal(Http2StreamState.Closed, stream2.State);

            Assert.Equal(3, _server.Remote.LastStreamKnownByPeer());
            Assert.Equal(5, _server.Remote.LastStreamCreated);
            // The local endpoint must not be affected by a sent GOAWAY frame.
            Assert.Equal(-1, _server.Local.LastStreamKnownByPeer());
            Assert.Equal(Http2StreamState.Open, localStream.State);
        }

        [Fact]
        public void ServerCreateStreamShouldSucceed()
        {
            IHttp2Stream stream = _server.Local.CreateStream(2, false);
            Assert.Equal(2, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(1, _server.NumActiveStreams);
            Assert.Equal(2, _server.Local.LastStreamCreated);

            stream = _server.Local.CreateStream(4, true);
            Assert.Equal(4, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedLocal, stream.State);
            Assert.Equal(2, _server.NumActiveStreams);
            Assert.Equal(4, _server.Local.LastStreamCreated);

            stream = _server.Remote.CreateStream(3, true);
            Assert.Equal(3, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.Equal(3, _server.NumActiveStreams);
            Assert.Equal(3, _server.Remote.LastStreamCreated);

            stream = _server.Remote.CreateStream(5, false);
            Assert.Equal(5, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(4, _server.NumActiveStreams);
            Assert.Equal(5, _server.Remote.LastStreamCreated);
        }

        [Fact]
        public void ClientCreateStreamShouldSucceed()
        {
            IHttp2Stream stream = _client.Remote.CreateStream(2, false);
            Assert.Equal(2, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(1, _client.NumActiveStreams);
            Assert.Equal(2, _client.Remote.LastStreamCreated);

            stream = _client.Remote.CreateStream(4, true);
            Assert.Equal(4, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.Equal(2, _client.NumActiveStreams);
            Assert.Equal(4, _client.Remote.LastStreamCreated);
            Assert.True(stream.IsHeadersReceived);

            stream = _client.Local.CreateStream(3, true);
            Assert.Equal(3, stream.Id);
            Assert.Equal(Http2StreamState.HalfClosedLocal, stream.State);
            Assert.Equal(3, _client.NumActiveStreams);
            Assert.Equal(3, _client.Local.LastStreamCreated);
            Assert.True(stream.IsHeadersSent);

            stream = _client.Local.CreateStream(5, false);
            Assert.Equal(5, stream.Id);
            Assert.Equal(Http2StreamState.Open, stream.State);
            Assert.Equal(4, _client.NumActiveStreams);
            Assert.Equal(5, _client.Local.LastStreamCreated);
        }

        [Fact]
        public void ServerReservePushStreamShouldSucceed()
        {
            IHttp2Stream stream = _server.Remote.CreateStream(3, true);
            IHttp2Stream pushStream = _server.Local.ReservePushStream(2, stream);
            Assert.Equal(2, pushStream.Id);
            Assert.Equal(Http2StreamState.ReservedLocal, pushStream.State);
            Assert.Equal(1, _server.NumActiveStreams);
            Assert.Equal(2, _server.Local.LastStreamCreated);
        }

        [Fact]
        public void ClientReservePushStreamShouldSucceed()
        {
            IHttp2Stream stream = _server.Remote.CreateStream(3, true);
            IHttp2Stream pushStream = _server.Local.ReservePushStream(4, stream);
            Assert.Equal(4, pushStream.Id);
            Assert.Equal(Http2StreamState.ReservedLocal, pushStream.State);
            Assert.Equal(1, _server.NumActiveStreams);
            Assert.Equal(4, _server.Local.LastStreamCreated);
        }

        [Fact]
        public void ServerRemoteIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(_server.Remote);
        }

        [Fact]
        public void ServerLocalIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(_server.Local);
        }

        [Fact]
        public void ClientRemoteIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(_client.Remote);
        }

        [Fact]
        public void ClientLocalIncrementAndGetStreamShouldSucceed()
        {
            IncrementAndGetStreamShouldSucceed(_client.Local);
        }

        [Fact]
        public void ServerRemoteIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(_server.Remote, int.MaxValue));
        }

        [Fact]
        public void ServerLocalIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(_server.Local, int.MaxValue - 1));
        }

        [Fact]
        public void ClientRemoteIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(_client.Remote, int.MaxValue - 1));
        }

        [Fact]
        public void ClientLocalIncrementAndGetStreamShouldRespectOverflow()
        {
            Assert.Throws<Http2NoMoreStreamIdsException>(() => IncrementAndGetStreamShouldRespectOverflow(_client.Local, int.MaxValue));
        }

        [Fact]
        public void NewStreamBehindExpectedShouldThrow()
        {
            Assert.Throws<Http2Exception>(() => _server.Local.CreateStream(0, true));
        }

        [Fact]
        public void NewStreamNotForServerShouldThrow()
        {
            Assert.Throws<Http2Exception>(() => _server.Local.CreateStream(11, true));
        }

        [Fact]
        public void NewStreamNotForClientShouldThrow()
        {
            Assert.Throws<Http2Exception>(() => _client.Local.CreateStream(10, true));
        }

        [Fact]
        public void CreateShouldThrowWhenMaxAllowedStreamsOpenExceeded()
        {
            Assert.Throws<StreamException>(() =>
            {
                _server.Local.SetMaxActiveStreams(0);
                _server.Local.CreateStream(2, true);
            });
        }

        [Fact]
        public void ServerCreatePushShouldFailOnRemoteEndpointWhenMaxAllowedStreamsExceeded()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                _server = new DefaultHttp2Connection(true, 0);
                _server.Remote.SetMaxActiveStreams(1);
                IHttp2Stream requestStream = _server.Remote.CreateStream(3, false);
                _server.Remote.ReservePushStream(2, requestStream);
            });
        }

        [Fact]
        public void ClientCreatePushShouldFailOnRemoteEndpointWhenMaxAllowedStreamsExceeded()
        {
            Assert.Throws<StreamException>(() =>
            {
                _client = new DefaultHttp2Connection(false, 0);
                _client.Remote.SetMaxActiveStreams(1);
                IHttp2Stream requestStream = _client.Remote.CreateStream(2, false);
                _client.Remote.ReservePushStream(4, requestStream);
            });
        }

        [Fact]
        public void ServerCreatePushShouldSucceedOnLocalEndpointWhenMaxAllowedStreamsExceeded()
        {
            _server = new DefaultHttp2Connection(true, 0);
            _server.Local.SetMaxActiveStreams(1);
            IHttp2Stream requestStream = _server.Remote.CreateStream(3, false);
            Assert.NotNull(_server.Local.ReservePushStream(2, requestStream));
        }

        [Fact]
        public void ReserveWithPushDisallowedShouldThrow()
        {
            Assert.Throws<Http2Exception>(() =>
            {
                IHttp2Stream stream = _server.Remote.CreateStream(3, true);
                _server.Remote.AllowPushTo(false);
                _server.Local.ReservePushStream(2, stream);
            });
        }

        [Fact]
        public void GoAwayReceivedShouldDisallowLocalCreation()
        {
            Assert.Throws<StreamException>(() =>
            {
                _server.GoAwayReceived(0, (Http2Error)1L, Unpooled.Empty);
                _server.Local.CreateStream(3, true);
            });
        }

        [Fact]
        public void GoAwayReceivedShouldAllowRemoteCreation()
        {
            _server.GoAwayReceived(0, (Http2Error)1L, Unpooled.Empty);
            _server.Remote.CreateStream(3, true);
        }

        [Fact]
        public void GoAwaySentShouldDisallowRemoteCreation()
        {
            Assert.Throws<StreamException>(() =>
            {
                _server.GoAwaySent(0, (Http2Error)1L, Unpooled.Empty);
                _server.Remote.CreateStream(2, true);
            });
        }

        [Fact]
        public void GoAwaySentShouldAllowLocalCreation()
        {
            _server.GoAwaySent(0, (Http2Error)1L, Unpooled.Empty);
            _server.Local.CreateStream(2, true);
        }

        [Fact]
        public void CloseShouldSucceed()
        {
            IHttp2Stream stream = _server.Remote.CreateStream(3, true);
            stream.Close();
            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.Equal(0, _server.NumActiveStreams);
        }

        [Fact]
        public void CloseLocalWhenOpenShouldSucceed()
        {
            IHttp2Stream stream = _server.Remote.CreateStream(3, false);
            stream.CloseLocalSide();
            Assert.Equal(Http2StreamState.HalfClosedLocal, stream.State);
            Assert.Equal(1, _server.NumActiveStreams);
        }

        [Fact]
        public void CloseRemoteWhenOpenShouldSucceed()
        {
            IHttp2Stream stream = _server.Remote.CreateStream(3, false);
            stream.CloseRemoteSide();
            Assert.Equal(Http2StreamState.HalfClosedRemote, stream.State);
            Assert.Equal(1, _server.NumActiveStreams);
        }

        [Fact]
        public void CloseOnlyOpenSideShouldClose()
        {
            IHttp2Stream stream = _server.Remote.CreateStream(3, true);
            stream.CloseLocalSide();
            Assert.Equal(Http2StreamState.Closed, stream.State);
            Assert.Equal(0, _server.NumActiveStreams);
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
            _clientListener
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            _clientListener
                .Setup(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            _clientListener
                .Setup(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            _clientListener
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            _clientListener
                .Setup(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            _clientListener
                .Setup(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            _clientListener
                .Setup(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()))
                .Callback<int, Http2Error, IByteBuffer>((id, err, buf) => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            _clientListener
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerExceptionThrower(calledArray, methodIndex));
            _clientListener2
                .Setup(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()))
                .Callback<IHttp2Stream>(s => ListenerVerifyCallAnswer(calledArray, methodIndex++));

            // Now we add clientListener2 and exercise all listener functionality
            try
            {
                _client.AddListener(_clientListener2.Object);
                IHttp2Stream stream = _client.Local.CreateStream(3, false);
                _clientListener.Verify(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()));
                _clientListener2.Verify(x => x.OnStreamAdded(It.IsAny<IHttp2Stream>()));
                _clientListener.Verify(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()));
                _clientListener2.Verify(x => x.OnStreamActive(It.IsAny<IHttp2Stream>()));

                IHttp2Stream reservedStream = _client.Remote.ReservePushStream(2, stream);
                _clientListener.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))), Times.Never());
                _clientListener2.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))), Times.Never());

                reservedStream.Open(false);
                _clientListener.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))));
                _clientListener2.Verify(x => x.OnStreamActive(It.Is<IHttp2Stream>(s => StreamEq(s, reservedStream))));

                stream.CloseLocalSide();
                _clientListener.Verify(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()));
                _clientListener2.Verify(x => x.OnStreamHalfClosed(It.IsAny<IHttp2Stream>()));

                stream.Close();
                _clientListener.Verify(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()));
                _clientListener2.Verify(x => x.OnStreamClosed(It.IsAny<IHttp2Stream>()));
                _clientListener.Verify(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()));
                _clientListener2.Verify(x => x.OnStreamRemoved(It.IsAny<IHttp2Stream>()));

                _client.GoAwaySent(_client.ConnectionStream.Id, Http2Error.InternalError, Unpooled.Empty);
                _clientListener.Verify(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));
                _clientListener2.Verify(x => x.OnGoAwaySent(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));

                _client.GoAwayReceived(_client.ConnectionStream.Id, Http2Error.InternalError, Unpooled.Empty);
                _clientListener.Verify(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));
                _clientListener2.Verify(x => x.OnGoAwayReceived(It.IsAny<int>(), It.IsAny<Http2Error>(), It.IsAny<IByteBuffer>()));
            }
            finally
            {
                _client.RemoveListener(_clientListener2.Object);
            }
        }

        void TestRemoveAllStreams()
        {
            var latch = new CountdownEvent(1);
            var promise = _group.GetNext().NewPromise();
            var closeFuture = _client.CloseAsync(promise);
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
