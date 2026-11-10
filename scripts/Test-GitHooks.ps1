$ErrorActionPreference = 'Stop'
$hooks = Join-Path (Split-Path $PSScriptRoot -Parent) '.githooks'
$temporaryRepository = Join-Path ([System.IO.Path]::GetTempPath()) ("chronicle-hooks-" + [guid]::NewGuid())

function Invoke-Git {
    param([string[]] $Arguments, [bool] $ShouldSucceed = $true)

    $output = & git -C $temporaryRepository @Arguments 2>&1
    $code = $LASTEXITCODE
    if (($code -eq 0) -ne $ShouldSucceed) {
        throw "Unexpected git exit code ${code}: git $($Arguments -join ' ')`n$($output -join "`n")"
    }
    return ($output -join "`n")
}

try {
    New-Item -ItemType Directory -Path $temporaryRepository | Out-Null
    Invoke-Git -Arguments @('init', '--initial-branch=main') | Out-Null
    Invoke-Git -Arguments @('config', 'user.name', 'Hook Test') | Out-Null
    Invoke-Git -Arguments @('config', 'user.email', 'hook-test@example.invalid') | Out-Null
    Invoke-Git -Arguments @('config', 'commit.gpgsign', 'false') | Out-Null
    Invoke-Git -Arguments @('config', 'core.hooksPath', $hooks) | Out-Null

    Push-Location $temporaryRepository
    try {
        $installer = Join-Path $PSScriptRoot 'Install-GitHooks.ps1'
        $expectedHooks = Join-Path $temporaryRepository '.githooks'
        Invoke-Git -Arguments @('config', '--unset', 'core.hooksPath') | Out-Null
        & $installer | Out-Null
        & $installer | Out-Null
        if ((Invoke-Git -Arguments @('config', '--get', 'core.hooksPath')) -ne $expectedHooks) {
            throw 'Installer did not configure the repository-local hook path.'
        }
        Invoke-Git -Arguments @('config', 'core.hooksPath', 'existing-hooks') | Out-Null
        $refused = $false
        try {
            & $installer | Out-Null
        }
        catch {
            if ($_.Exception.Message -notmatch 'already configured') { throw }
            $refused = $true
        }
        if (-not $refused) { throw 'Installer replaced an existing hook configuration.' }
        if ((Invoke-Git -Arguments @('config', '--get', 'core.hooksPath')) -ne 'existing-hooks') {
            throw 'Installer changed the existing hook configuration.'
        }
        Invoke-Git -Arguments @('config', 'core.hooksPath', $hooks) | Out-Null
    }
    finally {
        Pop-Location
    }

    $blocked = Invoke-Git -Arguments @('commit', '--allow-empty', '-m', 'blocked') -ShouldSucceed $false
    if ($blocked -notmatch 'main is protected') { throw "Expected protected-main diagnostic: $blocked" }
    Invoke-Git -Arguments @('rev-parse', '--verify', 'HEAD') -ShouldSucceed $false | Out-Null

    Invoke-Git -Arguments @('switch', '-c', 'task/hook-test') | Out-Null
    Invoke-Git -Arguments @('commit', '--allow-empty', '-m', 'allowed') | Out-Null
    $before = Invoke-Git -Arguments @('rev-parse', 'HEAD')
    Invoke-Git -Arguments @('switch', '-c', 'main') | Out-Null
    Invoke-Git -Arguments @('commit', '--allow-empty', '-m', 'blocked again') -ShouldSucceed $false | Out-Null
    if ((Invoke-Git -Arguments @('rev-parse', 'HEAD')) -ne $before) { throw 'Blocked commit changed HEAD.' }

    Invoke-Git -Arguments @('switch', '--detach') | Out-Null
    $detached = Invoke-Git -Arguments @('commit', '--allow-empty', '-m', 'detached') -ShouldSucceed $false
    if ($detached -notmatch 'check out a task branch') { throw "Expected detached-HEAD diagnostic: $detached" }
    if ((Invoke-Git -Arguments @('rev-parse', 'HEAD')) -ne $before) { throw 'Blocked detached commit changed HEAD.' }
    Write-Output 'Git hook checks passed: installer is idempotent and preserves existing hooks; main and detached HEAD rejected; task branch accepted.'
}
finally {
    if (Test-Path -LiteralPath $temporaryRepository) {
        Remove-Item -LiteralPath $temporaryRepository -Recurse -Force
    }
}
