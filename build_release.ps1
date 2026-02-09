$ErrorActionPreference = "Stop"

Write-Host "--- Paws Pre-Alpha Release Build ---" -ForegroundColor Cyan

# Define paths
$root = Get-Location
$backendProject = Join-Path $root "Paws.DotNet/Paws.Host/Paws.Host.csproj"
$publishDir = Join-Path $root "published_backend"
$frontendDir = Join-Path $root "frontend"

# 1. Clean and Publish Backend
Write-Host "Step 1: Publishing Backend (Release, SingleFile)..."
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

# We use self-contained + single file to minimize files and ensure runtime availability
dotnet publish $backendProject -c Release -r win-x64 --self-contained true -o $publishDir /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None

if (-not (Test-Path $publishDir)) {
    Write-Error "Backend publish failed."
}

# 2. Build Frontend
Write-Host "Step 2: Building Frontend (Electron)..."
Set-Location $frontendDir

# Clean previous frontend build artifacts to prevent "Can't open output file" errors
$distDir = Join-Path $frontendDir "dist"
if (Test-Path $distDir) {
    Write-Host "Cleaning frontend dist directory..."
    Remove-Item -Recurse -Force $distDir
}

# Set environment variable so copy-backend.js knows where to copy from
$env:PAWS_BACKEND_PATH = "../published_backend"

# Install dependencies just in case
pnpm install

# Run the build command (which triggers copy-backend.js internally via build:win -> build:backend)
pnpm run build:win

Write-Host "---------------------------------------------------" -ForegroundColor Green
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "Artifacts should be in: $frontendDir\dist" -ForegroundColor Green
Write-Host "---------------------------------------------------"

Set-Location $root
