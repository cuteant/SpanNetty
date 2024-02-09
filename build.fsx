#I @"tools/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open System.Text

open Fake
open Fake.DotNetCli
open Fake.NuGet.Install

// Variables
let configuration = environVarOrDefault "configuration" "Debug"
let solution = System.IO.Path.GetFullPath(string "./DotNetty.sln")

// Directories
let toolsDir = __SOURCE_DIRECTORY__ @@ "tools"
let output = __SOURCE_DIRECTORY__  @@ "Artifacts"
let outputTests = __SOURCE_DIRECTORY__ @@ "TestResults"
let outputPerfTests = __SOURCE_DIRECTORY__ @@ "PerfResults"

let buildNumber = environVarOrDefault "BUILD_NUMBER" "0"
let hasTeamCity = (not (buildNumber = "0")) // check if we have the TeamCity environment variable for build # set
let preReleaseVersionSuffix = "beta" + (if (not (buildNumber = "0")) then (buildNumber) else DateTime.UtcNow.Ticks.ToString())

let releaseNotes =
    File.ReadLines (__SOURCE_DIRECTORY__ @@ "RELEASE_NOTES.md")
    |> ReleaseNotesHelper.parseReleaseNotes

let versionFromReleaseNotes =
    match releaseNotes.SemVer.PreRelease with
    | Some r -> r.Origin
    | None -> ""

let versionSuffix = 
    match (getBuildParam "nugetprerelease") with
    | "main" -> preReleaseVersionSuffix
    | "" -> versionFromReleaseNotes
    | str -> str
    

// Incremental builds
let runIncrementally = hasBuildParam "incremental"
let incrementalistReport = output @@ "incrementalist.txt"

// Configuration values for tests
let testNetFrameworkVersion = "net471"
let testNetFramework451Version = "net452"
let testNetCoreVersion = "netcoreapp3.1"
let testNetCore21Version = "netcoreapp2.1"
let testNetVersion = "net5.0"

Target "Clean" (fun _ ->
    ActivateFinalTarget "KillCreatedProcesses"

    CleanDir output
    CleanDir outputTests
    CleanDir outputPerfTests

    CleanDirs !! "./**/TestResults"
    CleanDirs !! "./**/bin"
    CleanDirs !! "./**/obj"
)


//--------------------------------------------------------------------------------
// Incrementalist targets
//--------------------------------------------------------------------------------
// Pulls the set of all affected projects detected by Incrementalist from the cached file
let getAffectedProjectsTopology =
    lazy(
        log (sprintf "Checking inside %s for changes" incrementalistReport)

        let incrementalistFoundChanges = File.Exists incrementalistReport

        log (sprintf "Found changes via Incrementalist? %b - searched inside %s" incrementalistFoundChanges incrementalistReport)
        if not incrementalistFoundChanges then None
        else
            let sortedItems = (File.ReadAllLines incrementalistReport) |> Seq.map (fun x -> (x.Split ','))
                              |> Seq.map (fun items -> (items.[0], items))
            let d = dict sortedItems
            Some(d)
    )

let getAffectedProjects =
    lazy(
        let finalProjects = getAffectedProjectsTopology.Value
        match finalProjects with
        | None -> None
        | Some p -> Some (p.Values |> Seq.concat)
    )

Target "ComputeIncrementalChanges" (fun _ ->
    if runIncrementally then
        let targetBranch = match getBuildParam "targetBranch" with
                            | "" -> "main"
                            | null -> "main"
                            | b -> b
        let incrementalistPath =
                let incrementalistDir = toolsDir @@ "incrementalist"
                let globalTool = tryFindFileOnPath "incrementalist.exe"
                match globalTool with
                    | Some t -> t
                    | None -> if isWindows then findToolInSubPath "incrementalist.exe" incrementalistDir
                              elif isMacOS then incrementalistDir @@ "incrementalist"
                              else incrementalistDir @@ "incrementalist"
    
   
        let args = StringBuilder()
                |> append "-b"
                |> append targetBranch
                |> append "-s"
                |> append solution
                |> append "-f"
                |> append incrementalistReport
                |> append "--verbose"
                |> toText

        let result = ExecProcess(fun info ->
            info.FileName <- incrementalistPath
            info.WorkingDirectory <- __SOURCE_DIRECTORY__
            info.Arguments <- args) (System.TimeSpan.FromMinutes 5.0) (* Reasonably long-running task. *)
        
        if result <> 0 then failwithf "Incrementalist failed. %s" args
    else
        log "Skipping Incrementalist - not enabled for this build"
)

let filterProjects selectedProject =
    if runIncrementally then
        let affectedProjects = getAffectedProjects.Value

        (*
        if affectedProjects.IsSome then
            log (sprintf "Searching for %s inside [%s]" selectedProject (String.Join(",", affectedProjects.Value)))
        else
            log "No affected projects found"
        *)

        match affectedProjects with
        | None -> None
        | Some x when x |> Seq.exists (fun n -> n.Contains (System.IO.Path.GetFileName(string selectedProject))) -> Some selectedProject
        | _ -> None
    else
        log "Not running incrementally"
        Some selectedProject

