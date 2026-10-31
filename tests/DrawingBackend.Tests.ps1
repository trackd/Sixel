Describe 'Windows PowerShell System.Drawing backend' -Tag 'Integration', 'Windows', 'Net472' {
    BeforeAll {
        if ($IsWindows) {
            $modulePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../output/Sixel.psd1'))
            $resultPath = Join-Path $TestDrive 'drawing.json'
            $windowsPowerShell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
            $childOutput = & $windowsPowerShell -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot 'fixtures/DrawingBackend.ps1') -ModulePath $modulePath -ResultPath $resultPath 2>&1
            if ($LASTEXITCODE -ne 0) { throw ($childOutput -join [Environment]::NewLine) }
            $drawing = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        }
    }
    BeforeEach {
        if (-not $IsWindows) { Set-ItResult -Skipped -Because 'System.Drawing backend targets Windows PowerShell 5.1.' }
    }
    It 'Loads the framework backend without bundled runtime dependencies' {
        $drawing.Engine | Should -Be 5
        $drawing.References | Should -Contain 'System.Drawing'
        @($drawing.References | Where-Object { $_ -like 'SixLabors*' }).Count | Should -Be 0
        @($drawing.Loaded | Where-Object { $_ -like 'SixLabors*' }).Count | Should -Be 0
        @(Get-ChildItem (Join-Path $PSScriptRoot '../output/lib/net472') -Filter 'SixLabors*').Count | Should -Be 0
        $frameworkReferences = @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Net.Http', 'System.Management.Automation')
        @($drawing.References | Where-Object { $_ -notin $frameworkReferences }).Count | Should -Be 0
        $bundledDlls = @(Get-ChildItem (Join-Path $PSScriptRoot '../output/lib/net472') -Filter '*.dll')
        $bundledDlls.Count | Should -Be 1
        $bundledDlls[0].Name | Should -Be 'Sixel.dll'
    }
    It 'Encodes Sixel with bounded palette and six-pixel occupancy' {
        $drawing.Sixel | Should -Match '"1;1;3;1'
        # The encoder initially defines register 0, then redefines it for the first color.
        @([regex]::Matches($drawing.Sixel, '#(?<id>\d+);2;') | ForEach-Object { $_.Groups['id'].Value } | Select-Object -Unique).Count | Should -Be 2
        $drawing.Sixel | Should -Match ';2;100;0;0'
        $drawing.Sixel | Should -Match ';2;0;100;0'
        $drawing.SixelSize -join ',' | Should -Be '3,6'
        @([regex]::Matches($drawing.Quantized, '#(?<id>\d+);2;') | ForEach-Object { $_.Groups['id'].Value } | Select-Object -Unique).Count | Should -Be 2
    }
    It 'Encodes Kitty RGB and RGBA with correct channel order and transparency' {
        $rgba = [Convert]::FromBase64String($drawing.Rgba)
        $rgba.Length | Should -Be 12
        ($rgba[0..7] -join ',') | Should -Be '255,0,0,255,0,255,0,255'
        $rgba[11] | Should -Be 0
        $rgb = [Convert]::FromBase64String($drawing.Rgb)
        $rgb.Length | Should -Be 9
        ($rgb[0..5] -join ',') | Should -Be '255,0,0,0,255,0'
    }
    It 'Produces PNG inline and legacy Kitty payloads while preserving caller streams' {
        $drawing.InlineSize -join ',' | Should -Be '3,1'
        $drawing.InlineAlpha | Should -Be 0
        $png = [Convert]::FromBase64String($drawing.LegacyKitty)
        ($png[0..7] -join ',') | Should -Be '137,80,78,71,13,10,26,10'
        $drawing.StreamOpen | Should -BeTrue
    }
    It 'Resizes PNG output while preserving aspect ratio' {
        $drawing.ResizedSize -join ',' | Should -Be '6,2'
    }
    It 'Retains colored text fallbacks for terminals without image protocols' {
        $drawing.Blocks | Should -Match '\x1b\[38;2;255;0;0m'
        $drawing.Blocks | Should -Match '[\u2580\u2584]'
        $drawing.Braille | Should -Match '\x1b\[38;2;'
        $drawing.Braille | Should -Match '[\u2801-\u28ff]'
    }
    It 'Retains GIF frames and Kitty animation timing' {
        $drawing.GifFrames | Should -Be 2
        $drawing.GifDelay | Should -Be 70
        $drawing.Animated | Should -Match 'r=1,z=70'
        $drawing.Animated | Should -Match 'a=f,i=9,q=2,t=d,f=32,X=1,z=-1'
        $drawing.Animated | Should -Match ';AP8A/w=='
        $drawing.Animated | Should -Match 's=3,v=3'
    }
    It 'Reports invalid image input through the cmdlet error stream' {
        $drawing.InvalidFailed | Should -BeTrue
        $drawing.InlineAnimationRejected | Should -BeTrue
    }
}
