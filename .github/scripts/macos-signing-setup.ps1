# set up macos code signing and notarization on a github actions macos runner.
# imports developer id application certificate into a temporary keychain and stores
# app store connect api key credentials as a notarytool profile in the same keychain.
# exports MACOS_KEYCHAIN, MACOS_NOTARY_PROFILE and MACOS_SIGN_IDENTITY to github env for later steps.
#
# requires environment variables:
# - MACOS_CERTIFICATE_P12: base64 encoded developer id application certificate with private key
# - MACOS_CERTIFICATE_PASSWORD: password for p12 certificate
# - MACOS_NOTARY_KEY_P8: app store connect api key (.p8 contents)
# - MACOS_NOTARY_KEY_ID: app store connect api key id
# - MACOS_NOTARY_ISSUER_ID: app store connect api issuer id
# - MACOS_SIGN_IDENTITY: signing identity, e.g. "Developer ID Application: Name (TEAMID)"

$ErrorActionPreference = 'Stop'

function Invoke-Native([string]$errorMessage, [scriptblock]$command)
{
    & $command
    if ($LASTEXITCODE -ne 0) { throw $errorMessage }
}

foreach ($name in @('MACOS_CERTIFICATE_P12', 'MACOS_CERTIFICATE_PASSWORD', 'MACOS_NOTARY_KEY_P8', 'MACOS_NOTARY_KEY_ID', 'MACOS_NOTARY_ISSUER_ID', 'MACOS_SIGN_IDENTITY'))
{
    if (-not [Environment]::GetEnvironmentVariable($name)) { throw "Environment variable '$name' is not set" }
}

$keychainPath = Join-Path $env:RUNNER_TEMP -ChildPath 'signing.keychain-db'
$keychainPassword = [guid]::NewGuid().ToString()
$certificatePath = Join-Path $env:RUNNER_TEMP -ChildPath 'certificate.p12'
$intermediateCertificatePath = Join-Path $env:RUNNER_TEMP -ChildPath 'DeveloperIDG2CA.cer'
$notaryKeyPath = Join-Path $env:RUNNER_TEMP -ChildPath 'notary-key.p8'
$notaryProfile = 'hst-imager-notary'

try
{
    [IO.File]::WriteAllBytes($certificatePath, [Convert]::FromBase64String($env:MACOS_CERTIFICATE_P12))
    [IO.File]::WriteAllText($notaryKeyPath, $env:MACOS_NOTARY_KEY_P8)

    # create temporary keychain, which stays unlocked for 6 hours
    Invoke-Native 'Failed to create keychain' { security create-keychain -p $keychainPassword $keychainPath }
    Invoke-Native 'Failed to set keychain settings' { security set-keychain-settings -lut 21600 $keychainPath }
    Invoke-Native 'Failed to unlock keychain' { security unlock-keychain -p $keychainPassword $keychainPath }

    # import developer id g2 intermediate certificate, so codesign can build the full certificate chain
    Invoke-WebRequest -Uri 'https://www.apple.com/certificateauthority/DeveloperIDG2CA.cer' -OutFile $intermediateCertificatePath
    Invoke-Native 'Failed to import intermediate certificate' { security import $intermediateCertificatePath -k $keychainPath -T /usr/bin/codesign }

    # import certificate and allow codesign to use private key without prompting
    Invoke-Native 'Failed to import certificate' { security import $certificatePath -P $env:MACOS_CERTIFICATE_PASSWORD -A -t cert -f pkcs12 -k $keychainPath }
    Invoke-Native 'Failed to set key partition list' { security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k $keychainPassword $keychainPath | Out-Null }

    # add keychain to user search list, so codesign and spctl can find signing identity
    $keychains = @(security list-keychains -d user | ForEach-Object { $_.Trim().Trim('"') } | Where-Object { $_ })
    Invoke-Native 'Failed to add keychain to search list' { security list-keychains -d user -s $keychainPath @keychains }

    $identities = security find-identity -v -p codesigning $keychainPath
    if (-not ($identities | Select-String -SimpleMatch $env:MACOS_SIGN_IDENTITY))
    {
        throw "Signing identity '$env:MACOS_SIGN_IDENTITY' not found in keychain, valid identities: $($identities -join ', ')"
    }

    # store notarytool credentials as profile in keychain
    Invoke-Native 'Failed to store notarytool credentials' { xcrun notarytool store-credentials $notaryProfile --key $notaryKeyPath --key-id $env:MACOS_NOTARY_KEY_ID --issuer $env:MACOS_NOTARY_ISSUER_ID --keychain $keychainPath }
}
finally
{
    Remove-Item -Path $certificatePath, $intermediateCertificatePath, $notaryKeyPath -Force -ErrorAction SilentlyContinue
}

"MACOS_KEYCHAIN={0}" -f $keychainPath >> $env:GITHUB_ENV
"MACOS_NOTARY_PROFILE={0}" -f $notaryProfile >> $env:GITHUB_ENV
"MACOS_SIGN_IDENTITY={0}" -f $env:MACOS_SIGN_IDENTITY >> $env:GITHUB_ENV
