<#
.SYNOPSIS
    Publish Backend (API) dan Frontend (Web) InteractiveAI, keduanya project .NET.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -SkipFrontend
    .\publish.ps1 -Runtime win-x64 -SelfContained -Zip
    .\publish.ps1 -NoModels          # tanpa folder model AI (lebih kecil, mis. hanya update kode)

    Model AI (tidak ada di git) ikut disalin ke hasil publish backend:
      <repo>\model\*                   -> backend\model\
      src\...Backend\Models\ (non .cs)  -> backend\Models\ (+ yolo11n-coco.onnx dari model\ bila belum ada)
#>
[CmdletBinding()]
param(
    [string]$Root          = $PSScriptRoot,
    [string]$BackendProject  = "",   # kosong = auto-detect (nama mengandung Backend/Api)
    [string]$FrontendProject = "",   # kosong = auto-detect (nama mengandung Frontend/Web)
    [string]$OutputDir     = "",     # default: <Root>\publish\<timestamp>
    [string]$Configuration = "Release",
    [string]$Runtime       = "",     # contoh: win-x64, linux-x64
    [switch]$SelfContained,
    [switch]$SkipBackend,
    [switch]$SkipFrontend,
    [switch]$Zip,
    [switch]$Clean,
    [switch]$NoModels     # jangan salin folder model AI
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = (Get-Location).Path }
$Root = (Resolve-Path $Root).Path

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "'dotnet' tidak ditemukan di PATH. Install .NET SDK dulu."
}

# ---------- Auto-detect ----------
$excluded = '\\(node_modules|bin|obj|\.git|\.vs)\\'
$all = Get-ChildItem $Root -Recurse -Filter *.csproj -File |
    Where-Object { $_.FullName -notmatch $excluded -and $_.Name -notmatch 'Test' }

function Find-Project($pattern) {
    $all | Where-Object { $_.BaseName -match $pattern } | Select-Object -First 1 -ExpandProperty FullName
}

if (-not $SkipBackend -and -not $BackendProject) {
    $BackendProject = Find-Project 'Backend|Api'
    if (-not $BackendProject) { throw "Project Backend tidak ditemukan. Pakai -BackendProject <path.csproj>." }
}
if (-not $SkipFrontend -and -not $FrontendProject) {
    $FrontendProject = Find-Project 'Frontend|Web|Client|UI'
    if (-not $FrontendProject) { throw "Project Frontend tidak ditemukan. Pakai -FrontendProject <path.csproj>." }
}

if (-not $OutputDir) { $OutputDir = Join-Path $Root ("publish\" + (Get-Date -Format "yyyyMMdd-HHmmss")) }
if ($Clean -and (Test-Path $OutputDir)) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Write-Host "Root      : $Root"
Write-Host "Backend   : $(if ($SkipBackend) { '(skip)' } else { $BackendProject })"
Write-Host "Frontend  : $(if ($SkipFrontend) { '(skip)' } else { $FrontendProject })"
Write-Host "Output    : $OutputDir"

function Publish-Project($project, $outDir) {
    $dotnetArgs = @("publish", $project, "-c", $Configuration, "-o", $outDir, "--nologo")
    if ($Runtime) {
        $dotnetArgs += @("-r", $Runtime)
        $dotnetArgs += $(if ($SelfContained) { "--self-contained" } else { "--no-self-contained" })
    }
    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal untuk $project (exit code $LASTEXITCODE)." }
}

# ---------- Publish ----------
if (-not $SkipBackend) {
    Write-Step "Publish Backend"
    $out = Join-Path $OutputDir "backend"
    Publish-Project $BackendProject $out
    Write-Ok "Backend -> $out"

    if (-not $NoModels) {
        Write-Step "Salin model AI"
        # 1) <repo>\model -> backend\model  (Voice/STT: appsettings.json "model/...")
        $modelSrc = Join-Path $Root "model"
        if (Test-Path $modelSrc) {
            $modelOut = Join-Path $out "model"
            New-Item -ItemType Directory -Force -Path $modelOut | Out-Null
            Copy-Item -Path (Join-Path $modelSrc "*") -Destination $modelOut -Recurse -Force
            $mb = [math]::Round(((Get-ChildItem $modelOut -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
            Write-Ok "model\  -> $modelOut ($mb MB)"
        } else {
            Write-Warning "Folder $modelSrc tidak ada - model AI tidak ikut (taruh manual di server)."
        }

        # 2) src\...Backend\Models (file non-kode) -> backend\Models  (Vision: "Models/yolo11n-coco.onnx")
        $modelsOut = Join-Path $out "Models"
        New-Item -ItemType Directory -Force -Path $modelsOut | Out-Null
        $modelsSrc = Join-Path (Split-Path $BackendProject -Parent) "Models"
        if (Test-Path $modelsSrc) {
            Get-ChildItem $modelsSrc -File | Where-Object { $_.Extension -ne ".cs" } |
                Copy-Item -Destination $modelsOut -Force
        }
        foreach ($f in @("yolo11n-coco.onnx")) {
            $dst = Join-Path $modelsOut $f
            $alt = Join-Path $modelSrc $f
            if (-not (Test-Path $dst) -and (Test-Path $alt)) { Copy-Item $alt $dst -Force }
            if (-not (Test-Path $dst)) { Write-Warning "Models\$f tidak ditemukan - kamera/deteksi orang tidak aktif." }
        }
        Write-Ok "Models\ -> $modelsOut ($((Get-ChildItem $modelsOut -File).Name -join ', '))"
    }
}

if (-not $SkipFrontend) {
    Write-Step "Publish Frontend"
    $out = Join-Path $OutputDir "frontend"
    Publish-Project $FrontendProject $out
    Write-Ok "Frontend -> $out"
}

# ---------- Zip ----------
if ($Zip) {
    Write-Step "Membuat ZIP"
    $zipPath = "$OutputDir.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath
    Write-Ok "ZIP -> $zipPath"
}

Write-Host "`nSelesai. Hasil publish: $OutputDir" -ForegroundColor Green
