<#
.SYNOPSIS
    TypeU 跨平台 AOT 发布脚本（Windows PowerShell 版）。
.DESCRIPTION
    发布教师端与学生端为 Native AOT 单文件可执行程序。
    产物：publish/<rid>/Teacher/TypeU.Teacher.GUI.exe 与 publish/<rid>/Student/TypeU.Student.GUI.exe
    特性：无 PDB、符号剥离、单文件、AOT 原生编译。
.PARAMETER RuntimeIdentifier
    目标运行时标识符。默认 win-x64；可选 win-arm64、linux-x64、linux-arm64。
.PARAMETER Configuration
    构建配置。默认 Release。
.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -RuntimeIdentifier linux-x64
    .\publish.ps1 -RuntimeIdentifier win-arm64 -Configuration Release
#>
param(
    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64')]
    [string]$RuntimeIdentifier = 'win-x64',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoAot
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishRoot = Join-Path $root 'publish' $RuntimeIdentifier

Write-Host "=== TypeU AOT 发布 ===" -ForegroundColor Cyan
Write-Host "RuntimeIdentifier : $RuntimeIdentifier"
Write-Host "Configuration     : $Configuration"
Write-Host "AOT               : $(-not $NoAot)"
Write-Host "Output            : $publishRoot"
Write-Host ""

$dotnetPublishArgs = @(
    'publish',
    '-c', $Configuration,
    '-r', $RuntimeIdentifier,
    '-o', ''
    '/p:PublishSingleFile=true'
)

if (-not $NoAot) {
    $dotnetPublishArgs += '/p:PublishAot=true'
    $dotnetPublishArgs += '/p:StripSymbols=true'
    $dotnetPublishArgs += '/p:DebugType=none'
} else {
    $dotnetPublishArgs += '/p:PublishAot=false'
    $dotnetPublishArgs += '/p:IncludeNativeLibrariesForSelfExtract=true'
}

$projects = @(
    @{ Name = 'Teacher'; Path = 'src/TypeU.Teacher.GUI/TypeU.Teacher.GUI.csproj' },
    @{ Name = 'Student'; Path = 'src/TypeU.Student.GUI/TypeU.Student.GUI.csproj' }
)

foreach ($proj in $projects) {
    $outDir = Join-Path $publishRoot $proj.Name
    $dotnetPublishArgs[4] = $outDir
    Write-Host "[$($proj.Name)] 发布中..." -ForegroundColor Yellow
    dotnet @dotnetPublishArgs (Join-Path $root $proj.Path)

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[$($proj.Name)] 发布失败" -ForegroundColor Red
        exit 1
    }

    # 产物校验：确认无项目自身的 PDB 文件（第三方原生库如 SkiaSharp/HarfBuzzSharp 的 pdb 可接受）。
    $pdbs = Get-ChildItem -Path $outDir -Filter 'TypeU.*.pdb' -Recurse -ErrorAction SilentlyContinue
    if ($pdbs) {
        Write-Host "[$($proj.Name)] 警告：发现项目 PDB 文件" -ForegroundColor Red
        $pdbs | ForEach-Object { Write-Host "  $($_.FullName)" }
        exit 1
    }

    # 列出主可执行文件。
    $exeExt = if ($RuntimeIdentifier.StartsWith('win')) { '.exe' } else { '' }
    $mainExe = Get-ChildItem -Path $outDir -Filter "TypeU.*$exeExt" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($mainExe) {
        $sizeMb = [math]::Round($mainExe.Length / 1MB, 2)
        Write-Host "[$($proj.Name)] 产物：$($mainExe.Name) ($sizeMb MB)" -ForegroundColor Green
    }
    Write-Host ""
}

Write-Host "=== 发布完成 ===" -ForegroundColor Cyan
Write-Host "产物目录：$publishRoot"
