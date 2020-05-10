// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Cookies
{
    using System;
    using DotNetty.Common.Utilities;

    using static CookieUtil;

    public abstract class CookieEncoder
    {
        protected readonly bool Strict;

        protected CookieEncoder(bool strict)
        {
            this.Strict = strict;
        }

        protected void ValidateCookie(string name, string value)
        {
            if (!this.Strict)
            {
                return;
            }

            int pos;
            if ((pos = FirstInvalidCookieNameOctet(name)) >= 0)
            {
                ThrowHelper.ThrowArgumentException_CookieName(name, pos);
            }

            var sequnce = new StringCharSequence(value);
            ICharSequence unwrappedValue = UnwrapValue(sequnce);
            if (unwrappedValue is null)
            {
                ThrowHelper.ThrowArgumentException_CookieValue(value);
            }

            if ((pos = FirstInvalidCookieValueOctet(unwrappedValue)) >= 0)
            {
                ThrowHelper.ThrowArgumentException_CookieValue(unwrappedValue, pos);
            }
        }
    }
}
