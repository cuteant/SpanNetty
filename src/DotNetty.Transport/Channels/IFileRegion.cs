// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.IO;
    using DotNetty.Common;

    public interface IFileRegion : IReferenceCounted
    {
        /// <summary>Returns the offset in the file where the transfer began.</summary>
        long Position { get; }

        /// <summary>Returns the bytes which was transfered already.</summary>
        long Transferred { get; }

        /// <summary>Returns the number of bytes to transfer.</summary>
        long Count { get; }

        /// <summary>Transfers the content of this file region to the specified channel.</summary>
        /// <param name="target">the destination of the transfer</param>
        /// <param name="position">the relative offset of the file where the transfer
        /// begins from.  For example, <tt>0</tt> will make the
        /// transfer start from <see cref="Position"/>th byte and
        /// <tt><see cref="Count"/> - 1</tt> will make the last
        /// byte of the region transferred.</param>
        /// <returns></returns>
        long TransferTo(Stream target, long position);
    }
}
