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
            foreach ($commandName in 'ConvertTo-Sixel', 'ConvertTo-SixelGif', 'ConvertTo-Kitty') {
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
        $functions.count | Should -Be 3
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
    It 'Prefers RGBA for Contour without overriding an explicit format or reference' {
        $terminalField = [Sixel.Terminal.Compatibility].GetField('_terminalInfo', [System.Reflection.BindingFlags]'Static,NonPublic')
        $identityField = [Sixel.Terminal.Compatibility].GetField('_terminalIdentity', [System.Reflection.BindingFlags]'Static,NonPublic')
        $savedTerminal = $terminalField.GetValue($null)
        $savedIdentity = $identityField.GetValue($null)
        try {
            $terminalField.SetValue($null, [Sixel.Terminal.Models.TerminalInfo]@{ Terminal = 'Contour' })
            $identityField.SetValue($null, $null)
            $sequence = ConvertTo-Kitty -Path $cog -Width 2 -Force -ErrorAction Stop
            $sequence | Should -Match 'f=32'
            $sequence | Should -Match 'C=1'
            $sequence.EndsWith("`r") | Should -BeTrue
            $sequence | Should -Match "s=$($sequence.PixelWidth),v=$($sequence.PixelHeight)"
            ConvertTo-Kitty -Path $cog -Width 2 -Format Png -Force -ErrorAction Stop | Should -Match 'f=100'
            ConvertTo-Kitty -Reference '/tmp/example.png' -Force -ErrorAction Stop | Should -Match 'f=100'
            $legacySequence = ConvertTo-Sixel -Path $cog -Width 2 -Protocol KittyGraphicsProtocol -Force -ErrorAction Stop
            $legacySequence | Should -Match 'f=32'
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
        $test | Should -Match "q=2,c=2,r=$($test.Height),m="
        # $test | Should -Be $rec
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
