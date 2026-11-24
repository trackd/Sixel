---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/en-US/ConvertTo-GoodImage.md
schema: 2.0.0
---

# ConvertTo-GoodImage

## SYNOPSIS

Converts images to Good Image Protocol escape sequences, with every render option (preview).

## SYNTAX

### Path (Default)

```powershell
ConvertTo-GoodImage [-Path] <String> [-Name <String>] [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Alignment <GoodImageAlignment>] [-Resize <GoodImageResize>] [-Layer <GoodImageLayer>] [-Crop <Int32[]>] [-Passthrough] [-UpdateCursor] [-AutoScroll] [-NoCursorMove] [-RequestStatus] [-Header <String[]>] [-Force] [<CommonParameters>]
```

### Stream

```powershell
ConvertTo-GoodImage -Stream <Stream> [-Name <String>] [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Alignment <GoodImageAlignment>] [-Resize <GoodImageResize>] [-Layer <GoodImageLayer>] [-Crop <Int32[]>] [-Passthrough] [-UpdateCursor] [-AutoScroll] [-NoCursorMove] [-RequestStatus] [-Header <String[]>] [-Force] [<CommonParameters>]
```

### Url

```powershell
ConvertTo-GoodImage -Url <Uri> [-Timeout <TimeSpan>] [-Name <String>] [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Alignment <GoodImageAlignment>] [-Resize <GoodImageResize>] [-Layer <GoodImageLayer>] [-Crop <Int32[]>] [-Passthrough] [-UpdateCursor] [-AutoScroll] [-NoCursorMove] [-RequestStatus] [-Header <String[]>] [-Force] [<CommonParameters>]
```

### Base64

```powershell
ConvertTo-GoodImage -Base64 <String> [-Name <String>] [-Action <String>] [-Format <String>] [-Width <Int32>] [-Height <Int32>] [-CellWidth <Int32>] [-CellHeight <Int32>] [-Alignment <GoodImageAlignment>] [-Resize <GoodImageResize>] [-Layer <GoodImageLayer>] [-Crop <Int32[]>] [-Passthrough] [-UpdateCursor] [-AutoScroll] [-NoCursorMove] [-RequestStatus] [-Header <String[]>] [-Force] [<CommonParameters>]
```

### Render

```powershell
ConvertTo-GoodImage -Name <String> -Render [-Width <Int32>] [-Height <Int32>] [-Alignment <GoodImageAlignment>] [-Resize <GoodImageResize>] [-Layer <GoodImageLayer>] [-Crop <Int32[]>] [-UpdateCursor] [-AutoScroll] [-NoCursorMove] [-RequestStatus] [-Header <String[]>] [-Force] [<CommonParameters>]
```

### Release

```powershell
ConvertTo-GoodImage -Name <String> -Release [-Force] [<CommonParameters>]
```

### Query

```powershell
ConvertTo-GoodImage -QueryLimits [-Force] [<CommonParameters>]
```

### Command

```powershell
ConvertTo-GoodImage -Command <String[]> [-Payload <Byte[]>] [-Force] [<CommonParameters>]
```

## DESCRIPTION

A preview encoder for the Good Image Protocol (DCS ! g), a cell-bound image protocol: images occupy
cells like text and are fitted, aligned and layered by policy. The specification is alpha and may
change. ConvertTo-Sixel -Protocol GoodImageProtocol gives the same default output; this cmdlet exposes
the rest of the protocol.

By default the image is resized locally to fit Width and Height, padded to whole cells and sent as one
PNG (o=s,f=4,c,r,z=1,a=1). Rows are reserved first, so the image keeps the cursor's column and text
continues below it at column 1, as with the other protocols. Name uploads and renders by name instead,
which also allows a crop. Render, Release, QueryLimits and Command produce the other operations.

The terminal must list code 90 in its DA1 reply; the first eligible execution probes for it, and Force
skips the check. Contour supports the protocol with images: good_image_protocol: true in its
configuration. Windows PowerShell 5.1 uses System.Drawing and PowerShell 7 uses ImageSharp; both send
the same sequences.

## EXAMPLES

### Example 1: Display a sized image

```powershell
[Console]::Write((ConvertTo-GoodImage ./assets/cog.png -Width 20))
```

Writes the image 20 columns wide and leaves the cursor below it.

### Example 2: Upload once and render several ways

```powershell
[Console]::Write((ConvertTo-GoodImage ./assets/cog.png -Name cog -Action Upload))
[Console]::Write((ConvertTo-GoodImage -Name cog -Render -Width 8 -Height 4))
[Console]::Write((ConvertTo-GoodImage -Name cog -Render -Width 16 -Height 4 -Resize StretchToFill))
[Console]::Write((ConvertTo-GoodImage -Name cog -Release))
```

Uploads the image under a name, renders it fitted and stretched, then releases the name.

### Example 3: Crop, center and draw below the text

```powershell
[Console]::Write((ConvertTo-GoodImage ./assets/cog.png -Name cog -Crop 130,130,210,210 -Width 8 -Height 4 -Alignment MiddleCenter -Layer Below))
```

Shows the middle of the image under any text already in those cells.

### Example 4: Let the terminal scale the original PNG

```powershell
[Console]::Write((ConvertTo-GoodImage ./photo.png -Width 40 -Passthrough))
```

Sends the PNG unchanged; the terminal fits it into 40 columns.

## PARAMETERS
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

### -Name

Image name: 1-512 characters of A-Z, a-z, 0-9 and _. With an image source, the image is uploaded under this name (o=u) and, for Display, rendered by it (o=r) instead of sent as a oneshot (o=s). Required with Render and Release. Names are caller-managed; uploading a name again replaces it.

