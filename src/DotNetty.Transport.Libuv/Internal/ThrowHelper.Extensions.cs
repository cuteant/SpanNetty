/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Libuv.Handles;
using DotNetty.Transport.Libuv.Native;

namespace DotNetty.Transport.Libuv
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        s,

        pi,
        fi,
        ts,

        asm,
        key,
        obj,
        str,
        tcp,
        udp,

        list,
        pool,
        name,
        path,
        item,
        type,
        func,
        loop,
        pipe,
        size,
        node,
        task,

        match,
        array,
        other,
        inner,
        types,
        value,
        index,
        count,

        action,
        policy,
        handle,
        repeat,
        offset,
        method,
        buffer,
        source,
        values,
        parent,
        length,
        onRead,
        socket,
        target,
        member,

        buffers,
        backlog,
        feature,
        manager,
        newSize,
        invoker,
        options,
        minimum,
        initial,
        maximum,
        onError,
        service,
        timeout,

        assembly,
        capacity,
        endPoint,
        fullName,
        typeInfo,
        typeName,
        nThreads,
        onAccept,
        pipeName,
        callback,
        interval,

        allocator,
        defaultFn,
        fieldInfo,
        predicate,

        memberInfo,
        returnType,
        collection,
        expression,
        startIndex,
        remoteName,
        readAction,
        completion,
        sendHandle,

        directories,
        dirEnumArgs,
        destination,

        valueFactory,
        propertyInfo,
        instanceType,
        workCallback,
        streamHandle,
        onConnection,

        attributeType,
        channelUnsafe,
        localEndPoint,
        receiveAction,

        chooserFactory,
        eventLoopGroup,
        parameterTypes,
        remoteEndPoint,

        connectedAction,

        multicastAddress,
        interfaceAddress,

        assemblyPredicate,
        qualifiedTypeName,

        includedAssemblies,
    }

    #endregion

    #region -- ExceptionResource --

    /// <summary>The convention for this enum is using the resource name as the enum name</summary>
    internal enum ExceptionResource
    {
    }

    #endregion

    internal partial class ThrowHelper
    {
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
        internal static int ThrowArgumentException_PositiveOrOne(int value, ExceptionArgument argument)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{GetArgumentName(argument)}: {value} (expected: >= 1)");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_InvalidOffLen()
        {
            throw GetException();
            static ArgumentException GetException()
            {
                return new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_TtyMode_is_Unix_only(TtyMode mode)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException($"{mode} is Unix only.", nameof(mode));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Task FromArgumentException_RegChannel()
        {
            return TaskUtil.FromException(GetArgumentException());

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException($"channel must be of {typeof(INativeChannel)}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException_PipeName()
        {
            throw GetArgumentException();

            static ArgumentException GetArgumentException()
            {
                return new ArgumentException("Pipe name is required for worker event loop", "parent");
            }
        }

        #endregion

        #region -- InvalidOperationException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidOperationException GetInvalidOperationException_uv_handle_type_not_supported_or_IPC_over_Pipe_is_disabled(uv_handle_type handleType)
        {
            return new InvalidOperationException($"{handleType} not supported or IPC over Pipe is disabled.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static InvalidOperationException GetInvalidOperationException_Pipe_IPC_handle_not_supported(uv_handle_type type)
        {
            return new InvalidOperationException($"Pipe IPC handle {type} not supported");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_TcpHandle()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("Tcp handle not intialized");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_HandleNotInit()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("tcpListener handle not intialized");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Udp_data_handler_has_already_been_registered()
        {
            throw GetException();
            static InvalidOperationException GetException()
            {
                return new InvalidOperationException(
                    $"{nameof(Udp)} data handler has already been registered");
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
                return new InvalidOperationException($"Invalid {nameof(AbstractUVEventLoop)} state {executionState}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_uv_handle_type_is_not_readable(uv_handle_type handleType, IntPtr internalHandle, TtyType ttyType)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException(
                    $"{handleType} {internalHandle} mode {ttyType} is not readable");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Task FromInvalidOperationException(IntPtr loopHandle)
        {
            return TaskUtil.FromException(GetInvalidOperationException());
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Loop {loopHandle} does not exist");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_Dispatch()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("No pipe connections to dispatch handles.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException_ConnAttempt()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException("connection attempt already made");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static uint FromInvalidOperationException_Dispatch(AddressFamily addressFamily)
        {
            throw GetInvalidOperationException();
            InvalidOperationException GetInvalidOperationException()
            {
                return new InvalidOperationException($"Address family : {addressFamily} platform : {RuntimeInformation.OSDescription} not supported");
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

        #endregion

        #region -- ChannelException --

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

        #endregion

        #region -- NotSupportedException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotSupportedException GetNotSupportedException_Poll_argument_must_be_either_IntPtr_or_int()
        {
            return new NotSupportedException("Poll argument must be either IntPtr or int");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotSupportedException GetNotSupportedException_expecting_InterNetworkkV6OrV4(IPEndPoint endPoint)
        {
            return new NotSupportedException(
                $"End point {endPoint} is not supported, expecting InterNetwork/InterNetworkV6.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotSupportedException GetNotSupportedException_Handle_type_to_initialize_not_supported(uv_handle_type handleType)
        {
            return new NotSupportedException($"Handle type to initialize {handleType} not supported");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotSupportedException GetNotSupportedException_Handle_type_to_start_not_supported(uv_handle_type handleType)
        {
            return new NotSupportedException($"Handle type to start {handleType} not supported");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static NotSupportedException GetNotSupportedException_Handle_type_to_stop_not_supported(uv_handle_type handleType)
        {
            return new NotSupportedException($"Handle type to stop {handleType} not supported");
        }

        #endregion

        #region -- PlatformNotSupportedException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowPlatformNotSupportedException_handle_type_send_buffer_size_setting_not_supported_on_Windows(uv_handle_type handleType)
        {
            throw GetException();
            PlatformNotSupportedException GetException()
            {
                return new PlatformNotSupportedException($"{handleType} send buffer size setting not supported on Windows");
            }
        }

        #endregion

        #region -- SocketException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowSocketException(int errorCode)
        {
            throw GetSocketException();
            SocketException GetSocketException()
            {
                return new SocketException(errorCode);
            }
        }

        #endregion

        #region -- ConnectTimeoutException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static ConnectTimeoutException GetConnectTimeoutException(OperationException error)
        {
            return new ConnectTimeoutException(error.ToString());
        }

        #endregion

        #region -- TimeoutException --

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowTimeoutException(string pipeName)
        {
            throw GetException();
            TimeoutException GetException()
            {
                return new TimeoutException($"Connect to dispatcher pipe {pipeName} timed out.");
            }
        }

        #endregion

        #region -- ClosedChannelException --

        internal static ClosedChannelException GetClosedChannelException()
        {
            return new ClosedChannelException();
        }

        internal static ClosedChannelException GetClosedChannelException_FailedToWrite(Exception ex)
        {
            return new ClosedChannelException("Failed to write", ex);
        }

        #endregion
    }
}
