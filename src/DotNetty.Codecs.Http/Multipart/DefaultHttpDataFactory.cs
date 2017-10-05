// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using DotNetty.Common.Internal;

    public class DefaultHttpDataFactory : IHttpDataFactory
    {
        // Proposed default MINSIZE as 16 KB.
        public static readonly long MinSize = 0x4000;

        // Proposed default MAXSIZE = -1 as UNLIMITED
        public static readonly long MaxSize = -1;

        readonly bool useDisk;
        readonly bool checkSize;
        readonly long minSize;
        long maxSize = MaxSize;
        readonly Encoding charset = HttpConstants.DefaultEncoding;

        // Keep all HttpDatas until cleanAllHttpData() is called.
        readonly IDictionary<IHttpRequest, List<IHttpData>> requestFileDeleteMap = PlatformDependent.NewConcurrentHashMap<IHttpRequest, List<IHttpData>>();

        // HttpData will be in memory if less than default size (16KB).
        // The type will be Mixed.
        public DefaultHttpDataFactory()
        {
            this.useDisk = false;
            this.checkSize = true;
            this.minSize = MinSize;
        }

        public DefaultHttpDataFactory(Encoding charset) : this()
        {
            this.charset = charset;
        }

        // HttpData will be always on Disk if useDisk is True, else always in Memory if False
        public DefaultHttpDataFactory(bool useDisk)
        {
            this.useDisk = useDisk;
            this.checkSize = false;
        }

        public DefaultHttpDataFactory(bool useDisk, Encoding charset) : this(useDisk)
        {
            this.charset = charset;
        }

        public DefaultHttpDataFactory(long minSize)
        {
            this.useDisk = false;
            this.checkSize = true;
            this.minSize = minSize;
        }

        public DefaultHttpDataFactory(long minSize, Encoding charset) : this(minSize)
        {
            this.charset = charset;
        }

        public void SetMaxLimit(long max) => this.maxSize = max;

        List<IHttpData> GetList(IHttpRequest request)
        {
            if (!this.requestFileDeleteMap.TryGetValue(request, out List<IHttpData> list))
            {
                list = new List<IHttpData>();
                this.requestFileDeleteMap.Add(request, list);
            }
            return list;
        }

        public IAttribute CreateAttribute(IHttpRequest request, string name)
        {
            if (this.useDisk)
            {
                var diskAttribute = new DiskAttribute(name, this.charset);
                diskAttribute.MaxSize = this.maxSize;
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(diskAttribute);
                return diskAttribute;
            }
            if (this.checkSize)
            {
                var mixedAttribute = new MixedAttribute(name, this.minSize, this.charset);
                mixedAttribute.MaxSize = this.maxSize;
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(mixedAttribute);
                return mixedAttribute;
            }
            var attribute = new MemoryAttribute(name);
            attribute.MaxSize = this.maxSize;
            return attribute;
        }

        public IAttribute CreateAttribute(IHttpRequest request, string name, long definedSize)
        {
            if (this.useDisk)
            {
                var diskAttribute = new DiskAttribute(name, definedSize, this.charset);
                diskAttribute.MaxSize = this.maxSize;
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(diskAttribute);
                return diskAttribute;
            }
            if (this.checkSize)
            {
                var mixedAttribute = new MixedAttribute(name, definedSize, this.minSize, this.charset);
                mixedAttribute.MaxSize = this.maxSize;
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(mixedAttribute);
                return mixedAttribute;
            }
            var attribute = new MemoryAttribute(name, definedSize);
            attribute.MaxSize = this.maxSize;
            return attribute;
        }

        static void CheckHttpDataSize(IHttpData data)
        {
            try
            {
                data.CheckSize(data.Length);
            }
            catch (IOException)
            {
                throw new ArgumentException($"Attribute {data.DataType} bigger than maxSize allowed");
            }
        }

        public IAttribute CreateAttribute(IHttpRequest request, string name, string value)
        {
            if (this.useDisk)
            {
                IAttribute attribute;
                try
                {
                    attribute = new DiskAttribute(name, value, this.charset);
                    attribute.MaxSize = this.maxSize;
                }
                catch (IOException)
                {
                    // revert to Mixed mode
                    attribute = new MixedAttribute(name, value, this.minSize, this.charset);
                    attribute.MaxSize = this.maxSize;
                }
                CheckHttpDataSize(attribute);
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(attribute);
                return attribute;
            }
            if (this.checkSize)
            {
                var mixedAttribute = new MixedAttribute(name, value, this.minSize, this.charset);
                mixedAttribute.MaxSize = this.maxSize;
                CheckHttpDataSize(mixedAttribute);
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(mixedAttribute);
                return mixedAttribute;
            }
            try
            {
                var attribute = new MemoryAttribute(name, value, this.charset);
                attribute.MaxSize = this.maxSize;
                CheckHttpDataSize(attribute);
                return attribute;
            }
            catch (IOException e)
            {
                throw new ArgumentException($"({request}, {name}, {value})" ,e);
            }
        }

        public IFileUpload CreateFileUpload(IHttpRequest request, string name, string fileName, 
            string contentType, string contentTransferEncoding, Encoding encoding, 
            long size)
        {
            if (this.useDisk)
            {
                var fileUpload = new DiskFileUpload(name, fileName, contentType, 
                    contentTransferEncoding, encoding, size);
                fileUpload.MaxSize = this.maxSize;
                CheckHttpDataSize(fileUpload);
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(fileUpload);
                return fileUpload;
            }
            if (this.checkSize)
            {
                var fileUpload = new MixedFileUpload(name, fileName, contentType, 
                    contentTransferEncoding, encoding, size, this.minSize);
                fileUpload.MaxSize = this.maxSize;
                CheckHttpDataSize(fileUpload);
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Add(fileUpload);
                return fileUpload;
            }
            var memoryFileUpload = new MemoryFileUpload(name, fileName, contentType, 
                contentTransferEncoding, encoding, size);
            memoryFileUpload.MaxSize = this.maxSize;
            CheckHttpDataSize(memoryFileUpload);
            return memoryFileUpload;
        }

        public void RemoveHttpDataFromClean(IHttpRequest request, IInterfaceHttpData data)
        {
            if (data is IHttpData httpData)
            {
                List<IHttpData> fileToDelete = this.GetList(request);
                fileToDelete.Remove(httpData);
            }
        }

        public void CleanRequestHttpData(IHttpRequest request)
        {
            if (!this.requestFileDeleteMap.TryGetValue(request, out List<IHttpData> fileToDelete))
            {
                return;
            }

            this.requestFileDeleteMap.Remove(request);
            foreach (IHttpData data in fileToDelete)
            {
                data.Delete();
            }
        }

        public void CleanAllHttpData()
        {
            while (true)
            {
                IHttpRequest[] keys = this.requestFileDeleteMap.Keys.ToArray();
                if (keys.Length == 0)
                {
                    break;
                }
                foreach (IHttpRequest key in keys)
                {
                    if (this.requestFileDeleteMap.TryGetValue(key, out List<IHttpData> list))
                    {
                        this.requestFileDeleteMap.Remove(key);
                        foreach (IHttpData data in list)
                        {
                            data.Delete();
                        }
                        list.Clear();
                    }
                }
            }
        }
    }
}
