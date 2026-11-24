---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/zh-CN/ConvertTo-GoodImage.md
schema: 2.0.0
---

# ConvertTo-GoodImage

## SYNOPSIS

将图像转换为 Good Image Protocol 转义序列，并提供所有渲染选项（预览）。

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

Good Image Protocol（DCS ! g）的预览编码器。这是一种以单元格为单位的图像协议：图像像文本一样
占用单元格，并按策略进行适配、对齐和分层。该规范仍处于 alpha 阶段，可能会变化。
ConvertTo-Sixel -Protocol GoodImageProtocol 生成相同的默认输出；此 cmdlet 提供协议的其余部分。

默认情况下，图像会在本地缩放以适应 Width 和 Height，填充到完整的单元格，并作为一个 PNG 发送
（o=s,f=4,c,r,z=1,a=1）。会先预留行，因此图像保持光标所在的列，后续文本从图像下方的第 1 列
继续，与其他协议一致。指定 Name 时改为按名称上传并渲染，这样还可以裁剪。
Render、Release、QueryLimits 和 Command 生成其他操作。

终端必须在 DA1 回复中列出代码 90；第一次符合条件的执行会探测这一点，Force 会跳过检查。
Contour 在配置中设置 images: good_image_protocol: true 后支持该协议。
Windows PowerShell 5.1 使用 System.Drawing，PowerShell 7 使用 ImageSharp；两者发送相同的序列。

## EXAMPLES

### 示例 1：显示指定大小的图像

```powershell
ConvertTo-GoodImage ./assets/cog.png -Width 20
```

以 20 列宽显示图像，并将光标留在图像下方。

### 示例 2：上传一次，以多种方式渲染

```powershell
ConvertTo-GoodImage ./assets/cog.png -Name cog -Action Upload
ConvertTo-GoodImage -Name cog -Render -Width 8 -Height 4
ConvertTo-GoodImage -Name cog -Render -Width 16 -Height 4 -Resize StretchToFill
ConvertTo-GoodImage -Name cog -Release
```

以某个名称上传图像，分别以适配和拉伸方式渲染，然后释放该名称。

### 示例 3：裁剪、居中并绘制在文本下方

```powershell
ConvertTo-GoodImage ./assets/cog.png -Name cog -Crop 130,130,210,210 -Width 8 -Height 4 -Alignment MiddleCenter -Layer Below
```

在这些单元格中已有的文本下方显示图像的中间部分。

### 示例 4：由终端缩放原始 PNG

```powershell
ConvertTo-GoodImage ./photo.png -Width 40 -Passthrough
```

原样发送 PNG；由终端将其适配到 40 列。

## PARAMETERS
### -Path

本地图像路径。FileInfo.FullName 可从管道绑定。

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

可读取的图像流。可定位的流会被重置到开头；调用方拥有的流不会被释放。

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

HTTP 或 HTTPS 图像 URL。

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

下载超时时间。默认值：15 秒。

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

Base64 编码的图像文件，可带 data URI 前缀。

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

图像名称：1 到 512 个 A-Z、a-z、0-9 和 _ 字符。与图像来源一起使用时，图像以此名称上传（o=u），并在 Display 时按名称渲染（o=r），而不是作为一次性操作（o=s）发送。Render 和 Release 需要此参数。名称由调用方管理；再次上传同一名称会替换原图像。

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

在光标处渲染之前以 Name 上传的图像（o=r）。由于 cmdlet 不知道图像尺寸，只有在指定 Height 时才会预留行。

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

释放以 Name 存储的图像（o=d）。已经显示该图像的单元格会保留它。

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

返回限制查询（o=q）。终端以 DCS ! g s=8,m=<images>,b=<bytes>,w=<width>,h=<height> ST 应答，调用方必须读取该回复；GoodImageSession.QueryLimits() 会同时完成两者。不需要 DA1 声明。

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

一个 DCS ! g 序列的原始头部，例如 'o=r','n=cog','c=8','r=4'。布尔头部是不带值的名称，例如 'u' 或 's'。除封装外不做任何校验。

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

