// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.

[assembly: AssemblyTitle("DotNetty.Transport.Libuv")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Retail")]
#endif
[assembly: AssemblyDescription("Transport model in DotNetty")]

// The following GUID is for the ID of the typelib if this project is exposed to COM

[assembly: Guid("39F16D1D-77B0-4C4D-AA0A-F3ADF5A75A01")]