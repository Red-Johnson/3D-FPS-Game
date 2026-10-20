# Run from any directory after building CodeStyleCheck.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixtureRoot = Join-Path $projectRoot ('Temp/CodeStyleIntegration-' + [guid]::NewGuid().ToString('N'))
$scriptsRoot = Join-Path $fixtureRoot 'Assets/02.Scripts'
$fixtureSource = Join-Path $scriptsRoot 'Example.cs'
$checker = Join-Path $PSScriptRoot 'bin/Debug/net10.0/CodeStyleCheck.dll'
if (-not (Test-Path -LiteralPath $checker)) { throw 'Build CodeStyleCheck first.' }

function Invoke-Dotnet([string[]] $Arguments, [int] $ExpectedExit = 0, [string] $Diagnostic = '') {
    $output = & dotnet @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne $ExpectedExit -or ($Diagnostic -and ($output -join "`n") -notmatch $Diagnostic)) {
        throw "dotnet $Arguments returned $exitCode (expected $ExpectedExit).`n$($output -join "`n")"
    }
}

New-Item -ItemType Directory -Path $scriptsRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot '.editorconfig') -Destination $fixtureRoot
$solution = [IO.File]::ReadAllText((Join-Path $projectRoot '3D FPS Game.sln'))
$solution = $solution.Replace('Assembly-CSharp.csproj', 'Fixture.csproj')
[IO.File]::WriteAllText((Join-Path $fixtureRoot '3D FPS Game.sln'), $solution)
[IO.File]::WriteAllText((Join-Path $fixtureRoot 'Fixture.csproj'), @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Assets/**/*.cs" />
    <Compile Include="UnityStubs.cs" />
  </ItemGroup>
</Project>
'@)
[IO.File]::WriteAllText((Join-Path $fixtureRoot 'UnityStubs.cs'), @'
namespace UnityEngine
{
    public class MonoBehaviour { }
    public class HeaderAttribute : System.Attribute { public HeaderAttribute(string text) { } }
    public class SerializeField : System.Attribute { }
    public class MinAttribute : System.Attribute { public MinAttribute(float value) { } }
}
'@)
Invoke-Dotnet @('restore', (Join-Path $fixtureRoot 'Fixture.csproj'), '--verbosity', 'quiet')

$desired = @'
using UnityEngine;

public class Example : MonoBehaviour
{
    // 점프 제목과 값은 보존한다.
    [Header("점프 설정")] [SerializeField, Min(0f)]
    private float _jumpPower = 10f;
}

'@
$passed = 0
foreach ($encoding in @([Text.UTF8Encoding]::new($false), [Text.UTF8Encoding]::new($true), [Text.UnicodeEncoding]::new($false, $true))) {
    foreach ($newline in @("`n", "`r`n")) {
        $valid = $desired.Replace("`r`n", "`n").Replace("`n", $newline)
        [IO.File]::WriteAllText($fixtureSource, $valid, $encoding)
        $before = [IO.File]::ReadAllBytes($fixtureSource)
        Invoke-Dotnet @($checker, '--root', $fixtureRoot)
        if ([Convert]::ToHexString($before) -ne [Convert]::ToHexString([IO.File]::ReadAllBytes($fixtureSource))) {
            throw 'Check mode modified the source.'
        }
        $invalid = $valid.Replace(')] [SerializeField', ')]   [SerializeField').Replace(' = ', '=')
        [IO.File]::WriteAllText($fixtureSource, $invalid, $encoding)
        $invalidBytes = [IO.File]::ReadAllBytes($fixtureSource)
        Invoke-Dotnet @($checker, '--root', $fixtureRoot) 1 'STYLEFORMAT'
        if ([Convert]::ToHexString($invalidBytes) -ne [Convert]::ToHexString([IO.File]::ReadAllBytes($fixtureSource))) {
            throw 'Failed check modified the source.'
        }
        Invoke-Dotnet @($checker, '--root', $fixtureRoot, '--fix')
        if ([Convert]::ToHexString($before) -ne [Convert]::ToHexString([IO.File]::ReadAllBytes($fixtureSource))) {
            throw 'Fix did not preserve content, line endings, or encoding/BOM.'
        }
        Invoke-Dotnet @($checker, '--root', $fixtureRoot, '--fix')
        if ([Convert]::ToHexString($before) -ne [Convert]::ToHexString([IO.File]::ReadAllBytes($fixtureSource))) {
            throw 'Repeated fix was not idempotent.'
        }
        $passed++
        Write-Output "PASS: encoding/BOM $($encoding.WebName), newline length $($newline.Length)"
    }
}

[IO.File]::WriteAllText($fixtureSource, $desired, [Text.UTF8Encoding]::new($false))
$solutionPath = Join-Path $fixtureRoot '3D FPS Game.sln'
$formatArgs = @($solutionPath, '--no-restore', '--verify-no-changes', '--severity', 'warn', '--include', 'Assets/02.Scripts/')
# dotnet format resolves --include against its current directory. Match the hook's cwd.
Push-Location -LiteralPath $fixtureRoot
try {
    Invoke-Dotnet (@('format', 'style') + $formatArgs)
    Invoke-Dotnet (@('format', 'analyzers') + $formatArgs)
    [IO.File]::WriteAllText($fixtureSource, $desired.Replace('_jumpPower', 'jumpPower'))
    Invoke-Dotnet @($checker, '--root', $fixtureRoot)
    Invoke-Dotnet (@('format', 'style') + $formatArgs) 2 'IDE1006'
} finally { Pop-Location }
$passed++
Write-Output 'PASS: desired layout accepted; IDE1006 still blocked'

[IO.File]::WriteAllText($fixtureSource, $desired.Replace('10f;', ';'))
Invoke-Dotnet @($checker, '--root', $fixtureRoot, '--fix') 2 '구문 오류'
$passed++

# Exercise the real hook on a disposable Git checkout with the desired Header layout.
[IO.File]::WriteAllText($fixtureSource, $desired)
$toolCopy = Join-Path $fixtureRoot 'Tools/CodeStyleCheck'
New-Item -ItemType Directory -Path $toolCopy -Force | Out-Null
Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -in '.cs', '.csproj' } |
    Copy-Item -Destination $toolCopy
$hooksCopy = Join-Path $fixtureRoot '.husky'
New-Item -ItemType Directory -Path (Join-Path $hooksCopy '_') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot '.husky/pre-commit') -Destination $hooksCopy
Copy-Item -LiteralPath (Join-Path $projectRoot '.husky/_/husky.sh') -Destination (Join-Path $hooksCopy '_')
& git -C $fixtureRoot init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Could not initialize test checkout.' }
$sh = if ($IsWindows) {
    Join-Path (Split-Path (Split-Path (Get-Command git).Source)) 'bin/sh.exe'
} else { 'sh' }
Push-Location -LiteralPath $fixtureRoot
try {
    & $sh '.husky/pre-commit'
    if ($LASTEXITCODE -ne 0) { throw 'The hook rejected the desired Header layout.' }
    [IO.File]::WriteAllText($fixtureSource, $desired.Replace(')] [SerializeField', ')]   [SerializeField'))
    & $sh '.husky/pre-commit'
    if ($LASTEXITCODE -ne 1) { throw 'The hook did not block an invalid Header layout.' }
} finally { Pop-Location }
$passed += 2

Write-Output "CodeStyleCheck integration: $passed scenarios passed. Fixtures retained at $fixtureRoot"
