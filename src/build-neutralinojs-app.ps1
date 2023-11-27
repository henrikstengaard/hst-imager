# kill any running hst imager guiapp processes
Get-Process | Where-Object { $_.ProcessName -match 'Hst.Imager' } | Foreach-Object { Stop-Process -Id $_.Id }

# create build dir, if it doesn't exist
if (!(Test-Path 'build'))
{
    mkdir build | Out-Null
}

# create build hst imager guiapp dir, if it doesn't exist
if (!(Test-Path 'build/hst.imager.guiapp'))
{
    mkdir 'build/hst.imager.guiapp' | Out-Null
}

# build and publish hst imager guiapp
dotnet publish --output build/hst.imager.guiapp Hst.Imager.GuiApp/Hst.Imager.GuiApp.csproj -p:DefineConstants=BACKEND -p:PublishSingleFile=True -p:SelfContained=True -p:RuntimeIdentifier=win-x64 -p:PublishReadyToRun=True -p:IncludeNativeLibrariesForSelfExtract=true

if (!(Test-Path 'build/neutralinojs'))
{
    mkdir 'build/neutralinojs' | Out-Null
}

# copy neutralinojs template to build
Copy-Item -Recurse -Force desktop/neutralinojs/* build/neutralinojs

if (!(Test-Path 'build/neutralinojs/resources'))
{
    mkdir 'build/neutralinojs/resources' | Out-Null
}

# copy published hst imager guiapp client app to neutralinojs resources
Copy-Item -Recurse -Force 'build/hst.imager.guiapp/ClientApp/build/*' 'build/neutralinojs/resources'

# copy published hst imager guiapp client app to neutralinojs resources
Copy-Item -Recurse -Force 'build/hst.imager.guiapp/Hst.Imager.GuiApp.exe' 'build/neutralinojs/Hst.Imager.Backend.exe'

# add neutralinojs meta tag to index html 
$indexHtml = Get-Content -Path 'build/neutralinojs/resources/index.html' -Raw
$indexHtml = $indexHtml -replace '</head>', '<meta name="host" content="neutralinojs"></head>'
Set-Content -Path 'build/neutralinojs/resources/index.html' -Value $indexHtml

# update neutralinojs
Push-Location 'build/neutralinojs'
neu.cmd update
neu.cmd version
Pop-Location