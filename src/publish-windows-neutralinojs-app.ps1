# kill any running hst imager guiapp processes
Get-Process | Where-Object { $_.ProcessName -match 'Hst.Imager' } | Foreach-Object { Stop-Process -Id $_.Id }

$env:HST_IMAGER_VERSION = '1.1.0.0'

# build neutralinojs app
Push-Location 'Hst.Imager.GuiApp/build/neutralinojs'
neu.cmd build
Pop-Location

# update exe
Copy-Item Hst.Imager.GuiApp/build/neutralinojs/dist/hst.imager/hst.imager-win_x64.exe Hst.Imager.GuiApp/build/windows-tools -force
Copy-Item Hst.Imager.GuiApp/hst.imager.ico Hst.Imager.GuiApp/build/windows-tools -force
Push-Location 'Hst.Imager.GuiApp/build/windows-tools'
npm install
npm run update-exe
Pop-Location

# build squirrel setup
# --------------------

Push-Location 'Hst.Imager.GuiApp/build/squirrel'

# https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#packing-using-a-nuspec

if (Test-Path -Path ./bin/Release)
{
    Get-ChildItem -Path ./bin/Release -Filter *.nupkg | Foreach-Object { Remove-Item $_.FullName -force }
}
dotnet pack hst.imager.csproj --configuration Release -p:NuspecFile=hst.imager.nuspec -p:NuspecProperties="Version=$env:HST_IMAGER_VERSION;PackageVersion=$env:HST_IMAGER_VERSION" -p:NuspecBasePath=.

# restore squirrel windows nuget package
dotnet restore hst.imager.csproj

$squirrelPath =  (gci './packages/clowd.squirrel' -Recurse -Filter 'Squirrel.exe' | Select-Object -First 1).FullName
$packagePath = (gci './bin/Release' -Recurse -Filter 'Hst.Imager.*.nupkg' | Select-Object -First 1).FullName
& "$squirrelPath" releasify --package "$packagePath" --icon ..\..\hst.imager.ico --appIcon ..\..\hst.imager.ico --msi=x64 --releaseDir .\release

Pop-Location


