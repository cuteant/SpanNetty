/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

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
                if (name is null || 0u >= (uint)name.Count)
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

        private HeaderEntry<ICharSequence, ICharSequence> _firstNonPseudo;

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
            _firstNonPseudo = _head;
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
            _firstNonPseudo = _head;
        }

        public override IHeaders<ICharSequence, ICharSequence> Clear()
        {
            _firstNonPseudo = _head;
            return base.Clear();
        }

        public override bool Equals(object obj)
        {
            return obj is IHttp2Headers headers && Equals(headers, AsciiString.CaseSensitiveHasher);
        }

        public override int GetHashCode()
        {
            return HashCode(AsciiString.CaseSensitiveHasher);
        }

        public ICharSequence Method
        {
            get => Get(PseudoHeaderName.Method.Value, null);
            set => Set(PseudoHeaderName.Method.Value, value);
        }

        public ICharSequence Scheme
        {
            get => Get(PseudoHeaderName.Scheme.Value, null);
            set => Set(PseudoHeaderName.Scheme.Value, value);
        }

        public ICharSequence Authority
        {
            get => Get(PseudoHeaderName.Authority.Value, null);
            set => Set(PseudoHeaderName.Authority.Value, value);
        }

        public ICharSequence Path
        {
            get => Get(PseudoHeaderName.Path.Value, null);
            set => Set(PseudoHeaderName.Path.Value, value);
        }

        public ICharSequence Status
        {
            get => Get(PseudoHeaderName.Status.Value, null);
            set => Set(PseudoHeaderName.Status.Value, value);
        }

        public bool Contains(ICharSequence name, ICharSequence value, bool caseInsensitive)
        {
            return Contains(name, value, caseInsensitive ? AsciiString.CaseInsensitiveHasher : AsciiString.CaseSensitiveHasher);
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
            readonly DefaultHttp2Headers _headers;

            internal Http2HeaderEntry(DefaultHttp2Headers headers, int hash, ICharSequence key, ICharSequence value, HeaderEntry<ICharSequence, ICharSequence> next)
                : base(hash, key)
            {
                _headers = headers;
                _value = value;
                Next = next;

                // Make sure the pseudo headers fields are first in iteration order
                if (PseudoHeaderName.HasPseudoHeaderFormat(key))
                {
                    After = _headers._firstNonPseudo;
                    Before = _headers._firstNonPseudo.Before;
                }
                else
                {
                    After = _headers._head;
                    Before = _headers._head.Before;
                    if (_headers._firstNonPseudo == _headers._head)
                    {
                        _headers._firstNonPseudo = this;
                    }
                }

                PointNeighborsToThis();
            }

            public override void Remove()
            {
                if (this == _headers._firstNonPseudo)
                {
                    _headers._firstNonPseudo = _headers._firstNonPseudo.After;
                }

                base.Remove();
            }
        }
    }
}
