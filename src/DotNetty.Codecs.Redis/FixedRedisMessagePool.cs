// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Redis.Messages;

    public sealed class FixedRedisMessagePool : IRedisMessagePool
    {
#if NET40
        readonly IDictionary<string, SimpleStringRedisMessage> stringToSimpleStringMessages;
        readonly IDictionary<IByteBuffer, SimpleStringRedisMessage> byteBufferToSimpleStringMessages;

        readonly IDictionary<string, ErrorRedisMessage> stringToErrorMessages;
        readonly IDictionary<IByteBuffer, ErrorRedisMessage> byteBufferToErrorMessages;

        readonly IDictionary<long, IntegerRedisMessage> longToIntegerMessages;
        readonly IDictionary<long, byte[]> longToBytes;
        readonly IDictionary<IByteBuffer, IntegerRedisMessage> byteBufferToIntegerMessages;
#else
        readonly IDictionary<string, SimpleStringRedisMessage> stringToSimpleStringMessages;
        readonly IReadOnlyDictionary<IByteBuffer, SimpleStringRedisMessage> byteBufferToSimpleStringMessages;

        readonly IDictionary<string, ErrorRedisMessage> stringToErrorMessages;
        readonly IReadOnlyDictionary<IByteBuffer, ErrorRedisMessage> byteBufferToErrorMessages;

        readonly IReadOnlyDictionary<long, IntegerRedisMessage> longToIntegerMessages;
        readonly IReadOnlyDictionary<long, byte[]> longToBytes;
        readonly IReadOnlyDictionary<IByteBuffer, IntegerRedisMessage> byteBufferToIntegerMessages;
#endif

        static readonly FixedRedisMessagePool Instance;

        static FixedRedisMessagePool()
        {
            Instance = new FixedRedisMessagePool();
        }

        public static IRedisMessagePool Default => Instance;

        static readonly string[] SimpleStrings =
        {
            "OK",
            "PONG",
            "QUEUED"
        };

        static readonly string[] Errors =
        {
            "ERR",
            "ERR index out of range",
            "ERR no such key",
            "ERR source and destination objects are the same",
            "ERR syntax error",
            "BUSY Redis is busy running a script. You can only call SCRIPT KILL or SHUTDOWN NOSAVE.",
            "BUSYKEY Target key name already exists.",
            "EXECABORT Transaction discarded because of previous errors.",
            "LOADING Redis is loading the dataset in memory",
            "MASTERDOWN Link with MASTER is down and slave-serve-stale-data is set to 'no'.",
            "MISCONF Redis is configured to save RDB snapshots, but is currently not able to persist on disk. "
            + "Commands that may modify the data set are disabled. Please check Redis logs for details "
            + "about the error.",
            "NOAUTH Authentication required.",
            "NOREPLICAS Not enough good slaves to write.",
            "NOSCRIPT No matching script. Please use EVAL.",
            "OOM command not allowed when used memory > 'maxmemory'.",
            "READONLY You can't write against a read only slave.",
            "WRONGTYPE Operation against a key holding the wrong kind of value"
        };

        FixedRedisMessagePool()
        {
            var stringToSimpleStringValues = new Dictionary<string, SimpleStringRedisMessage>(System.StringComparer.Ordinal);
            var byteBufferToSimpleStringValues = new Dictionary<IByteBuffer, SimpleStringRedisMessage>();

            foreach (string simpleString in SimpleStrings)
            {
                IByteBuffer key = Unpooled.UnreleasableBuffer(
                    Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(simpleString)));

                var redisMessage = new SimpleStringRedisMessage(simpleString);
                stringToSimpleStringValues.Add(simpleString, redisMessage);
                byteBufferToSimpleStringValues.Add(key, redisMessage);
            }
#if NET40
            this.stringToSimpleStringMessages = new Dictionary<string, SimpleStringRedisMessage>(stringToSimpleStringValues, System.StringComparer.Ordinal);
            this.byteBufferToSimpleStringMessages = new Dictionary<IByteBuffer, SimpleStringRedisMessage>(byteBufferToSimpleStringValues);
#else
            this.stringToSimpleStringMessages = new Dictionary<string, SimpleStringRedisMessage>(stringToSimpleStringValues, System.StringComparer.Ordinal);
            this.byteBufferToSimpleStringMessages = new ReadOnlyDictionary<IByteBuffer, SimpleStringRedisMessage>(byteBufferToSimpleStringValues);
#endif

            var errorToErrorValues = new Dictionary<string, ErrorRedisMessage>(System.StringComparer.Ordinal);
            var byteBufferToErrorValues = new Dictionary<IByteBuffer, ErrorRedisMessage>();
            foreach (string error in Errors)
            {
                IByteBuffer key = Unpooled.UnreleasableBuffer(
                    Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(error)));

                var redisMessage = new ErrorRedisMessage(error);
                errorToErrorValues.Add(error, redisMessage);
                byteBufferToErrorValues.Add(key, redisMessage);
            }
