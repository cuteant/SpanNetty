using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DotNetty.Codecs.Compression;
using DotNetty.Codecs.Http.Cookies;
using DotNetty.Codecs.Http.Multipart;
using DotNetty.Codecs.Http.WebSockets;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

namespace DotNetty.Codecs.Http
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        array,
        channel,
        assembly,
        buffer,
        destination,
        key,
        obj,
        s,
        str,
        source,
        type,
        types,
        value,
        values,
        valueFactory,
        name,
        item,
        options,
        list,
        ts,
        other,
        pool,
        inner,
        policy,
        offset,
        count,
        path,
        typeInfo,
        method,
        qualifiedTypeName,
        fullName,
        feature,
        manager,
        directories,
        dirEnumArgs,
        asm,
        includedAssemblies,
        func,
        defaultFn,
        returnType,
        propertyInfo,
        parameterTypes,
        fieldInfo,
        memberInfo,
        attributeType,
        pi,
        fi,
        invoker,
        instanceType,
        target,
        member,
        typeName,
        predicate,
        assemblyPredicate,
        collection,
        capacity,
        match,
        index,
        length,
        startIndex,
        newSize,
        expression,
        contentTypeValue,
        text,
        protocolName,
        preferredClientWindowSize,
        requestedServerWindowSize,
        reasonPhrase,
        majorVersion,
        minorVersion,
        HttpPostMultipartRequestDecoder,
        HttpPostStandardRequestDecoder,
        ReadDelimiter,
        ReadDelimiterStandard,
        configList,
        uri,
        queryString,
        header,
        cookie,
        cookies,
        inputStream,
        filename,
        contentType,
        request,
        charset,
        factory,
        encoding,
        fileStream,
        data,
        stringValue,
        fileName,
        extensionHandshakers,
        parameters,
        input,
        trailingHeader,
        content,
        trailingHeaders,
        version,
        headers,
        status,
        upgradeCodec,
        sourceCodec,
        contentEncoder,
        targetContentEncoding,
        upgradeCodecFactory,
        maxParams,
        maxInitialLineLength,
        maxHeaderSize,
        maxChunkSize,
        contentSizeThreshold,
        output,
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
        internal static void ThrowException_FrameDecoder()
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("Shouldn't reach here.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static WebSocketFrame ThrowException_UnkonwFrameType()
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("Unkonw WebSocketFrame type, must be either TextWebSocketFrame or BinaryWebSocketFrame");
            }
        }

        #endregion

        #region -- ArgumentException --

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
        internal static void ThrowArgumentException_Positive(ExceptionArgument argument)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException($"{GetArgumentName(argument)} (expected: > 0)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Empty(ExceptionArgument argument)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("empty " + GetArgumentName(argument));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_NegativeVersion(ExceptionArgument argument)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("negative " + GetArgumentName(argument));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_DiffArrayLen()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Different array length");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_BufferNoBacking()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("buffer hasn't backing byte array");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_FileTooBig()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("File too big to be loaded in memory");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CookieName(string name, int pos)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"Cookie name contains an invalid char: {name[pos]}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CookieValue(string value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"Cookie value wrapping quotes are not balanced: {value}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CookieValue(ICharSequence unwrappedValue, int pos)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"Cookie value contains an invalid char: {unwrappedValue[pos]}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ValidateAttrValue(string name, string value, int index)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{name} contains the prohibited characters: ${value[index]}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowArgumentException_CompareToCookie()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"obj must be of {nameof(ICookie)} type");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowArgumentException_CompareToHttpVersion()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"obj must be of {nameof(HttpVersion)} type");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowArgumentException_CompareToHttpData(HttpDataType x, HttpDataType y)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"Cannot compare {x} with {y}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Stream_NotReadable()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"inputStream is not readable");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Stream_NotWritable()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"destination is not writable");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_AttrBigger()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Attribute bigger than maxSize allowed");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_TextFrame()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("a text frame should not contain 0xFF.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_HeadCantAddSelf()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("can't add to itself.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ChunkedMsgNotSupported()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Chunked messages not supported");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidMethodName(char c, string name)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"Invalid character '{c}' in {name}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CompressionLevel(int compressionLevel)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"compressionLevel: {compressionLevel} (expected: 0-9)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_WindowBits(int windowBits)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("windowBits: " + windowBits + " (expected: 9-15)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MemLevel(int memLevel)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("memLevel: " + memLevel + " (expected: 1-9)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_WindowSize(ExceptionArgument argument, int windowSize)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {windowSize} (expected: 8-15)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidResponseCode(int code)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"code: {code} (expected: 0+)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static HttpResponseStatus ThrowArgumentException_ParseLine<T>(T line, Exception e)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"malformed status line: {line}", e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_ReasonPhrase(AsciiString reasonPhrase)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"reasonPhrase contains one of the following prohibited characters: \\r\\n: {reasonPhrase}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidVersion(string text)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"invalid version format: {text}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidProtocolName(char c)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"invalid character {c} in protocolName");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_UnterminatedEscapeSeq(int index, string s)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"unterminated escape sequence at index {index} of: {s}");
            }
        }

        #endregion

        #region -- IOException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIOException_CheckSize()
        {
            throw GetException();
            IOException GetException()
            {
                return new IOException("Size exceed allowed maximum capacity");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIOException_CheckSize(HttpDataType dataType)
        {
            throw GetException();
            IOException GetException()
            {
                return new IOException($"{dataType} Size exceed allowed maximum capacity");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIOException_CheckSize(long maxSize)
        {
            throw GetException();
            IOException GetException()
            {
                return new IOException($"Size exceed allowed maximum capacity of {maxSize}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIOException_OutOfSize(long size, long definedSize)
        {
            throw GetException();
            IOException GetException()
            {
                return new IOException($"Out of size: {size} > {definedSize}");
            }
        }

        #endregion

        #region -- InvalidOperationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_HttpRequestEncoder()
        {
            throw GetInvalidOperationException<HttpRequestEncoder>();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Task ThrowInvalidOperationException_HttpResponseDecoder()
        {
            return TaskUtil.FromException(GetInvalidOperationException<HttpResponseDecoder>());
        }

        internal static InvalidOperationException GetInvalidOperationException<T>()
        {
            return new InvalidOperationException($"ChannelPipeline does not contain a {typeof(T).Name} or HttpClientCodec");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Task ThrowInvalidOperationException_NoHttpDecoderAndServerCodec()
        {
            return TaskUtil.FromException(GetInvalidOperationException_NoHttpDecoderAndServerCodec());
        }

        internal static InvalidOperationException GetInvalidOperationException_NoHttpDecoderAndServerCodec()
        {
            return new InvalidOperationException("No HttpDecoder and no HttpServerCodec in the pipeline");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Task ThrowInvalidOperationException_Attempting()
        {
            return TaskUtil.FromException(GetException());
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Attempting to write HTTP request with upgrade in progress");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static object ThrowInvalidOperationException_Cqrs(object callable, Exception exception)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"Could not generate value for callable [{callable}]", exception);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_NoMoreElement()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("No more element to iterate");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_WebSocketClientHandshaker()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("WebSocketClientHandshaker should have been finished yet");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_UnknownWebSocketVersion()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Unknown web socket version");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_OnlyHaveOneValue()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"{nameof(CombinedHttpHeaders)} should only have one value");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_NoFileDefined()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("No file defined so cannot be renamed");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CannotSendMore()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("cannot send more responses than requests");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ReadHttpResponse()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Read HTTP response without requesting protocol switch");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CheckDestroyed<T>()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"{StringUtil.SimpleClassName<T>()} was destroyed already");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_UnexpectedMsg(object message)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"unexpected message type: {StringUtil.SimpleClassName(message)}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_UnexpectedMsg(object message, int state)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"unexpected message type: {StringUtil.SimpleClassName(message)}, state: {state}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_InvalidType(IHttpMessage oversized)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"Invalid type {StringUtil.SimpleClassName(oversized)}, expecting {nameof(IHttpRequest)} or {nameof(IHttpResponse)}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_UnexpectedUpgradeProtocol(ICharSequence upgradeHeader)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"Switching Protocols response with unexpected UPGRADE protocol: {upgradeHeader}");
            }
        }

        #endregion

        #region -- ChannelException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowChannelException_IO(IOException e)
        {
            throw GetException();
            ChannelException GetException()
            {
                return new ChannelException(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowChannelException_IO<T>(IOException e)
        {
            throw GetException();
            ChannelException GetException()
            {
                return new ChannelException(e);
            }
        }

        #endregion

        #region -- EncoderException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEncoderException_UnexpectedState(int state, object message)
        {
            throw GetException();
            EncoderException GetException()
            {
                return new EncoderException($"unexpected state {state}: {StringUtil.SimpleClassName(message)}");
            }
        }

        #endregion

        #region -- FormatException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static long ThrowFormatException_HeaderNotFound()
        {
            throw GetException();
            FormatException GetException()
            {
                return new FormatException($"header not found: {HttpHeaderNames.ContentLength}");
            }
        }

        #endregion

        #region -- TooLongFrameException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowTooLongFrameException_WebSocket00FrameDecoder()
        {
            throw GetException();
            TooLongFrameException GetException()
            {
                return new TooLongFrameException(nameof(WebSocket00FrameDecoder));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowTooLongFrameException_ResponseTooLarge(IHttpMessage oversized)
        {
            throw GetException();
            TooLongFrameException GetException()
            {
                return new TooLongFrameException($"Response entity too large: {oversized}");
            }
        }

        #endregion

        #region -- NotEnoughDataDecoderException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotEnoughDataDecoderException(ExceptionArgument argument)
        {
            throw GetException();
            NotEnoughDataDecoderException GetException()
            {
                return new NotEnoughDataDecoderException(GetArgumentName(argument));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotEnoughDataDecoderException(Exception e)
        {
            throw GetException();
            NotEnoughDataDecoderException GetException()
            {
                return new NotEnoughDataDecoderException(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotEnoughDataDecoderException_AccessOutOfBounds()
        {
            throw GetException();
            NotEnoughDataDecoderException GetException()
            {
                return new NotEnoughDataDecoderException("Access out of bounds");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static StringCharSequence ThrowNotEnoughDataDecoderException_ReadLineStandard()
        {
            throw GetException();
            NotEnoughDataDecoderException GetException()
            {
                return new NotEnoughDataDecoderException("ReadLineStandard");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static StringCharSequence ThrowNotEnoughDataDecoderException_ReadLine()
        {
            throw GetException();
            NotEnoughDataDecoderException GetException()
            {
                return new NotEnoughDataDecoderException("ReadLine");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static StringBuilderCharSequence ThrowNotEnoughDataDecoderException_ReadDelimiterStandard()
        {
            throw GetException();
            NotEnoughDataDecoderException GetException()
            {
                return new NotEnoughDataDecoderException("ReadDelimiterStandard");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static StringBuilderCharSequence ThrowNotEnoughDataDecoderException_ReadDelimiter()
        {
            throw GetException();
            NotEnoughDataDecoderException GetException()
            {
                return new NotEnoughDataDecoderException("ReadDelimiter");
            }
        }

        #endregion

        #region -- ErrorDataDecoderException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException(Exception e)
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_GetStatus()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("Should not be called with the current getStatus");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_Attr()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException($"{HttpHeaderValues.Name} attribute cannot be null.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_NameAttr()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException($"{HttpHeaderValues.Name} attribute cannot be null for file upload");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_FileNameAttr()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException($"{HttpHeaderValues.FileName} attribute cannot be null for file upload");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_ReachHere()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("Shouldn't reach here.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_NoMultipartDelimiterFound()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("No Multipart delimiter found");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_MixedMultipartFound()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("Mixed Multipart found in a previous Mixed Multipart");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_UnknownParams(StringCharSequence newline)
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException($"Unknown Params: {newline}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_FileName()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("Filename not found");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_NeedBoundaryValue()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("Needs a boundary value");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_BadEndOfLine()
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("Bad end of line");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string ThrowErrorDataDecoderException_BadString(string s, Exception e)
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException($"Bad string: '{s}'", e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataDecoderException_TransferEncoding(string code)
        {
            throw GetErrorDataDecoderException();
            ErrorDataDecoderException GetErrorDataDecoderException()
            {
                return new ErrorDataDecoderException("TransferEncoding Unknown: " + code);
            }
        }

        #endregion

        #region -- ErrorDataEncoderException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataEncoderException(Exception e)
        {
            throw GetErrorDataEncoderException();
            ErrorDataEncoderException GetErrorDataEncoderException()
            {
                return new ErrorDataEncoderException(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataEncoderException_HeaderAlreadyEncoded()
        {
            throw GetErrorDataEncoderException();
            ErrorDataEncoderException GetErrorDataEncoderException()
            {
                return new ErrorDataEncoderException("Header already encoded");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataEncoderException_CannotAddValue()
        {
            throw GetErrorDataEncoderException();
            ErrorDataEncoderException GetErrorDataEncoderException()
            {
                return new ErrorDataEncoderException("Cannot add value once finalized");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowErrorDataEncoderException_CannotCreate()
        {
            throw GetErrorDataEncoderException();
            ErrorDataEncoderException GetErrorDataEncoderException()
            {
                return new ErrorDataEncoderException("Cannot create a Encoder if request is a TRACE");
            }
        }

        #endregion

        #region -- CodecException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IFullHttpMessage ThrowCodecException_InvalidType(IHttpMessage start)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"Invalid type {StringUtil.SimpleClassName(start)} expecting {nameof(IHttpRequest)} or {nameof(IHttpResponse)}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_EnsureContent(IHttpObject msg)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"unexpected message type: {msg.GetType().Name} (expected: {StringUtil.SimpleClassName<IHttpContent>()})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_EnsureHeaders(IHttpObject msg)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"unexpected message type: {msg.GetType().Name} (expected: {StringUtil.SimpleClassName<IHttpResponse>()})");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_InvalidHttpMsg(IHttpMessage httpMessage)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"Object of class {StringUtil.SimpleClassName(httpMessage.GetType())} is not a HttpRequest or HttpResponse");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_InvalidCompression(ZlibWrapper mode)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"{mode} not supported, only Gzip and Zlib are allowed.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_InvalidWSExHandshake(string extensionsHeader)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"invalid WebSocket Extension handshake for \"{extensionsHeader}\"");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_UnexpectedFrameType(WebSocketFrame msg)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"unexpected frame type: {msg.GetType().Name}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_CannotReadCompressedBuf()
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException("cannot read compressed buffer");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_CannotReadUncompressedBuf()
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException("cannot read uncompressed buffer");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCodecException_UnexpectedInitialFrameType(WebSocketFrame msg)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException($"unexpected initial frame type: {msg.GetType().Name}");
            }
        }

        #endregion

        #region -- WebSocketHandshakeException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static WebSocketClientHandshaker ThrowWebSocketHandshakeException_InvalidVersion(WebSocketVersion version)
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException($"Protocol version {version}not supported.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_InvalidSubprotocol(string receivedProtocol, string expectedSubprotocol)
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException($"Invalid subprotocol. Actual: {receivedProtocol}. Expected one of: {expectedSubprotocol}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_InvalidHandshakeResponseGS(IFullHttpResponse response)
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException($"Invalid handshake response getStatus: {response.Status}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_InvalidHandshakeResponseU(ICharSequence upgrade)
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException($"Invalid handshake response upgrade: {upgrade}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_InvalidHandshakeResponseConn(ICharSequence upgrade)
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException($"Invalid handshake response connection: {upgrade}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_InvalidChallenge()
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException("Invalid challenge");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_InvalidChallenge(ICharSequence accept, AsciiString expectedChallengeResponseString)
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException($"Invalid challenge. Actual: {accept}. Expected: {expectedChallengeResponseString}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_MissingUpgrade()
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException("not a WebSocket handshake request: missing upgrade");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowWebSocketHandshakeException_MissingKey()
        {
            throw GetException();
            WebSocketHandshakeException GetException()
            {
                return new WebSocketHandshakeException("not a WebSocket request: missing key");
            }
        }

        #endregion

        #region -- EndOfDataDecoderException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEndOfDataDecoderException_HttpPostStandardRequestDecoder()
        {
            throw GetException();
            EndOfDataDecoderException GetException()
            {
                return new EndOfDataDecoderException(nameof(HttpPostStandardRequestDecoder));
            }
        }

        #endregion

        #region -- MessageAggregationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowMessageAggregationException_StartMessage()
        {
            throw GetException();
            MessageAggregationException GetException()
            {
                return new MessageAggregationException("Start message should not have any current content.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowMessageAggregationException_UnknownAggregationState()
        {
            throw GetException();
            MessageAggregationException GetException()
            {
                return new MessageAggregationException("Unknown aggregation state.");
            }
        }

        #endregion
    }
}
