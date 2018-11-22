
namespace DotNetty.Codecs.Http2.Tests
{
    using Moq;
    using Xunit;

    public class WeightedFairQueueByteDistributorDependencyTreeTest : AbstractWeightedFairQueueByteDistributorDependencyTest
    {
        private const int leadersId = 3; // js, css
        private const int unblockedId = 5;
        private const int backgroundId = 7;
        private const int speculativeId = 9;
        private const int followersId = 11; // images
        private const short leadersWeight = 201;
        private const short unblockedWeight = 101;
        private const short backgroundWeight = 1;
        private const short speculativeWeight = 1;
        private const short followersWeight = 1;

        public WeightedFairQueueByteDistributorDependencyTreeTest()
        {
            this.writer = new Mock<IStreamByteDistributorWriter>();
            this.Setup(0);
        }

        private void Setup(int maxStateOnlySize)
        {
            this.connection = new DefaultHttp2Connection(false);
            this.distributor = new WeightedFairQueueByteDistributor(this.connection, maxStateOnlySize);

            // Assume we always write all the allocated bytes.
            this.writer.Setup(x => x.Write(It.IsAny<IHttp2Stream>(), It.IsAny<int>()))
                       .Callback<IHttp2Stream, int>((stream, numBytes) => this.WriteAnswer(stream, numBytes, false));
        }

        [Fact]
        public void ClosingStreamWithChildrenDoesNotCauseConcurrentModification()
        {
            // We create enough streams to wrap around the child array. We carefully craft the stream ids so that they hash
            // codes overlap with respect to the child collection. If the implementation is not careful this may lead to a
            // concurrent modification exception while promoting all children to the connection stream.
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            int numStreams = WeightedFairQueueByteDistributor.InitialChildrenMapSize - 1;
            for (int i = 0, streamId = 3; i < numStreams; ++i, streamId += WeightedFairQueueByteDistributor.InitialChildrenMapSize)
            {
                IHttp2Stream stream = connection.Local.CreateStream(streamId, false);
                this.SetPriority(stream.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            }
            Assert.Equal(WeightedFairQueueByteDistributor.InitialChildrenMapSize, connection.NumActiveStreams);
            streamA.Close();
            Assert.Equal(numStreams, connection.NumActiveStreams);
        }

        [Fact]
        public void CloseWhileIteratingDoesNotNPE()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(3, false);
            IHttp2Stream streamB = connection.Local.CreateStream(5, false);
            IHttp2Stream streamC = connection.Local.CreateStream(7, false);
            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            connection.ForEachActiveStream(stream =>
            {
                streamA.Close();
                this.SetPriority(streamB.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);
                return true;
            });
        }

        [Fact]
        public void LocalStreamCanDependUponIdleStream()
        {
            this.Setup(1);

            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            this.SetPriority(3, streamA.Id, Http2CodecUtil.MinWeight, true);
            Assert.True(distributor.IsChild(3, streamA.Id, Http2CodecUtil.MinWeight));
        }

        [Fact]
        public void RemoteStreamCanDependUponIdleStream()
        {
            this.Setup(1);

            IHttp2Stream streamA = connection.Remote.CreateStream(2, false);
            this.SetPriority(4, streamA.Id, Http2CodecUtil.MinWeight, true);
            Assert.True(distributor.IsChild(4, streamA.Id, Http2CodecUtil.MinWeight));
        }