#if NET40
            this.stringToErrorMessages = new Dictionary<string, ErrorRedisMessage>(errorToErrorValues, System.StringComparer.Ordinal);
            this.byteBufferToErrorMessages = new Dictionary<IByteBuffer, ErrorRedisMessage>(byteBufferToErrorValues);
#else
            this.stringToErrorMessages = new Dictionary<string, ErrorRedisMessage>(errorToErrorValues, System.StringComparer.Ordinal);
            this.byteBufferToErrorMessages = new ReadOnlyDictionary<IByteBuffer, ErrorRedisMessage>(byteBufferToErrorValues);
#endif

            var longToIntegerValues = new Dictionary<long, IntegerRedisMessage>();
            var longToByteBufferValues = new Dictionary<long, byte[]>();
            var byteBufferToIntegerValues = new Dictionary<IByteBuffer, IntegerRedisMessage>();

            for (long value = MinimumCachedIntegerNumber; value < MaximumCachedIntegerNumber; value++)
            {
                byte[] bytes = RedisCodecUtil.LongToAsciiBytes(value);
                IByteBuffer key = Unpooled.UnreleasableBuffer(
                    Unpooled.WrappedBuffer(bytes));

                var redisMessage = new IntegerRedisMessage(value);
                longToIntegerValues.Add(value, redisMessage);
                longToByteBufferValues.Add(value, bytes);
                byteBufferToIntegerValues.Add(key, redisMessage);
            }

#if NET40
            this.longToIntegerMessages = new Dictionary<long, IntegerRedisMessage>(longToIntegerValues);
            this.longToBytes = new Dictionary<long, byte[]>(longToByteBufferValues);
            this.byteBufferToIntegerMessages = new Dictionary<IByteBuffer, IntegerRedisMessage>(byteBufferToIntegerValues);
#else
            this.longToIntegerMessages = new ReadOnlyDictionary<long, IntegerRedisMessage>(longToIntegerValues);
            this.longToBytes = new ReadOnlyDictionary<long, byte[]>(longToByteBufferValues);
            this.byteBufferToIntegerMessages = new ReadOnlyDictionary<IByteBuffer, IntegerRedisMessage>(byteBufferToIntegerValues);
#endif
        }

        static readonly long MinimumCachedIntegerNumber = RedisConstants.NullValue; // inclusive
        const long MaximumCachedIntegerNumber = 128; // exclusive

        public bool TryGetMessage(string content, out SimpleStringRedisMessage message)
            => this.stringToSimpleStringMessages.TryGetValue(content, out message);

        public bool TryGetMessage(IByteBuffer content, out SimpleStringRedisMessage message)
            => this.byteBufferToSimpleStringMessages.TryGetValue(content, out message);

        public bool TryGetMessage(string content, out ErrorRedisMessage message)
            => this.stringToErrorMessages.TryGetValue(content, out message);

        public bool TryGetMessage(IByteBuffer content, out ErrorRedisMessage message)
            => this.byteBufferToErrorMessages.TryGetValue(content, out message);

        public bool TryGetMessage(long value, out IntegerRedisMessage message)
            => this.longToIntegerMessages.TryGetValue(value, out message);

        public bool TryGetMessage(IByteBuffer content, out IntegerRedisMessage message)
            => this.byteBufferToIntegerMessages.TryGetValue(content, out message);

        public bool TryGetBytes(long value, out byte[] bytes)
            => this.longToBytes.TryGetValue(value, out bytes);
    }
}