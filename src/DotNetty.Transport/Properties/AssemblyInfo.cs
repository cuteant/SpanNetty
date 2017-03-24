// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

#if NET40
[assembly: AssemblyTitle("DotNetty.Transport for .NetFx4.0")]
#elif NET451
[assembly: AssemblyTitle("DotNetty.Transport for .NetFx4.5")]
#elif NET46
[assembly: AssemblyTitle("DotNetty.Transport for .NetFx4.6")]
#endif
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Retail")]
#endif
[assembly: AssemblyDescription("Transport model in DotNetty")]

// The following GUID is for the ID of the typelib if this project is exposed to COM

[assembly: Guid("DA70AFC8-CB22-4B08-8DFB-2DA4E8777C50")]