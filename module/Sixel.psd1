#
# Module manifest for module 'Sixel'
#
# Generated by: trackd
#
# Generated on: 2024-10-09
#

@{
    RootModule           = 'Sixel.psm1'
    ModuleVersion        = '0.1'
    CompatiblePSEditions = 'Core'
    PowerShellVersion    = '7.4'
    GUID                 = '95f4627c-f8f5-43d5-824b-4c356737f87b'
    Author               = 'trackd', 'Shaun Lawrie'
    Copyright            = '(c) trackd. All rights reserved.'
    Description          = 'Convert images to Sixel format and display them in the terminal'
    CmdletsToExport      = @('ConvertTo-Sixel')
    AliasesToExport      = @('cts')
    PrivateData          = @{
        PSData = @{
            Tags = @(
                'Sixel',
                'Terminal',
                'Graphics',
                'Image'
            )
            LicenseUri = 'https://github.com/trackd/Sixel/blob/main/LICENSE'
            ProjectUri = 'https://github.com/trackd/Sixel'
        }
    }
}