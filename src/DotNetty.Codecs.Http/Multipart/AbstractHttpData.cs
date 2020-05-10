// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http.Multipart
{
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public abstract class AbstractHttpData : AbstractReferenceCounted, IHttpData
    {
        static readonly Regex StripPattern = new Regex("(?:^\\s+|\\s+$|\\n)", RegexOptions.Compiled);
        static readonly Regex ReplacePattern = new Regex("[\\r\\t]", RegexOptions.Compiled);

        readonly string name;
        protected long DefinedSize;
        protected long Size;
        Encoding charset = HttpConstants.DefaultEncoding;
        bool completed;
        long maxSize = DefaultHttpDataFactory.MaxSize;

        protected AbstractHttpData(string name, Encoding charset, long size)
        {
            if (null == name) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }

            name = StripPattern.Replace(name, " ");
            name = ReplacePattern.Replace(name, "");
            if (string.IsNullOrEmpty(name))
            {
                ThrowHelper.ThrowArgumentException_Empty(ExceptionArgument.name);
            }

            this.name = name;
            if (charset is object)
            {
                this.charset = charset;
            }

            this.DefinedSize = size;
        }

        public long MaxSize
        {
            get => this.maxSize;
            set => this.maxSize = value;
        }

        public void CheckSize(long newSize) => CheckSize(newSize, this.maxSize);

        [MethodImpl(InlineMethod.Value)]
        internal static void CheckSize(long newSize, long maxSize)
        {
            if (maxSize >= 0 && newSize > maxSize)
            {
                ThrowHelper.ThrowIOException_CheckSize();
            }
        }

        public string Name => this.name;

        public bool IsCompleted => this.completed;

        protected void SetCompleted() => this.completed = true;

        public Encoding Charset
        {
            get => this.charset;
            set
            {
                if (null == value) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                this.charset = value;
            }
        }

        public long Length => this.Size;

        public long DefinedLength => this.DefinedSize;

        public IByteBuffer Content
        {
            get
            {
                try
                {
                    return this.GetByteBuffer();
                }
                catch (IOException e)
                {
                    return ThrowHelper.ThrowChannelException_IO<IByteBuffer>(e);
                }
            }
        }

        protected override void Deallocate() => this.Delete();

        public abstract int CompareTo(IInterfaceHttpData other);

        public abstract HttpDataType DataType { get; }

        public abstract IByteBufferHolder Copy();

        public abstract IByteBufferHolder Duplicate();

        public abstract IByteBufferHolder RetainedDuplicate();

        public abstract void SetContent(IByteBuffer buffer);

        public abstract void SetContent(Stream source);

        public abstract void AddContent(IByteBuffer buffer, bool last);

        public abstract void Delete();

        public abstract byte[] GetBytes();

        public abstract IByteBuffer GetByteBuffer();

        public abstract IByteBuffer GetChunk(int length);

        public virtual string GetString() => this.GetString(this.charset);

        public abstract string GetString(Encoding encoding);

        public abstract bool RenameTo(FileStream destination);

        public abstract bool IsInMemory { get; }

        public abstract FileStream GetFile();

        public abstract IByteBufferHolder Replace(IByteBuffer content);
    }
}
