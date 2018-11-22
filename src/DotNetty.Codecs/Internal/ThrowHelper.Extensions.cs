using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DotNetty.Common.Utilities;

namespace DotNetty.Codecs
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        array,
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
        encoding,
        src,
        dialect_alphabet,
        dialect_decodabet,
        nameHashingStrategy,
        valueConverter,
        nameValidator,
        newValue,
        delimiters,
        delimiter,
        dictionary,
        context,
        message,
        output,
        cumulationFunc,
        input,
        decoder,
        encoder,
        txt,
        cause,
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNullReferenceException(ExceptionArgument argument)
        {
            throw GetNullReferenceException();
            NullReferenceException GetNullReferenceException()
            {
                return new NullReferenceException(GetArgumentName(argument));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowException_UnknownLen()
        {
            throw GetException();
            Exception GetException()
            {
                return new Exception("Unknown length field length");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_DiscardAfterReads()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("discardAfterReads must be > 0");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_EmptyDelimiter()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("empty delimiter");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_EmptyDelimiters()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("empty delimiters");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CannotAddToItSelf()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("can't add to itself.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CannotHaveEndStart()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Can't have end < start");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CannotParseMoreThan64Chars()
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Can't parse more than 64 chars, looks like a user error or a malformed header");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CompressionLevel(int compressionLevel)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("compressionLevel: " + compressionLevel + " (expected: 0-9)");
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
        internal static void ThrowArgumentException_MaxFrameLengthMustBe(int maxFrameLength)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("maxFrameLength must be a positive integer: " + maxFrameLength);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MaxCumulationBufferComponents(int value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"maxCumulationBufferComponents: {value} (expected: >= 2)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_MaxContentLength(int maxContentLength)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"maxContentLength: {maxContentLength}(expected: >= 0)", nameof(maxContentLength));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_BadBase64InputChar(int index, sbyte value)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException(string.Format("bad Base64 input character at {0}:{1}", index, value));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_LessThanZero(int length)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Adjusted frame length (" + length + ") is less than zero");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Byte(int length)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("length of object does not fit into one byte: " + length);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Short(int length)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("length of object does not fit into a short integer: " + length);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_Medium(int length)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("length of object does not fit into a medium integer: " + length);
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
        internal static void ThrowInvalidOperationException_EnumeratorNotInitOrCompleted()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("Enumerator not initialized or completed.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_NotAddedToAPipelineYet()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("not added to a pipeline yet");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_DecoderProperties()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException("decoder properties cannot be changed once the decoder is added to a pipeline.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ByteToMessageDecoder()
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException($"Decoders inheriting from {typeof(ByteToMessageDecoder).Name} cannot be sharable.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_ByteToMessageDecoder(Type type)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"{type.Name}.Decode() did not read anything but decoded a message.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException(Exception e)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException(e);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_Anything(Type type)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"{type.Name}.Decode() must consume the inbound data or change its state if it did not decode anything.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_Something(Type type)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"{type.Name}.Decode() method must consume the inbound data or change its state if it decoded something.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static long ThrowDecoderException(int lengthFieldLength)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException("unsupported lengthFieldLength: " + lengthFieldLength + " (expected: 1, 2, 3, 4, or 8)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEncoderException(Exception ex)
        {
            throw GetException();
            EncoderException GetException()
            {
                return new EncoderException(ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static EncoderException GetEncoderException(Exception ex)
        {
            return new EncoderException(ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEncoderException_MustProduceAtLeastOneMsg(Type type)
        {
            throw GetException();
            EncoderException GetException()
            {
                return new EncoderException(type.Name + " must produce at least one message.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEncoderException_MustProduceOnlyOneMsg(Type type)
        {
            throw GetException();
            EncoderException GetException()
            {
                return new EncoderException($"{type} must produce only one message.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEncoderException_MustProduceOnlyByteBuf(Type type)
        {
            throw GetException();
            EncoderException GetException()
            {
                return new EncoderException($"{type} must produce only IByteBuffer.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowFormatException(ICharSequence value)
        {
            throw GetException();
            FormatException GetException()
            {
                return new FormatException($"header can't be parsed into a Date: {value}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCorruptedFrameException_FrameLength(long frameLength)
        {
            throw GetException();
            CorruptedFrameException GetException()
            {
                return new CorruptedFrameException("negative pre-adjustment length field: " + frameLength);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCorruptedFrameException_LengthFieldEndOffset(long frameLength, int lengthFieldEndOffset)
        {
            throw GetException();
            CorruptedFrameException GetException()
            {
                return new CorruptedFrameException("Adjusted frame length (" + frameLength + ") is less " +
                    "than lengthFieldEndOffset: " + lengthFieldEndOffset);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowCorruptedFrameException_InitialBytesToStrip(long frameLength, int initialBytesToStrip)
        {
            throw GetException();
            CorruptedFrameException GetException()
            {
                return new CorruptedFrameException("Adjusted frame length (" + frameLength + ") is less " +
                    "than initialBytesToStrip: " + initialBytesToStrip);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowTooLongFrameException(int maxFrameLength, long frameLength)
        {
            throw GetException();
            TooLongFrameException GetException()
            {
                return new TooLongFrameException("frame length exceeds " + maxFrameLength + ": " + frameLength + " - discarded");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowTooLongFrameException(int maxFrameLength)
        {
            throw GetException();
            TooLongFrameException GetException()
            {
                return new TooLongFrameException("frame length exceeds " + maxFrameLength + " - discarding");
            }
        }
        internal static TooLongFrameException GetTooLongFrameException(int maxContentLength)
        {
            return new TooLongFrameException($"content length exceeded {maxContentLength} bytes.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException_Readonly()
        {
            throw GetException();
            NotSupportedException GetException()
            {
                return new NotSupportedException("read only");
            }
        }
    }
}
