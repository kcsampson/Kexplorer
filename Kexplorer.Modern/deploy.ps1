# deploy.ps1 — Kill running Kimbonics, then copy published build to c:\kimbonics-exp

$ErrorActionPreference = 'Stop'

$source = "C:\kexplorer\github-dev\Kexplorer\Kexplorer.Modern\Kexplorer.UI\bin\Release\net8.0-windows\publish"
$dest = "C:\kimbonics-exp"

# Kill any running Kimbonics processes
$procs = Get-Process -Name Kimbonics -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Stopping Kimbonics process(es)..."
    $procs | Stop-Process -Force
    Start-Sleep -Milliseconds 500
} else {
    Write-Host "No running Kimbonics process found."
}

# Create destination if it doesn't exist
if (-not (Test-Path $dest)) {
    New-Item -ItemType Directory -Path $dest | Out-Null
    Write-Host "Created $dest"
}

# Copy published build
Write-Host "Copying from $source to $dest ..."
Copy-Item -Path "$source\*" -Destination $dest -Recurse -Force

Write-Host "Deploy complete. Files copied to $dest"
