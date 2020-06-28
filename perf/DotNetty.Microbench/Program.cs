// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench
{
    using System;
    using BenchmarkDotNet.Running;

    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {   // if no args, we're probably using Ctrl+F5 in the IDE; enlargen thyself!
                try
                {
                    Console.WindowWidth = Console.LargestWindowWidth - 20;
                }
                catch { }
            }
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
