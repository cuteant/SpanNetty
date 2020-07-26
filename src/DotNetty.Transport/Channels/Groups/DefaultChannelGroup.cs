// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    public class DefaultChannelGroup : IChannelGroup, IComparable<IChannelGroup>
    {
        private static readonly Action<Task, object> RemoveChannelAfterCloseAction = RemoveChannelAfterClose;
        private static int s_nextId;

        private readonly IEventExecutor _executor;
        private readonly ConcurrentDictionary<IChannelId, IChannel> _nonServerChannels;
        private readonly ConcurrentDictionary<IChannelId, IChannel> _serverChannels;
        private readonly bool _stayClosed;
        private int _closed;

        public DefaultChannelGroup()
            : this(false)
        {
        }

        public DefaultChannelGroup(bool stayClosed)
            : this(executor: null, stayClosed)
        {
        }

        public DefaultChannelGroup(IEventExecutor executor)
            : this(executor, false)
        {
        }

        public DefaultChannelGroup(IEventExecutor executor, bool stayClosed)
            : this($"group-{Interlocked.Increment(ref s_nextId):X2}", executor, stayClosed)
        {
        }

        public DefaultChannelGroup(string name, bool stayClosed)
            : this(name, null, stayClosed)
        {
        }

        public DefaultChannelGroup(string name, IEventExecutor executor)
            : this(name, executor, false)
        {
        }

        public DefaultChannelGroup(string name, IEventExecutor executor, bool stayClosed)
        {
            if (name is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }

            _nonServerChannels = new ConcurrentDictionary<IChannelId, IChannel>(ChannelIdComparer.Default);
            _serverChannels = new ConcurrentDictionary<IChannelId, IChannel>(ChannelIdComparer.Default);

            Name = name;
            _executor = executor;
            _stayClosed = stayClosed;
        }

        public bool IsEmpty => 0u >= (uint)_serverChannels.Count && 0u >= (uint)_nonServerChannels.Count;

        public string Name { get; }

        public IChannel Find(IChannelId id)
        {
            if (_nonServerChannels.TryGetValue(id, out IChannel channel))
            {
                return channel;
            }
            else
            {
                _ = _serverChannels.TryGetValue(id, out channel);
                return channel;
            }
        }

        public Task WriteAsync(object message) => WriteAsync(message, ChannelMatchers.All(), false);

        public Task WriteAsync(object message, IChannelMatcher matcher) => WriteAsync(message, matcher, false);

        public Task WriteAsync(object message, IChannelMatcher matcher, bool voidPromise)
        {
            if (message is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.message); }
            if (matcher is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }

            Task result;
            if (voidPromise)
            {
                foreach (IChannel c in _nonServerChannels.Values)
                {
                    if (matcher.Matches(c))
                    {
                        _ = c.WriteAsync(SafeDuplicate(message), c.VoidPromise());
                    }
                }

                result = TaskUtil.Completed;
            }
            else
            {
                var futures = new Dictionary<IChannel, Task>(_nonServerChannels.Count, ChannelComparer.Default);

                foreach (IChannel c in _nonServerChannels.Values)
                {
                    if (matcher.Matches(c))
                    {
                        futures.Add(c, c.WriteAsync(SafeDuplicate(message)));
                    }
                }
                result = new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
            }

            _ = ReferenceCountUtil.Release(message);
            return result;
        }

        public IChannelGroup Flush(IChannelMatcher matcher)
        {
            foreach (IChannel c in _nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    _ = c.Flush();
                }
            }
            return this;
        }

        public IChannelGroup Flush() => Flush(ChannelMatchers.All());

        public int CompareTo(IChannelGroup other)
        {
            int v = string.Compare(Name, other.Name, StringComparison.Ordinal);
            if (v != 0)
            {
                return v;
            }

            return GetHashCode() - other.GetHashCode();
        }

        void ICollection<IChannel>.Add(IChannel item) => Add(item);

        public void Clear()
        {
            _serverChannels.Clear();
            _nonServerChannels.Clear();
        }

        public bool Contains(IChannel item)
        {
            IChannel channel;
            if (item is IServerChannel)
            {
                return _serverChannels.TryGetValue(item.Id, out channel) && channel == item;
            }
            else
            {
                return _nonServerChannels.TryGetValue(item.Id, out channel) && channel == item;
            }
        }

        public void CopyTo(IChannel[] array, int arrayIndex) => ToArray().CopyTo(array, arrayIndex);

        public int Count => _nonServerChannels.Count + _serverChannels.Count;

        public bool IsReadOnly => false;

        public bool Remove(IChannel channel)
        {
            //IChannel ch;
            if (channel is IServerChannel)
            {
                return _serverChannels.TryRemove(channel.Id, out _);
            }
            else
            {
                return _nonServerChannels.TryRemove(channel.Id, out _);
            }
        }

        public IEnumerator<IChannel> GetEnumerator() => new CombinedEnumerator<IChannel>(_serverChannels.Values.GetEnumerator(),
            _nonServerChannels.Values.GetEnumerator());

        IEnumerator IEnumerable.GetEnumerator() => new CombinedEnumerator<IChannel>(_serverChannels.Values.GetEnumerator(),
            _nonServerChannels.Values.GetEnumerator());

        public Task WriteAndFlushAsync(object message) => WriteAndFlushAsync(message, ChannelMatchers.All(), false);

        public Task WriteAndFlushAsync(object message, IChannelMatcher matcher) => WriteAndFlushAsync(message, matcher, false);

        public Task WriteAndFlushAsync(object message, IChannelMatcher matcher, bool voidPromise)
        {
            if (message is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.message); }
            if (matcher is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }

            Task result;
            if (voidPromise)
            {
                foreach (IChannel c in _nonServerChannels.Values)
                {
                    if (matcher.Matches(c))
                    {
                        _ = c.WriteAndFlushAsync(SafeDuplicate(message), c.VoidPromise());
                    }
                }

                result = TaskUtil.Completed;
            }
            else
            {
                var futures = new Dictionary<IChannel, Task>(_nonServerChannels.Count, ChannelComparer.Default);
                foreach (IChannel c in _nonServerChannels.Values)
                {
                    if (matcher.Matches(c))
                    {
                        futures.Add(c, c.WriteAndFlushAsync(SafeDuplicate(message)));
                    }
                }

                result = new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
            }

            _ = ReferenceCountUtil.Release(message);
            return result;
        }

        public Task DisconnectAsync() => DisconnectAsync(ChannelMatchers.All());

        public Task DisconnectAsync(IChannelMatcher matcher)
        {
            if (matcher is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in _nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }
            foreach (IChannel c in _serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task CloseAsync() => CloseAsync(ChannelMatchers.All());

        public Task CloseAsync(IChannelMatcher matcher)
        {
            if (matcher is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);

            if (_stayClosed)
            {
                // It is important to set the closed to true, before closing channels.
                // Our invariants are:
                // closed=true happens-before ChannelGroup.close()
                // ChannelGroup.add() happens-before checking closed==true
                //
                // See https://github.com/netty/netty/issues/4020
                _ = Interlocked.Exchange(ref _closed, SharedConstants.True);
            }

            foreach (IChannel c in _nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }
            foreach (IChannel c in _serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task DeregisterAsync() => DeregisterAsync(ChannelMatchers.All());

        public Task DeregisterAsync(IChannelMatcher matcher)
        {
            if (matcher is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in _nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }
            foreach (IChannel c in _serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task NewCloseFuture() => NewCloseFuture(ChannelMatchers.All());

        public Task NewCloseFuture(IChannelMatcher matcher)
        {
            if (matcher is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in _nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseCompletion);
                }
            }
            foreach (IChannel c in _serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseCompletion);
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        static object SafeDuplicate(object message)
        {
            switch (message)
            {
                case IByteBuffer buffer:
                    return buffer.RetainedDuplicate();

                case IByteBufferHolder byteBufferHolder:
                    return byteBufferHolder.RetainedDuplicate();

                default:
                    return ReferenceCountUtil.Retain(message);
            }
        }

        public override string ToString() => $"{GetType().Name}(name: {Name}, size: {Count})";

        public bool Add(IChannel channel)
        {
            ConcurrentDictionary<IChannelId, IChannel> map = channel is IServerChannel ? _serverChannels : _nonServerChannels;
            bool added = map.TryAdd(channel.Id, channel);
            if (added)
            {
                _ = channel.CloseCompletion.ContinueWith(RemoveChannelAfterCloseAction, new Tuple<DefaultChannelGroup, IChannel>(this, channel), TaskContinuationOptions.ExecuteSynchronously);
            }

            if (_stayClosed && (SharedConstants.False < (uint)Volatile.Read(ref _closed)))
            {

                // First add channel, than check if closed.
                // Seems inefficient at first, but this way a volatile
                // gives us enough synchronization to be thread-safe.
                //
                // If true: Close right away.
                // (Might be closed a second time by ChannelGroup.close(), but this is ok)
                //
                // If false: Channel will definitely be closed by the ChannelGroup.
                // (Because closed=true always happens-before ChannelGroup.close())
                //
                // See https://github.com/netty/netty/issues/4020
                _ = channel.CloseAsync();
            }

            return added;
        }

        static void RemoveChannelAfterClose(Task t, object s)
        {
            var wrapped = (Tuple<DefaultChannelGroup, IChannel>)s;
            _ = wrapped.Item1.Remove(wrapped.Item2);
        }

        public IChannel[] ToArray()
        {
            var channels = new List<IChannel>(Count);
            channels.AddRange(_serverChannels.Values);
            channels.AddRange(_nonServerChannels.Values);
            return channels.ToArray();
        }

        public bool Remove(IChannelId channelId)
        {
            //IChannel ch;

            if (_serverChannels.TryRemove(channelId, out _))
            {
                return true;
            }

            if (_nonServerChannels.TryRemove(channelId, out _))
            {
                return true;
            }

            return false;
        }

        public bool Remove(object o)
        {
            if (o is IChannelId id)
            {
                return Remove(id);
            }
            else
            {
                if (o is IChannel channel)
                {
                    return Remove(channel);
                }
            }
            return false;
        }
    }
}