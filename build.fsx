#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open System

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.BuildServer

BuildServer.install [
    TeamCity.Installer ]

let serverPath = Path.getFullName "./src/Server"
let clientPath = Path.getFullName "./src/Client"
let clientDeployPath = Path.combine clientPath "deploy"
let deployDir = Path.getFullName "./deploy"

let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match ProcessUtils.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " +
            "Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg

let nodeTool = platformTool "node" "node.exe"
let yarnTool = platformTool "yarn" "yarn.cmd"

let runTool cmd args workingDir =
    let arguments = args |> String.split ' ' |> Arguments.OfArgs
    Command.RawCommand (cmd, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let runDotNet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let openBrowser url =
    //https://github.com/dotnet/corefx/issues/10361
    Command.ShellCommand url
    |> CreateProcess.fromCommand
    |> CreateProcess.ensureExitCodeWithMessage "opening browser failed"
    |> Proc.run
    |> ignore


Target.create "Clean" (fun _ ->
    use __ = TeamCity.block "Clean" "Cleaning"
    [ deployDir
      clientDeployPath ]
    |> Shell.cleanDirs
)

Target.create "InstallClient" (fun _ ->
    use __ = TeamCity.block "Install Client" "Installing the client"
    printfn "Node version:"
    runTool nodeTool "--version" __SOURCE_DIRECTORY__
    printfn "Yarn version:"
    runTool yarnTool "--version" __SOURCE_DIRECTORY__
    runTool yarnTool "install --frozen-lockfile" __SOURCE_DIRECTORY__
    runDotNet "restore" clientPath
)

Target.create "Build" (fun _ ->
    use __ = TeamCity.block "Build" "Building"
    runDotNet "build" serverPath
    runTool yarnTool "webpack-cli -p" __SOURCE_DIRECTORY__
)


let server = async {
        runDotNet "watch run" serverPath
    }

let client = async {
    runTool yarnTool "webpack-dev-server" __SOURCE_DIRECTORY__
}

let browser = async {
    do! Async.Sleep 5000
    openBrowser "http://localhost:8080"
}

Target.create "RunClient" (fun _ ->
    [client; browser]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
)

Target.create "RunServer" (fun _ ->
    [server; browser]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

)

Target.create "RunBoth" (fun _ ->
    [client; server; browser]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
)

open Fake.Core.TargetOperators

"Clean"
    ==> "InstallClient"
    ==> "Build"


"Clean"
    ==> "InstallClient"
    ==> "RunClient"

"Clean"
    ==> "RunServer"

"Clean"
    ==> "InstallClient"
    ==> "RunBoth"

Target.runOrDefaultWithArguments "Build"
