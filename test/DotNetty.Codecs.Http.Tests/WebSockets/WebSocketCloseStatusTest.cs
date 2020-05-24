namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class WebSocketCloseStatusTest
    {
        private static readonly List<WebSocketCloseStatus> s_validCodes;

        static WebSocketCloseStatusTest()
        {
            s_validCodes = new List<WebSocketCloseStatus>
            {
                WebSocketCloseStatus.NormalClosure,
                WebSocketCloseStatus.EndpointUnavailable,
                WebSocketCloseStatus.ProtocolError,
                WebSocketCloseStatus.InvalidMessageType,
                WebSocketCloseStatus.InvalidPayloadData,
                WebSocketCloseStatus.PolicyViolation,
                WebSocketCloseStatus.MessageTooBig,
                WebSocketCloseStatus.MandatoryExtension,
                WebSocketCloseStatus.InternalServerError,
                WebSocketCloseStatus.ServiceRestart,
                WebSocketCloseStatus.TryAgainLater,
                WebSocketCloseStatus.BadGateway
            };
        }

        [Fact]
        public void TestToString()
        {
            Assert.Equal("1000 Bye", WebSocketCloseStatus.NormalClosure.ToString());
        }

        [Fact]
        public void TestKnownStatuses()
        {
            Assert.Same(WebSocketCloseStatus.NormalClosure, WebSocketCloseStatus.ValueOf(1000));
            Assert.Same(WebSocketCloseStatus.EndpointUnavailable, WebSocketCloseStatus.ValueOf(1001));
            Assert.Same(WebSocketCloseStatus.ProtocolError, WebSocketCloseStatus.ValueOf(1002));
            Assert.Same(WebSocketCloseStatus.InvalidMessageType, WebSocketCloseStatus.ValueOf(1003));
            Assert.Same(WebSocketCloseStatus.InvalidPayloadData, WebSocketCloseStatus.ValueOf(1007));
            Assert.Same(WebSocketCloseStatus.PolicyViolation, WebSocketCloseStatus.ValueOf(1008));
            Assert.Same(WebSocketCloseStatus.MessageTooBig, WebSocketCloseStatus.ValueOf(1009));
            Assert.Same(WebSocketCloseStatus.MandatoryExtension, WebSocketCloseStatus.ValueOf(1010));
            Assert.Same(WebSocketCloseStatus.InternalServerError, WebSocketCloseStatus.ValueOf(1011));
            Assert.Same(WebSocketCloseStatus.ServiceRestart, WebSocketCloseStatus.ValueOf(1012));
            Assert.Same(WebSocketCloseStatus.TryAgainLater, WebSocketCloseStatus.ValueOf(1013));
            Assert.Same(WebSocketCloseStatus.BadGateway, WebSocketCloseStatus.ValueOf(1014));
        }

        [Fact]
        public void TestNaturalOrder()
        {
            Assert.True(WebSocketCloseStatus.ProtocolError.CompareTo(WebSocketCloseStatus.NormalClosure) > 0);
            Assert.True(WebSocketCloseStatus.ProtocolError.CompareTo(WebSocketCloseStatus.ValueOf(1001)) > 0);
            Assert.True(WebSocketCloseStatus.ProtocolError.CompareTo(WebSocketCloseStatus.ProtocolError) == 0);
            Assert.True(WebSocketCloseStatus.ProtocolError.CompareTo(WebSocketCloseStatus.ValueOf(1002)) == 0);
            Assert.True(WebSocketCloseStatus.ProtocolError.CompareTo(WebSocketCloseStatus.InvalidMessageType) < 0);
            Assert.True(WebSocketCloseStatus.ProtocolError.CompareTo(WebSocketCloseStatus.ValueOf(1007)) < 0);
        }

        [Fact]
        public void TestUserDefinedStatuses()
        {
            // Given, when
            WebSocketCloseStatus feedTimeot = new WebSocketCloseStatus(6033, (AsciiString)"Feed timed out");
            WebSocketCloseStatus untradablePrice = new WebSocketCloseStatus(6034, (AsciiString)"Untradable price");

            // Then
            Assert.NotSame(feedTimeot, WebSocketCloseStatus.ValueOf(6033));
            Assert.Equal(6033, feedTimeot.Code);
            Assert.Equal("Feed timed out", feedTimeot.ReasonText);

            Assert.NotSame(untradablePrice, WebSocketCloseStatus.ValueOf(6034));
            Assert.Equal(6034, untradablePrice.Code);
            Assert.Equal("Untradable price", untradablePrice.ReasonText);
        }

        [Fact]
        public void TestRfc6455CodeValidation()
        {
            var knownCodes = new List<int>
            {
                WebSocketCloseStatus.NormalClosure.Code,
                WebSocketCloseStatus.EndpointUnavailable.Code,
                WebSocketCloseStatus.ProtocolError.Code,
                WebSocketCloseStatus.InvalidMessageType.Code,
                WebSocketCloseStatus.InvalidPayloadData.Code,
                WebSocketCloseStatus.PolicyViolation.Code,
                WebSocketCloseStatus.MessageTooBig.Code,
                WebSocketCloseStatus.MandatoryExtension.Code,
                WebSocketCloseStatus.InternalServerError.Code,
                WebSocketCloseStatus.ServiceRestart.Code,
                WebSocketCloseStatus.TryAgainLater.Code,
                WebSocketCloseStatus.BadGateway.Code
            };

            SortedSet<int> invalidCodes = new SortedSet<int>();

            // When
            for (int statusCode = short.MinValue; statusCode < short.MaxValue; statusCode++)
            {
                if (!WebSocketCloseStatus.IsValidStatusCode(statusCode))
                {
                    invalidCodes.Add(statusCode);
                }
            }

            // Then
            Assert.Equal(0, invalidCodes.First());
            Assert.Equal(2999, invalidCodes.Last());
            Assert.Equal(3000 - s_validCodes.Count, invalidCodes.Count);

            invalidCodes.IntersectWith(knownCodes);
            Assert.Empty(invalidCodes);
        }
    }
}
