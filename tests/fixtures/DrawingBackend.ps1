# Isolated Windows PowerShell 5.1 compatibility fixture. Results, not VT output,
# cross the process boundary. The parent Pester test owns the temporary directory.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ModulePath,
    [Parameter(Mandatory)][string]$ResultPath
)
$ErrorActionPreference = 'Stop'
Import-Module $ModulePath -ErrorAction Stop
Add-Type -AssemblyName System.Drawing
[Sixel.Terminal.Compatibility]::SetCellSize(1, 1)
$bitmap = New-Object System.Drawing.Bitmap 3, 1
$png = New-Object System.IO.MemoryStream
try {
    $bitmap.SetPixel(0, 0, [Drawing.Color]::Red)
    $bitmap.SetPixel(1, 0, [Drawing.Color]::Lime)
    $bitmap.SetPixel(2, 0, [Drawing.Color]::FromArgb(0, 0, 0, 255))
    $bitmap.Save($png, [Drawing.Imaging.ImageFormat]::Png)
    $imageBytes = $png.ToArray()
}
finally { $bitmap.Dispose(); $png.Dispose() }

$gradient = New-Object Drawing.Bitmap 4, 1
$gradientStream = New-Object IO.MemoryStream
try {
    $colors = @([Drawing.Color]::Red, [Drawing.Color]::Lime, [Drawing.Color]::Blue, [Drawing.Color]::White)
    for ($index = 0; $index -lt 4; $index++) { $gradient.SetPixel($index, 0, $colors[$index]) }
    $gradient.Save($gradientStream, [Drawing.Imaging.ImageFormat]::Png)
    $gradientStream.Position = 0
    $quantized = ConvertTo-Sixel -Stream $gradientStream -Protocol Sixel -MaxColors 2 -Force -ErrorAction Stop
}
finally { $gradient.Dispose(); $gradientStream.Dispose() }

function Get-KittyPayload {
    param([string]$Text)
    $packets = [regex]::Matches($Text, [char]27 + '_G(?<header>[^;]*);(?<payload>[^' + [char]27 + ']*)' + [char]27 + '\\')
    $encoded = ($packets | ForEach-Object { $_.Groups['payload'].Value }) -join ''
    ,([Convert]::FromBase64String($encoded))
}

$stream = New-Object System.IO.MemoryStream (,$imageBytes)
try {
    $sixel = ConvertTo-Sixel -Stream $stream -Protocol Sixel -MaxColors 2 -Force -ErrorAction Stop
    $streamOpen = $stream.CanRead
    $stream.Position = 0
    $inline = ConvertTo-Sixel -Stream $stream -Protocol InlineImageProtocol -Force -ErrorAction Stop
    $stream.Position = 0
    $legacyKitty = ConvertTo-Sixel -Stream $stream -Protocol KittyGraphicsProtocol -Force -ErrorAction Stop
    $stream.Position = 0
    $blocks = ConvertTo-Sixel -Stream $stream -Protocol Blocks -Force -ErrorAction Stop
    $stream.Position = 0
    $braille = ConvertTo-Sixel -Stream $stream -Protocol Braille -Force -ErrorAction Stop
}
finally { $stream.Dispose() }
$rgba = ConvertTo-Kitty -Bytes $imageBytes -Format Rgba -CellWidth 1 -CellHeight 1 -Force -ErrorAction Stop
$rgb = ConvertTo-Kitty -Bytes $imageBytes -Format Rgb -CellWidth 1 -CellHeight 1 -Force -ErrorAction Stop
$resized = ConvertTo-Kitty -Bytes $imageBytes -Format Png -Width 6 -CellWidth 1 -CellHeight 1 -Force -ErrorAction Stop
$inlineMatch = [regex]::Match([string]$inline, 'File=[^:]*:(?<payload>[A-Za-z0-9+/=]+)')
$inlineBytes = [Convert]::FromBase64String($inlineMatch.Groups['payload'].Value)
$inlineStream = New-Object IO.MemoryStream (,$inlineBytes)
$inlineImage = [Drawing.Image]::FromStream($inlineStream)
try {
    $inlineSize = @($inlineImage.Width, $inlineImage.Height)
    $inlineAlpha = $inlineImage.GetPixel(2, 0).A
}
finally { $inlineImage.Dispose(); $inlineStream.Dispose() }
$resizedBytes = Get-KittyPayload ([string]$resized)
$resizedStream = New-Object IO.MemoryStream (,$resizedBytes)
$resizedImage = [Drawing.Image]::FromStream($resizedStream)
try { $resizedSize = @($resizedImage.Width, $resizedImage.Height) }
finally { $resizedImage.Dispose(); $resizedStream.Dispose() }

# Minimal two-frame GIF: 1x1 red then green; delays 7/0 centiseconds, repeat=2.
$gifHex = '47494638396101000100800000FF000000FF0021FF0B4E45545343415045322E30030102000021F90400070000002C000000000100010000020244010021F90400000000002C00000000010001000002024C01003B'
$gifBytes = New-Object byte[] ($gifHex.Length / 2)
for ($index = 0; $index -lt $gifBytes.Length; $index++) { $gifBytes[$index] = [Convert]::ToByte($gifHex.Substring($index * 2, 2), 16) }
$gifStream = New-Object IO.MemoryStream (,$gifBytes)
try { $gif = ConvertTo-SixelGif -Stream $gifStream -MaxColors 2 -Force -ErrorAction Stop }
finally { $gifStream.Dispose() }
$animated = ConvertTo-Kitty -Bytes $gifBytes -Animate -ImageId 9 -Format Rgba -CellWidth 1 -CellHeight 1 -Force -ErrorAction Stop
$inlineAnimationRejected = $false
$gifStream = New-Object IO.MemoryStream (,$gifBytes)
try {
    try { $null = ConvertTo-Sixel -Stream $gifStream -Protocol InlineImageProtocol -Force -ErrorAction Stop }
    catch { $inlineAnimationRejected = $_.Exception.Message -like '*inline animation requires PowerShell 7*' }
}
finally { $gifStream.Dispose() }
$invalidFailed = $false
try { $null = ConvertTo-Kitty -Bytes ([byte[]]@(1, 2, 3)) -Force -ErrorAction Stop }
catch { $invalidFailed = $true }

$result = [ordered]@{
    Engine = $PSVersionTable.PSVersion.Major
    References = @([Sixel.Terminal.Compatibility].Assembly.GetReferencedAssemblies() | ForEach-Object Name)
    Loaded = @([AppDomain]::CurrentDomain.GetAssemblies() | ForEach-Object { $_.GetName().Name })
    StreamOpen = $streamOpen
    Blocks = [string]$blocks
    Braille = [string]$braille
    Sixel = [string]$sixel
    Quantized = [string]$quantized
    SixelSize = @($sixel.Width, $sixel.Height)
    Rgba = [Convert]::ToBase64String((Get-KittyPayload ([string]$rgba)))
    Rgb = [Convert]::ToBase64String((Get-KittyPayload ([string]$rgb)))
    InlineSize = $inlineSize
    InlineAlpha = $inlineAlpha
    LegacyKitty = [Convert]::ToBase64String((Get-KittyPayload ([string]$legacyKitty)))
    ResizedSize = $resizedSize
    GifFrames = $gif.FrameCount
    GifDelay = $gif.Delay
    Animated = [string]$animated
    InlineAnimationRejected = $inlineAnimationRejected
    InvalidFailed = $invalidFailed
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ResultPath -Encoding UTF8
