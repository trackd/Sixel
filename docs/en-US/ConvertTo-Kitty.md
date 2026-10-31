---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/en-US/ConvertTo-Kitty.md
schema: 2.0.0
---

# ConvertTo-Kitty

## SYNOPSIS

Converts images and control operations to Kitty graphics escape sequences (preview).

## SYNTAX

### Path (Default)

```powershell
ConvertTo-Kitty [-Path] <String> [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-ImageId <UInt32>] [-ImageNumber <UInt32>] [-PlacementId <UInt32>] [-Quiet <Int32>] [-NoCursorMove] [-Tmux] [-ChunkSize <Int32>] [-Control <String[]>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Compress] [-Animate] [-UnicodePlaceholder] [-Force] [<CommonParameters>]
```

### Stream

```powershell
ConvertTo-Kitty -Stream <Stream> [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-ImageId <UInt32>] [-ImageNumber <UInt32>] [-PlacementId <UInt32>] [-Quiet <Int32>] [-NoCursorMove] [-Tmux] [-ChunkSize <Int32>] [-Control <String[]>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Compress] [-Animate] [-UnicodePlaceholder] [-Force] [<CommonParameters>]
```

### Url

```powershell
ConvertTo-Kitty -Url <Uri> [-Timeout <TimeSpan>] [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-ImageId <UInt32>] [-ImageNumber <UInt32>] [-PlacementId <UInt32>] [-Quiet <Int32>] [-NoCursorMove] [-Tmux] [-ChunkSize <Int32>] [-Control <String[]>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Compress] [-Animate] [-UnicodePlaceholder] [-Force] [<CommonParameters>]
```

### Bytes

```powershell
ConvertTo-Kitty -Bytes <Byte[]> [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-ImageId <UInt32>] [-ImageNumber <UInt32>] [-PlacementId <UInt32>] [-Quiet <Int32>] [-NoCursorMove] [-Tmux] [-ChunkSize <Int32>] [-Control <String[]>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Compress] [-Animate] [-UnicodePlaceholder] [-Force] [<CommonParameters>]
```

### Base64

```powershell
ConvertTo-Kitty -Base64 <String> [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-ImageId <UInt32>] [-ImageNumber <UInt32>] [-PlacementId <UInt32>] [-Quiet <Int32>] [-NoCursorMove] [-Tmux] [-ChunkSize <Int32>] [-Control <String[]>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Compress] [-Animate] [-UnicodePlaceholder] [-Force] [<CommonParameters>]
```

### Command

```powershell
ConvertTo-Kitty -Command <String[]> [-Payload <Byte[]>] [-Tmux] [-ChunkSize <Int32>] [-Force] [<CommonParameters>]
```

### Reference

```powershell
ConvertTo-Kitty -Reference <String> [-Medium <String>] [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-ImageId <UInt32>] [-ImageNumber <UInt32>] [-PlacementId <UInt32>] [-Quiet <Int32>] [-NoCursorMove] [-Tmux] [-ChunkSize <Int32>] [-Control <String[]>] [-Force] [<CommonParameters>]
```

## DESCRIPTION

Windows PowerShell 5.1 uses System.Drawing for BMP, GIF, JPEG, PNG, and TIFF input,
including GIF animation. PowerShell 7 retains ImageSharp and its additional codecs.
The Kitty wire controls, raw RGB/RGBA output, and PNG output are available on both engines.

A separate preview encoder for the Kitty graphics protocol. ConvertTo-Sixel and its existing Kitty path are unchanged.
Returns the image/control sequence as a string. The first eligible cmdlet execution
probes terminal capabilities and metrics; use Force to suppress this terminal I/O.
Import, discovery, completion, and passive getters do not probe.
The receiving terminal determines which protocol features it implements.

Decoded images are resized in pixel space with aspect ratio preserved and no transparent cell padding.
Native placement uses exact pixel dimensions, avoiding rounding a partial cell into a larger raster.
Width/Height output properties describe occupied cells using the chosen cell metrics; PixelWidth/PixelHeight describe the actual raster.
Explicit advanced placement controls can change the display footprint independently of those raster measurements.

