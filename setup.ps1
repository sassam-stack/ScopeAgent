# Scope Agent Setup Script for Windows PowerShell

Write-Host "Scope Agent - Setup Script" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

# Check .NET SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "✗ .NET SDK not found. Please install .NET 8.0 SDK" -ForegroundColor Red
    exit 1
}

# Check Node.js
Write-Host "Checking Node.js..." -ForegroundColor Yellow
$nodeVersion = node --version 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Node.js found: $nodeVersion" -ForegroundColor Green
} else {
    Write-Host "✗ Node.js not found. Please install Node.js 18+" -ForegroundColor Red
    exit 1
}

# Restore backend packages
Write-Host ""
Write-Host "Restoring backend packages..." -ForegroundColor Yellow
Set-Location "src\ScopeAgent.Api"
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to restore backend packages" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Backend packages restored" -ForegroundColor Green
Set-Location "..\..\"

# Install frontend packages
Write-Host ""
Write-Host "Installing frontend packages..." -ForegroundColor Yellow
Set-Location "frontend"
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to install frontend packages" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Frontend packages installed" -ForegroundColor Green
Set-Location ".."

Write-Host ""
Write-Host "Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Configure Azure OpenAI in src\ScopeAgent.Api\appsettings.json" -ForegroundColor White
Write-Host "2. Start the backend: cd src\ScopeAgent.Api && dotnet run" -ForegroundColor White
Write-Host "3. Start the frontend: cd frontend && npm run dev" -ForegroundColor White
Write-Host ""

