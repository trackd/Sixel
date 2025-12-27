#!/usr/bin/env pwsh
# Test script to diagnose cell size detection issues

Write-Host '=== Sixel Module Cell Size Diagnostics ===' -ForegroundColor Cyan
Write-Host ''

# Import the module
Import-Module .\output\Sixel.psd1 -Force

Write-Host "Platform: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)" -ForegroundColor Yellow
Write-Host "Terminal: $env:TERM" -ForegroundColor Yellow
Write-Host "Terminal Program: $env:TERM_PROGRAM" -ForegroundColor Yellow
Write-Host ''

# Test basic cell size detection
Write-Host 'Testing GetCellSize()...' -ForegroundColor Green
$cellSize = [Sixel.Terminal.Compatibility]::GetCellSize()
Write-Host "  PixelWidth  : $($cellSize.PixelWidth)" -ForegroundColor White
Write-Host "  PixelHeight : $($cellSize.PixelHeight)" -ForegroundColor White
Write-Host ''

# Get diagnostics
Write-Host 'Detailed Diagnostics:' -ForegroundColor Green
$diagnostics = [Sixel.Terminal.CellSizeDiagnostics]::GetDiagnostics()
Write-Host $diagnostics

# Test with environment variable override
Write-Host ''
Write-Host 'Testing environment variable override...' -ForegroundColor Green
$env:SIXEL_CELL_WIDTH = '12'
$env:SIXEL_CELL_HEIGHT = '24'

[Sixel.Terminal.Compatibility]::ResetCache()
$cellSize2 = [Sixel.Terminal.Compatibility]::GetCellSize()
Write-Host '  With SIXEL_CELL_WIDTH=12, SIXEL_CELL_HEIGHT=24:' -ForegroundColor White
Write-Host "  PixelWidth  : $($cellSize2.PixelWidth)" -ForegroundColor White
Write-Host "  PixelHeight : $($cellSize2.PixelHeight)" -ForegroundColor White

# Clean up
Remove-Item Env:\SIXEL_CELL_WIDTH
Remove-Item Env:\SIXEL_CELL_HEIGHT
[Sixel.Terminal.Compatibility]::ResetCache()

Write-Host ''
Write-Host '=== Test Complete ===' -ForegroundColor Cyan