Use Command for display, deletion, animation control, composition, queries, or raw transfer.
Case-sensitive string entries preserve pairs such as i/I, x/X, and s/S in PowerShell.
Use the examples below to compare image transmission, placement, and animation.

## EXAMPLES

### Example 1: Display a sized image

```powershell
$image = ConvertTo-Kitty ./assets/cog.png -Width 20 -CellWidth 10 -CellHeight 20
[Console]::Write($image)
```

Writes the returned sequence without PowerShell adding a newline.

### Example 2: Upload once and place twice

```powershell
[Console]::Write((ConvertTo-Kitty ./assets/cog.png -Action Transmit -ImageId 42))
[Console]::Write((ConvertTo-Kitty -Command 'a=p','i=42','p=1','c=10'))
[Console]::Write((ConvertTo-Kitty -Command 'a=p','i=42','p=2','c=20'))
```

Creates two independent placements of one uploaded image.

### Example 3: Native GIF animation

```powershell
[Console]::Write((ConvertTo-Kitty ./animation.gif -ImageId 42 -Width 30 -Animate))
[Console]::Write((ConvertTo-Kitty -Command 'a=a','i=42','s=1'))
[Console]::Write((ConvertTo-Kitty -Command 'a=d','d=I','i=42'))
```

Starts an animation, stops it, then removes its placements and image data.

### Example 4: Unicode placeholders through tmux

```powershell
[Console]::Write((ConvertTo-Kitty ./assets/cog.png -ImageId 42 -Width 20 -UnicodePlaceholder -Tmux))
```

Requires terminal placeholder support and tmux passthrough configuration.

## PARAMETERS

### -Force

Bypass terminal feature compatibility checks and skip automatic terminal probing. Wire validation still applies. Uses cached/environment information and fallback cell sizing when measurements are unavailable. A later invocation without Force can still perform the session's first probe.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:
Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```


### -Path

Local image path. FileInfo.FullName binds from the pipeline.

```yaml
Type: String
Parameter Sets: Path
Aliases:FullName
Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Stream

Readable image stream. Seekable streams are rewound; caller-owned streams are never disposed.

```yaml
Type: Stream
Parameter Sets: Stream
Aliases:
Required: True
Position: Named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Url

HTTP or HTTPS image URL.

```yaml
Type: Uri
Parameter Sets: Url
Aliases:Uri
Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout

Download timeout. Default: 15 seconds.

```yaml
Type: TimeSpan
Parameter Sets: Url
Aliases:
Required: False
Position: Named
Default value: 00:00:15
Accept pipeline input: False
Accept wildcard characters: False
```

### -Bytes

Encoded image file bytes, such as PNG or GIF. For raw pixels use -Command and -Payload.

```yaml
Type: Byte[]
Parameter Sets: Bytes
Aliases:
Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Base64

Base64 encoded image file, optionally with a data URI prefix.

```yaml
Type: String
Parameter Sets: Base64
Aliases:
Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Command

Complete case-sensitive wire controls, for example 'a=p','i=42','c=20'. The encoder supplies chunk framing and defaults q=2. Payload, Tmux and Force may accompany this parameter.

```yaml
Type: String[]
Parameter Sets: Command
Aliases:
Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Payload

Uncompressed image bytes for a direct raw command, or UTF-8 filename/shared-memory name for a reference command. If o=z is specified for direct transmission, the encoder compresses these bytes.

```yaml
Type: Byte[]
Parameter Sets: Command
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Reference

Terminal-side filename or shared-memory name. No local file is created, read, checked, or deleted. Temporary-file and shared-memory transfers permit the terminal to remove the referenced resource.

```yaml
Type: String
Parameter Sets: Reference
Aliases:
Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Medium

Reference transfer medium: File (default), TemporaryFile, or SharedMemory.

```yaml
Type: String
Parameter Sets: Reference
Aliases:
Required: False
Position: Named
Default value: File
Accept pipeline input: False
Accept wildcard characters: False
```

### -Action

Image/reference action: TransmitAndDisplay (default), Transmit, Query, or Frame. Other actions use Command. Query requires an image ID and explicit Quiet 0 to request a reply.

```yaml
Type: String
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: TransmitAndDisplay
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format

