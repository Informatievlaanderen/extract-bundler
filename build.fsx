#r "paket:
version 7.0.2
framework: net6.0
source https://api.nuget.org/v3/index.json

nuget System.Text.Encodings.Web 6.0.0
nuget System.Collections.Immutable 6.0.0
nuget System.Configuration.ConfigurationManager 6.0.0
nuget System.Reflection.Metadata 6.0.0
nuget System.Reflection.MetadataLoadContext 6.0.0
nuget System.Text.Encoding.CodePages 6.0.0
nuget System.Text.Json 6.0.0
nuget System.Threading.Tasks.Dataflow 6.0.0
nuget System.Security.Cryptography.ProtectedData 6.0.0
nuget System.Security.Cryptography.Pkcs 6.0.0
nuget System.Security.Permissions 6.0.0
nuget System.Formats.Asn1 6.0.0
nuget System.Windows.Extensions 6.0.0
nuget System.Drawing.Common 6.0.0
nuget Microsoft.Win32.SystemEvents 6.0.0

nuget Microsoft.NET.StringTools 17.3.2
nuget Microsoft.NET.Test.Sdk 17.3.2
nuget Microsoft.TestPlatform.TestHost 17.3.2
nuget Microsoft.TestPlatform.ObjectModel 17.3.2
nuget Microsoft.Build 17.3.2
nuget Microsoft.Build.Framework 17.3.2
nuget Microsoft.Build.Utilities.Core 17.3.2

nuget NuGet.Common 6.4.0
nuget NuGet.Frameworks 6.4.0
nuget NuGet.Configuration 6.4.0
nuget NuGet.Packaging 6.4.0

nuget FSharp.Core 6.0

nuget Be.Vlaanderen.Basisregisters.Build.Pipeline 6.0.6 //"

#load "packages/Be.Vlaanderen.Basisregisters.Build.Pipeline/Content/build-generic.fsx"

open Fake
open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.FileSystemOperators
open ``Build-generic``

let product = "Basisregisters Vlaanderen"
let copyright = "Copyright (c) Vlaamse overheid"
let company = "Vlaamse overheid"

//TODO
let dockerRepository = "extract-bundler"
let assemblyVersionNumber = (sprintf "2.%s")
let nugetVersionNumber = (sprintf "%s")

let buildSolution = buildSolution assemblyVersionNumber
let buildSource = build assemblyVersionNumber
let buildTest = buildTest assemblyVersionNumber
let setVersions = (setSolutionVersions assemblyVersionNumber product copyright company)
let test = testSolution
let publishSource = publish assemblyVersionNumber
let pack = pack nugetVersionNumber
let containerize = containerize dockerRepository
let push = push dockerRepository

supportedRuntimeIdentifiers <- [ "msil"; "linux-x64" ]

// Solution -----------------------------------------------------------------------

Target.create "Restore_Solution" (fun _ -> restore "ExtractBundler")

Target.create "Build_Solution" (fun _ ->
  setVersions "SolutionInfo.cs"
  buildSolution "ExtractBundler")

// TODO
Target.create "Test_Solution" (fun _ ->
    [
      
        "test" @@ "ExtractBundler.IntegrationTests"
    ] |> List.iter testWithDotNet
)

Target.create "Publish_Solution" (fun _ ->
  [
    "ExtractBundler.Console"
  ] |> List.iter publishSource)

//TODO
Target.create "Containerize_ExtractBundler" (fun _ -> containerize "ExtractBundler.Console" "extract-bundler")

Target.create "SetAssemblyVersions" (fun _ -> setVersions "SolutionInfo.cs")
// --------------------------------------------------------------------------------

Target.create "Build" ignore
Target.create "Test" ignore
Target.create "Publish" ignore
Target.create "Pack" ignore
Target.create "Containerize" ignore

"NpmInstall"
  ==> "DotNetCli"
  ==> "Clean"
  ==> "Restore_Solution"
  ==> "Build_Solution"
  ==> "Build"

"Build"
  ==> "Test_Solution"
  ==> "Test"

"Test"
  ==> "Publish_Solution"
  ==> "Publish"

"Publish"
  ==> "Pack"

"Pack"
  ==> "Containerize"
// Possibly add more projects to containerize here

// By default we build & test
Target.runOrDefault "Test"