//--------------------------------------------------------------------------------
// Build targets
//--------------------------------------------------------------------------------
let skipBuild =
    lazy(
        match getAffectedProjects.Value with
        | None when runIncrementally -> true
        | _ -> false
    )

let headProjects =
    lazy(
        match getAffectedProjectsTopology.Value with
        | None when runIncrementally -> [||]
        | None -> [|solution|]
        | Some p -> p.Keys |> Seq.toArray
    )

Target "Build" (fun _ ->
    if not skipBuild.Value then
        let additionalArgs = if versionSuffix.Length > 0 then [sprintf "/p:VersionSuffix=%s" versionSuffix] else []
        let buildProject proj =
            DotNetCli.Build
                (fun p ->
                    { p with
                        Project = proj
                        Configuration = configuration
                        AdditionalArgs = additionalArgs })

        match getAffectedProjects.Value with
        | Some p -> p |> Seq.iter buildProject
        | None -> buildProject solution // build the entire solution if incrementalist is disabled
)

//--------------------------------------------------------------------------------
// Tests targets
//--------------------------------------------------------------------------------
type Runtime =
    | NetCore
    | Net
    | NetFramework

let getTestAssembly runtime project =
    let assemblyPath = match runtime with
                        | NetCore -> !! ("test" @@ "**" @@ "bin" @@ "Debug" @@ testNetCoreVersion @@ fileNameWithoutExt project + ".dll")
                        | NetFramework -> !! ("test" @@ "**" @@ "bin" @@ "Debug" @@ "win-x64" @@ testNetFrameworkVersion @@ fileNameWithoutExt project + ".dll")
                        | Net -> !! ("src" @@ "**" @@ "bin" @@ "Release" @@ testNetVersion @@ fileNameWithoutExt project + ".dll")

    if Seq.isEmpty assemblyPath then
        None
    else
        Some (assemblyPath |> Seq.head)

module internal ResultHandling =
    let (|OK|Failure|) = function
        | 0 -> OK
        | x -> Failure x

    let buildErrorMessage = function
        | OK -> None
        | Failure errorCode ->
            Some (sprintf "xUnit2 reported an error (Error Code %d)" errorCode)

    let failBuildWithMessage = function
        | DontFailBuild -> traceError
        | _ -> (fun m -> raise(FailedTestsException m))

    let failBuildIfXUnitReportedError errorLevel =
        buildErrorMessage
        >> Option.iter (failBuildWithMessage errorLevel)

Target "RunTests" (fun _ ->
    if not skipBuild.Value then
        let projects = 
            let rawProjects = match (isWindows) with 
                                | true -> !! "./test/*.Tests/*.Tests.csproj"
                                          -- "./test/*.Tests/DotNetty.Transport.Tests.csproj"
                                          -- "./test/*.Tests/DotNetty.Suite.Tests.csproj"
                                | _ -> !! "./test/*.Tests/*.Tests.csproj" // if you need to filter specs for Linux vs. Windows, do it here
                                       -- "./test/*.Tests/DotNetty.Transport.Tests.csproj"
                                       -- "./test/*.Tests/DotNetty.Suite.Tests.csproj"
                                       -- "./test/*.Tests/DotNetty.Codecs.Http2.Tests.csproj"
                                       -- "./test/*.Tests/DotNetty.Handlers.Tests.csproj"
            rawProjects |> Seq.choose filterProjects
     
        let runSingleProject project =
            let arguments =
                match (hasTeamCity) with
                | true -> (sprintf "test -c %s --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none -teamcity" configuration testNetVersion outputTests)
                | false -> (sprintf "test -c %s --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none" configuration testNetVersion outputTests)

            let result = ExecProcess(fun info ->
                info.FileName <- "dotnet"
                info.WorkingDirectory <- (Directory.GetParent project).FullName
                info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0) 
        
            ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

        CreateDir outputTests
        projects |> Seq.iter (runSingleProject)
)

Target "RunTestsNetCore31" (fun _ ->
    if not skipBuild.Value then
        let projects = 
            let rawProjects = match (isWindows) with 
                                | true -> !! "./test/*.Tests/*.Tests.csproj"
                                | _ -> !! "./test/*.Tests/*.Tests.csproj" // if you need to filter specs for Linux vs. Windows, do it here
            rawProjects |> Seq.choose filterProjects
     
        let runSingleProject project =
            let arguments =
                match (hasTeamCity) with
                | true -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none -teamcity" testNetCoreVersion outputTests)
                | false -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none" testNetCoreVersion outputTests)

            let result = ExecProcess(fun info ->
                info.FileName <- "dotnet"
                info.WorkingDirectory <- (Directory.GetParent project).FullName
                info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0) 
        
            ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

        CreateDir outputTests
        projects |> Seq.iter (runSingleProject)
)

