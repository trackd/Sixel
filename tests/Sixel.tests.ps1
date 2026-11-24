BeforeAll {
    # Load the module to be tested
    $modulePath = if ($env:SIXEL_TEST_MODULE_PATH) {
        $env:SIXEL_TEST_MODULE_PATH
    }
    else {
        [System.IO.Path]::Combine($PSScriptRoot, '..', 'output', 'Sixel.psd1')
    }
    Import-Module $modulePath
    $imagePath = [System.IO.Path]::Combine($PSScriptRoot, '..', 'assets')
    $samplePath = [System.IO.Path]::Combine($PSScriptRoot, 'data')
    $cog = [System.IO.Path]::Combine($imagePath, 'cog.png')

}

<#

TODO: update the test files after changing the output format to include the height value in the string.


sixlabor versions can affect output, might need to visually compare and update the test files.

the results in 5.1 and 7+ are different.

$kitty = ConvertTo-Sixel -Path .\assets\cog.png -Width 2 -Protocol KittyGraphicsProtocol -Force
$kitty | Set-Content .\tools\tests\cog_kitty_w2.kitty -NoNewline

$inline = ConvertTo-Sixel -Path .\assets\cog.png -Width 2 -Protocol InlineImageProtocol -Force
$inline | Set-Content .\tools\tests\cog_inline_w2.iip -NoNewline

$six = ConvertTo-Sixel -Path .\assets\cog.png -Width 2 -Protocol Sixel
$six | set-content .\tools\tests\\cog_sixel_w2.sixel -NoNewline

#>

