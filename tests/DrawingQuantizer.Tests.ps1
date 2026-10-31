Describe 'Drawing palette quantization' -Tag Integration, Windows, Net472 {
    BeforeAll {
        if ($IsWindows) {
            $modulePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../output/Sixel.psd1'))
            $resultPath = Join-Path $TestDrive 'quantizer.json'
            $windowsPowerShell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
            $childOutput = & $windowsPowerShell -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot 'fixtures/DrawingQuantizer.ps1') -ModulePath $modulePath -ResultPath $resultPath 2>&1
            if ($LASTEXITCODE -ne 0) { throw ($childOutput -join [Environment]::NewLine) }
            $quantizer = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        }
    }
    BeforeEach {
        if (-not $IsWindows) { Set-ItResult -Skipped -Because 'System.Drawing targets Windows PowerShell 5.1.' }
    }
    It 'Preserves exact low-color art without modifying the source bitmap' {
        $quantizer.ExactColors | Should -BeTrue
        $quantizer.SourceUnchanged | Should -BeTrue
        $quantizer.WeightedMean | Should -BeTrue
    }
    It 'Bounds the palette and preserves transparent holes on narrow rasters' {
        $quantizer.PaletteBounded | Should -BeTrue
        $quantizer.AlphaPreserved | Should -BeTrue
        $quantizer.Transparent | Should -BeTrue
    }
    It 'Diffuses gradient error while retaining average tone and frame determinism' {
        $quantizer.DitherColumnColors | Should -Be 2
        $quantizer.DitherCenterMean | Should -BeGreaterThan 123
        $quantizer.DitherCenterMean | Should -BeLessThan 129
        $quantizer.Deterministic | Should -BeTrue
    }
    It 'Reads negative-stride pixels in display order' {
        $quantizer.NegativeStride | Should -BeTrue
    }
    It 'Matches exhaustive nearest-color search including cache hits and ties' {
        $quantizer.NearestMatchesBruteForce | Should -BeTrue
        $quantizer.StableTie | Should -BeTrue
    }
    It 'Reuses GIF workspace without leaking palette or diffusion state between frames' {
        $quantizer.GifFramesIndependent | Should -BeTrue
    }
}
