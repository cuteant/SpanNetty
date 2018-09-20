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

    public partial class DefaultChannelGroup : IChannelGroup, IComparable<IChannelGroup>
    {
        static int nextId;
        readonly IEventExecutor executor;
        readonly ConcurrentDictionary<IChannelId, IChannel> nonServerChannels = new ConcurrentDictionary<IChannelId, IChannel>(ChannelIdComparer.Default);
        readonly ConcurrentDictionary<IChannelId, IChannel> serverChannels = new ConcurrentDictionary<IChannelId, IChannel>(ChannelIdComparer.Default);
        readonly bool stayClosed;
        int closed;

        public DefaultChannelGroup(IEventExecutor executor)
            : this(executor, false)
        {
        }

        public DefaultChannelGroup(string name, IEventExecutor executor)
            : this(name, executor, false)
        {
        }

        public DefaultChannelGroup(IEventExecutor executor, bool stayClosed)
            : this($"group-{Interlocked.Increment(ref nextId):X2}", executor, stayClosed)
        {
        }

        public DefaultChannelGroup(string name, IEventExecutor executor, bool stayClosed)
        {
            if (name == null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }

            this.Name = name;
            this.executor = executor;
            this.stayClosed = stayClosed;
        }

        public bool IsEmpty => this.serverChannels.Count == 0 && this.nonServerChannels.Count == 0;

        public string Name { get; }

        public IChannel Find(IChannelId id)
        {
            if (this.nonServerChannels.TryGetValue(id, out IChannel channel))
            {
                return channel;
            }
            else
            {
                this.serverChannels.TryGetValue(id, out channel);
                return channel;
            }
        }

        public Task WriteAsync(object message) => this.WriteAsync(message, ChannelMatchers.All());

        public Task WriteAsync(object message, IChannelMatcher matcher)
        {
            if (null == message) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.message); }
            if (null == matcher) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.WriteAsync(SafeDuplicate(message)));
                }
            }

            ReferenceCountUtil.Release(message);
            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public IChannelGroup Flush(IChannelMatcher matcher)
        {
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    c.Flush();
                }
            }
            return this;
        }

        public IChannelGroup Flush() => this.Flush(ChannelMatchers.All());

        public int CompareTo(IChannelGroup other)
        {
            int v = string.Compare(this.Name, other.Name, StringComparison.Ordinal);
            if (v != 0)
            {
                return v;
            }

            return this.GetHashCode() - other.GetHashCode();
        }

        void ICollection<IChannel>.Add(IChannel item) => this.Add(item);

        public void Clear()
        {
            this.serverChannels.Clear();
            this.nonServerChannels.Clear();
        }

        public bool Contains(IChannel item)
        {
            IChannel channel;
            if (item is IServerChannel)
            {
                return this.serverChannels.TryGetValue(item.Id, out channel) && channel == item;
            }
            else
            {
                return this.nonServerChannels.TryGetValue(item.Id, out channel) && channel == item;
            }
        }

        public void CopyTo(IChannel[] array, int arrayIndex) => this.ToArray().CopyTo(array, arrayIndex);

        public int Count => this.nonServerChannels.Count + this.serverChannels.Count;

        public bool IsReadOnly => false;

        public bool Remove(IChannel channel)
        {
            //IChannel ch;
            if (channel is IServerChannel)
            {
                return this.serverChannels.TryRemove(channel.Id, out _);
            }
            else
            {
                return this.nonServerChannels.TryRemove(channel.Id, out _);
            }
        }

        public IEnumerator<IChannel> GetEnumerator() => new CombinedEnumerator<IChannel>(this.serverChannels.Values.GetEnumerator(),
            this.nonServerChannels.Values.GetEnumerator());

        IEnumerator IEnumerable.GetEnumerator() => new CombinedEnumerator<IChannel>(this.serverChannels.Values.GetEnumerator(),
            this.nonServerChannels.Values.GetEnumerator());

        public Task WriteAndFlushAsync(object message) => this.WriteAndFlushAsync(message, ChannelMatchers.All());

        public Task WriteAndFlushAsync(object message, IChannelMatcher matcher)
        {
            if (null == message) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.message); }
            if (null == matcher) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.WriteAndFlushAsync(SafeDuplicate(message)));
                }
            }

            ReferenceCountUtil.Release(message);
            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task DisconnectAsync() => this.DisconnectAsync(ChannelMatchers.All());

        public Task DisconnectAsync(IChannelMatcher matcher)
        {
            if (null == matcher) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DisconnectAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task CloseAsync() => this.CloseAsync(ChannelMatchers.All());

        public Task CloseAsync(IChannelMatcher matcher)
        {
            if (null == matcher) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);

            if (this.stayClosed)
            {
                // It is important to set the closed to true, before closing channels.
                // Our invariants are:
                // closed=true happens-before ChannelGroup.close()
                // ChannelGroup.add() happens-before checking closed==true
                //
                // See https://github.com/netty/netty/issues/4020
                Interlocked.Exchange(ref this.closed, Constants.True);
            }

            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task DeregisterAsync() => this.DeregisterAsync(ChannelMatchers.All());

        public Task DeregisterAsync(IChannelMatcher matcher)
        {
            if (null == matcher) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.DeregisterAsync());
                }
            }

            return new DefaultChannelGroupCompletionSource(this, futures /*, this.executor*/).Task;
        }

        public Task NewCloseFuture() => this.NewCloseFuture(ChannelMatchers.All());

        public Task NewCloseFuture(IChannelMatcher matcher)
        {
            if (null == matcher) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.matcher); }
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
            foreach (IChannel c in this.nonServerChannels.Values)
            {
                if (matcher.Matches(c))
                {
                    futures.Add(c, c.CloseCompletion);
                }
            }
            foreach (IChannel c in this.serverChannels.Values)
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

        public override string ToString() => $"{this.GetType().Name}(name: {this.Name}, size: {this.Count})";

        public bool Add(IChannel channel)
        {
            ConcurrentDictionary<IChannelId, IChannel> map = channel is IServerChannel ? this.serverChannels : this.nonServerChannels;
            bool added = map.TryAdd(channel.Id, channel);
            if (added)
            {
#if NET40
                void continueRemoveChannelAction(Task t) => this.Remove(channel);
                channel.CloseCompletion.ContinueWith(continueRemoveChannelAction, TaskContinuationOptions.ExecuteSynchronously);
#else
                channel.CloseCompletion.ContinueWith(RemoveChannelAfterCloseAction, new Tuple<DefaultChannelGroup, IChannel>(this, channel), TaskContinuationOptions.ExecuteSynchronously);
#endif
            }

            if (this.stayClosed && (Constants.True == Volatile.Read(ref this.closed)))
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
                channel.CloseAsync();
            }

            return added;
        }

        public IChannel[] ToArray()
        {
            var channels = new List<IChannel>(this.Count);
            channels.AddRange(this.serverChannels.Values);
            channels.AddRange(this.nonServerChannels.Values);
            return channels.ToArray();
        }

        public bool Remove(IChannelId channelId)
        {
            //IChannel ch;

            if (this.serverChannels.TryRemove(channelId, out _))
            {
                return true;
            }

            if (this.nonServerChannels.TryRemove(channelId, out _))
            {
                return true;
            }

            return false;
        }

        public bool Remove(object o)
        {
            if (o is IChannelId id)
            {
                return this.Remove(id);
            }
            else
            {
                if (o is IChannel channel)
                {
                    return this.Remove(channel);
                }
            }
            return false;
        }
    }
}