Png, Rgb (24-bit), or Rgba (32-bit). RGB discards alpha; RGBA carries red, green, blue, and transparency bytes for each pixel. When omitted, decoded images use PNG except in recognized Contour terminals, where RGBA avoids its Kitty PNG-decoding issue. Explicit choices are preserved. Reference data must already be in this format and defaults to PNG.

```yaml
Type: String
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: Png
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width

Maximum raster width in cells for decoded images. Combined with Height, fits within both constraints while preserving aspect ratio. For references, sets placement columns c without reading image data.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height

Maximum raster height in cells for decoded images. For references, sets placement rows r.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -CellWidth

Cell pixel width used in sizing. Defaults to cached passive terminal metrics or the module fallback. Does not query the terminal.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Bytes, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CellHeight

Cell pixel height used in sizing. Defaults to cached passive terminal metrics or the module fallback.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Bytes, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImageId

Explicit image identifier. Required for Animate and UnicodePlaceholder. Mutually exclusive with ImageNumber.

```yaml
Type: UInt32
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImageNumber

Client image number I. The terminal assigns its own ID. Mutually exclusive with ImageId.

```yaml
Type: UInt32
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -PlacementId

Placement identifier. Placeholder text supports the lower 24-bit range only.

```yaml
Type: UInt32
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -Quiet

2 suppresses replies to the encoded image command (default); 1 permits errors; 0 permits all replies. The converter reads its own lazy-probe replies but does not read replies to the returned image/control sequence. Only request those replies when your application owns terminal input.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 2
Accept pipeline input: False
Accept wildcard characters: False
```

### -Compress

Apply RFC 1950 zlib compression before direct transmission. References instead use Control 'o=z' for already-compressed data.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Bytes, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Animate

Convert coalesced animation frames to native Kitty animation, retaining frame delays and loop count. Supports GIF on both runtimes and animated PNG/WebP on PowerShell 7. Requires ImageId. Other sources can be submitted with Frame/Command.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Bytes, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UnicodePlaceholder

Create a virtual placement and append its Unicode text grid. Requires ImageId. Ends at the first column below the grid and resets foreground/underline colors. Maximum 297 rows and columns.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Bytes, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoCursorMove

Set C=1 for placement. Cannot be combined with UnicodePlaceholder. Normal placement otherwise uses the terminal's cursor movement rules.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tmux

Wrap each graphics packet in tmux passthrough. Placeholder text remains outside the wrapper. The tmux session must allow passthrough.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ChunkSize

Maximum base64 characters in each direct-transfer packet. Must be a multiple of four from 4 to 4096. Smaller values accommodate transport limits; reference names remain a single packet. This does not enable GNU Screen passthrough.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:
Required: False
Position: Named
Default value: 4096
Accept pipeline input: False
Accept wildcard characters: False
```

### -Control

Additional case-sensitive key=value controls for image/reference commands. Duplicates, malformed values, and controls invalid for the action are rejected. Use crop, placement, frame, relative placement, and transient keys described in the protocol.

```yaml
Type: String[]
Parameter Sets: Path, Stream, Url, Bytes, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters


This cmdlet supports the common parameters: Debug, ErrorAction, ErrorVariable, InformationAction, InformationVariable, OutBuffer, OutVariable, PipelineVariable, Verbose, WarningAction, and WarningVariable.

## INPUTS

### System.IO.FileInfo

FullName binds to Path.

### System.IO.Stream

Binds to Stream.

## OUTPUTS

### System.String

Kitty escape sequences. Decoded image output also has Width, Height, PixelWidth, PixelHeight, and ImageId note properties.

## NOTES

Preview API. Import and completion are passive. The first eligible ConsoleHost execution probes once; pending input, redirection, known input-reader call stacks, or Force skip probing. Explicit RefreshTerminalInfo can refresh the cache. Exceptionally late replies and concurrent input readers remain a limitation.
Nonzero image IDs are caller-managed; use distinct IDs when retaining multiple images.
KittyTransfer provides disposable local resource creation. KittyResponse and KittySession support acknowledgement parsing and caller-owned exchanges; see the preview guide. Input routing and live terminal state remain application-owned.

## RELATED LINKS

[Kitty graphics protocol](https://sw.kovidgoyal.net/kitty/graphics-protocol/)

[Preview guide](../kitty-preview.md)
