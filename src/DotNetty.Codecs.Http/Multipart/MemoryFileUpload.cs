// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.Multipart
{
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;

    public class MemoryFileUpload : AbstractMemoryHttpData, IFileUpload
    {
        string fileName;
        string contentType;
        string contentTransferEncoding;

        public MemoryFileUpload(string name, string fileName, string contentType, 
            string contentTransferEncoding, Encoding charset, long size)
            : base(name, charset, size)
        {
            if (fileName is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.fileName); }
            if (contentType is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.contentType); }

            this.fileName = fileName;
            this.contentType = contentType;
            this.contentTransferEncoding = contentTransferEncoding;
        }

        public override HttpDataType DataType => HttpDataType.FileUpload;

        public string FileName
        {
            get => this.fileName;
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                this.fileName = value;
            }
        }

        public override int GetHashCode() => FileUploadUtil.HashCode(this);

        public override bool Equals(object obj)
        {
            if (obj is IFileUpload fileUpload)
            {
                return FileUploadUtil.Equals(this, fileUpload);
            }
            return false;
        }

        public override int CompareTo(IInterfaceHttpData other)
        {
            if (other is IFileUpload fu)
            {
                return this.CompareTo(fu);
            }

            return ThrowHelper.ThrowArgumentException_CompareToHttpData(this.DataType, other.DataType);
        }

        public int CompareTo(IFileUpload other) => FileUploadUtil.CompareTo(this, other);

        public string ContentType
        {
            get => this.contentType;
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                this.contentType = value;
            }
        }

        public string ContentTransferEncoding
        {
            get => this.contentTransferEncoding;
            set => this.contentTransferEncoding = value;
        }

        public override string ToString()
        {
            return HttpHeaderNames.ContentDisposition + ": " +
                HttpHeaderValues.FormData + "; " + HttpHeaderValues.Name + "=\"" + this.Name +
                "\"; " + HttpHeaderValues.FileName + "=\"" + this.FileName + "\"\r\n" +
                HttpHeaderNames.ContentType + ": " + this.contentType +
                (this.Charset is object ? "; " + HttpHeaderValues.Charset + '=' + this.Charset.WebName + "\r\n" : "\r\n") +
                HttpHeaderNames.ContentLength + ": " + this.Length + "\r\n" +
                "Completed: " + this.IsCompleted +
                "\r\nIsInMemory: " + this.IsInMemory;
        }

        public override IByteBufferHolder Copy() => this.Replace(this.Content?.Copy());

        public override IByteBufferHolder Duplicate() => this.Replace(this.Content?.Duplicate());

        public override IByteBufferHolder RetainedDuplicate()
        {
            IByteBuffer content = this.Content;
            if (content is object)
            {
                content = content.RetainedDuplicate();
                bool success = false;
                try
                {
                    var duplicate = (IFileUpload)this.Replace(content);
                    success = true;
                    return duplicate;
                }
                finally
                {
                    if (!success)
                    {
                        _ = content.Release();
                    }
                }
            }
            else
            {
                return this.Replace(null);
            }
        }

        public override IByteBufferHolder Replace(IByteBuffer content)
        {
            var upload = new MemoryFileUpload(
                this.Name, this.FileName, this.ContentType, this.contentTransferEncoding, this.Charset, this.Size);
            if (content is object)
            {
                try
                {
                    upload.SetContent(content);
                    return upload;
                }
                catch (IOException e)
                {
                    ThrowHelper.ThrowChannelException_IO(e);
                }
            }
            return upload;
        }
    }
}
