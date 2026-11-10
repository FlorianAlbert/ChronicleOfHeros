$ErrorActionPreference = 'Stop'

$root = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw 'Run this script inside the repository.' }

$current = git config --get core.hooksPath
if ($LASTEXITCODE -gt 1) { throw 'Could not read core.hooksPath.' }
$hooks = Join-Path $root '.githooks'
if ($current -and [System.IO.Path]::GetFullPath($current) -ne [System.IO.Path]::GetFullPath($hooks)) {
    throw "core.hooksPath is already configured as '$current'. Integrate the existing hooks before installing."
}

git config --local core.hooksPath $hooks
if ($LASTEXITCODE -ne 0) { throw 'Could not install repository hooks.' }
Write-Output "Installed repository hooks from $hooks"
