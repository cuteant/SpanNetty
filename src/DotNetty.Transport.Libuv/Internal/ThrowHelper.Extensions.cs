using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Libuv.Native;

namespace DotNetty.Transport.Libuv
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
        internal static void ThrowArgumentException_RegChannel()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException($"channel must be of {typeof(INativeChannel)}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PipeName()
        {
            throw GetArgumentException();
            ArgumentException GetArgumentException()
            {
                return new ArgumentException("Pipe name is required for worker event loop", "parent");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Task ThrowInvalidOperationException(IntPtr loopHandle)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Loop {loopHandle} does not exist");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ExecutionState(int executionState)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Invalid state {executionState}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ExecutionState0(int executionState)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Invalid {nameof(LoopExecutor)} state {executionState}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ExpectingTcpHandle(uv_handle_type type)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Expecting tcp handle, {type} not supported");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Dispatch()
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("No pipe connections to dispatch handles.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ConnAttempt()
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("connection attempt already made");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_TcpHandle()
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("Tcp handle not intialized");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static uint ThrowInvalidOperationException_Dispatch(AddressFamily addressFamily)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Address family : {addressFamily} platform : {RuntimeInformation.OSDescription} not supported");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_FailedToCreateChildEventLoop(Exception ex)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("failed to create a child event loop.", ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_CreateChild(Exception ex)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Failed to create a child {nameof(WorkerEventLoop)}.", ex.Unwrap());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_HandleNotInit()
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("tcpListener handle not intialized");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowSocketException(int errorCode)
        {
            throw GetSocketException();
            SocketException GetSocketException()
            {
                return new SocketException(errorCode);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowRejectedExecutionException_Terminated()
        {
            throw GetSocketException();
            RejectedExecutionException GetSocketException()
            {
                return new RejectedExecutionException($"{nameof(LoopExecutor)} terminated");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowRejectedExecutionException_Shutdown()
        {
            throw GetSocketException();
            RejectedExecutionException GetSocketException()
            {
                return new RejectedExecutionException($"{nameof(LoopExecutor)} already shutdown");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowRejectedExecutionException_Queue()
        {
            throw GetSocketException();
            RejectedExecutionException GetSocketException()
            {
                return new RejectedExecutionException($"{nameof(LoopExecutor)} queue task failed");
            }
        }

        internal static ChannelException GetChannelException(OperationException ex)
        {
            return new ChannelException(ex);
        }

        internal static ChannelException GetChannelException_FailedToWrite(OperationException error)
        {
            return new ChannelException("Failed to write", error);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowChannelException(Exception exc)
        {
            throw GetChannelException();
            ChannelException GetChannelException()
            {
                return new ChannelException(exc);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowChannelException(ChannelOption option)
        {
            throw GetChannelException();
            ChannelException GetChannelException()
            {
                return new ChannelException($"Invalid channel option {option}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowTimeoutException(string pipeName)
        {
            throw GetException();
            TimeoutException GetException()
            {
                return new TimeoutException($"Connect to dispatcher pipe {pipeName} timed out.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException(IPEndPoint endPoint, out sockaddr addr)
        {
            throw GetException();
            NotSupportedException GetException()
            {
                return new NotSupportedException($"End point {endPoint} is not supported, expecting InterNetwork/InterNetworkV6.");
            }
        }

        internal static ConnectTimeoutException GetConnectTimeoutException(OperationException error)
        {
            return new ConnectTimeoutException(error.ToString());
        }

        internal static ClosedChannelException GetClosedChannelException()
        {
            return new ClosedChannelException();
        }

        internal static ClosedChannelException GetClosedChannelException_FailedToWrite(Exception ex)
        {
            return new ClosedChannelException("Failed to write", ex);
        }
    }
}
