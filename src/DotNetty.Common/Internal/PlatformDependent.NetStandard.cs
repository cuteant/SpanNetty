// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40

namespace DotNetty.Common.Internal
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    partial class PlatformDependent
    {
        public static bool IsX64 { get; } = IntPtr.Size >= 8;

        public static unsafe int FindIndex(ref byte searchSpace, Predicate<byte> match, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;

                offset += 8;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;

                offset += 4;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;

                offset += 1;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);

        }

        public static unsafe int FindLastIndex(ref byte searchSpace, Predicate<byte> match, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)length; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;
                offset -= 8;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;
                offset -= 4;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;
                offset -= 1;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);
        }

        public static unsafe int ForEachByte(ref byte searchSpace, IByteProcessor processor, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;

                offset += 8;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;

                offset += 4;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;

                offset += 1;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);

        }

        public static unsafe int ForEachByteDesc(ref byte searchSpace, IByteProcessor processor, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)length; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;
                offset -= 8;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;
                offset -= 4;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;
                offset -= 1;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);
        }
    }
}

#endif