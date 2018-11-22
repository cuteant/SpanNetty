// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Always return <c>false</c> for <see cref="ISensitivityDetector.IsSensitive(ICharSequence, ICharSequence)"/>.
    /// </summary>
    public sealed class NeverSensitiveDetector : ISensitivityDetector
    {
        public static readonly ISensitivityDetector Instance = new NeverSensitiveDetector();

        private NeverSensitiveDetector() { }

        public bool IsSensitive(ICharSequence name, ICharSequence value) => false;
    }
}