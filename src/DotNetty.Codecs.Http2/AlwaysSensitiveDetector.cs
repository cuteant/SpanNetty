// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Always return <c>true</c> for <see cref="ISensitivityDetector.IsSensitive(ICharSequence, ICharSequence)"/>.
    /// </summary>
    public sealed class AlwaysSensitiveDetector : ISensitivityDetector
    {
        public static readonly ISensitivityDetector Instance = new AlwaysSensitiveDetector();

        private AlwaysSensitiveDetector() { }

        public bool IsSensitive(ICharSequence name, ICharSequence value) => true;
    }
}