```yaml
Type: String
Parameter Sets: Path, Stream, Url, Base64, Render, Release
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Render

Renders an image uploaded earlier under Name (o=r) at the cursor. Rows are reserved only when Height is given, since the cmdlet does not know the image size.

```yaml
Type: SwitchParameter
Parameter Sets: Render
Aliases:

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Release

Releases the image stored under Name (o=d). Cells already showing it keep it.

```yaml
Type: SwitchParameter
Parameter Sets: Release
Aliases:

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -QueryLimits

Returns the limits query (o=q). The terminal answers DCS ! g s=8,m=<images>,b=<bytes>,w=<width>,h=<height> ST, which the caller must read; GoodImageSession.QueryLimits() does both. Needs no DA1 announcement.

```yaml
Type: SwitchParameter
Parameter Sets: Query
Aliases:

Required: True
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Command

Raw headers for one DCS ! g sequence, for example 'o=r','n=cog','c=8','r=4'. Boolean headers are names without a value, such as 'u' or 's'. Nothing is validated except the framing.

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

Body for a raw Command, sent Base64 encoded after ;!. At most 16 MB.

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

### -Action

Display (default) shows the image: a oneshot (o=s), or upload and render by name when Name is given. Upload only stores it under Name (o=u), for later -Render calls, and moves no cursor.

```yaml
Type: String
Parameter Sets: Path, Stream, Url, Base64
Aliases:

Required: False
Position: Named
Default value: Display
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format

Pixel format of the body: Png (f=4, default), Rgb (f=2) or Rgba (f=3). Raw formats add the pixel width and height (w, h) and are larger.

```yaml
Type: String
Parameter Sets: Path, Stream, Url, Base64
Aliases:

Required: False
Position: Named
Default value: Png
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width

Columns the image occupies (c). With the default ResizeToFit, Width and Height bound the image and the aspect ratio decides the other axis; other resize policies use them as given. 0 leaves the axis to the aspect ratio, or, with Render, to the terminal.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height

Rows the image occupies (r). A placed image is never taller than the page, because Contour clamps r to the page height.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -CellWidth

Cell width in pixels for sizing, overriding the probed or fallback value.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Base64
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CellHeight

Cell height in pixels for sizing, overriding the probed or fallback value.

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Base64
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Alignment

Where the image sits inside its cells (a): TopStart, TopCenter, TopEnd, MiddleStart, MiddleCenter, MiddleEnd, BottomStart, BottomCenter or BottomEnd.

```yaml
Type: GoodImageAlignment
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: TopStart
Accept pipeline input: False
Accept wildcard characters: False
```

### -Resize

How the image is scaled into its cells (z): NoResize, ResizeToFit (default), ResizeToFill or StretchToFill. ResizeToFit is done locally, padded to whole cells, so no gap shows. Any other policy sends the image at its own size and lets the terminal scale it.

```yaml
Type: GoodImageResize
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: ResizeToFit
Accept pipeline input: False
Accept wildcard characters: False
```

### -Layer

Below (L=0) draws the image under the text in its cells, Replace (default) instead of it, Above (L=2) over it.

```yaml
Type: GoodImageLayer
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: Replace
Accept pipeline input: False
Accept wildcard characters: False
```

### -Crop

Source rectangle x, y, width, height in pixels (x, y, w, h). Only rendering by name can crop, so a crop needs Name. Width or height 0 means to the edge of the image. The cells follow the cropped size.

```yaml
Type: Int32[]
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Passthrough

Sends the source at its own size: a single-frame PNG is sent byte for byte, anything else is re-encoded without resizing. The terminal scales it into the cells, and fills any part the image does not cover with its background.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -UpdateCursor

Asks the terminal to move the cursor below the image (u) instead of the cmdlet reserving rows. Contour keeps the image's column; the specification says column 1.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -AutoScroll

Lets the terminal scroll the page when the image extends below it (l). Placed images as tall as the page get u and l automatically.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoCursorMove

Returns the bare sequence without reserving rows or moving the cursor, so the cursor stays at the image's top-left cell.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestStatus

Asks for a status reply (s) to each operation: DCS ! g s=0 ST on success, 1 for an unknown name, 2 for invalid data, 3 over a limit. The reply arrives as terminal input and the caller must read it; GoodImageSession handles this.

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Header

Extra raw headers appended to the last operation. A later header overrides an earlier one, so this can replace any computed value.

```yaml
Type: String[]
Parameter Sets: Path, Stream, Url, Base64, Render
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force

Skips the DA1 code 90 check and automatic terminal probing, and uses cached or fallback cell sizes.

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

### CommonParameters


This cmdlet supports the common parameters: Debug, ErrorAction, ErrorVariable, InformationAction, InformationVariable, OutBuffer, OutVariable, PipelineVariable, Verbose, WarningAction, and WarningVariable.

## INPUTS

### System.IO.FileInfo

FullName binds to Path.

### System.IO.Stream

Binds to Stream.

## OUTPUTS

### System.String

Good Image Protocol escape sequences. Image output also has Width, Height (cells), PixelWidth, PixelHeight and Name note properties.

## NOTES

Preview of an alpha specification; the wire format may change. Auto protocol selection never picks it.
Use [Sixel.Protocols.GoodImageSession] when an application owns terminal input and wants status
replies: it uploads, renders, queries limits and releases its names on dispose.
[Sixel.Terminal.Compatibility]::TerminalSupportsGoodImageProtocol() reports the DA1 code 90 result.

## RELATED LINKS

[Good Image Protocol specification](https://github.com/contour-terminal/terminal-good-image-protocol)

[ConvertTo-Sixel](ConvertTo-Sixel.md)
