param (
    [string]$target
)

$targets = if ($target) { @($target) } else { @('win-x64', 'win-x86', 'win-arm64', 'osx-x64', 'osx-arm64', 'linux-x64', 'linux-arm', 'linux-arm64') }

$commitCount = (git rev-list --count HEAD)
$buildVersion = (Select-Xml -Path ./Directory.Build.props -XPath '/Project/PropertyGroup/Version').Node.InnerXML

$version = $buildVersion -replace '^(.*)\.\d+.*$', "`$1.$commitCount"
$assemblyVersion = $buildVersion -replace '^(.*)\.\d+.*$', "`$1.$commitCount.0"

Push-Location 'Hst.Imager.ConsoleApp'

foreach ($target in $targets)
{
	Write-Host "Building target: $target"
	Write-Host "Version: $version"
	Write-Host "Assembly version: $assemblyVersion"
	
	dotnet publish --configuration Release -p:PublishSingleFile=True -p:SelfContained=True -p:RuntimeIdentifier=$target -p:PublishReadyToRun=True -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishDir=publish/$target -p:Version=$version -p:AssemblyVersion=$assemblyVersion -p:FileVersion=$assemblyVersion -p:PackageVersion=$assemblyVersion
}

Pop-Location