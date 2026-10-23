# notarize a file (.dmg, .zip or .pkg) with apple notary service and optionally staple the notarization ticket.
# requires MACOS_KEYCHAIN and MACOS_NOTARY_PROFILE environment variables exported by macos-signing-setup.ps1.

param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [switch]$Staple
)

$ErrorActionPreference = 'Stop'

Write-Output "Notarizing '$Path'"
$output = xcrun notarytool submit $Path --keychain-profile $env:MACOS_NOTARY_PROFILE --keychain $env:MACOS_KEYCHAIN --wait --output-format json
$exitCode = $LASTEXITCODE
$result = try { $output | ConvertFrom-Json } catch { $null }
if (-not $result -or -not $result.id) { throw "Failed to submit '$Path' for notarization (exit code $exitCode): $output" }

# check status instead of exit code, as rejected notarizations are not reliably reported by exit code
Write-Output "Notarization submission $($result.id) has status '$($result.status)'"
if ($result.status -ne 'Accepted')
{
    xcrun notarytool log $result.id --keychain-profile $env:MACOS_NOTARY_PROFILE --keychain $env:MACOS_KEYCHAIN
    throw "Notarization of '$Path' failed with status '$($result.status)'"
}

if ($Staple)
{
    xcrun stapler staple $Path
    if ($LASTEXITCODE -ne 0) { throw "Failed to staple notarization ticket to '$Path'" }
}
