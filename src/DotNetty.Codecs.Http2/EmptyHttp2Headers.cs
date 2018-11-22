// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Codecs;
    using DotNetty.Common.Utilities;

    public sealed class EmptyHttp2Headers : EmptyHeaders<ICharSequence, ICharSequence>, IHttp2Headers
    {
        public static readonly EmptyHttp2Headers Instance = new EmptyHttp2Headers();

        private EmptyHttp2Headers() { }

        public ICharSequence Method
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Scheme
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Authority
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Path
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Status
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public bool Contains(ICharSequence name, ICharSequence value, bool caseInsensitive)
        {
            return false;
        }
    }
}
