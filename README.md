# SpanNetty

This is a fork of [DotNetty](https://github.com/azure/dotnetty).

## Build Status

| Stage                                         | Status                                                                                                                                                                                                                                                            	|
|-----------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------	|
| Build                                         | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netfx-validation?branchName=main&jobName=Windows%20Build)](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=6&branchName=main) |
| .NET Framework 451 Unit Tests                 | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netfx-validation?branchName=main&jobName=.NET%20Framework%20451%20Unit%20Tests%20(Windows))](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=6&branchName=main) |
| .NET Framework 471 Unit Tests                 | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netfx-validation?branchName=main&jobName=.NET%20Framework%20Unit%20Tests%20(Windows))](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=6&branchName=main) |
| .NET Core (Windows) Unit Tests                | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netcore-validation?branchName=main&jobName=.NET%20Core%20Unit%20Tests%20(Windows))](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=7&branchName=main) |
| .NET Core (Ubuntu 16.04) Unit Tests           | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netcore-validation?branchName=main&jobName=.NET%20Core%20Unit%20Tests%20(Ubuntu-16))](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=7&branchName=main) |
| .NET Core (Ubuntu 18.04) Unit Tests           | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netcore-validation?branchName=main&jobName=.NET%20Core%20Unit%20Tests%20(Ubuntu-18))](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=7&branchName=main) |
| .NET Core (macOS X Mojave 10.14) Unit Tests   | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netcore-validation?branchName=main&jobName=.NET%20Core%20Unit%20Tests%20(MacOS-10.14))](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=7&branchName=main) |
| .NET Core (macOS X Catalina 10.15) Unit Tests | [![Build Status](https://dev.azure.com/SpanNetty/SpanNetty/_apis/build/status/SpanNetty/pr-netcore-validation?branchName=main&jobName=.NET%20Core%20Unit%20Tests%20(MacOS-10.15))](https://dev.azure.com/SpanNetty/SpanNetty/_build/latest?definitionId=7&branchName=main) |
| .NET Netstandard (Windows) Unit Tests         | [![Build status](https://ci.appveyor.com/api/projects/status/rvx3h1bmahad2giw/branch/main?svg=true)](https://ci.appveyor.com/project/cuteant/SpanNetty/branch/main) |

## Features
  - Align with [Netty-4.1.51.Final](https://github.com/netty/netty/tree/netty-4.1.51.Final)
  - ArrayPooledByteBuffer
  - Support **Span&#60;byte&#62;** and **Memory&#60;byte&#62;** in Buffer/Common APIs
  - Add support for IBufferWriter&#60;byte&#62; to the **IByteBuffer**
  - [ByteBufferReader](https://github.com/cuteant/spannetty/tree/main/src/DotNetty.Buffers/Reader) and [ByteBufferWriter](https://github.com/cuteant/dotnetty-span-fork/tree/main/src/DotNetty.Buffers/Writer)
  - [HTTP 2 codec](https://github.com/cuteant/spannetty/tree/main/src/DotNetty.Codecs.Http2)

## Use

* Stable builds are available on [NuGet](https://www.nuget.org/packages?q=spannetty).
* Nightly builds are available on [MyGet](https://www.myget.org/F/cuteant/api/v2).


|Package|NuGet Version|MyGet Version|
|------|-------------|-------------|
|SpanNetty.Common|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Common)](https://www.nuget.org/packages/SpanNetty.Common/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Common)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Common)|
|SpanNetty.Buffers|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Buffers)](https://www.nuget.org/packages/SpanNetty.Buffers/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Buffers)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Buffers)|
|SpanNetty.Codecs|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Codecs)](https://www.nuget.org/packages/SpanNetty.Codecs/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Codecs)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Codecs)|
|SpanNetty.Codecs.Http|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Codecs.Http)](https://www.nuget.org/packages/SpanNetty.Codecs.Http/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Codecs.Http)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Codecs.Http)|
|SpanNetty.Codecs.Http2|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Codecs.Http2)](https://www.nuget.org/packages/SpanNetty.Codecs.Http2/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Codecs.Http2)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Codecs.Http2)|
|SpanNetty.Codecs.Mqtt|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Codecs.Mqtt)](https://www.nuget.org/packages/SpanNetty.Codecs.Mqtt/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Codecs.Mqtt)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Codecs.Mqtt)|
|SpanNetty.Codecs.Protobuf|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Codecs.Protobuf)](https://www.nuget.org/packages/SpanNetty.Codecs.Protobuf/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Codecs.Protobuf)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Codecs.Protobuf)|
|SpanNetty.Handlers|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Handlers)](https://www.nuget.org/packages/SpanNetty.Handlers/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Handlers)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Handlers)|
|SpanNetty.Transport|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Transport)](https://www.nuget.org/packages/SpanNetty.Transport/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Transport)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Transport)|
|SpanNetty.Transport.Libuv|[![NuGet Version and Downloads count](https://buildstats.info/nuget/SpanNetty.Transport.Libuv)](https://www.nuget.org/packages/SpanNetty.Transport.Libuv/)|[![MyGet Version](https://img.shields.io/myget/cuteant/vpre/SpanNetty.Transport.Libuv)](https://www.myget.org/feed/cuteant/package/nuget/SpanNetty.Transport.Libuv)|

## Performance

``` ini

OS=Windows 10.0.17134.1667
Intel Xeon CPU E3-1230 V2 3.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.401

```

Here are some performance numbers from [Akka.RemotePingPong(With SpanNetty) benchmark](https://github.com/cuteant/akka.net/tree/future/benchmark/RemotePingPong), which uses high volumes of small messages. 

These numbers were all produced on a 4 core Intel i5 3.30hz PC over a single Akka.Remote connection running .NET Core 3.1 on Windows 10:

### ~ With Message Batching (**Socket**)

| Num clients (actors) | Total [msg] | Msgs/sec | Total [ms] |
|----------------------|-------------|----------|------------|
| 1                    | 200000      | 74075    | 2700.29    |
| 5                    | 1000000     | 167281   | 5978.33    |
| 10                   | 2000000     | 196406   | 10183.36   |
| 15                   | 3000000     | 209805   | 14299.36   |
| 20                   | 4000000     | 210096   | 19039.21   |
| 25                   | 5000000     | 210678   | 23733.14   |
| 30                   | 6000000     | 203985   | 29414.13   |

Average performance: **181,760 msg/s**.

### ~ With Message Batching (_Libuv_)

| Num clients (actors) | Total [msg] | Msgs/sec | Total [ms] |
|----------------------|-------------|----------|------------|
| 1                    | 200000      | 76570    | 2612.17    |
| 5                    | 1000000     | 159516   | 6269.25    |
| 10                   | 2000000     | 187161   | 10686.69   |
| 15                   | 3000000     | 198073   | 15146.09   |
| 20                   | 4000000     | 190124   | 21039.95   |
| 25                   | 5000000     | 184027   | 27170.75   |
| 30                   | 6000000     | 173752   | 34532.69   |

Average performance: **167,031 msg/s**.

### ~ With I/O Batching (**Socket**)

| Num clients (actors) | Total [msg] | Msgs/sec | Total [ms] |
|----------------------|-------------|----------|------------|
| 1                    | 200000      | 64893    | 3082.78    |
| 5                    | 1000000     | 145181   | 6888.77    |
| 10                   | 2000000     | 162761   | 12288.34   |
| 15                   | 3000000     | 160231   | 18723.05   |
| 20                   | 4000000     | 148242   | 26983.94   |
| 25                   | 5000000     | 132269   | 37802.50   |
| 30                   | 6000000     | 123597   | 48545.25   |

Average performance: **133,882 msg/s**.

### ~ With I/O Batching (_Libuv_)

| Num clients (actors) | Total [msg] | Msgs/sec | Total [ms] |
|----------------------|-------------|----------|------------|
| 1                    | 200000      | 63634    | 3143.60    |
| 5                    | 1000000     | 133298   | 7502.06    |
| 10                   | 2000000     | 149288   | 13397.27   |
| 15                   | 3000000     | 146865   | 20427.17   |
| 20                   | 4000000     | 132101   | 30280.71   |
| 25                   | 5000000     | 115415   | 43322.88   |
| 30                   | 6000000     | 111620   | 53754.96   |

Average performance: **121,745 msg/s**.

### ~ No I/O Batching (_Socket_)

| Num clients (actors) | Total [msg] | Msgs/sec | Total [ms] |
|----------------------|-------------|----------|------------|
| 1                    | 200000      | 31348    | 6380.59    |
| 5                    | 1000000     | 53698    | 18623.22   |
| 10                   | 2000000     | 62066    | 32224.90   |
| 15                   | 3000000     | 60902    | 49260.73   |
| 20                   | 4000000     | 56694    | 70555.15   |
| 25                   | 5000000     | 15152    | 330000.86  |

Average performance: **46,643 msg/s**.

### ~ No I/O Batching (**Libuv**)

| Num clients (actors) | Total [msg] | Msgs/sec | Total [ms] |
|----------------------|-------------|----------|------------|
| 1                    | 200000      | 71995    | 2778.50    |
| 5                    | 1000000     | 131441   | 7608.04    |
| 10                   | 2000000     | 144041   | 13885.52   |
| 15                   | 3000000     | 134433   | 22316.79   |
| 20                   | 4000000     | 126575   | 31602.55   |
| 25                   | 5000000     | 120759   | 41405.54   |
| 30                   | 6000000     | 119919   | 50034.57   |

Average performance: **121,309 msg/s**.

# ~ ORIGINAL README ~

# DotNetty Project

[![Join the chat at https://gitter.im/Azure/DotNetty](https://img.shields.io/gitter/room/Azure/DotNetty.js.svg?style=flat-square)](https://gitter.im/Azure/DotNetty?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Available on NuGet https://www.nuget.org/packages?q=DotNetty](https://img.shields.io/nuget/v/DotNetty.Common.svg?style=flat-square)](https://www.nuget.org/packages?q=DotNetty)
[![AppVeyor](https://img.shields.io/appveyor/ci/nayato/dotnetty.svg?label=appveyor&style=flat-square)](https://ci.appveyor.com/project/nayato/dotnetty)

DotNetty is a port of [Netty](https://github.com/netty/netty), asynchronous event-driven network application framework for rapid development of maintainable high performance protocol servers & clients.

## Use

* Official releases are on [NuGet](https://www.nuget.org/packages?q=DotNetty).
* Nightly builds are available on [MyGet](https://www.myget.org/F/dotnetty/api/v2/).

## Contribute

We gladly accept community contributions.

* Issues: Please report bugs using the Issues section of GitHub
* Source Code Contributions:
 * Please follow the [Contribution Guidelines for Microsoft Azure](http://azure.github.io/guidelines.html) open source that details information on onboarding as a contributor
 * See [C# Coding Style](https://github.com/Azure/DotNetty/wiki/C%23-Coding-Style) for reference on coding style.
