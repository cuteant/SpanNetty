// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable RedundantAssignment
namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public class HttpPostStandardRequestDecoder : IInterfaceHttpPostRequestDecoder
    {
        // Factory used to create InterfaceHttpData
        readonly IHttpDataFactory factory;

        // Request to decode
        readonly IHttpRequest request;

        // Default charset to use
        readonly Encoding charset;

        // Does the last chunk already received
        bool isLastChunk;

        // HttpDatas from Body
        readonly List<IInterfaceHttpData> bodyListHttpData = new List<IInterfaceHttpData>();

        //  HttpDatas as Map from Body
        readonly Dictionary<ICharSequence, List<IInterfaceHttpData>> bodyMapHttpData = new Dictionary<ICharSequence, List<IInterfaceHttpData>>(CaseIgnoringComparator.Default);

        // The current channelBuffer
        IByteBuffer undecodedChunk;

        // Body HttpDatas current position
        int bodyListHttpDataRank;

        // Current getStatus
        MultiPartStatus currentStatus = MultiPartStatus.Notstarted;

        // The current Attribute that is currently in decode process
        IAttribute currentAttribute;

        bool destroyed;

        int discardThreshold = HttpPostRequestDecoder.DefaultDiscardThreshold;

        public HttpPostStandardRequestDecoder(IHttpRequest request)
            : this(new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize), request, HttpConstants.DefaultEncoding)
        {
        }

        public HttpPostStandardRequestDecoder(IHttpDataFactory factory, IHttpRequest request)
            : this(factory, request, HttpConstants.DefaultEncoding)
        {
        }

        public HttpPostStandardRequestDecoder(IHttpDataFactory factory, IHttpRequest request, Encoding charset)
        {
            Contract.Requires(request != null);
            Contract.Requires(charset != null);
            Contract.Requires(factory != null);

            this.factory = factory;
            this.request = request;
            this.charset = charset;
            if (request is IHttpContent content)
            {
                // Offer automatically if the given request is als type of HttpContent
                // See #1089
                this.Offer(content);
            }
            else
            {
                this.undecodedChunk = Unpooled.Buffer();
                this.ParseBody();
            }
        }

        void CheckDestroyed()
        {
            if (this.destroyed)
            {
                ThrowHelper.ThrowInvalidOperationException_CheckDestroyed<HttpPostStandardRequestDecoder>();
            }
        }

        public bool IsMultipart
        {
            get
            {
                this.CheckDestroyed();
                return false;
            }
        }

        public int DiscardThreshold
        {
            get => this.discardThreshold;
            set
            {
                Contract.Requires(value >= 0);
                this.discardThreshold = value;
            }
        }

        public List<IInterfaceHttpData> GetBodyHttpDatas()
        {
            this.CheckDestroyed();

            if (!this.isLastChunk)
            {
                ThrowHelper.ThrowNotEnoughDataDecoderException(ExceptionArgument.HttpPostStandardRequestDecoder);
            }
            return this.bodyListHttpData;
        }

        public List<IInterfaceHttpData> GetBodyHttpDatas(AsciiString name)
        {
            this.CheckDestroyed();

            if (!this.isLastChunk)
            {
                ThrowHelper.ThrowNotEnoughDataDecoderException(ExceptionArgument.HttpPostStandardRequestDecoder);
            }
            return this.bodyMapHttpData[name];
        }

        public IInterfaceHttpData GetBodyHttpData(AsciiString name)
        {
            this.CheckDestroyed();

            if (!this.isLastChunk)
            {
                ThrowHelper.ThrowNotEnoughDataDecoderException(ExceptionArgument.HttpPostStandardRequestDecoder);
            }

            if (this.bodyMapHttpData.TryGetValue(name, out List<IInterfaceHttpData> list))
            {
                return list[0];
            }
            return null;
        }

        public IInterfaceHttpPostRequestDecoder Offer(IHttpContent content)
        {
            this.CheckDestroyed();

            // Maybe we should better not copy here for performance reasons but this will need
            // more care by the caller to release the content in a correct manner later
            // So maybe something to optimize on a later stage
            IByteBuffer buf = content.Content;
            if (this.undecodedChunk == null)
            {
                this.undecodedChunk = buf.Copy();
            }
            else
            {
                this.undecodedChunk.WriteBytes(buf);
            }

            if (content is ILastHttpContent)
            {
                this.isLastChunk = true;
            }

            this.ParseBody();
            if (this.undecodedChunk != null && this.undecodedChunk.WriterIndex > this.discardThreshold)
            {
                this.undecodedChunk.DiscardReadBytes();
            }

            return this;
        }

        public bool HasNext
        {
            get
            {
                this.CheckDestroyed();

                if (this.currentStatus == MultiPartStatus.Epilogue)
                {
                    // OK except if end of list
                    if (this.bodyListHttpDataRank >= this.bodyListHttpData.Count)
                    {
                        ThrowHelper.ThrowEndOfDataDecoderException_HttpPostStandardRequestDecoder();
                    }
                }

                return this.bodyListHttpData.Count > 0 && this.bodyListHttpDataRank < this.bodyListHttpData.Count;
            }
        }

        public IInterfaceHttpData Next()
        {
            this.CheckDestroyed();

            return this.HasNext
                ? this.bodyListHttpData[this.bodyListHttpDataRank++]
                : null;
        }

        public IInterfaceHttpData CurrentPartialHttpData => this.currentAttribute;

        void ParseBody()
        {
            if (this.currentStatus == MultiPartStatus.PreEpilogue || this.currentStatus == MultiPartStatus.Epilogue)
            {
                if (this.isLastChunk)
                {
                    this.currentStatus = MultiPartStatus.Epilogue;
                }

                return;
            }
            this.ParseBodyAttributes();
        }

        protected void AddHttpData(IInterfaceHttpData data)
        {
            if (data == null)
            {
                return;
            }
            ICharSequence name = new StringCharSequence(data.Name);
            if (!this.bodyMapHttpData.TryGetValue(name, out List<IInterfaceHttpData> datas))
            {
                datas = new List<IInterfaceHttpData>(1);
                this.bodyMapHttpData.Add(name, datas);
            }
            datas.Add(data);
            this.bodyListHttpData.Add(data);
        }

        void ParseBodyAttributesStandard()
        {
            int firstpos = this.undecodedChunk.ReaderIndex;
            int currentpos = firstpos;
            if (this.currentStatus == MultiPartStatus.Notstarted)
            {
                this.currentStatus = MultiPartStatus.Disposition;
            }
            bool contRead = true;
            try
            {
                int ampersandpos;
                while (this.undecodedChunk.IsReadable() && contRead)
                {
                    char read = (char)this.undecodedChunk.ReadByte();
                    currentpos++;
                    switch (this.currentStatus)
                    {
                        case MultiPartStatus.Disposition:// search '='
                            switch (read)
                            {
                                case '=':
                                    this.currentStatus = MultiPartStatus.Field;
                                    int equalpos = currentpos - 1;
                                    string key = DecodeAttribute(this.undecodedChunk.ToString(firstpos, equalpos - firstpos, this.charset), this.charset);
                                    this.currentAttribute = this.factory.CreateAttribute(this.request, key);
                                    firstpos = currentpos;
                                    break;

                                case '&':
                                    // special empty FIELD
                                    this.currentStatus = MultiPartStatus.Disposition;
                                    ampersandpos = currentpos - 1;
                                    string key0 = DecodeAttribute(this.undecodedChunk.ToString(firstpos, ampersandpos - firstpos, this.charset), this.charset);
                                    this.currentAttribute = this.factory.CreateAttribute(this.request, key0);
                                    this.currentAttribute.Value = ""; // empty
                                    this.AddHttpData(this.currentAttribute);
                                    this.currentAttribute = null;
                                    firstpos = currentpos;
                                    contRead = true;
                                    break;
                            }
                            break;
                        case MultiPartStatus.Field:// search '&' or end of line
                            switch (read)
                            {
                                case '&':
                                    this.currentStatus = MultiPartStatus.Disposition;
                                    ampersandpos = currentpos - 1;
                                    this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                                    firstpos = currentpos;
                                    contRead = true;
                                    break;

                                case HttpConstants.CarriageReturnChar:
                                    if (this.undecodedChunk.IsReadable())
                                    {
                                        read = (char)this.undecodedChunk.ReadByte();
                                        currentpos++;
                                        if (read == HttpConstants.LineFeed)
                                        {
                                            this.currentStatus = MultiPartStatus.PreEpilogue;
                                            ampersandpos = currentpos - 2;
                                            this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                                            firstpos = currentpos;
                                            contRead = false;
                                        }
                                        else
                                        {
                                            // Error
                                            ThrowHelper.ThrowErrorDataDecoderException_BadEndOfLine();
                                        }
                                    }
                                    else
                                    {
                                        currentpos--;
                                    }
                                    break;

                                case HttpConstants.LineFeedChar:
                                    this.currentStatus = MultiPartStatus.PreEpilogue;
                                    ampersandpos = currentpos - 1;
                                    this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                                    firstpos = currentpos;
                                    contRead = false;
                                    break;
                            }
                            break;
                        default:
                            // just stop
                            contRead = false;
                            break;
                    }
                }
                if (this.isLastChunk && this.currentAttribute != null)
                {
                    // special case
                    ampersandpos = currentpos;
                    if (ampersandpos > firstpos)
                    {
                        this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                    }
                    else if (!this.currentAttribute.IsCompleted)
                    {
                        this.SetFinalBuffer(Unpooled.Empty);
                    }
                    firstpos = currentpos;
                    this.currentStatus = MultiPartStatus.Epilogue;
                }
                else if (contRead && this.currentAttribute != null && this.currentStatus == MultiPartStatus.Field)
                {
                    // reset index except if to continue in case of FIELD getStatus
                    this.currentAttribute.AddContent(this.undecodedChunk.Copy(firstpos, currentpos - firstpos), false);
                    firstpos = currentpos;
                }
                this.undecodedChunk.SetReaderIndex(firstpos);
            }
            catch (ErrorDataDecoderException)
            {
                // error while decoding
                this.undecodedChunk.SetReaderIndex(firstpos);
                throw;
            }
            catch (IOException e)
            {
                // error while decoding
                this.undecodedChunk.SetReaderIndex(firstpos);
                ThrowHelper.ThrowErrorDataDecoderException(e);
            }
        }

        void ParseBodyAttributes()
        {
            if (!this.undecodedChunk.HasArray)
            {
                this.ParseBodyAttributesStandard();
                return;
            }
            var sao = new HttpPostBodyUtil.SeekAheadOptimize(this.undecodedChunk);
            int firstpos = this.undecodedChunk.ReaderIndex;
            int currentpos = firstpos;
            if (this.currentStatus == MultiPartStatus.Notstarted)
            {
                this.currentStatus = MultiPartStatus.Disposition;
            }
            bool contRead = true;
            try
            {
                //loop:
                int ampersandpos;
                while (sao.Pos < sao.Limit)
                {
                    char read = (char)(sao.Bytes[sao.Pos++]);
                    currentpos++;
                    switch (this.currentStatus)
                    {
                        case MultiPartStatus.Disposition:// search '='
                            switch (read)
                            {
                                case '=':
                                    this.currentStatus = MultiPartStatus.Field;
                                    int equalpos = currentpos - 1;
                                    string key = DecodeAttribute(this.undecodedChunk.ToString(firstpos, equalpos - firstpos, this.charset), this.charset);
                                    this.currentAttribute = this.factory.CreateAttribute(this.request, key);
                                    firstpos = currentpos;
                                    break;

                                case '&':
                                    // special empty FIELD
                                    this.currentStatus = MultiPartStatus.Disposition;
                                    ampersandpos = currentpos - 1;
                                    string key0 = DecodeAttribute(this.undecodedChunk.ToString(firstpos, ampersandpos - firstpos, this.charset), this.charset);
                                    this.currentAttribute = this.factory.CreateAttribute(this.request, key0);
                                    this.currentAttribute.Value = ""; // empty
                                    this.AddHttpData(this.currentAttribute);
                                    this.currentAttribute = null;
                                    firstpos = currentpos;
                                    contRead = true;
                                    break;
                            }
                            break;
                        case MultiPartStatus.Field:// search '&' or end of line
                            switch (read)
                            {
                                case '&':
                                    this.currentStatus = MultiPartStatus.Disposition;
                                    ampersandpos = currentpos - 1;
                                    this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                                    firstpos = currentpos;
                                    contRead = true;
                                    break;

                                case HttpConstants.CarriageReturnChar:
                                    if (sao.Pos < sao.Limit)
                                    {
                                        read = (char)(sao.Bytes[sao.Pos++]);
                                        currentpos++;
                                        if (read == HttpConstants.LineFeed)
                                        {
                                            this.currentStatus = MultiPartStatus.PreEpilogue;
                                            ampersandpos = currentpos - 2;
                                            sao.SetReadPosition(0);
                                            this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                                            firstpos = currentpos;
                                            contRead = false;
                                            goto loop;
                                        }
                                        else
                                        {
                                            // Error
                                            sao.SetReadPosition(0);
                                            ThrowHelper.ThrowErrorDataDecoderException_BadEndOfLine();
                                        }
                                    }
                                    else
                                    {
                                        if (sao.Limit > 0)
                                        {
                                            currentpos--;
                                        }
                                    }
                                    break;

                                case HttpConstants.LineFeedChar:
                                    this.currentStatus = MultiPartStatus.PreEpilogue;
                                    ampersandpos = currentpos - 1;
                                    sao.SetReadPosition(0);
                                    this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                                    firstpos = currentpos;
                                    contRead = false;
                                    goto loop;
                            }
                            break;
                        default:
                            // just stop
                            sao.SetReadPosition(0);
                            contRead = false;
                            goto loop;
                    }
                }
                loop:
                if (this.isLastChunk && this.currentAttribute != null)
                {
                    // special case
                    ampersandpos = currentpos;
                    if (ampersandpos > firstpos)
                    {
                        this.SetFinalBuffer(this.undecodedChunk.Copy(firstpos, ampersandpos - firstpos));
                    }
                    else if (!this.currentAttribute.IsCompleted)
                    {
                        this.SetFinalBuffer(Unpooled.Empty);
                    }
                    firstpos = currentpos;
                    this.currentStatus = MultiPartStatus.Epilogue;
                }
                else if (contRead && this.currentAttribute != null && this.currentStatus == MultiPartStatus.Field)
                {
                    // reset index except if to continue in case of FIELD getStatus
                    this.currentAttribute.AddContent(this.undecodedChunk.Copy(firstpos, currentpos - firstpos), false);
                    firstpos = currentpos;
                }
                this.undecodedChunk.SetReaderIndex(firstpos);
            }
            catch (ErrorDataDecoderException)
            {
                // error while decoding
                this.undecodedChunk.SetReaderIndex(firstpos);
                throw;
            }
            catch (IOException e)
            {
                // error while decoding
                this.undecodedChunk.SetReaderIndex(firstpos);
                ThrowHelper.ThrowErrorDataDecoderException(e);
            }
            catch (ArgumentException e)
            {
                // error while decoding
                this.undecodedChunk.SetReaderIndex(firstpos);
                ThrowHelper.ThrowErrorDataDecoderException(e);
            }
        }

        void SetFinalBuffer(IByteBuffer buffer)
        {
            this.currentAttribute.AddContent(buffer, true);
            string value = DecodeAttribute(this.currentAttribute.GetByteBuffer().ToString(this.charset), this.charset);
            this.currentAttribute.Value = value;
            this.AddHttpData(this.currentAttribute);
            this.currentAttribute = null;
        }

        static string DecodeAttribute(string s, Encoding charset)
        {
            try
            {
                return QueryStringDecoder.DecodeComponent(s, charset);
            }
            catch (ArgumentException e)
            {
                return ThrowHelper.ThrowErrorDataDecoderException_BadString(s, e);
            }
        }

        public void Destroy()
        {
            // Release all data items, including those not yet pulled
            this.CleanFiles();

            this.destroyed = true;

            if (this.undecodedChunk != null && this.undecodedChunk.ReferenceCount > 0)
            {
                this.undecodedChunk.Release();
                this.undecodedChunk = null;
            }
        }

        public void CleanFiles()
        {
            this.CheckDestroyed();

            this.factory.CleanRequestHttpData(this.request);
        }

        public void RemoveHttpDataFromClean(IInterfaceHttpData data)
        {
            this.CheckDestroyed();

            this.factory.RemoveHttpDataFromClean(this.request, data);
        }
    }
}
