using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using DotNetty.Handlers.IPFilter;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;

namespace DotNetty.Handlers
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
        sslStreamFactory,
        settings,
        doBindAction,
        input,
        chunkSize,
        serverTlsSettingMap,
        hostName,
        serverTlsSetting,
        exception,
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
        internal static void ThrowArgumentException_ServerCertificateRequired()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("The server certificate parameter is required.", "settings");
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_CannotDetermineToAcceptOrRejectAChannel(IChannelHandlerContext ctx)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("cannot determine to accept or reject a channel: " + ctx.Channel, nameof(ctx));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_IdleState(IdleState state, bool first)
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Unhandled: state=" + state + ", first=" + first);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IIPFilterRule ThrowArgumentOutOfRangeException_IPv4RequiresTheSubnetPrefixToBeInRangeOf0_32(int cidrPrefix)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(
                    nameof(cidrPrefix),
                    string.Format(
                        "IPv4 requires the subnet prefix to be in range of " +
                        "[0,32]. The prefix was: {0}",
                        cidrPrefix));
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IIPFilterRule ThrowArgumentOutOfRangeException_IPv6RequiresTheSubnetPrefixToBeInRangeOf0_128(int cidrPrefix)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException(
                    nameof(cidrPrefix),
                    string.Format(
                        "IPv6 requires the subnet prefix to be in range of " +
                        "[0,128]. The prefix was: {0}",
                        cidrPrefix));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IIPFilterRule ThrowArgumentOutOfRangeException_OctetsCountMustBeEqual4ForIPv4()
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("ipAddress", "Octets count must be equal 4 for IPv4 address.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IIPFilterRule ThrowArgumentOutOfRangeException_OctetsCountMustBeEqual16ForIPv6()
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("ipAddress", "Octets count must be equal 16 for IPv6 address.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IIPFilterRule ThrowArgumentOutOfRangeException_OnlySupportIPv4AndIPv6Addresses()
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("ipAddress", "Only IPv4 and IPv6 addresses are supported");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException_HandshakeCompleted(TaskStatus status)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentOutOfRangeException GetArgumentOutOfRangeException()
            {
                return new ArgumentOutOfRangeException("task", "Unexpected task status: " + status);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_WriteComplete(TaskStatus status)
        {
            throw GetArgumentOutOfRangeException();
            ArgumentException GetArgumentOutOfRangeException()
            {
                return new ArgumentException("Unexpected task status: " + status);
            }
        }

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
        internal static void ThrowInvalidOperationException_InvalidServerCertificateEku(X509Certificate2 certificate)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException(
                  $"Certificate {certificate.Thumbprint} cannot be used as an SSL server certificate. It has an Extended Key Usage extension but the usages do not include Server Authentication (OID 1.3.6.1.5.5.7.3.1).");
            }
        }
    }
}
