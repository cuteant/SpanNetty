// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    public interface IHttp2Headers : IHeaders<ICharSequence, ICharSequence>
    {
        /// <summary>
        /// Gets or sets the <see cref="PseudoHeaderName.Method"/> header or <c>null</c> if there is no such header
        /// </summary>
        /// <returns></returns>
        ICharSequence Method { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="PseudoHeaderName.Scheme"/> header or <c>null</c> if there is no such header
        /// </summary>
        /// <returns></returns>
        ICharSequence Scheme { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="PseudoHeaderName.Authority"/> header or <c>null</c> if there is no such header
        /// </summary>
        /// <returns></returns>
        ICharSequence Authority { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="PseudoHeaderName.Path"/> header or <c>null</c> if there is no such header
        /// </summary>
        /// <returns></returns>
        ICharSequence Path { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="PseudoHeaderName.Status"/> header or <c>null</c> if there is no such header
        /// </summary>
        /// <returns></returns>
        ICharSequence Status { get; set; }

        /// <summary>
        /// Returns <c>true</c> if a header with the <paramref name="name"/> and <paramref name="value"/> exists, <c>false</c> otherwise.
        /// If <paramref name="caseInsensitive"/> is <c>true</c> then a case insensitive compare is done on the value.
        /// </summary>
        /// <param name="name">the name of the header to find</param>
        /// <param name="value">the value of the header to find</param>
        /// <param name="caseInsensitive"><c>true</c> then a case insensitive compare is run to compare values.
        /// otherwise a case sensitive compare is run to compare values.</param>
        /// <returns></returns>
        bool Contains(ICharSequence name, ICharSequence value, bool caseInsensitive);
    }
}