Describe 'Sixel Module Tests' {
    It 'Should discover and complete commands without active terminal queries' {
        $compatibilityType = [Sixel.Terminal.Compatibility]
        $queryFactory = $compatibilityType.GetProperty('CreateQueryTransport', [System.Reflection.BindingFlags]'Static, NonPublic')
        $originalFactory = $queryFactory.GetValue($null)
        $transportType = $queryFactory.PropertyType.GenericTypeArguments[0]
        $queryAttempts = [System.Collections.Generic.List[int]]::new()
        $recordAttempt = [System.Linq.Expressions.Expression]::Call(
            [System.Linq.Expressions.Expression]::Constant($queryAttempts),
            $queryAttempts.GetType().GetMethod('Add'),
            [System.Linq.Expressions.Expression]::Constant(1)
        )
        $failure = [System.Linq.Expressions.Expression]::Throw(
            [System.Linq.Expressions.Expression]::Constant([System.InvalidOperationException]::new('Unexpected terminal query during discovery')),
            $transportType
        )
        $blockedFactory = [System.Linq.Expressions.Expression]::Lambda(
            $queryFactory.PropertyType,
            [System.Linq.Expressions.Expression]::Block([System.Linq.Expressions.Expression[]]@($recordAttempt, $failure)),
            [System.Linq.Expressions.ParameterExpression[]]@()
        ).Compile()
        try {
            $queryFactory.SetValue($null, $blockedFactory)
            Get-Command -Name ConvertTo-Sixel | Should -Not -BeNullOrEmpty
            Get-Command -Name ConvertTo-Kitty | Should -Not -BeNullOrEmpty
            foreach ($commandName in 'ConvertTo-Sixel', 'ConvertTo-SixelGif', 'ConvertTo-Kitty', 'ConvertTo-GoodImage') {
                $command = Get-Command -Name $commandName
                $command.Parameters.ContainsKey('NoProbe') | Should -BeFalse
                $command.Parameters.ContainsKey('Force') | Should -BeTrue
            }
            $kittyCompletion = 'ConvertTo-Kitty -For'
            (TabExpansion2 -InputScript $kittyCompletion -CursorColumn $kittyCompletion.Length).CompletionMatches.CompletionText | Should -Contain '-Format'
            $preview = ConvertTo-Kitty -Path $cog -Width 2 -Force -ErrorAction Stop
            $preview.PixelWidth | Should -BeGreaterThan 0
            $completionText = 'ConvertTo-Sixel -Pro'
            $completion = TabExpansion2 -InputScript $completionText -CursorColumn $completionText.Length
            $completion.CompletionMatches.CompletionText | Should -Contain '-Protocol'
            [Sixel.Terminal.Compatibility]::GetTerminalInfo() | Should -Not -BeNullOrEmpty
            [Sixel.Terminal.Compatibility]::GetCellSize().PixelHeight | Should -BeGreaterThan 0
            [Sixel.Terminal.Compatibility]::GetKittyCapabilities() | Should -Not -BeNullOrEmpty
            $queryAttempts.Count | Should -Be 0
        }
        finally {
            $queryFactory.SetValue($null, $originalFactory)
        }
    }

    It 'Should have the Sixel module loaded' {
        $module = Get-Module -Name Sixel
        $module | Should -Not -BeNullOrEmpty
    }

    It 'Should have the Sixel module exported functions' {
        $functions = Get-Command -Module Sixel
        $functions.count | Should -Be 4
    }

    It 'Should be able to detect terminal support' {
        [Sixel.Terminal.Compatibility]::TerminalSupportsSixel() | Should -Not -Be $null
    }

    It 'Should be able to detect kitty graphics support' {
        [Sixel.Terminal.Compatibility]::TerminalSupportsKitty() | Should -Not -Be $null
    }

    It 'Dummy' {
        [PSCustomObject]@{
            SixelSupport = [Sixel.Terminal.Compatibility]::TerminalSupportsSixel()
            KittySupport = [Sixel.Terminal.Compatibility]::TerminalSupportsKitty()
            TerminalType = [Sixel.Terminal.Compatibility]::GetTerminalInfo()
        } | Out-Host
        $true
    }
}
Describe 'Kitty preview' {
    It 'Converts an image without disposing the caller stream' {
        $imageStream = [System.IO.File]::OpenRead($cog)
        try {
            $preview = ConvertTo-Kitty -Stream $imageStream -Width 2 -CellWidth 10 -CellHeight 20 -Format Rgba -Compress -ImageId 42 -ErrorAction Stop
            $preview.PixelWidth | Should -Be 20
            $preview.Width | Should -Be 2
            $preview.ImageId | Should -Be 42
            $preview | Should -Match 'q=2'
            $preview | Should -Match 'o=z'
            $imageStream.CanRead | Should -BeTrue
        }
        finally {
            $imageStream.Dispose()
        }
    }

    It 'Preserves case-sensitive wire controls and supplies silent replies' {
        $sequence = ConvertTo-Kitty -Command 'a=p','i=42','x=2','X=1','C=1' -ErrorAction Stop
        $sequence | Should -Be "$([char]27)_Ga=p,i=42,x=2,X=1,C=1,q=2$([char]27)\"
    }

    It 'Rejects ambiguous command parameters and conflicting IDs' {
        { ConvertTo-Kitty -Command 'a=p','i=42' -ImageId 42 -ErrorAction Stop } | Should -Throw
        { ConvertTo-Kitty -Path $cog -ImageId 1 -ImageNumber 2 -ErrorAction Stop } | Should -Throw
    }

    It 'Encodes a reference without accessing the local filesystem' {
        $sequence = ConvertTo-Kitty -Reference '/terminal-only/image.png' -ImageId 42 -ErrorAction Stop
        $sequence | Should -Match 't=f'
        $sequence | Should -Match ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('/terminal-only/image.png')))
    }

    It 'Creates a virtual placement and Unicode grid' {
        $sequence = ConvertTo-Kitty -Path $cog -ImageId 42 -Width 2 -UnicodePlaceholder -Force -ErrorAction Stop
        $sequence | Should -Match 'U=1'
        $sequence.Contains([char]::ConvertFromUtf32(0x10eeee)) | Should -BeTrue
    }

    It 'Produces a valid zlib envelope on both runtime targets' {
        $sequence = ConvertTo-Kitty -Command 'a=t','f=24','s=1','v=1','o=z' -Payload ([byte[]](255, 0, 0)) -ErrorAction Stop
        $encoded = $sequence.Substring($sequence.IndexOf(';') + 1)
        $compressed = [Convert]::FromBase64String($encoded.Substring(0, $encoded.Length - 2))
        (($compressed[0] * 256 + $compressed[1]) % 31) | Should -Be 0
        [BitConverter]::ToString($compressed[($compressed.Length - 4)..($compressed.Length - 1)]) | Should -Be '03-00-01-00'
        $compressedStream = [System.IO.MemoryStream]::new($compressed, 2, $compressed.Length - 6)
        $inflater = [System.IO.Compression.DeflateStream]::new($compressedStream, [System.IO.Compression.CompressionMode]::Decompress)
        $expanded = [System.IO.MemoryStream]::new()
        try {
            $inflater.CopyTo($expanded)
            [BitConverter]::ToString($expanded.ToArray()) | Should -Be 'FF-00-00'
        }
        finally {
            $inflater.Dispose()
            $compressedStream.Dispose()
            $expanded.Dispose()
        }
    }

    It 'Rejects WezTerm placeholders through named and raw controls unless forced' {
        $compatibilityType = [Sixel.Terminal.Compatibility]
        $terminalField = $compatibilityType.GetField('_terminalInfo', [System.Reflection.BindingFlags]'Static, NonPublic')
        $identityField = $compatibilityType.GetField('_terminalIdentity', [System.Reflection.BindingFlags]'Static, NonPublic')
        $originalTerminal = $terminalField.GetValue($null)
        $originalIdentity = $identityField.GetValue($null)
        $attemptField = $compatibilityType.GetField('_runtimeProbeAttempted', [System.Reflection.BindingFlags]'Static, NonPublic')
        $originalAttempt = $attemptField.GetValue($null)
        try {
            $attemptField.SetValue($null, $true)
            $terminalField.SetValue($null, [Sixel.Terminal.Models.TerminalInfo]@{ Terminal = 'WezTerm' })
            $identityField.SetValue($null, $null)
            [Sixel.Terminal.Compatibility]::GetKittyCapabilities().UnicodePlaceholders.ToString() | Should -Be 'Unsupported'
            { ConvertTo-Kitty -Path $cog -ImageId 42 -UnicodePlaceholder -ErrorAction Stop } | Should -Throw '*Unicode placeholders*'
            { ConvertTo-Kitty -Path $cog -ImageId 42 -Control 'U=1' -ErrorAction Stop } | Should -Throw '*Unicode placeholders*'
            { ConvertTo-Kitty -Command 'a=p','i=42','U=1','c=2','r=2' -ErrorAction Stop } | Should -Throw '*Unicode placeholders*'
            $forced = ConvertTo-Kitty -Path $cog -ImageId 42 -Width 2 -UnicodePlaceholder -Force -ErrorAction Stop
            $forced.Contains([char]::ConvertFromUtf32(0x10eeee)) | Should -BeTrue
        }
        finally {
            $terminalField.SetValue($null, $originalTerminal)
            $identityField.SetValue($null, $originalIdentity)
            $attemptField.SetValue($null, $originalAttempt)
        }
    }
}
Describe 'Kitty client integration' {
    It 'Uses PNG for Contour and keeps an explicit format' {
        $terminalField = [Sixel.Terminal.Compatibility].GetField('_terminalInfo', [System.Reflection.BindingFlags]'Static,NonPublic')
        $identityField = [Sixel.Terminal.Compatibility].GetField('_terminalIdentity', [System.Reflection.BindingFlags]'Static,NonPublic')
        $savedTerminal = $terminalField.GetValue($null)
        $savedIdentity = $identityField.GetValue($null)
        try {
            $terminalField.SetValue($null, [Sixel.Terminal.Models.TerminalInfo]@{ Terminal = 'Contour' })
            $identityField.SetValue($null, $null)
            $sequence = ConvertTo-Kitty -Path $cog -Width 2 -Force -ErrorAction Stop
            $sequence | Should -Match 'f=100'
            $sequence | Should -Match 'C=1'
            $sequence.EndsWith("`r") | Should -BeTrue
            ConvertTo-Kitty -Path $cog -Width 2 -Format Rgba -Force -ErrorAction Stop | Should -Match "f=32,.*s=$($sequence.PixelWidth),v=$($sequence.PixelHeight)"
            ConvertTo-Kitty -Reference '/tmp/example.png' -Force -ErrorAction Stop | Should -Match 'f=100'
            ConvertTo-Kitty -Path $cog -Width 2 -Action Query -ImageId 31 -Force -ErrorAction Stop | Should -Match 'a=q,q=0'
            ConvertTo-Kitty -Path $cog -Width 2 -Action Query -ImageId 31 -Quiet 1 -Force -ErrorAction Stop | Should -Match 'a=q,q=1'
            $legacySequence = ConvertTo-Sixel -Path $cog -Width 2 -Protocol KittyGraphicsProtocol -Force -ErrorAction Stop
            $legacySequence | Should -Match 'f=100'
            $legacySequence | Should -Match 'C=1'
            ([string]$legacySequence).EndsWith("`r") | Should -BeTrue
        }
        finally {
            $terminalField.SetValue($null, $savedTerminal)
            $identityField.SetValue($null, $savedIdentity)
        }
    }

    It 'Honors packet limits and rejects misaligned limits' {
        $sequence = ConvertTo-Kitty -Path $cog -ChunkSize 84 -Force -ErrorAction Stop
        foreach ($packet in [regex]::Matches($sequence, ';([^\x1b]*)\x1b\\')) {
            $packet.Groups[1].Length | Should -BeLessOrEqual 84
        }
        { ConvertTo-Kitty -Path $cog -ChunkSize 7 -Force -ErrorAction Stop } | Should -Throw '*multiple of four*'
    }

    It 'Exposes disposable transfers and reply parsing on both runtimes' {
        $transfer = [Sixel.Protocols.KittyTransfer]::CreateTemporaryFile([byte[]](1, 2, 3), $true)
        try {
            $transfer.Encode([string[]]@('f=100'), $false) | Should -Match 'S=3,o=z'
            Test-Path -LiteralPath $transfer.Reference | Should -BeTrue
            $reply = $null
            [Sixel.Protocols.KittyResponse]::TryParse("$([char]27)_Gi=42;OK$([char]27)\", [ref]$reply) | Should -BeTrue
            $reply.ImageId | Should -Be 42
            $reply.Success | Should -BeTrue
        }
        finally { $transfer.Dispose() }
        Test-Path -LiteralPath $transfer.Reference | Should -BeFalse
    }
}

Describe 'Sixel Module ConvertTo-Sixel Tests' {
    It 'Should convert an image to Sixel format' {
        $test = ConvertTo-Sixel -Path $cog -Width 2 -Protocol Sixel -Force
        $test | Should -Not -BeNullOrEmpty
        # $rec = Get-Content -Path ([System.IO.Path]::Combine($samplePath, 'cog_sixel_w2.sixel')) -Raw
        $test | Should -Match ([regex]::Escape("$([char]27)P0;1q"))
        # $test | Should -Be $rec
    }

    It 'Should report the resolved inline image height' {
        $test = ConvertTo-Sixel -Path $cog -Width 2 -Protocol InlineImageProtocol -Force
        $test.Width | Should -Be 2
        $test.Height | Should -BeGreaterThan 0
        $test | Should -Match "width=2;height=$($test.Height);preserveAspectRatio=0:"
    }

    It 'Should convert an image to Kitty format' {
        $test = ConvertTo-Sixel -Path $cog -Width 2 -Protocol KittyGraphicsProtocol -Force
        $test | Should -Not -BeNullOrEmpty
        # $rec = Get-Content -Path ([System.IO.Path]::Combine($samplePath, 'cog_kitty_w2.kitty')) -Raw
        $controls = [regex]::Match([string]$test, '\x1b_G([^;]+);').Groups[1].Value.Split(',')
        $controls | Should -Contain 'q=2'
        $controls | Should -Contain 'c=2'
        $controls | Should -Contain "r=$($test.Height)"
        @($controls -cmatch '^m=[01]$').Count | Should -Be 1
        # $test | Should -Be $rec
    }

    It 'Should convert an image to the Good Image Protocol preview' {
        $test = ConvertTo-Sixel -Path $cog -Width 2 -Protocol GoodImageProtocol -Force
        $test.Width | Should -Be 2
        $match = [regex]::Match([string]$test, '\x1bP!go=s,f=4,c=2,r=(\d+),z=1,a=1;!([A-Za-z0-9+/=]+)\x1b\\')
        $match.Success | Should -BeTrue
        [int]$match.Groups[1].Value | Should -Be $test.Height
        $png = [Convert]::FromBase64String($match.Groups[2].Value)
        [Text.Encoding]::ASCII.GetString($png, 1, 3) | Should -Be 'PNG'
    }

    It 'Should not use the Good Image Protocol preview unless the terminal announced it' {
        if ([Sixel.Terminal.Compatibility]::TerminalSupportsGoodImageProtocol()) {
            Set-ItResult -Skipped -Because 'this terminal announced DA1 code 90'
            return
        }
        { ConvertTo-Sixel -Path $cog -Width 2 -Protocol GoodImageProtocol -ErrorAction Stop } |
            Should -Throw '*DA1 90*'
        ConvertTo-Sixel -Path $cog -Width 2 -Force | Should -Not -Match ([regex]::Escape("$([char]27)P!g"))
    }
    It 'Should convert a filestream to sixel' {
        $test =
        [System.IO.FileStream]::new(
            $cog,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read
        ) |
            ConvertTo-Sixel -Width 2 -Protocol Sixel -Force
        $test | Should -Not -BeNullOrEmpty
        # $rec = Get-Content -Path ([System.IO.Path]::Combine($samplePath, 'cog_sixel_w2.sixel')) -Raw
        # $test | Should -Be $rec
    }
}
Describe 'Sixel Module ConvertTo-GoodImage Tests' {
    BeforeAll {
        $esc = [char]27
        $st = "$esc\"
    }

    It 'Fits locally and frames the oneshot like ConvertTo-Sixel' {
        $test = ConvertTo-GoodImage -Path $cog -Width 4 -CellWidth 10 -CellHeight 20 -Force -ErrorAction Stop
        $test.Width | Should -Be 4
        $test.PixelWidth | Should -Be 40
        $test.PixelHeight | Should -Be ($test.Height * 20)
        [string]$test | Should -Match ("^($esc" + 'D)+' + [regex]::Escape("$esc[$($test.Height)A$($esc)7$($esc)P!go=s,f=4,c=4,r=$($test.Height),z=1,a=1;!"))
    }

    It 'Uploads by name and renders with the requested policies' {
        $test = ConvertTo-GoodImage -Path $cog -Name cog_test -Width 6 -Height 3 -Resize StretchToFill -Alignment MiddleCenter -Layer Above -NoCursorMove -RequestStatus -Force -ErrorAction Stop
        $sequences = [string]$test -split [regex]::Escape($st) | Where-Object { $_ }
        $sequences.Count | Should -Be 2
        $sequences[0] | Should -BeLike "$($esc)P!go=u,n=cog_test,f=4,s;!*"
        $sequences[1] | Should -BeExactly "$($esc)P!go=r,n=cog_test,c=6,r=3,z=3,a=5,L=2,s"
    }

    It 'Passes a PNG through unchanged' {
        $test = ConvertTo-GoodImage -Path $cog -Width 4 -Passthrough -NoCursorMove -Force -ErrorAction Stop
        $body = ([string]$test).Split('!')[-1].TrimEnd($st.ToCharArray())
        [Convert]::FromBase64String($body) | Should -Be ([IO.File]::ReadAllBytes($cog))
    }

    It 'Sends raw RGBA with its pixel size' {
        $test = ConvertTo-GoodImage -Path $cog -Width 2 -CellWidth 10 -CellHeight 20 -Format Rgba -NoCursorMove -Force -ErrorAction Stop
        [string]$test | Should -BeLike "$($esc)P!go=s,f=3,w=20,h=$($test.PixelHeight),c=2,r=$($test.Height),z=1,a=1;!*"
        $body = ([string]$test).Split('!')[-1].TrimEnd($st.ToCharArray())
        [Convert]::FromBase64String($body).Length | Should -Be (20 * $test.PixelHeight * 4)
    }

    It 'Crops only named images' {
        { ConvertTo-GoodImage -Path $cog -Crop 0, 0, 10, 10 -Force -ErrorAction Stop } | Should -Throw '*-Name*'
        $test = ConvertTo-GoodImage -Path $cog -Name c -Crop 10, 20, 100, 50 -Width 4 -CellWidth 10 -CellHeight 20 -NoCursorMove -Force -ErrorAction Stop
        [string]$test | Should -Match ([regex]::Escape("$($esc)P!go=r,n=c,c=4,r=1,z=1,a=1,x=10,y=20,w=100,h=50$st") + '$')
    }

    It 'Renders, releases, queries and sends raw commands' {
        ConvertTo-GoodImage -Name cog -Render -Width 8 -Height 4 -NoCursorMove -Force | Should -BeExactly "$($esc)P!go=r,n=cog,c=8,r=4,z=1,a=1$st"
        [string](ConvertTo-GoodImage -Name cog -Render -Width 8 -Height 4 -Force) |
            Should -BeExactly (("$($esc)D" * 4) + "$esc[4A$($esc)7$($esc)P!go=r,n=cog,c=8,r=4,z=1,a=1$st$($esc)8$esc[4B`r")
        ConvertTo-GoodImage -Name cog -Release -Force | Should -BeExactly "$($esc)P!go=d,n=cog$st"
        ConvertTo-GoodImage -QueryLimits | Should -BeExactly "$($esc)P!go=q$st"
        ConvertTo-GoodImage -Command 'o=s', 'f=4', 'c=2' -Payload ([byte[]](1, 2, 3)) -Force | Should -BeExactly "$($esc)P!go=s,f=4,c=2;!AQID$st"
    }

    It 'Needs the DA1 90 announcement or -Force' {
        if ([Sixel.Terminal.Compatibility]::TerminalSupportsGoodImageProtocol()) {
            Set-ItResult -Skipped -Because 'this terminal announced DA1 code 90'
            return
        }
        { ConvertTo-GoodImage -Path $cog -Width 2 -ErrorAction Stop } | Should -Throw '*DA1 90*'
    }
}
Describe 'Sixel Module ConvertTo-SixelGif Tests' {
    It 'Should convert an image to SixelGif format' {
        $test = ConvertTo-SixelGif -Path ([System.IO.Path]::Combine($imagePath, 'excited.gif')) -Force -Width 30 -LoopCount 1
        $test | Should -Not -BeNullOrEmpty
        $test.GetType().FullName | Should -Be 'Sixel.Terminal.Models.SixelGif'
        $test.FrameCount | Should -Be 28
        $test.Width | Should -Be 30
        $frames = $test.GetType().GetProperty('Sixel', [System.Reflection.BindingFlags]'Instance, NonPublic').GetValue($test)
        $raster = [regex]::Match($frames[0], '"1;1;(\d+);(\d+)')
        $raster.Success | Should -BeTrue
        $cellSize = [Sixel.Terminal.Compatibility]::GetCellSize()
        $occupiedPixelHeight = [Math]::Ceiling([int]$raster.Groups[2].Value / 6.0) * 6
        $test.Height | Should -Be ([Math]::Ceiling($occupiedPixelHeight / $cellSize.PixelHeight))
        $test.LoopCount | Should -Be 1
        $test.Delay | Should -Be 100
    }
}