Target "RunTestsNetCore21" (fun _ ->
    if not skipBuild.Value then
        let projects = 
            let rawProjects = match (isWindows) with 
                                | true -> !! "./test/*.Tests/*.Tests.csproj"
                                | _ -> !! "./test/*.Tests/*.Tests.csproj" // if you need to filter specs for Linux vs. Windows, do it here
            rawProjects |> Seq.choose filterProjects
     
        let runSingleProject project =
            let arguments =
                match (hasTeamCity) with
                | true -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none -teamcity" testNetCore21Version outputTests)
                | false -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none" testNetCore21Version outputTests)

            let result = ExecProcess(fun info ->
                info.FileName <- "dotnet"
                info.WorkingDirectory <- (Directory.GetParent project).FullName
                info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0) 
        
            ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

        CreateDir outputTests
        projects |> Seq.iter (runSingleProject)
)

Target "RunTestsNetFx471" (fun _ ->    
    let projects = 
        let rawProjects = match (isWindows) with 
                            | true -> !! "./test/*.Tests/*.Tests.csproj"
                                      -- "./test/*.Tests/DotNetty.Suite.Tests.csproj"
                                      -- "./test/*.Tests/DotNetty.Buffers.ReaderWriter.Tests"
                            | _ -> !! "./test/*.Tests/*.Tests.csproj" // if you need to filter specs for Linux vs. Windows, do it here
                                   -- "./test/*.Tests/DotNetty.Suite.Tests.csproj"
                                   -- "./test/*.Tests/DotNetty.Buffers.ReaderWriter.Tests"
        rawProjects |> Seq.choose filterProjects
    
    let runSingleProject project =
        let arguments =
            match (hasTeamCity) with
            | true -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none -teamcity" testNetFrameworkVersion outputTests)
            | false -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none" testNetFrameworkVersion outputTests)

        let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0) 
        
        ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

    CreateDir outputTests
    projects |> Seq.iter (runSingleProject)
)

Target "RunTestsNetFx451" (fun _ ->    
    let projects = 
        let rawProjects = match (isWindows) with 
                            | true -> !! "./test/*.Tests/*.Tests.csproj"
                                      -- "./test/*.Tests/DotNetty.Suite.Tests.csproj"
                                      -- "./test/*.Tests/DotNetty.Buffers.ReaderWriter.Tests"
                            | _ -> !! "./test/*.Tests/*.Tests.csproj" // if you need to filter specs for Linux vs. Windows, do it here
                                   -- "./test/*.Tests/DotNetty.Suite.Tests.csproj"
                                   -- "./test/*.Tests/DotNetty.Buffers.ReaderWriter.Tests"
        rawProjects |> Seq.choose filterProjects
    
    let runSingleProject project =
        let arguments =
            match (hasTeamCity) with
            | true -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none -teamcity" testNetFramework451Version outputTests)
            | false -> (sprintf "test -c Debug --no-build --logger:trx --logger:\"console;verbosity=normal\" --framework %s -- RunConfiguration.TargetPlatform=x64 --results-directory \"%s\" -- -parallel none" testNetFramework451Version outputTests)

        let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- (Directory.GetParent project).FullName
            info.Arguments <- arguments) (TimeSpan.FromMinutes 30.0) 
        
        ResultHandling.failBuildIfXUnitReportedError TestRunnerErrorLevel.Error result

    CreateDir outputTests
    projects |> Seq.iter (runSingleProject)
)

FinalTarget "KillCreatedProcesses" (fun _ ->
    log "Shutting down dotnet build-server"
    let result = ExecProcess(fun info ->
            info.FileName <- "dotnet"
            info.WorkingDirectory <- __SOURCE_DIRECTORY__
            info.Arguments <- "build-server shutdown") (System.TimeSpan.FromMinutes 2.0)
    if result <> 0 then failwithf "dotnet build-server shutdown failed"
)

//--------------------------------------------------------------------------------
// Help
//--------------------------------------------------------------------------------

Target "Help" <| fun _ ->
    List.iter printfn [
      "usage:"
      "/build [target]"
      ""
      " Targets for building:"
      " * Build      Builds"
      " * RunTests   Runs tests"
      " * All        Builds, run tests, creates and optionally publish nuget packages"
      ""
      " Other Targets"
      " * Help       Display this help"
      ""]

//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------

Target "BuildDebug" DoNothing
Target "All" DoNothing
Target "Nuget" DoNothing
Target "RunTestsFull" DoNothing
Target "RunTestsNetCoreFull" DoNothing

// build dependencies
"Clean" ==> "Build"
"Build" ==> "BuildDebug"
"ComputeIncrementalChanges" ==> "Build" // compute incremental changes

// tests dependencies
"Build" ==> "RunTests"
"Build" ==> "RunTestsNetCore31"
"Build" ==> "RunTestsNetCore21"
"Build" ==> "RunTestsNetFx471"
"Build" ==> "RunTestsNetFx451"

// all
"BuildDebug" ==> "All"
"RunTests" ==> "All"
"RunTestsNetCore31" ==> "All"
"RunTestsNetCore21" ==> "All"
"RunTestsNetFx471" ==> "All"
"RunTestsNetFx451" ==> "All"

RunTargetOrDefault "Help"