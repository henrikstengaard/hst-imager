# kill any running hst imager guiapp processes
Get-Process | Where-Object { $_.ProcessName -match 'Hst.Imager' } | Foreach-Object { Stop-Process -Id $_.Id }

# create build dir, if it doesn't exist
if (!(Test-Path 'Hst.Imager.GuiApp/build'))
{
    mkdir 'Hst.Imager.GuiApp/build' | Out-Null
}

# create build hst imager guiapp dir, if it doesn't exist
if (!(Test-Path 'Hst.Imager.GuiApp/build/hst.imager.guiapp'))
{
    mkdir 'Hst.Imager.GuiApp/build/hst.imager.guiapp' | Out-Null
}

# build and publish hst imager guiapp
dotnet publish --configuration Release --output Hst.Imager.GuiApp/build/hst.imager.guiapp Hst.Imager.GuiApp/Hst.Imager.GuiApp.csproj -p:DefineConstants="WINDOWS%3BBACKEND" -p:PublishSingleFile=True -p:SelfContained=True -p:RuntimeIdentifier=win-x64 -p:PublishReadyToRun=True -p:IncludeNativeLibrariesForSelfExtract=true

# copy desktop to build
Copy-Item -Recurse -Force Hst.Imager.GuiApp/desktop/* Hst.Imager.GuiApp/build

if (!(Test-Path 'Hst.Imager.GuiApp/build/neutralinojs/resources'))
{
    mkdir 'Hst.Imager.GuiApp/build/neutralinojs/resources' | Out-Null
}

# copy published hst imager guiapp client app to neutralinojs resources
Copy-Item -Recurse -Force 'Hst.Imager.GuiApp/build/hst.imager.guiapp/ClientApp/build/*' 'Hst.Imager.GuiApp/build/neutralinojs/resources'

# copy published hst imager guiapp client app to neutralinojs resources
Copy-Item -Recurse -Force 'Hst.Imager.GuiApp/build/hst.imager.guiapp/Hst.Imager.GuiApp.exe' 'Hst.Imager.GuiApp/build/neutralinojs/Hst.Imager.Backend.exe'

# add neutralinojs meta tag to index html 
$indexHtml = Get-Content -Path 'Hst.Imager.GuiApp/build/neutralinojs/resources/index.html' -Raw
$indexHtml = $indexHtml -replace '</head>', '<meta name="host" content="neutralinojs"></head>'
Set-Content -Path 'Hst.Imager.GuiApp/build/neutralinojs/resources/index.html' -Value $indexHtml

# update neutralinojs
Push-Location 'Hst.Imager.GuiApp/build/neutralinojs'
neu.cmd update
neu.cmd version
Pop-Location