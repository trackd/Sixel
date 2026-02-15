BeforeAll {
    # Load the module to be tested
    Import-Module ([System.IO.Path]::Combine($PSScriptRoot, '..', 'output', 'Sixel.psd1'))
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
    It 'Should have the Sixel module loaded' {
        $module = Get-Module -Name Sixel
        $module | Should -Not -BeNullOrEmpty
    }

    # It 'Should have the Sixel module exported functions' {
    #     $functions = Get-Command -Module Sixel
    #     $functions.count | Should -Be 2
    # }

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
Describe 'Sixel Module ConvertTo-Sixel Tests' {
    It 'Should convert an image to Sixel format' {
        $test = ConvertTo-Sixel -Path $cog -Width 2 -Protocol Sixel -Force
        $test | Should -Not -BeNullOrEmpty
        # $rec = Get-Content -Path ([System.IO.Path]::Combine($samplePath, 'cog_sixel_w2.sixel')) -Raw
        # "`nSixel", $test | Out-Host
        # $test | Should -Be $rec
    }

    # # need to update the test file after including the height value in the string
    It 'Should convert an image to InlineImage format' {
        $test = ConvertTo-Sixel -Path $cog -Width 2 -Protocol InlineImageProtocol -Force
        $test | Should -Not -BeNullOrEmpty
        # $rec = Get-Content -Path ([System.IO.Path]::Combine($samplePath, 'cog_inline_w2.iip')) -Raw
        # "`nInlineImage", $test | Out-Host
        # $test | Should -Be $rec
    }

    It 'Should convert an image to Kitty format' {
        $test = ConvertTo-Sixel -Path $cog -Width 2 -Protocol KittyGraphicsProtocol -Force
        $test | Should -Not -BeNullOrEmpty
        # $rec = Get-Content -Path ([System.IO.Path]::Combine($samplePath, 'cog_kitty_w2.kitty')) -Raw
        # "`nKitty", $test | Out-Host
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
        # $test.Height | Should -Be 12
        $test.LoopCount | Should -Be 1
        $test.Delay | Should -Be 100
    }
}