        [Fact]
        public void PrioritizeShouldUseDefaults()
        {
            IHttp2Stream stream = connection.Local.CreateStream(1, false);
            Assert.True(distributor.IsChild(stream.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.Equal(0, distributor.NumChildren(stream.Id));
        }

        [Fact]
        public void ReprioritizeWithNoChangeShouldDoNothing()
        {
            IHttp2Stream stream = connection.Local.CreateStream(1, false);
            this.SetPriority(stream.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.True(distributor.IsChild(stream.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.Equal(0, distributor.NumChildren(stream.Id));
        }

        [Fact]
        public void StateOnlyPriorityShouldBePreservedWhenStreamsAreCreatedAndClosed()
        {
            this.Setup(3);

            short weight3 = Http2CodecUtil.MinWeight + 1;
            short weight5 = (short)(weight3 + 1);
            short weight7 = (short)(weight5 + 1);
            this.SetPriority(3, connection.ConnectionStream.Id, weight3, true);
            this.SetPriority(5, connection.ConnectionStream.Id, weight5, true);
            this.SetPriority(7, connection.ConnectionStream.Id, weight7, true);

            Assert.Equal(0, connection.NumActiveStreams);
            VerifyStateOnlyPriorityShouldBePreservedWhenStreamsAreCreated(weight3, weight5, weight7);

            // Now create stream objects and ensure the state and dependency tree is preserved.
            IHttp2Stream streamA = connection.Local.CreateStream(3, false);
            IHttp2Stream streamB = connection.Local.CreateStream(5, false);
            IHttp2Stream streamC = connection.Local.CreateStream(7, false);

            Assert.Equal(3, connection.NumActiveStreams);
            VerifyStateOnlyPriorityShouldBePreservedWhenStreamsAreCreated(weight3, weight5, weight7);

            // Close all the streams and ensure the state and dependency tree is preserved.
            streamA.Close();
            streamB.Close();
            streamC.Close();

            Assert.Equal(0, connection.NumActiveStreams);
            VerifyStateOnlyPriorityShouldBePreservedWhenStreamsAreCreated(weight3, weight5, weight7);
        }

        private void VerifyStateOnlyPriorityShouldBePreservedWhenStreamsAreCreated(short weight3, short weight5,
                                                                                   short weight7)
        {
            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(7, connection.ConnectionStream.Id, weight7));
            Assert.Equal(1, distributor.NumChildren(7));

            // Level 2
            Assert.True(distributor.IsChild(5, 7, weight5));
            Assert.Equal(1, distributor.NumChildren(5));

            // Level 3
            Assert.True(distributor.IsChild(3, 5, weight3));
            Assert.Equal(0, distributor.NumChildren(3));
        }

        [Fact]
        public void FireFoxQoSStreamsRemainAfterDataStreamsAreClosed()
        {
            // http://bitsup.blogspot.com/2015/01/http2-dependency-priorities-in-firefox.html
            this.Setup(5);

            this.SetPriority(leadersId, connection.ConnectionStream.Id, leadersWeight, false);
            this.SetPriority(unblockedId, connection.ConnectionStream.Id, unblockedWeight, false);
            this.SetPriority(backgroundId, connection.ConnectionStream.Id, backgroundWeight, false);
            this.SetPriority(speculativeId, backgroundId, speculativeWeight, false);
            this.SetPriority(followersId, leadersId, followersWeight, false);

            VerifyFireFoxQoSStreams();

            // Simulate a HTML request
            short htmlGetStreamWeight = 2;
            IHttp2Stream htmlGetStream = connection.Local.CreateStream(13, false);
            this.SetPriority(htmlGetStream.Id, followersId, htmlGetStreamWeight, false);
            IHttp2Stream favIconStream = connection.Local.CreateStream(15, false);
            this.SetPriority(favIconStream.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            IHttp2Stream cssStream = connection.Local.CreateStream(17, false);
            this.SetPriority(cssStream.Id, leadersId, Http2CodecUtil.DefaultPriorityWeight, false);
            IHttp2Stream jsStream = connection.Local.CreateStream(19, false);
            this.SetPriority(jsStream.Id, leadersId, Http2CodecUtil.DefaultPriorityWeight, false);
            IHttp2Stream imageStream = connection.Local.CreateStream(21, false);
            this.SetPriority(imageStream.Id, followersId, 1, false);

            // Level 0
            Assert.Equal(4, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(leadersId, connection.ConnectionStream.Id, leadersWeight));
            Assert.Equal(3, distributor.NumChildren(leadersId));

            Assert.True(distributor.IsChild(unblockedId, connection.ConnectionStream.Id, unblockedWeight));
            Assert.Equal(0, distributor.NumChildren(unblockedId));

            Assert.True(distributor.IsChild(backgroundId, connection.ConnectionStream.Id, backgroundWeight));
            Assert.Equal(1, distributor.NumChildren(backgroundId));

            Assert.True(distributor.IsChild(favIconStream.Id, connection.ConnectionStream.Id,
                                           Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(favIconStream.Id));

            // Level 2
            Assert.True(distributor.IsChild(followersId, leadersId, followersWeight));
            Assert.Equal(2, distributor.NumChildren(followersId));

            Assert.True(distributor.IsChild(speculativeId, backgroundId, speculativeWeight));
            Assert.Equal(0, distributor.NumChildren(speculativeId));

            Assert.True(distributor.IsChild(cssStream.Id, leadersId, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(cssStream.Id));

            Assert.True(distributor.IsChild(jsStream.Id, leadersId, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(jsStream.Id));

            // Level 3
            Assert.True(distributor.IsChild(htmlGetStream.Id, followersId, htmlGetStreamWeight));
            Assert.Equal(0, distributor.NumChildren(htmlGetStream.Id));

            Assert.True(distributor.IsChild(imageStream.Id, followersId, followersWeight));
            Assert.Equal(0, distributor.NumChildren(imageStream.Id));

            // Close all the data streams and ensure the "priority only streams" are retained in the dependency tree.
            htmlGetStream.Close();
            favIconStream.Close();
            cssStream.Close();
            jsStream.Close();
            imageStream.Close();

            VerifyFireFoxQoSStreams();
        }

        private void VerifyFireFoxQoSStreams()
        {
            // Level 0
            Assert.Equal(3, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(leadersId, connection.ConnectionStream.Id, leadersWeight));
            Assert.Equal(1, distributor.NumChildren(leadersId));

            Assert.True(distributor.IsChild(unblockedId, connection.ConnectionStream.Id, unblockedWeight));
            Assert.Equal(0, distributor.NumChildren(unblockedId));

            Assert.True(distributor.IsChild(backgroundId, connection.ConnectionStream.Id, backgroundWeight));
            Assert.Equal(1, distributor.NumChildren(backgroundId));

            // Level 2
            Assert.True(distributor.IsChild(followersId, leadersId, followersWeight));
            Assert.Equal(0, distributor.NumChildren(followersId));

            Assert.True(distributor.IsChild(speculativeId, backgroundId, speculativeWeight));
            Assert.Equal(0, distributor.NumChildren(speculativeId));
        }

        [Fact]
        public void LowestPrecedenceStateShouldBeDropped()
        {
            this.Setup(3);

            short weight3 = Http2CodecUtil.MaxWeight;
            short weight5 = (short)(weight3 - 1);
            short weight7 = (short)(weight5 - 1);
            short weight9 = (short)(weight7 - 1);
            this.SetPriority(3, connection.ConnectionStream.Id, weight3, true);
            this.SetPriority(5, connection.ConnectionStream.Id, weight5, true);
            this.SetPriority(7, connection.ConnectionStream.Id, weight7, false);
            Assert.Equal(0, connection.NumActiveStreams);
            VerifyLowestPrecedenceStateShouldBeDropped1(weight3, weight5, weight7);

            // Attempt to create a new item in the dependency tree but the maximum amount of "state only" streams is meet
            // so a stream will have to be dropped. Currently the new stream is the lowest "precedence" so it is dropped.
            this.SetPriority(9, 3, weight9, false);
            Assert.Equal(0, connection.NumActiveStreams);
            VerifyLowestPrecedenceStateShouldBeDropped1(weight3, weight5, weight7);

            // Set the priority for stream 9 such that its depth in the dependency tree is numerically lower than stream 3,
            // and therefore the dependency state associated with stream 3 will be dropped.
            this.SetPriority(9, 5, weight9, true);
            VerifyLowestPrecedenceStateShouldBeDropped2(weight9, weight5, weight7);

            // Test that stream which has been activated is lower priority than other streams that have not been activated.
            IHttp2Stream streamA = connection.Local.CreateStream(5, false);
            streamA.Close();
            VerifyLowestPrecedenceStateShouldBeDropped2(weight9, weight5, weight7);

            // Stream 3 (hasn't been opened) should result in stream 5 being dropped.
            this.SetPriority(3, 9, weight3, false);
            VerifyLowestPrecedenceStateShouldBeDropped3(weight3, weight7, weight9);

            // Stream 5's state has been discarded so we should be able to re-insert this state.
            this.SetPriority(5, 0, weight5, false);
            VerifyLowestPrecedenceStateShouldBeDropped4(weight5, weight7, weight9);

            // All streams are at the same level, so stream ID should be used to drop the numeric lowest valued stream.
            short weight11 = (short)(weight9 - 1);
            this.SetPriority(11, 0, weight11, false);
            VerifyLowestPrecedenceStateShouldBeDropped5(weight7, weight9, weight11);
        }

        private void VerifyLowestPrecedenceStateShouldBeDropped1(short weight3, short weight5, short weight7)
        {
            // Level 0
            Assert.Equal(2, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(7, connection.ConnectionStream.Id, weight7));
            Assert.Equal(0, distributor.NumChildren(7));

            Assert.True(distributor.IsChild(5, connection.ConnectionStream.Id, weight5));
            Assert.Equal(1, distributor.NumChildren(5));

            // Level 2
            Assert.True(distributor.IsChild(3, 5, weight3));
            Assert.Equal(0, distributor.NumChildren(3));
        }

        private void VerifyLowestPrecedenceStateShouldBeDropped2(short weight9, short weight5, short weight7)
        {
            // Level 0
            Assert.Equal(2, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(7, connection.ConnectionStream.Id, weight7));
            Assert.Equal(0, distributor.NumChildren(7));

            Assert.True(distributor.IsChild(5, connection.ConnectionStream.Id, weight5));
            Assert.Equal(1, distributor.NumChildren(5));

            // Level 2
            Assert.True(distributor.IsChild(9, 5, weight9));
            Assert.Equal(0, distributor.NumChildren(9));
        }

        private void VerifyLowestPrecedenceStateShouldBeDropped3(short weight3, short weight7, short weight9)
        {
            // Level 0
            Assert.Equal(2, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(7, connection.ConnectionStream.Id, weight7));
            Assert.Equal(0, distributor.NumChildren(7));

            Assert.True(distributor.IsChild(9, connection.ConnectionStream.Id, weight9));
            Assert.Equal(1, distributor.NumChildren(9));

            // Level 2
            Assert.True(distributor.IsChild(3, 9, weight3));
            Assert.Equal(0, distributor.NumChildren(3));
        }

        private void VerifyLowestPrecedenceStateShouldBeDropped4(short weight5, short weight7, short weight9)
        {
            // Level 0
            Assert.Equal(3, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(5, connection.ConnectionStream.Id, weight5));
            Assert.Equal(0, distributor.NumChildren(5));

            Assert.True(distributor.IsChild(7, connection.ConnectionStream.Id, weight7));
            Assert.Equal(0, distributor.NumChildren(7));

            Assert.True(distributor.IsChild(9, connection.ConnectionStream.Id, weight9));
            Assert.Equal(0, distributor.NumChildren(9));
        }

        private void VerifyLowestPrecedenceStateShouldBeDropped5(short weight7, short weight9, short weight11)
        {
            // Level 0
            Assert.Equal(3, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(11, connection.ConnectionStream.Id, weight11));
            Assert.Equal(0, distributor.NumChildren(11));

            Assert.True(distributor.IsChild(7, connection.ConnectionStream.Id, weight7));
            Assert.Equal(0, distributor.NumChildren(7));

            Assert.True(distributor.IsChild(9, connection.ConnectionStream.Id, weight9));
            Assert.Equal(0, distributor.NumChildren(9));
        }

        [Fact]
        public void PriorityOnlyStreamsArePreservedWhenReservedStreamsAreClosed()
        {
            this.Setup(1);

            short weight3 = Http2CodecUtil.MinWeight;
            this.SetPriority(3, connection.ConnectionStream.Id, weight3, true);

            IHttp2Stream streamA = connection.Local.CreateStream(5, false);
            IHttp2Stream streamB = connection.Remote.ReservePushStream(4, streamA);

            // Level 0
            Assert.Equal(3, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(3, connection.ConnectionStream.Id, weight3));
            Assert.Equal(0, distributor.NumChildren(3));

            Assert.True(distributor.IsChild(streamA.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamA.Id));

            Assert.True(distributor.IsChild(streamB.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            // Close both streams.
            streamB.Close();
            streamA.Close();

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(3, connection.ConnectionStream.Id, weight3));
            Assert.Equal(0, distributor.NumChildren(3));
        }

        [Fact]
        public void InsertExclusiveShouldAddNewLevel()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.Equal(4, connection.NumActiveStreams);

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamA.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamA.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamD.Id));

            // Level 3
            Assert.True(distributor.IsChild(streamB.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            Assert.True(distributor.IsChild(streamC.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamC.Id));
        }

        [Fact]
        public void ExistingChildMadeExclusiveShouldNotCreateTreeCycle()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Stream C is already dependent on Stream A, but now make that an exclusive dependency
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.Equal(4, connection.NumActiveStreams);

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamA.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamA.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamC.Id));

            // Level 3
            Assert.True(distributor.IsChild(streamB.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            Assert.True(distributor.IsChild(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamD.Id));
        }

        [Fact]
        public void NewExclusiveChildShouldUpdateOldParentCorrectly()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);
            IHttp2Stream streamE = connection.Local.CreateStream(9, false);
            IHttp2Stream streamF = connection.Local.CreateStream(11, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamF.Id, streamE.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // F is now going to be exclusively dependent on A, after this we should check that stream E
            // prioritizableForTree is not over decremented.
            this.SetPriority(streamF.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.Equal(6, connection.NumActiveStreams);

            // Level 0
            Assert.Equal(2, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamE.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamE.Id));

            Assert.True(distributor.IsChild(streamA.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamA.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamF.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamF.Id));

            // Level 3
            Assert.True(distributor.IsChild(streamB.Id, streamF.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            Assert.True(distributor.IsChild(streamC.Id, streamF.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamC.Id));

            // Level 4
            Assert.True(distributor.IsChild(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamD.Id));
        }

        [Fact]
        public void WeightChangeWithNoTreeChangeShouldBeRespected()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.Equal(4, connection.NumActiveStreams);

            short newWeight = (short)(Http2CodecUtil.DefaultPriorityWeight + 1);
            this.SetPriority(streamD.Id, streamA.Id, newWeight, false);

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamA.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamA.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamD.Id, streamA.Id, newWeight));
            Assert.Equal(2, distributor.NumChildren(streamD.Id));

            // Level 3
            Assert.True(distributor.IsChild(streamB.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            Assert.True(distributor.IsChild(streamC.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamC.Id));
        }

        [Fact]
        public void SameNodeDependentShouldNotStackOverflowNorChangePrioritizableForTree()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            bool[] exclusives = { true, false };
            short[] weights = { Http2CodecUtil.DefaultPriorityWeight, 100, 200, Http2CodecUtil.DefaultPriorityWeight };

            Assert.Equal(4, connection.NumActiveStreams);

            // The goal is to call this.SetPriority with the same parent and vary the parameters
            // we were at one point adding a circular depends to the tree and then throwing
            // a StackOverflow due to infinite recursive operation.
            foreach (short weight in weights)
            {
                foreach (bool exclusive in exclusives)
                {
                    this.SetPriority(streamD.Id, streamA.Id, weight, exclusive);

                    Assert.Equal(0, distributor.NumChildren(streamB.Id));
                    Assert.Equal(0, distributor.NumChildren(streamC.Id));
                    Assert.Equal(1, distributor.NumChildren(streamA.Id));
                    Assert.Equal(2, distributor.NumChildren(streamD.Id));
                    Assert.False(distributor.IsChild(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
                    Assert.False(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
                    Assert.True(distributor.IsChild(streamB.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
                    Assert.True(distributor.IsChild(streamC.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
                    Assert.True(distributor.IsChild(streamD.Id, streamA.Id, weight));
                }
            }
        }

        [Fact]
        public void MultipleCircularDependencyShouldUpdatePrioritizable()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            Assert.Equal(4, connection.NumActiveStreams);

            // Bring B to the root
            this.SetPriority(streamA.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            // Move all streams to be children of B
            this.SetPriority(streamC.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Move A back to the root
            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            // Move all streams to be children of A
            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamA.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(3, distributor.NumChildren(streamA.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            Assert.True(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamC.Id));

            Assert.True(distributor.IsChild(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamD.Id));
        }

        [Fact]
        public void RemoveWithPrioritizableDependentsShouldNotRestructureTree()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Default removal policy will cause it to be removed immediately.
            streamB.Close();

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamA.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamA.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamC.Id));

            Assert.True(distributor.IsChild(streamD.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamD.Id));
        }

        [Fact]
        public void CloseWithNoPrioritizableDependentsShouldRestructureTree()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);
            IHttp2Stream streamE = connection.Local.CreateStream(9, false);
            IHttp2Stream streamF = connection.Local.CreateStream(11, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamF.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Close internal nodes, leave 1 leaf node open, the only remaining stream is the one that is not closed (E).
            streamA.Close();
            streamB.Close();
            streamC.Close();
            streamD.Close();
            streamF.Close();

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamE.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamE.Id));
        }

        [Fact]
        public void PriorityChangeWithNoPrioritizableDependentsShouldRestructureTree()
        {
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);
            IHttp2Stream streamE = connection.Local.CreateStream(9, false);
            IHttp2Stream streamF = connection.Local.CreateStream(11, false);

            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamC.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamD.Id, streamB.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamF.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            this.SetPriority(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Leave leaf nodes open (E & F)
            streamA.Close();
            streamB.Close();
            streamC.Close();
            streamD.Close();

            // Move F to depend on C, even though C is closed.
            this.SetPriority(streamF.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Level 0
            Assert.Equal(2, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamE.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamE.Id));

            Assert.True(distributor.IsChild(streamF.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamF.Id));
        }

        [Fact]
        public void CircularDependencyShouldRestructureTree()
        {
            // Using example from https://tools.ietf.org/html/rfc7540#section-5.3.3
            // Initialize all the nodes
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);
            IHttp2Stream streamE = connection.Local.CreateStream(9, false);
            IHttp2Stream streamF = connection.Local.CreateStream(11, false);

            Assert.Equal(6, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.Equal(0, distributor.NumChildren(streamA.Id));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));
            Assert.Equal(0, distributor.NumChildren(streamC.Id));
            Assert.Equal(0, distributor.NumChildren(streamD.Id));
            Assert.Equal(0, distributor.NumChildren(streamE.Id));
            Assert.Equal(0, distributor.NumChildren(streamF.Id));

            // Build the tree
            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(5, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamA.Id));

            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(4, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamA.Id));

            this.SetPriority(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(3, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamC.Id));

            this.SetPriority(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(2, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamC.Id));

            this.SetPriority(streamF.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamF.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamD.Id));

            Assert.Equal(6, connection.NumActiveStreams);

            // Non-exclusive re-prioritization of a->d.
            this.SetPriority(streamA.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight, false);

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamD.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamD.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamF.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamF.Id));

            Assert.True(distributor.IsChild(streamA.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamA.Id));

            // Level 3
            Assert.True(distributor.IsChild(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            Assert.True(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamC.Id));

            // Level 4
            Assert.True(distributor.IsChild(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamE.Id));
        }

        [Fact]
        public void CircularDependencyWithExclusiveShouldRestructureTree()
        {
            // Using example from https://tools.ietf.org/html/rfc7540#section-5.3.3
            // Initialize all the nodes
            IHttp2Stream streamA = connection.Local.CreateStream(1, false);
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);
            IHttp2Stream streamC = connection.Local.CreateStream(5, false);
            IHttp2Stream streamD = connection.Local.CreateStream(7, false);
            IHttp2Stream streamE = connection.Local.CreateStream(9, false);
            IHttp2Stream streamF = connection.Local.CreateStream(11, false);

            Assert.Equal(6, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.Equal(0, distributor.NumChildren(streamA.Id));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));
            Assert.Equal(0, distributor.NumChildren(streamC.Id));
            Assert.Equal(0, distributor.NumChildren(streamD.Id));
            Assert.Equal(0, distributor.NumChildren(streamE.Id));
            Assert.Equal(0, distributor.NumChildren(streamF.Id));

            // Build the tree
            this.SetPriority(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(5, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamA.Id));

            this.SetPriority(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(4, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamA.Id));

            this.SetPriority(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(3, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamD.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamC.Id));

            this.SetPriority(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(2, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(2, distributor.NumChildren(streamC.Id));

            this.SetPriority(streamF.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight, false);
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.True(distributor.IsChild(streamF.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamD.Id));

            Assert.Equal(6, connection.NumActiveStreams);

            // Exclusive re-prioritization of a->d.
            this.SetPriority(streamA.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight, true);

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamD.Id, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamD.Id));

            // Level 2
            Assert.True(distributor.IsChild(streamA.Id, streamD.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(3, distributor.NumChildren(streamA.Id));

            // Level 3
            Assert.True(distributor.IsChild(streamB.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            Assert.True(distributor.IsChild(streamF.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamF.Id));

            Assert.True(distributor.IsChild(streamC.Id, streamA.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamC.Id));

            // Level 4;
            Assert.True(distributor.IsChild(streamE.Id, streamC.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamE.Id));
        }

        // Unknown parent streams can come about in two ways:
        //  1. Because the stream is old and its state was purged
        //  2. This is the first reference to the stream, as implied at least by RFC7540§5.3.1:
        //    > A dependency on a stream that is not currently in the tree — such as a stream in the
        //    > "idle" state — results in that stream being given a default priority
        [Fact]
        public void UnknownParentShouldBeCreatedUnderConnection()
        {
            this.Setup(5);

            // Purposefully avoid creating streamA's IHttp2Stream so that is it completely unknown.
            // It shouldn't matter whether the ID is before or after streamB.Id
            int streamAId = 1;
            IHttp2Stream streamB = connection.Local.CreateStream(3, false);

            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));

            // Build the tree
            this.SetPriority(streamB.Id, streamAId, Http2CodecUtil.DefaultPriorityWeight, false);

            Assert.Equal(1, connection.NumActiveStreams);

            // Level 0
            Assert.Equal(1, distributor.NumChildren(connection.ConnectionStream.Id));

            // Level 1
            Assert.True(distributor.IsChild(streamAId, connection.ConnectionStream.Id, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(1, distributor.NumChildren(streamAId));

            // Level 2
            Assert.True(distributor.IsChild(streamB.Id, streamAId, Http2CodecUtil.DefaultPriorityWeight));
            Assert.Equal(0, distributor.NumChildren(streamB.Id));
        }
    }
}
