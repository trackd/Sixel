# Module manifest for module 'Sixel'
# Generated by: trackd
# Generated on: 2024-10-09

@{
    RootModule             = 'Sixel.psm1'
    ModuleVersion          = '0.4.0'
    CompatiblePSEditions   = @('Desktop', 'Core')
    PowerShellVersion      = '5.1'
    DotNetFrameworkVersion = '4.7.2'
    ClrVersion             = '4.0'
    GUID                   = '95f4627c-f8f5-43d5-824b-4c356737f87b'
    Author                 = 'trackd, ShaunLawrie'
    Copyright              = '(c) trackd. All rights reserved.'
    Description            = '
    Display images in the terminal using various protocols

    ✔️ Sixel
        - Gif support using ConvertTo-SixelGif with Audio support.
    ✔️ Inline Image Protocol
        - Supported by VSCode, xterm2, WezTerm, and others.
    ✔️ Kitty Graphics Protocol
        - Supported by Kitty terminal.
    ✔️ Block cells
        - Fallback to block cells if no image protocol is supported.

    Note: Sixel requires Windows Terminal v1.22+ or VSCode Insiders.
'
    CmdletsToExport        = 'ConvertTo-Sixel', 'ConvertTo-SixelGif'
    AliasesToExport        = 'cts', 'gif'
    FormatsToProcess       = 'Sixel.format.ps1xml'
    PrivateData            = @{
        PSData = @{
            Tags         = @(
                'Sixel',
                'Image',
                'ConsoleImage',
                'InlineImageProtocol',
                'KittyGraphicsProtocol',
                'Linux',
                'Mac',
                'Windows'
            )
            LicenseUri   = 'https://github.com/trackd/Sixel/blob/main/LICENSE'
            ProjectUri   = 'https://github.com/trackd/Sixel'
            # Prerelease   = 'prerelease01'
            ReleaseNotes = @'
            0.4.0 - Added support for Windows Powershell 5.1, added parameter -Stream.
            0.3.2 - Added support for Audio in ConvertTo-SixelGif.
            0.3.1 - Added GIF support, added Ascii art using halfblock cells.
            0.2.5 - bugfix, cleanup, added experimental support for inline image protocol and Kitty Graphics Protocol.
            0.2.0 - Added better scaling, changes -Width to use cell width instead.
            0.1.0 - Initial release.
'@
        }
    }
    # A missing or $null entry is equivalent to specifying the wildcard *. declare unused with @() for better perf.
    FunctionsToExport      = @()
    VariablesToExport      = @()
    TypesToProcess         = @()
}
