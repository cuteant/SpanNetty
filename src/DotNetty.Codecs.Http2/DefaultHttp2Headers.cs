// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;

    public class DefaultHttp2Headers : DefaultHeaders<ICharSequence, ICharSequence>, IHttp2Headers
    {
        sealed class Http2NameValidatorProcessor : IByteProcessor
        {
            public static readonly IByteProcessor Instance = new Http2NameValidatorProcessor();

            private Http2NameValidatorProcessor() { }

            public bool Process(byte value) => !AsciiString.IsUpperCase(value);
        }

        internal sealed class Http2NameValidator : INameValidator<ICharSequence>
        {
            public static readonly INameValidator<ICharSequence> Instance = new Http2NameValidator();

            private Http2NameValidator() { }

            public void ValidateName(ICharSequence name)
            {
                if (name == null || 0u >= (uint)name.Count)
                {
                    ThrowHelper.ThrowConnectionError_EmptyHeadersAreNotAllowed(name);
                }

                if (name is AsciiString asciiString)
                {
                    int index = 0;
                    try
                    {
                        index = asciiString.ForEachByte(Http2NameValidatorProcessor.Instance);
                    }
                    catch (Http2Exception)
                    {
                        throw;
                    }
                    catch (Exception t)
                    {
                        ThrowHelper.ThrowConnectionError_InvalidHeaderName(name, t);
                    }

                    if (index != -1)
                    {
                        ThrowHelper.ThrowConnectionError_InvalidHeaderName(name);
                    }
                }
                else
                {
                    for (int i = 0; i < name.Count; ++i)
                    {
                        if (AsciiString.IsUpperCase(name[i]))
                        {
                            ThrowHelper.ThrowConnectionError_InvalidHeaderName(name);
                        }
                    }
                }
            }
        }

        HeaderEntry<ICharSequence, ICharSequence> firstNonPseudo;

        /// <summary>
        /// Create a new instance.
        /// Header names will be validated according to <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>.
        /// </summary>
        public DefaultHttp2Headers()
            : this(true)
        {
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="validate"><c>true</c> to validate header names according to
        /// <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>. <c>false</c> to not validate header names.</param>
        public DefaultHttp2Headers(bool validate)
            : base(AsciiString.CaseSensitiveHasher, CharSequenceValueConverter.Default, validate ? Http2NameValidator.Instance : DefaultHttpHeaders.NotNullValidator)
        {
            // Case sensitive compare is used because it is cheaper, and header validation can be used to catch invalid
            // headers.
            this.firstNonPseudo = this.head;
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="validate"></param>
        /// <param name="arraySizeHint"></param>
        public DefaultHttp2Headers(bool validate, int arraySizeHint)
            : base(AsciiString.CaseSensitiveHasher, CharSequenceValueConverter.Default, validate ? Http2NameValidator.Instance : DefaultHttpHeaders.NotNullValidator, arraySizeHint)
        {
            // Case sensitive compare is used because it is cheaper, and header validation can be used to catch invalid
            // headers.
            this.firstNonPseudo = this.head;
        }

        public override IHeaders<ICharSequence, ICharSequence> Clear()
        {
            this.firstNonPseudo = this.head;
            return base.Clear();
        }

        public override bool Equals(object obj)
        {
            return obj is IHttp2Headers headers && this.Equals(headers, AsciiString.CaseSensitiveHasher);
        }

        public override int GetHashCode()
        {
            return this.HashCode(AsciiString.CaseSensitiveHasher);
        }

        public ICharSequence Method
        {
            get => this.Get(PseudoHeaderName.Method.Value, null);
            set => this.Set(PseudoHeaderName.Method.Value, value);
        }

        public ICharSequence Scheme
        {
            get => this.Get(PseudoHeaderName.Scheme.Value, null);
            set => this.Set(PseudoHeaderName.Scheme.Value, value);
        }

        public ICharSequence Authority
        {
            get => this.Get(PseudoHeaderName.Authority.Value, null);
            set => this.Set(PseudoHeaderName.Authority.Value, value);
        }

        public ICharSequence Path
        {
            get => this.Get(PseudoHeaderName.Path.Value, null);
            set => this.Set(PseudoHeaderName.Path.Value, value);
        }

        public ICharSequence Status
        {
            get => this.Get(PseudoHeaderName.Status.Value, null);
            set => this.Set(PseudoHeaderName.Status.Value, value);
        }

        public bool Contains(ICharSequence name, ICharSequence value, bool caseInsensitive)
        {
            return this.Contains(name, value, caseInsensitive ? AsciiString.CaseInsensitiveHasher : AsciiString.CaseSensitiveHasher);
        }

        protected sealed override HeaderEntry<ICharSequence, ICharSequence> NewHeaderEntry(
            int h,
            ICharSequence name,
            ICharSequence value,
            HeaderEntry<ICharSequence, ICharSequence> next)
        {
            return new Http2HeaderEntry(this, h, name, value, next);
        }

        sealed class Http2HeaderEntry : HeaderEntry<ICharSequence, ICharSequence>
        {
            readonly DefaultHttp2Headers headers;

            internal Http2HeaderEntry(DefaultHttp2Headers headers, int hash, ICharSequence key, ICharSequence value, HeaderEntry<ICharSequence, ICharSequence> next)
                : base(hash, key)
            {
                this.headers = headers;
                this.value = value;
                this.Next = next;

                // Make sure the pseudo headers fields are first in iteration order
                if (PseudoHeaderName.HasPseudoHeaderFormat(key))
                {
                    this.After = this.headers.firstNonPseudo;
                    this.Before = this.headers.firstNonPseudo.Before;
                }
                else
                {
                    this.After = this.headers.head;
                    this.Before = this.headers.head.Before;
                    if (this.headers.firstNonPseudo == this.headers.head)
                    {
                        this.headers.firstNonPseudo = this;
                    }
                }

                this.PointNeighborsToThis();
            }

            public override void Remove()
            {
                if (this == this.headers.firstNonPseudo)
                {
                    this.headers.firstNonPseudo = this.headers.firstNonPseudo.After;
                }

                base.Remove();
            }
        }
    }
}
