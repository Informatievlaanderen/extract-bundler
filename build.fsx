#r "paket:
version 7.0.2
framework: net6.0
source https://api.nuget.org/v3/index.json
nuget Be.Vlaanderen.Basisregisters.Build.Pipeline 6.0.3 //"

#load "packages/Be.Vlaanderen.Basisregisters.Build.Pipeline/Content/build-generic.fsx"

open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO
open Fake.IO.FileSystemOperators
open ``Build-generic``

let product = "Basisregisters Vlaanderen"
let copyright = "Copyright (c) Vlaamse overheid"
let company = "Vlaamse overheid"

let dockerRepository = "public-api"
let assemblyVersionNumber = (sprintf "2.%s")

Target.create "BuildContainer" (fun _ ->
  let filterAllFiles = (fun _ -> true)
  let destinationDirectory = buildDir @@ "BaseRegistries" @@ "linux"

  Shell.copyDir destinationDirectory "src" filterAllFiles
  Shell.copyDir (destinationDirectory @@ "node_modules") "node_modules" filterAllFiles
  containerize dockerRepository "BaseRegistries" "extract-bundler"
)

"NpmInstall"
==> "Clean"
==> "BuildContainer"

Target.runOrDefault "BuildContainer"
