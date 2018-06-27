using System;
using System.Runtime.CompilerServices;
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
        internal static void ThrowNullReferenceException()
        {
            throw GetNullReferenceException();
            NullReferenceException GetNullReferenceException()
            {
                return new NullReferenceException("encoding");
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
        internal static void ThrowDecoderException(int lengthFieldLength)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException("unsupported lengthFieldLength: " + lengthFieldLength + " (expected: 1, 2, 3, 4, or 8)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowEncoderException(Type type)
        {
            throw GetException();
            EncoderException GetException()
            {
                return new EncoderException(type.Name + " must produce at least one message.");
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
    }
}