原始 Command 的主体，以 Base64 编码后放在 ;! 之后发送。最大 16 MB。

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

Display（默认）显示图像：一次性操作（o=s），或在指定 Name 时按名称上传并渲染。Upload 只以 Name 存储图像（o=u），供之后的 -Render 调用使用，不移动光标。

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

主体的像素格式：Png（f=4，默认）、Rgb（f=2）或 Rgba（f=3）。原始格式会附加像素宽度和高度（w、h），且数据更大。

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

图像占用的列数（c）。使用默认的 ResizeToFit 时，Width 和 Height 限定图像范围，由宽高比决定另一个轴；其他缩放策略按给定值使用。0 表示该轴由宽高比决定，与 Render 一起使用时由终端决定。

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

图像占用的行数（r）。放置的图像不会高于页面，因为 Contour 会把 r 限制为页面高度。

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

计算尺寸时使用的单元格像素宽度，覆盖探测值或后备值。

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

计算尺寸时使用的单元格像素高度，覆盖探测值或后备值。

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

图像在其单元格内的位置（a）：TopStart、TopCenter、TopEnd、MiddleStart、MiddleCenter、MiddleEnd、BottomStart、BottomCenter 或 BottomEnd。

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

图像缩放到其单元格中的方式（z）：NoResize、ResizeToFit（默认）、ResizeToFill 或 StretchToFill。ResizeToFit 在本地完成并填充到完整的单元格，因此不会出现空隙。其他策略会以图像自身尺寸发送，由终端缩放。

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

Below（L=0）将图像绘制在其单元格中的文本下方，Replace（默认）取代文本，Above（L=2）绘制在文本上方。

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

以像素表示的源矩形 x、y、宽度、高度（x、y、w、h）。只有按名称渲染才能裁剪，因此裁剪需要 Name。宽度或高度为 0 表示延伸到图像边缘。单元格按裁剪后的尺寸计算。

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

以来源自身的尺寸发送：单帧 PNG 逐字节原样发送，其他格式重新编码但不缩放。由终端将其缩放到单元格中，并用背景色填充图像未覆盖的部分。

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

要求终端将光标移到图像下方（u），而不是由 cmdlet 预留行。Contour 保持图像所在的列；规范规定为第 1 列。

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

当图像超出页面底部时，允许终端滚动页面（l）。与页面等高的放置图像会自动获得 u 和 l。

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

返回不预留行、不移动光标的原始序列，光标停留在图像左上角的单元格。

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

为每个操作请求状态回复（s）：成功时为 DCS ! g s=0 ST，名称未知为 1，数据无效为 2，超出限制为 3。回复以终端输入的形式到达，调用方必须读取；GoodImageSession 会处理这些回复。

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

追加到最后一个操作的额外原始头部。后出现的头部会覆盖先前的头部，因此可以替换任何计算出的值。

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

跳过 DA1 代码 90 检查和自动终端探测，并使用缓存或后备的单元格尺寸。

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


此 cmdlet 支持以下通用参数：Debug、ErrorAction、ErrorVariable、InformationAction、InformationVariable、OutBuffer、OutVariable、PipelineVariable、Verbose、WarningAction 和 WarningVariable。

## INPUTS

### System.IO.FileInfo

FullName 绑定到 Path。

### System.IO.Stream

绑定到 Stream。

## OUTPUTS

### System.String

Good Image Protocol 转义序列。图像输出还带有 Width、Height（单元格）、PixelWidth、PixelHeight 和 Name 注释属性。

## NOTES

alpha 规范的预览；传输格式可能会变化。自动协议选择从不选用它。
当应用程序拥有终端输入并需要状态回复时，请使用 [Sixel.Protocols.GoodImageSession]：
它负责上传、渲染、查询限制，并在释放时释放其名称。
[Sixel.Terminal.Compatibility]::TerminalSupportsGoodImageProtocol() 报告 DA1 代码 90 的结果。

## RELATED LINKS

[Good Image Protocol 规范](https://github.com/contour-terminal/terminal-good-image-protocol)

[ConvertTo-Sixel](ConvertTo-Sixel.md)
