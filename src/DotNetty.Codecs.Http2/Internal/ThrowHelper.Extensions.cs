using System;
using System.Runtime.CompilerServices;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using DotNetty.Codecs.Http;
using DotNetty.Transport.Channels;

namespace DotNetty.Codecs.Http2
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        cause,
        name,
        value,
        ouput,
        initialCapacity,
        maxHeaderListSize,
        stream,
        content,
        headers,
        settings,
        hpackDecoder,
        sensitivityDetector,
        hpackEncoder,
        listener,
        writer,
        encoder,
        decoder,
        flowController,
        writeAction,
        visitFunc,
        key,
        lifecycleManager,
        connection,
        frameReader,
        requestVerifier,
        logger,
        reader,
        initialSettings,
        frameWriter,
        connectionHandler,
        upgradeToHandler,
        httpServerCodec,
        httpServerUpgradeHandler,
        http2ServerHandler,
        promise,
        childHandler,
        channel,
        option,
        handler,
        ctx,
        streamByteDistributor,
        maxStateOnlySize,
        maxReservedStreams,
        frame,
        maxContentLength,
        allocationQuantum,
        minAllocationChunk,
        windowSizeIncrement,
        StreamDependency,
        StreamID,
        LastStreamId,
        PromisedStreamId,
        padding,
    }

    #endregion

    #region -- ExceptionResource --

    /// <summary>The convention for this enum is using the resource name as the enum name</summary>
    internal enum ExceptionResource
    {
    }

    #endregion

    partial class ThrowHelper
    {
        #region -- Exception --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_ShouldNotReachHere()
        {
            throw GetException();

            static Exception GetException()
            {
                return new Exception("should not reach here");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_ShouldNotReachHere(byte state)
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("should not reach here state: " + state);
            }
        }

        #endregion

        #region -- ArgumentException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_FirstFrameMustBeHeadersFrame(IHttp2StreamFrame frame)
        {
            return new ArgumentException("The first frame must be a headers frame. Was: " + frame.Name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_MsgMustBeStreamFrame(object msg)
        {
            return new ArgumentException(
                "Message must be an " + StringUtil.SimpleClassName<IHttp2StreamFrame>() +
                ": " + msg.ToString());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ArgumentException GetArgumentException_PaddingIsTooLarge(int padding, int maxFrameSize)
        {
            return new ArgumentException("Padding [" + padding + "] is too large for max frame size [" + maxFrameSize + "]");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidArraySize()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("pseudoHeaders and otherHeaders must be arrays of [name, value] pairs");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidContendAndPadding()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("content + padding must be <= int.MaxValue");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ExtraStreamIdsNonNegative()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("extraStreamIds must be non-negative");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NotEnoughData(IByteBuffer input)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("decode only works with an entire header block! " + input);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Positive(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: must be > 0");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Positive(int value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: > 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Positive(long value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: > 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PositiveOrZero(ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: must be >= 0");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PositiveOrZero(int value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PositiveOrZero(long value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidCapacity(long capacity)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("capacity is invalid: " + capacity);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidPadding(int padding)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"Invalid padding '{padding}'. Padding must be between 0 and {Http2CodecUtil.MaxPadding} (inclusive).");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidHeaderTableSize(long value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Setting HEADER_TABLE_SIZE is invalid: " + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidEnablePush(long value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Setting ENABLE_PUSH is invalid: " + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidConcurrentStreams(long value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Setting Http2CodecUtil.MAX_CONCURRENT_STREAMS is invalid: " + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidInitialWindowSize(long value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Setting INITIAL_WINDOW_SIZE is invalid: " + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidMaxFrameSize(long value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Setting Http2CodecUtil.MAX_FRAME_SIZE is invalid: " + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidHeaderListSize(long value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Setting Http2CodecUtil.MAX_HEADER_LIST_SIZE is invalid: " + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PseudoHeadersValueIsNull(int i)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("pseudoHeaders value at index " + i + " is null");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_OtherHeadersValueIsNull(int i)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("otherHeaders value at index " + i + " is null");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_OtherHeadersNameIsPseudoHeader(int i)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("otherHeaders name at index " + i + " is a pseudo header that appears after non-pseudo headers.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_SchemeMustBeSpecified()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException(":scheme must be specified. " +
                        "see https://tools.ietf.org/html/rfc7540#section-8.1.2.3");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Http2AuthorityIsEmpty(string authority)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("authority: " + authority);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CookieValueIsOfUnexpectedFormat(AsciiString value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("cookie value is of unexpected format: " + value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException_MethodHeader()
        {
            throw GetException();

            static ArgumentNullException GetException()
            {
                return new ArgumentNullException("method header cannot be null in conversion to HTTP/1.x");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException_PathHeader()
        {
            throw GetException();

            static ArgumentNullException GetException()
            {
                return new ArgumentNullException("path header cannot be null in conversion to HTTP/1.x");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MustOnlyOne()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("There must be 1 and only 1 " + Http2CodecUtil.HttpUpgradeSettingsHeader + " header.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ServerCodecsDonotUseAnExtraHandlerForTheUpgradeStream()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Server codecs don't use an extra handler for the upgrade stream");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_TheHandlerMustBeSharable()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("The handler must be Sharable");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_StreamMustNotBeSetOnTheFrame(IHttp2StreamFrame frame)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException(
                        "Stream " + frame.Stream + " must not be set on the frame: " + frame.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_LastStreamIdMustNotBeSetOnGoAwayFrame()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Last stream id must not be set on GOAWAY frame");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_EncoderAndDecoderDonotShareTheSameConnObject()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Encoder and Decoder do not share the same connection object");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_GracefulShutdownTimeout(TimeSpan value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("gracefulShutdownTimeoutMillis: " + value +
                        " (expected: -1 for indefinite or >= 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_RequireHttp2FrameCodec()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException(StringUtil.SimpleClassName<Http2FrameCodec>()
                        + " was not found in the channel pipeline.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_DecompressedBytesMustNotBeNegative(int decompressedBytes)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("decompressedBytes must not be negative: " + decompressedBytes);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidInitialWindowSize(int newWindowSize)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Invalid initial window size: " + newWindowSize);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidRatio(float ratio)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Invalid ratio: " + ratio);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NumBytesMustNotBeNegative()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("numBytes must not be negative");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidPingFramePayloadLength()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Opaque data must be " + Http2CodecUtil.PingFramePayloadLength + " bytes");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidErrorCode(long errorCode)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Invalid errorCode: " + errorCode);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidWeight(short weight)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Invalid weight: " + weight);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_RequireStream(IHttp2Connection connection, int streamId)
        {
            throw GetException();
            ArgumentException GetException()
            {
                string message;
                if (connection.StreamMayHaveExisted(streamId))
                {
                    message = "Stream no longer exists: " + streamId;
                }
                else
                {
                    message = "Stream does not exist: " + streamId;
                }
                return new ArgumentException(message);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_UsingAKeyThatWasNotCreatedByThisConnection()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Using a key that was not created by this connection");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ServersDoNotAllowPush()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("Servers do not allow push");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidCompressionLevel(int compressionLevel)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("compressionLevel: " + compressionLevel + " (expected: 0-9)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidWindowBits(int windowBits)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("windowBits: " + windowBits + " (expected: 9-15)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidMemLevel(int memLevel)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("memLevel: " + memLevel + " (expected: 1-9)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_EncoderEnforceMaxConcurrentStreamsNotSupportedForServer(bool encoderEnforceMaxConcurrentStreams)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException(
                        "encoderEnforceMaxConcurrentStreams: " + encoderEnforceMaxConcurrentStreams +
                        " not supported for server");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_DifferentConnections()
        {
            throw GetException();

            static ArgumentException GetException()
            {
                return new ArgumentException("The specified encoder and decoder have different connections.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidGracefulShutdownTimeout(TimeSpan value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("gracefulShutdownTimeoutMillis: " + value.TotalMilliseconds +
                        " (expected: -1 for indefinite or >= 0)");
            }
        }

        #endregion

        #region -- InvalidOperationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Inval1idHuffmanCode()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("invalid Huffman code: prefix not unique");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EventExecutorMustBeEventLoopOfChannel()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException("EventExecutor must be EventLoop of Channel");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_StreamObjectRequiredForIdentifier(int streamId)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Stream object required for identifier: " + streamId);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CloseListenerThrewAnUnexpectedException(Exception e)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Close listener threw an unexpected exception", e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_InvalidWindowStateWhenWritingFrame(Http2Exception e)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Invalid window state when writing frame: " + e.Message, e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_RequireHttp2FrameCodec()
        {
            throw GetException();

            static InvalidOperationException GetException()
            {
                return new InvalidOperationException(StringUtil.SimpleClassName<Http2FrameCodec>() + " not found." +
                        " Has the handler been added to a pipeline?");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_StreamInUnexpectedState(IHttp2Stream stream)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Stream " + stream.Id + " in unexpected state " + stream.State);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_StreamSentTooManyHeadersEOS(int streamId, bool endOfStream)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Stream " + streamId + " sent too many headers EOS: " + endOfStream);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_FailedToCreateInboundHttp2ToHttpAdapter(Exception t)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("failed to create a new InboundHttp2ToHttpAdapter", t);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_EnforceConstraint(string methodName, string rejectorName)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException(
                        methodName + "() cannot be called because " + rejectorName + "() has been called already.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_FailedToBuildHttp2ConnectionHandler(Exception t)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("failed to build a Http2ConnectionHandler", t);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidOperationException GetInvalidOperationException_StreamNoLongerExists(int streamId, Exception cause)
        {
            return new InvalidOperationException("Stream no longer exists: " + streamId, cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidOperationException GetInvalidOperationException_MustBeInTheChannelPipelineOfChannel(IChannel channel)
        {
            return new InvalidOperationException(StringUtil.SimpleClassName<Http2MultiplexCodec>() +
                    " must be in the ChannelPipeline of Channel " + channel);
        }

        #endregion

        #region -- Http2Exception --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_EOSDecoded()
        {
            throw HpackHuffmanDecoder.EOSDecoded;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_InvalidPadding()
        {
            throw HpackHuffmanDecoder.InvalidPadding;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static long ThrowHttp2Exception_DecodeULE128Decompression()
        {
            throw HpackDecoder.DecodeULE128DecompressionException;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_DecodeULE128ToLongDecompression()
        {
            throw HpackDecoder.DecodeULE128ToLongDecompressionException;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_DecodeULE128ToIntDecompression()
        {
            throw HpackDecoder.DecodeULE128ToIntDecompressionException;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_DecodeIllegalIndexValue()
        {
            throw HpackDecoder.DecodeIllegalIndexValue;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_IndexHeaderIllegalIndexValue()
        {
            throw HpackDecoder.IndexHeaderIllegalIndexValue;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_ReadNameIllegalIndexValue()
        {
            throw HpackDecoder.ReadNameIllegalIndexValue;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_InvalidMaxDynamicTableSize()
        {
            throw HpackDecoder.InvalidMaxDynamicTableSize;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2Exception_MaxDynamicTableSizeChangeRequired()
        {
            throw HpackDecoder.MaxDynamicTableSizeChangeRequired;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Http2Exception GetConnectionError_StreamDoesNotExist(int streamId)
        {
            return Http2Exception.ConnectionError(Http2Error.ProtocolError, "Stream does not exist {0}", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Http2Exception GetConnectionError_ErrorFlushing(Exception cause)
        {
            return Http2Exception.ConnectionError(Http2Error.InternalError, cause, "Error flushing");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Http2Exception GetConnectionError_ErrorDecodeSizeError(Exception cause2)
        {
            return Http2Exception.ConnectionError(Http2Error.InternalError, cause2, "Error DecodeSizeError");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_SetMaxHeaderListSize(long maxHeaderListSize)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Header List Size must be >= {0} and <= {1} but was {2}",
                    Http2CodecUtil.MinHeaderListSize, Http2CodecUtil.MaxHeaderListSize, maxHeaderListSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_SetMaxHeaderTableSize(long maxHeaderTableSize)
        {
            throw Http2Exception.ConnectionError(
                Http2Error.ProtocolError,
                "Header Table Size must be >= {0} and <= {1} but was {2}",
                Http2CodecUtil.MinHeaderTableSize,
                Http2CodecUtil.MaxHeaderTableSize,
                maxHeaderTableSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_IncompleteHeaderBlockFragment()
        {
            throw Http2Exception.ConnectionError(Http2Error.CompressionError, "Incomplete header block fragment.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ByteDistributionWriteError(Exception t)
        {
            throw Http2Exception.ConnectionError(Http2Error.InternalError, t, "byte distribution write error");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_PushPromiseFrameReceivedForPreExistingStreamId(int promisedStreamId)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError,
                    "Push Promise Frame received for pre-existing stream id {0}", promisedStreamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ContentLengthExceededMax(int maxContentLength, int streamId)
        {
            throw Http2Exception.ConnectionError(Http2Error.InternalError,
                    "Content length exceeded max of {0} for stream id {1}", maxContentLength, streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_DataFrameReceivedForUnknownStream(int streamId)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Data Frame received for unknown stream id {0}", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_InvalidHttp2StatusCode(int code)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Invalid HTTP/2 status code '{0}'", code);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_UnrecognizedHttpStatusCode(Exception t, ICharSequence status)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, t,
                                "Unrecognized HTTP status code {0} encountered in translation to HTTP/1.x", status);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ClientIsMisconfiguredForUpgradeRequests()
        {
            throw Http2Exception.ConnectionError(Http2Error.InternalError, "Client is misconfigured for upgrade requests");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_FirstReceivedFrameWasNotSettings(IByteBuffer input)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError,
                "First received frame was not SETTINGS. Hex dump for first 5 bytes: {0}",
                ByteBufferUtil.HexDump(input, input.ReaderIndex, 5));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_Http2ClientPrefaceStringMissingOrCorrupt(IByteBuffer input, int readableBytes)
        {
            string receivedBytes = ByteBufferUtil.HexDump(input, input.ReaderIndex,
                Math.Min(input.ReadableBytes, readableBytes));
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "HTTP/2 client preface string missing or corrupt. " +
                                                  "Hex dump for received bytes: {0}", receivedBytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_UnexpectedHttp1Request(IByteBuffer input, int http1Index)
        {
            string chunk = input.ToString(input.ReaderIndex, http1Index - input.ReaderIndex, Encoding.ASCII);
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Unexpected HTTP/1.x request: {0}", chunk);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_HttpUpgradeRequested(bool isServer)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError,
                isServer ? "Client-side HTTP upgrade requested for a server" : "Server-side HTTP upgrade requested for a client");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_HttpUpgradeMustOccurAfterPrefaceWasSent()
        {
            throw Http2Exception.ConnectionError(Http2Error.InternalError, "HTTP upgrade must occur after preface was sent");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_HttpUpgradeMustOccurBeforeHttp2PrefaceIsReceived()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "HTTP upgrade must occur before HTTP/2 preface is received");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_AttemptingToReturnTooManyBytesForStream(int streamId, Exception t)
        {
            throw Http2Exception.ConnectionError(
                Http2Error.InternalError,
                t,
                "Attempting to return too many bytes for stream {0}",
                streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_FailedEncodingHeadersBlock(Exception t)
        {
            throw Http2Exception.ConnectionError(Http2Error.CompressionError, t, "Failed encoding headers block: {0}", t.Message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IHttp2Headers ThrowConnectionError_FailedEncodingHe1adersBlock(Exception e)
        {
            throw Http2Exception.ConnectionError(Http2Error.CompressionError, e, e.Message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_HeaderListSizeGoAwayNonNegative(long goAwayMax, long max)
        {
            throw Http2Exception.ConnectionError(Http2Error.InternalError,
                "Header List Size GO_AWAY {0} must be non-negative and >= {1}", goAwayMax, max);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_InvalidHeaderName(ICharSequence name)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "invalid header name [{0}]", name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_InvalidHeaderName(ICharSequence name, Exception t)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, t, "unexpected error. invalid header name [{0}]", name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_EmptyHeadersAreNotAllowed(ICharSequence name)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "empty headers are not allowed [{0}]", name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_InvalidMaxFrameSizeSpecifiedInSentSettings(int max)
        {
            throw Http2Exception.ConnectionError(Http2Error.FrameSizeError, "Invalid MAX_FRAME_SIZE specified in sent settings: {0}", max);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_StreamIdPositiveOrZero(int streamId)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "{0} must be >= 0", "Stream ID");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_FrameTypeMustBeAssociatedWithAStream(Http2FrameTypes frameType)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Frame of type {0} must be associated with a stream.", frameType);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_TotalPayloadLengthExceedsMaxFrameLength(int payloadLength)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Total payload length {0} exceeds max frame length.", payloadLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ReceivedFrameTypeWhileProcessingHeadersOnStream(Http2FrameTypes frameType, int streamId)
        {
            throw Http2Exception.ConnectionError(
                Http2Error.ProtocolError,
                "Received frame of type {0} while processing headers on stream {1}.",
                frameType,
                streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_FramePayloadTooSmallForPadding()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Frame payload too small for padding.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ContinuationStreamIDDoesNotMatchPendingHeaders(int expectedStreamId, int streamId)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Continuation stream ID does not match pending headers. " + "Expected {0}, but received {1}.", expectedStreamId, streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ReceivedFrameButNotCurrentlyProcessingHeaders(Http2FrameTypes frameType)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Received {0} frame but not currently processing headers.", frameType);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_InvalidFrameLength(int payloadLength)
        {
            throw Http2Exception.ConnectionError(Http2Error.FrameSizeError, "Invalid frame length {0}.", payloadLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_FrameLengthTooSmall(int payloadLength)
        {
            throw Http2Exception.ConnectionError(Http2Error.FrameSizeError, "Frame length {0} too small.", payloadLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_AckSettingsFrameMustHaveAnEmptyPayload()
        {
            throw Http2Exception.ConnectionError(Http2Error.FrameSizeError, "Ack settings frame must have an empty payload.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_FrameLengthExceedsMaximum(int payloadLength, int maxFrameSize)
        {
            throw Http2Exception.ConnectionError(Http2Error.FrameSizeError, "Frame length: {0} exceeds maximum: {1}", payloadLength, maxFrameSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_FrameLengthIncorrectSizeForPing(int payloadLength)
        {
            throw Http2Exception.ConnectionError(
                Http2Error.FrameSizeError,
                "Frame length {0} incorrect size for ping.",
                payloadLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_AStreamIDMustBeZero()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "A stream ID must be zero.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_SendingPushPromiseAfterGoAwayReceived()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Sending PUSH_PROMISE after GO_AWAY received.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ServerSendingSettintsFrameWithEnablePushSpecified()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Server sending SETTINGS frame with ENABLE_PUSH specified");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ClientReceivedAValueOfEnablePushSpecifiedToOtherThan0()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError,
                "Client received a value of ENABLE_PUSH specified to other than 0");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ReceivedNonSettingsAsFirstFrame()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Received Non-SETTINGS as first frame.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_StreamDoesNotExist(int streamId)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Stream {0} does not exist", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_RstStreamReceivedForIdleStream(int streamId)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "RST_STREAM received for IDLE stream {0}", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_StreamInUnexpectedStateForReceivingPushPromise(IHttp2Stream parentStream)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError,
                "Stream {0} in unexpected state for receiving push promise: {1}",
                parentStream.Id, parentStream.State);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_StreamInUnexpectedState(IHttp2Stream stream)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Stream {0} in unexpected state: {1}",
                stream.Id, stream.State);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_AClientCannotPush()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "A client cannot push.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError(Http2Error error, Exception e)
        {
            throw Http2Exception.ConnectionError(error, e, e.Message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_AttemptedToCreateStreamAfterConnectionWasClosed(int streamId)
        {
            throw Http2Exception.ConnectionError(
                Http2Error.InternalError,
                "Attempted to create stream id {0} after connection was closed",
                streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_StreamIDsAreExhaustedForThisEndpoint()
        {
            throw Http2Exception.ConnectionError(Http2Error.RefusedStream, "Stream IDs are exhausted for this endpoint.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_RequestStreamIsNotCorrectForConnection(int streamId, bool isServer)
        {
            throw Http2Exception.ConnectionError(
                Http2Error.ProtocolError,
                "Request stream {0} is not correct for {1} connection",
                streamId,
                isServer ? "server" : "client");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_UnhandledErrorOnDataStream(int streamId, Exception t)
        {
            throw Http2Exception.ConnectionError(Http2Error.InternalError, t, "Unhandled error on data stream id {0}", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_MaximumActiveStreamsViolatedForThisEndpoint()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Maximum active streams violated for this endpoint.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ParentStreamMissing()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Parent stream missing");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_ServerPushNotAllowedToOppositeEndpoint()
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Server push not allowed to opposite endpoint");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_LastStreamIdMustNotIncrease(int oldLastStreamKnownByPeer, int lastKnownStream)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "lastStreamId MUST NOT increase. Current value: {0} new value: {1}",
                oldLastStreamKnownByPeer, lastKnownStream);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_LastStreamIdentifierMustNotIncreaseBetween(int oldLastStreamKnownByPeer, int lastKnownStream)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Last stream identifier must not increase between " +
                            "sending multiple GOAWAY frames (was '{0}', is '{1}').",
                    oldLastStreamKnownByPeer, lastKnownStream);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowConnectionError_StreamIsNotOpenForSendingPushPromise(int parentId)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError, "Stream {0} is not open for sending push promise", parentId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowClosedStreamError_RequestStreamIsBehindTheNextExpectedStream(int streamId, int nextStreamIdToCreate)
        {
            throw Http2Exception.ClosedStreamError(
                Http2Error.ProtocolError,
                "Request stream {0} is behind the next expected stream {1}",
                streamId,
                nextStreamIdToCreate);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Http2Exception GetStreamError_StreamClosedBeforeWriteCouldTakePlace(int streamId, Http2Error error, Exception cause)
        {
            return Http2Exception.StreamError(streamId, error, cause,
                "Stream closed before write could take place");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Http2Exception GetStreamError_Http2ToHttpLayerCaughtStreamReset(int streamId, Http2Error errorCode)
        {
            return Http2Exception.StreamError(streamId, errorCode, "HTTP/2 to HTTP layer caught stream reset");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Http2Exception GetStreamError_StreamInUnexpectedState(Http2Error error, IHttp2Stream stream)
        {
            return Http2Exception.StreamError(stream.Id, error,
                "Stream {0} in unexpected state: {1}", stream.Id, stream.State);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_CannotCreateStreamGreaterThanLastStreamIDFromGoAway(int streamId, int lastStreamKnownByPeer)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.RefusedStream,
                    "Cannot create stream {0} greater than Last-Stream-ID {1} from GOAWAY.",
                    streamId, lastStreamKnownByPeer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_MaximumActiveStreamsViolatedForThisEndpoint(int streamId)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.RefusedStream, "Maximum active streams violated for this endpoint.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Http2StreamState ThrowStreamError_AttemptingToOpenAStreamInAnInvalidState(int streamId, Http2StreamState initialState)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError, "Attempting to open a stream in an invalid state: " + initialState);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_StreamReceivedTooManyHeadersEOS(int streamId, bool endOfStream, IHttp2Stream stream)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError,
                              "Stream {0} received too many headers EOS: {1} state: {2}",
                              streamId, endOfStream, stream.State);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_StreamInUnexpectedState(IHttp2Stream stream)
        {
            throw Http2Exception.StreamError(stream.Id, Http2Error.StreamClosed,
                "Stream {0} in unexpected state: {1}", stream.Id, stream.State);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_PromisedRequestOnStreamForPromisedStreamIsNotAuthoritative(int streamId, int promisedStreamId)
        {
            throw Http2Exception.StreamError(promisedStreamId, Http2Error.ProtocolError,
                    "Promised request on stream {0} for promised stream {1} is not authoritative",
                    streamId, promisedStreamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_PromisedRequestOnStreamForPromisedStreamIsNotKnownToBeCacheable(int streamId, int promisedStreamId)
        {
            throw Http2Exception.StreamError(promisedStreamId, Http2Error.ProtocolError,
                    "Promised request on stream {0} for promised stream {1} is not known to be cacheable",
                    streamId, promisedStreamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_PromisedRequestOnStreamForPromisedStreamIsNotKnownToBeSafe(int streamId, int promisedStreamId)
        {
            throw Http2Exception.StreamError(promisedStreamId, Http2Error.ProtocolError,
                    "Promised request on stream {0} for promised stream {1} is not known to be safe",
                    streamId, promisedStreamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_ReceivedFrameForAnUnknownStream(int streamId, Http2FrameTypes frameName)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.StreamClosed,
                "Received {0} frame for an unknown stream {1}", frameName, streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_InvalidMaxFrameSizeSpecifiedInSentSettings(int streamId, int max)
        {
            throw Http2Exception.StreamError(
                streamId,
                Http2Error.FrameSizeError,
                "Invalid MAX_FRAME_SIZE specified in sent settings: {0}",
                max);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_InvalidFrameLength(int streamId, int payloadLength)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.FrameSizeError, "Invalid frame length {0}.", payloadLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_FrameLengthTooSmall(int streamId, int payloadLength)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.FrameSizeError, "Frame length {0} too small.", payloadLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_FrameLengthTooSmallForPadding(int streamId, int payloadLength)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.FrameSizeError, "Frame length {0} too small for padding.", payloadLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_AStreamCannotDependOnItself(int streamId)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError, "A stream cannot depend on itself.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_ReceivedWindowUpdateWithDelta0ForStream(int streamId)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError, "Received WINDOW_UPDATE with delta 0 for stream: {0}", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_AfterRegularHeader(int streamId, ICharSequence name)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError,
                    "Pseudo-header field '{0}' found after regular header.", name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_InvalidPseudoHeader(int streamId, ICharSequence name)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError, "Invalid HTTP/2 pseudo-header '{0}' encountered.", name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_MixOfRequest(int streamId)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError, "Mix of request and response pseudo-headers.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_InvalidHttp2HeaderEncounteredInTranslationToHttp1(int streamId, ICharSequence name)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError,
                    "Invalid HTTP/2 header {0} encountered in translation to HTTP/1.x", name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_Http2ToHttp1HeadersConversionError(int streamId, Exception t)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.ProtocolError, t, "HTTP/2 to HTTP/1.x headers conversion error");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_OverflowWhenConvertingDecompressedBytesToCompressedBytesForStream(
            int streamId, int decompressedBytes, int decompressed, int compressed, int consumedCompressed)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.InternalError,
                    "overflow when converting decompressed bytes to compressed bytes for stream {0}." +
                            "decompressedBytes: {1} decompressed: {2} compressed: {3} consumedCompressed: {4}",
                    streamId, decompressedBytes, decompressed, compressed, consumedCompressed);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_AttemptingToReturnTooManyBytesForStream(
            int streamId, int decompressed, int decompressedBytes)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.InternalError,
                    "Attempting to return too many bytes for stream {0}. decompressed: {1} " +
                            "decompressedBytes: {2}", streamId, decompressed, decompressedBytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool ThrowStreamError_ErrorWhileReturningBytesToFlowControlWindow(int streamId, Exception t)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.InternalError, t, "Error while returning bytes to flow control window");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowStreamError_DecompressorErrorDetectedWhileDelegatingDataReadOnStream(int streamId, Exception t)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.InternalError, t,
                    "Decompressor error detected while delegating data read on streamId {0}", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_WindowSizeOverflowForStream(int streamId)
        {
            throw Http2Exception.StreamError(streamId, Http2Error.FlowControlError, "Window size overflow for stream: {0}", streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_AttemptingToReturnTooManyBytesForStream(int streamId)
        {
            throw Http2Exception.StreamError(
                streamId,
                Http2Error.InternalError,
                "Attempting to return too many bytes for stream {0}",
                streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_FlowControlWindowExceededForStream(int streamId)
        {
            throw Http2Exception.StreamError(
                streamId,
                Http2Error.FlowControlError,
                "Flow control window exceeded for stream: {0}",
                streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowStreamError_FlowControlWindowOverflowedForStream(int streamId)
        {
            throw Http2Exception.StreamError(
                streamId,
                Http2Error.FlowControlError,
                "Flow control window overflowed for stream: {0}",
                streamId);
        }

        #endregion

        #region -- Http2RuntimeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2RuntimeException_CaughtUnexpectedExceptionFromCheckAllWritabilityChanged(Http2Exception e)
        {
            throw GetException();
            Http2RuntimeException GetException()
            {
                return new Http2RuntimeException("Caught unexpected exception from checkAllWritabilityChanged", e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2RuntimeException_CaughtUnexpectedExceptionFromWindow(Http2Exception e)
        {
            throw GetException();
            Http2RuntimeException GetException()
            {
                return new Http2RuntimeException("Caught unexpected exception from window", e);
            }
        }

        #endregion

        #region -- Others --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEncoderException_ContinueResponseMustBeFullHttpResponse()
        {
            throw GetException();

            static EncoderException GetException()
            {
                return new EncoderException(HttpResponseStatus.Continue.ToString() + " must be a FullHttpResponse");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIllegalReferenceCountException(int refCnt)
        {
            throw GetException();
            IllegalReferenceCountException GetException()
            {
                return new IllegalReferenceCountException(refCnt);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowUnsupportedMessageTypeException()
        {
            throw GetException();

            static UnsupportedMessageTypeException GetException()
            {
                return new UnsupportedMessageTypeException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ReturningBytesForTheConnectionWindowIsNotSupported()
        {
            throw GetException();

            static NotSupportedException GetException()
            {
                return new NotSupportedException("Returning bytes for the connection window is not supported");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowHttp2NoMoreStreamIdsException()
        {
            throw GetException();

            static Http2NoMoreStreamIdsException GetException()
            {
                return new Http2NoMoreStreamIdsException();
            }
        }

        #endregion
    }
}
