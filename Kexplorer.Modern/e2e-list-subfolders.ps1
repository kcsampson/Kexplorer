param(
    [Parameter(Mandatory = $false)]
    [string]$Path = "C:\Users\kimball.sampson\OneDrive - Precisely Inc\Precisely\Administrative"
)

$ErrorActionPreference = "Stop"

Write-Host "Kexplorer e2e folder listing"
Write-Host "Target: $Path"

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Path does not exist: $Path"
    exit 1
}

try {
    $rootItem = Get-Item -LiteralPath $Path -Force
    Write-Host "Root attributes: $($rootItem.Attributes)"

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $dirs = Get-ChildItem -LiteralPath $Path -Directory -Force -ErrorAction Stop |
        Sort-Object Name
    $sw.Stop()

    Write-Host "Sub-folder count: $($dirs.Count)"
    Write-Host "Enumeration elapsed: $($sw.ElapsedMilliseconds) ms"
    Write-Host ""

    if ($dirs.Count -eq 0) {
        Write-Host "No sub-folders found."
        exit 0
    }

    $dirs |
        Select-Object Name, FullName, Attributes, LastWriteTime |
        Format-Table -AutoSize |
        Out-String -Width 500 |
        Write-Host
}
catch {
    Write-Error "Enumeration failed: $($_.Exception.Message)"
    exit 2
}
