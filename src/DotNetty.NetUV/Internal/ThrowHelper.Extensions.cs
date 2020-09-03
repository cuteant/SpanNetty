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
using System.Runtime.CompilerServices;
using DotNetty.NetUV.Handles;
using DotNetty.NetUV.Native;

namespace DotNetty.NetUV
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
        internal static void ThrowInvalidOperationException_uv_handle_type_is_not_readable(uv_handle_type handleType, IntPtr internalHandle, TtyType ttyType)
        {
            throw GetException();
            InvalidOperationException GetException()
            {
                return new InvalidOperationException(
                    $"{handleType} {internalHandle} mode {ttyType} is not readable");
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
    }
}
