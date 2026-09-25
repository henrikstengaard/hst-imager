param (
    [string]$target
)

$targets = if ($target) { @($target) } else { @('win-x64', 'win-arm64', 'osx-x64', 'osx-arm64', 'linux-x64', 'linux-arm64') }

$commitCount = (git rev-list --count HEAD)
$buildVersion = (Select-Xml -Path ./Directory.Build.props -XPath '/Project/PropertyGroup/Version').Node.InnerXML

$version = $buildVersion -replace '^(.*)\.\d+.*$', "`$1.$commitCount"

Push-Location 'Hst.Imager.AvaloniaApp'

foreach ($target in $targets)
{
	Write-Host "Building target: $target"
	Write-Host "Version: $version"

	# include native libraries in single file for windows only, same as build and release workflow
	$includeNativeLibraries = if ($target -like 'win-*') { 'true' } else { 'false' }

	dotnet publish --configuration Release -p:PublishSingleFile=True -p:SelfContained=True -p:RuntimeIdentifier=$target -p:IncludeNativeLibrariesForSelfExtract=$includeNativeLibraries -p:PublishDir=publish/$target -p:Version=$version
}

Pop-Location
