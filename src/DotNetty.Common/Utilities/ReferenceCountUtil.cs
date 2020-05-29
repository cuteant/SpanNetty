// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Internal.Logging;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public static class ReferenceCountUtil
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(ReferenceCountUtil));

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Retain()"/> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static T Retain<T>(T msg)
        {
            return msg is IReferenceCounted counted ? (T)counted.Retain() : msg;
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Retain(int)"/> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static T Retain<T>(T msg, int increment)
        {
            return msg is IReferenceCounted counted ? (T)counted.Retain(increment) : msg;
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Touch()" /> if the specified message implements
        /// <see cref="IReferenceCounted" />.
        /// If the specified message doesn't implement <see cref="IReferenceCounted" />, this method does nothing.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static T Touch<T>(T msg)
        {
            return msg is IReferenceCounted refCnt ? (T)refCnt.Touch() : msg;
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Touch(object)" /> if the specified message implements
        /// <see cref="IReferenceCounted" />. If the specified message doesn't implement
        /// <see cref="IReferenceCounted" />, this method does nothing.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static T Touch<T>(T msg, object hint)
        {
            return msg is IReferenceCounted refCnt ? (T)refCnt.Touch(hint) : msg;
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Release()" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static bool Release(object msg)
        {
            return msg is IReferenceCounted counted && counted.Release();
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Release(int)" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static bool Release(object msg, int decrement)
        {
            return msg is IReferenceCounted counted && counted.Release(decrement);
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Release()" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing. Unlike <see cref="Release(object)"/>, this
        /// method catches an exception raised by <see cref="IReferenceCounted.Release()" /> and logs it, rather than
        /// rethrowing it to the caller. It is usually recommended to use <see cref="Release(object)"/> instead, unless
        /// you absolutely need to swallow an exception.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static void SafeRelease(object msg)
        {
            try
            {
                Release(msg);
            }
            catch (Exception ex)
            {
                Logger.FailedToReleaseAMessage(msg, ex);
            }
        }

        /// <summary>
        /// Tries to call <see cref="IReferenceCounted.Release(int)" /> if the specified message implements
        /// <see cref="IReferenceCounted"/>. If the specified message doesn't implement
        /// <see cref="IReferenceCounted"/>, this method does nothing. Unlike <see cref="Release(object)"/>, this
        /// method catches an exception raised by <see cref="IReferenceCounted.Release(int)" /> and logs it, rather
        /// than rethrowing it to the caller. It is usually recommended to use <see cref="Release(object, int)"/>
        /// instead, unless you absolutely need to swallow an exception.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static void SafeRelease(object msg, int decrement)
        {
            try
            {
                Release(msg, decrement);
            }
            catch (Exception ex)
            {
                if (Logger.WarnEnabled)
                {
                    Logger.FailedToReleaseAMessage(msg, decrement, ex);
                }
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static void SafeRelease(this IReferenceCounted msg)
        {
            try
            {
                msg?.Release();
            }
            catch (Exception ex)
            {
                Logger.FailedToReleaseAMessage(msg, ex);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static void SafeRelease(this IReferenceCounted msg, int decrement)
        {
            try
            {
                msg?.Release(decrement);
            }
            catch (Exception ex)
            {
                Logger.FailedToReleaseAMessage(msg, decrement, ex);
            }
        }

        /// <summary>
        /// Schedules the specified object to be released when the caller thread terminates. Note that this operation
        /// is intended to simplify reference counting of ephemeral objects during unit tests. Do not use it beyond the
        /// intended use case.
        /// </summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static T ReleaseLater<T>(T msg) => ReleaseLater(msg, 1);

        /// <summary>
        /// Schedules the specified object to be released when the caller thread terminates. Note that this operation
        /// is intended to simplify reference counting of ephemeral objects during unit tests. Do not use it beyond the
        /// intended use case.
        /// </summary>
        public static T ReleaseLater<T>(T msg, int decrement)
        {
            if (msg is IReferenceCounted referenceCounted)
            {
                ThreadDeathWatcher.Watch(Thread.CurrentThread, () =>
                {
                    try
                    {
                        if (!referenceCounted.Release(decrement))
                        {
                            Logger.NonZeroRefCnt(referenceCounted, decrement);
                        }
                        else
                        {
                            if (Logger.DebugEnabled) Logger.ReleasedObject(referenceCounted, decrement);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.FailedToReleaseAObject(referenceCounted, ex);
                    }
                });
            }
            return msg;
        }

        internal static string FormatReleaseString(IReferenceCounted referenceCounted, int decrement)
            => $"{referenceCounted.GetType().Name}.Release({decrement.ToString(CultureInfo.InvariantCulture)}) refCnt: {referenceCounted.ReferenceCount.ToString(CultureInfo.InvariantCulture)}";
    }
}