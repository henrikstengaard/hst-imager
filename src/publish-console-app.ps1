# update neutralinojs
Push-Location 'Hst.Imager.ConsoleApp'

foreach ($target in @('win-x64', 'win-arm64', 'osx-x64', 'osx-arm64', 'linux-x64', 'linux-arm', 'linux-arm64'))
{
    dotnet publish --configuration Release -p:PublishSingleFile=True -p:SelfContained=True -p:RuntimeIdentifier=$target -p:PublishReadyToRun=True -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishDir=publish/$target
}

Pop-Location