# Generate test data
# ------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2026-02-10
#
# A powershell script to generate test data with many files and directories
# for testing copying files.

# get current directory
$currentDir = Get-Location

# test data directory path
$testDataDir = Join-Path -Path $currentDir -ChildPath "test-data"

# remove existing test data directory if it exists
if (Test-Path -Path $testDataDir) {
    Remove-Item -Path $testDataDir -Recurse -Force
}

# create test data directory
New-Item -Path $testDataDir -ItemType Directory | Out-Null

for ($dir = 1; $dir -le 10; $dir++)
{
    # create dir directory
    $dirPath = Join-Path -Path $testDataDir -ChildPath "dir-$dir"
    New-Item -Path $dirPath -ItemType Directory | Out-Null

    for ($file = 1; $file -le 1000; $file++)
    {
        # create data to write
        [byte[]]$data = New-Object byte[] (10 * $file)

    
        # create file
        $filePath = Join-Path -Path $dirPath -ChildPath "file-$file"
        Set-Content -Path $filePath -Value $data | Out-Null
    }
}