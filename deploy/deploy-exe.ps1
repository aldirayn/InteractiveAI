<#
.SYNOPSIS
    Deploy hasil "dotnet publish" ke server dev yang menjalankan app sebagai .exe (tanpa IIS).

.DESCRIPTION
    1. Stop proses <ExeName> yang berjalan DARI folder target (proses di folder lain tidak disentuh).
    2. robocopy hasil publish ke target (TANPA /MIR, file lama tidak dihapus).
    3. Start lagi <ExeName> dengan working directory = folder target
       (wajib: app membaca appsettings.json & model\ relatif ke working directory).
    4. Pastikan proses masih hidup setelah beberapa detik.

    Tidak pernah ditimpa: appsettings.DevServer.json, *.db, logs\, App_Data\,
    dan model AI (Models\, model\, *.onnx, *.bin, *.tar.gz, weya_nc*.dll) kecuali -IncludeModels.

.EXAMPLE
    .\deploy-exe.ps1 -Source .\out\backend -Target D:\InteractiveAI_Dev\backend -ExeName InteractiveAI.Backend -Name Backend
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Target,
    [Parameter(Mandatory = $true)][string]$ExeName,      # tanpa .exe, mis. InteractiveAI.Backend
    [string]$Name = "App",
    [int]$StartCheckSeconds = 8,
    [switch]$IncludeModels,
    [switch]$NoStart,
    [string[]]$ExtraExcludeFiles = @(),
    [string[]]$ExtraExcludeDirs = @()
)

$ErrorActionPreference = "Stop"

# ---------- validasi ----------
if (-not (Test-Path $Source -PathType Container)) { throw "[$Name] Folder sumber tidak ada: $Source" }
$Source = (Resolve-Path $Source).Path
if (-not (Get-ChildItem $Source -Filter "$ExeName.dll" -File | Select-Object -First 1)) {
    throw "[$Name] $Source tidak berisi $ExeName.dll - bukan hasil publish?"
}
$t = $Target.TrimEnd('\', '/')
if ($t -match '^[A-Za-z]:$' -or ($t -split '[\\/]').Where({ $_ }).Count -lt 3) {
    throw "[$Name] Target terlalu dangkal/berbahaya: '$Target'. Pakai folder khusus app, mis. D:\InteractiveAI_Dev\backend"
}
if (-not (Test-Path $Target)) { New-Item -ItemType Directory -Force -Path $Target | Out-Null }
$Target  = (Resolve-Path $Target).Path
$exePath = Join-Path $Target "$ExeName.exe"

function Get-AppProcess {
    Get-Process -Name $ExeName -ErrorAction SilentlyContinue | Where-Object {
        $p = $null; try { $p = $_.Path } catch { }
        if (-not $p) { throw "[$Name] Ada proses $ExeName yang path-nya tidak bisa dibaca (beda user / kurang hak). Stop manual dulu, atau jalankan agent Jenkins sebagai admin/LocalSystem." }
        $p.StartsWith($Target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
    }
}

$excludeFiles = @("appsettings.DevServer.json", "*.db", "*.db-shm", "*.db-wal") + $ExtraExcludeFiles
$excludeDirs  = @("logs", "App_Data") + $ExtraExcludeDirs
if (-not $IncludeModels) {
    $excludeFiles += @("*.onnx", "*.bin", "*.tar.gz", "weya_nc*.dll")
    $excludeDirs  += @("Models", "model")
}

Write-Host "[$Name] Source : $Source"
Write-Host "[$Name] Target : $Target"
Write-Host "[$Name] Model AI ikut disalin: $([bool]$IncludeModels)"
if (-not $PSCmdlet.ShouldProcess($Target, "Deploy $Name")) { return }

# ---------- 1. stop ----------
$procs = @(Get-AppProcess)
if ($procs.Count -gt 0) {
    Write-Host "[$Name] Stop $($procs.Count) proses (PID $($procs.Id -join ', '))"
    $procs | Stop-Process -Force
    $procs | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    if (@(Get-AppProcess).Count -gt 0) { throw "[$Name] Proses $ExeName belum berhenti." }
} else {
    Write-Host "[$Name] Tidak ada proses $ExeName yang berjalan dari $Target"
}

# ---------- 2. copy ----------
$rcArgs = @($Source, $Target, "/E", "/R:10", "/W:3", "/NP", "/NDL", "/NJH")
$rcArgs += "/XF"; $rcArgs += $excludeFiles
$rcArgs += "/XD"; $rcArgs += $excludeDirs
& robocopy @rcArgs
$rc = $LASTEXITCODE
if ($rc -ge 8) { throw "[$Name] robocopy gagal (exit code $rc)" }
Write-Host "[$Name] Copy selesai (robocopy exit $rc)"

# ---------- 3. start ----------
if ($NoStart) { Write-Host "[$Name] -NoStart: app tidak dijalankan."; $global:LASTEXITCODE = 0; return }
if (-not (Test-Path $exePath)) { throw "[$Name] $exePath tidak ada setelah copy." }
$sp = @{ FilePath = $exePath; WorkingDirectory = $Target; PassThru = $true }
if ($PSVersionTable.PSEdition -eq "Desktop" -or $IsWindows) { $sp.WindowStyle = "Minimized" }
$p = Start-Process @sp
Write-Host "[$Name] Start $ExeName (PID $($p.Id)), cek $StartCheckSeconds detik..."
Start-Sleep -Seconds $StartCheckSeconds
if ($p.HasExited) {
    throw "[$Name] $ExeName langsung berhenti (exit code $($p.ExitCode)). Cek log di $Target\logs atau jalankan manual di folder itu."
}
Write-Host "[$Name] Berjalan (PID $($p.Id))."
$global:LASTEXITCODE = 0
