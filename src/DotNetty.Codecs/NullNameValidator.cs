// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    public sealed class NullNameValidator<T> : INameValidator<T>
    {
        public static readonly NullNameValidator<T> Instance = new NullNameValidator<T>();

        public void ValidateName(T name)
        {
            if (name == null)
            {
                CThrowHelper.ThrowArgumentNullException(CExceptionArgument.name);
            }
        }
    }
}
