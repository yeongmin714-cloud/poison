$ErrorActionPreference = 'Stop'
$src = 'C:\Unity\code\Training\TrainingInfra\venv'
$dst = 'C:\UnityArchive\training_venv_20260910.zip'
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (-not (Test-Path $src)) { Write-Output 'SRC_MISSING'; exit 1 }
    if (Test-Path $dst) { Remove-Item $dst -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($src, $dst, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    $z = Get-Item $dst
    Write-Output ('ZIP_OK ' + [math]::Round($z.Length / 1MB, 1) + 'MB')
} catch {
    Write-Output ('ZIP_FAIL ' + $_.Exception.Message)
    exit 1
}
