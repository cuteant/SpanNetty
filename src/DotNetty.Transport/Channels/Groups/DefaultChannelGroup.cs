// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    public class DefaultChannelGroup : IChannelGroup, IComparable<IChannelGroup>
    {
        static int nextId;
        readonly IEventExecutor executor;
        readonly ConcurrentDictionary<IChannelId, IChannel> nonServerChannels = new ConcurrentDictionary<IChannelId, IChannel>(ChannelIdComparer.Default);
        readonly ConcurrentDictionary<IChannelId, IChannel> serverChannels = new ConcurrentDictionary<IChannelId, IChannel>(ChannelIdComparer.Default);

        public DefaultChannelGroup(IEventExecutor executor)
            : this($"group-{Interlocked.Increment(ref nextId):X2}", executor)
        {
        }

        public DefaultChannelGroup(string name, IEventExecutor executor)
        {
            if (name == null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name);
            }
            this.Name = name;
            this.executor = executor;
        }

        public bool IsEmpty => this.serverChannels.Count == 0 && this.nonServerChannels.Count == 0;

        public string Name { get; }

        public IChannel Find(IChannelId id)
        {
            IChannel channel;
            if (this.nonServerChannels.TryGetValue(id, out channel))
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
            Contract.Requires(message != null);
            Contract.Requires(matcher != null);
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
            Contract.Requires(message != null);
            Contract.Requires(matcher != null);
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
            Contract.Requires(matcher != null);
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
            Contract.Requires(matcher != null);
            var futures = new Dictionary<IChannel, Task>(ChannelComparer.Default);
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
            Contract.Requires(matcher != null);
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
            Contract.Requires(matcher != null);
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
            var buffer = message as IByteBuffer;
            if (buffer != null)
            {
                return buffer.Duplicate().Retain();
            }

            var byteBufferHolder = message as IByteBufferHolder;
            if (byteBufferHolder != null)
            {
                return byteBufferHolder.Duplicate().Retain();
            }

            return ReferenceCountUtil.Retain(message);
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
                channel.CloseCompletion.ContinueWith(ContinueRemoveChannelAction, Tuple.Create(this, channel), TaskContinuationOptions.ExecuteSynchronously);
#endif
            }
            return added;
        }

        static void ContinueRemoveChannelAction(Task t, object s)
        {
            var wrapped = (Tuple<DefaultChannelGroup, IChannel>)s;
            wrapped.Item1.Remove(wrapped.Item2);
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
            var id = o as IChannelId;
            if (id != null)
            {
                return this.Remove(id);
            }
            else
            {
                var channel = o as IChannel;
                if (channel != null)
                {
                    return this.Remove(channel);
                }
            }
            return false;
        }
    }
}