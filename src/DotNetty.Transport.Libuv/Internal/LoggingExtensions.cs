﻿/*
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
using System.Runtime.CompilerServices;
using DotNetty.Buffers;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Internal.Logging;
using DotNetty.Transport.Libuv.Handles;
using DotNetty.Transport.Libuv.Native;
using DotNetty.Transport.Libuv.Requests;

namespace DotNetty.Transport.Libuv
{
    internal static partial class LibuvLoggingExtensions
    {
        #region -- Debug --

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LoopDisposing(this IInternalLogger logger, IDisposable handle)
        {
            logger.Debug("Disposing {}", handle.GetType());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FailedToCloseChannelCleanly(this IInternalLogger logger, object channelObject, Exception ex)
        {
            logger.Debug($"Failed to close channel {channelObject} cleanly.", ex);
        }

        #endregion

        #region -- Info --

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HandleAllocated(this IInternalLogger logger, uv_handle_type handleType, IntPtr handle)
        {
            logger.Info("{} {} allocated.", handleType, handle);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HandleClosedReleasingResourcesPending(this IInternalLogger logger, uv_handle_type handleType, IntPtr handle)
        {
            logger.Info("{} {} closed, releasing resources pending.", handleType, handle);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LoopWalkCallbackDisposed(this IInternalLogger logger, IntPtr loopHandle, IntPtr handle, IDisposable target)
        {
            logger.Info($"Loop {loopHandle} walk callback disposed {handle} {target?.GetType()}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Loop_memory_released(this IInternalLogger logger, IntPtr handle)
        {
            logger.Info($"Loop {handle} memory released.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Loop_GCHandle_released(this IInternalLogger logger, IntPtr handle)
        {
            logger.Info($"Loop {handle} GCHandle released.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Loop_closed(this IInternalLogger logger, IntPtr handle)
        {
            logger.Info($"Loop {handle} closed.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Loop_allocated(this IInternalLogger logger, IntPtr handle)
        {
            logger.Info($"Loop {handle} allocated.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LoopThreadFinished(this IInternalLogger logger, XThread thread)
        {
            logger.Info("Loop {}: thread finished.", thread.Name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LoopDisposed(this IInternalLogger logger, XThread thread)
        {
            logger.Info("{}:disposed.", thread.Name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AcceptClientConnectionFailed(this IInternalLogger logger, Exception ex)
        {
            logger.Info("Accept client connection failed.", ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FailedToConnectToDispatcher(this IInternalLogger logger, int retryCount, OperationException error)
        {
            logger.Info($"{nameof(WorkerEventLoop)} failed to connect to dispatcher, Retry count = {retryCount}", error);
        }

        #endregion

        #region -- Warn --

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Handle_receive_result_truncated(this IInternalLogger logger, IntPtr handle, IByteBuffer byteBuffer)
        {
            logger.Warn($"{uv_handle_type.UV_UDP} {handle} receive result truncated, buffer size = {byteBuffer.Capacity}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Udp_Exception_whilst_invoking_read_callback(this IInternalLogger logger, Exception exception)
        {
            logger.Warn($"{nameof(Udp)} Exception whilst invoking read callback.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Failed_to_close_and_releasing_resources(this IInternalLogger logger, HandleContext handle, Exception exception)
        {
            logger.Warn($"{handle} Failed to close and releasing resources.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Failed_to_get_loop(this IInternalLogger logger, uv_handle_type handleType, Exception exception)
        {
            logger.Warn($"{handleType} Failed to get loop.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Pipeline_Exception_whilst_invoking_read_callback(this IInternalLogger logger, Exception exception)
        {
            logger.Warn($"Pipeline Exception whilst invoking read callback.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Loop_Walk_callback_attempt_to_close_handle_failed(this IInternalLogger logger, IntPtr loopHandle, IntPtr handle, Exception exception)
        {
            logger.Warn($"Loop {loopHandle} Walk callback attempt to close handle {handle} failed.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Loop_close_all_handles_limit_20_times_exceeded(this IInternalLogger logger, IntPtr handle)
        {
            logger.Warn($"Loop {handle} close all handles limit 20 times exceeded.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LoopReleaseError(this IInternalLogger logger, XThread thread, Exception ex)
        {
            logger.Warn("{}:release error {}", thread.Name, ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LoopDisposeError(this IInternalLogger logger, IDisposable handle, Exception ex)
        {
            logger.Warn("{} dispose error {}", handle.GetType(), ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FailedToCreateConnectRequestToDispatcher(this IInternalLogger logger, Exception exception)
        {
            logger.Warn($"{nameof(WorkerEventLoop)} failed to create connect request to dispatcher", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FailedToDisposeAClientConnection(this IInternalLogger logger, Exception ex)
        {
            logger.Warn("Failed to dispose a client connection.", ex);
        }

        #endregion

        #region -- Error --

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RequestType_after_callback_error(this IInternalLogger logger, uv_req_type requestType, Exception exception)
        {
            logger.Error($"{requestType} after callback error", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RequestType_work_callback_error(this IInternalLogger logger, uv_req_type requestType, Exception exception)
        {
            logger.Error($"{requestType} work callback error", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RequestType_OnWatcherCallback_error(this IInternalLogger logger, uv_req_type requestType, Exception exception)
        {
            logger.Error($"{requestType} OnWatcherCallback error.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RequestType_OnWatcherCallback_error(this IInternalLogger logger, uv_req_type requestType, IntPtr handle, OperationException error)
        {
            logger.Error($"{requestType} {handle} error : {error.ErrorCode} {error.Name}.", error);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UV_SHUTDOWN_callback_error(this IInternalLogger logger, Exception exception)
        {
            logger.Error("UV_SHUTDOWN callback error.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void NativeHandle_error_whilst_closing_handle(this IInternalLogger logger, IntPtr handle, Exception exception)
        {
            logger.Error($"{nameof(NativeHandle)} {handle} error whilst closing handle.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Handle_callback_error(this IInternalLogger logger, uv_handle_type handleType, IntPtr handle, Exception exception)
        {
            logger.Error($"{handleType} {handle} callback error.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Handle_read_error(this IInternalLogger logger, uv_handle_type handleType, IntPtr handle, int status, Exception exception)
        {
            logger.Error($"{handleType} {handle} read error, status = {status}", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Handle_faulted(this IInternalLogger logger, uv_handle_type handleType, Exception exception)
        {
            logger.Error($"{handleType} faulted.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Pipeline_Handle_faulted(this IInternalLogger logger, uv_handle_type handleType, Exception exception)
        {
            logger.Error($"Pipeline {handleType} faulted.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Handle_close_handle_callback_error(this IInternalLogger logger, uv_handle_type handleType, Exception exception)
        {
            logger.Error($"{handleType} close handle callback error.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Failed_to_close_handle(this IInternalLogger logger, uv_handle_type handleType, Exception exception)
        {
            logger.Error($"{handleType} Failed to close handle.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Handle_failed_to_shutdown(this IInternalLogger logger, uv_handle_type handleType, IntPtr handle, Exception error)
        {
            logger.Error($"{handleType} {handle} failed to shutdown.", error);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Failed_to_write_data(this IInternalLogger logger, uv_handle_type handleType, WriteRequest request, Exception exception)
        {
            logger.Error($"{handleType} Failed to write data {request}.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void LoopRunDefaultError(this IInternalLogger logger, XThread thread, Exception ex)
        {
            logger.Error("Loop {}:run default error.", thread.Name, ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ShuttingDownLoopError(this IInternalLogger logger, Exception ex)
        {
            logger.Error("{}: shutting down loop error", ex);
        }

        #endregion
    }
}
