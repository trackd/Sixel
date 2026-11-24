---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/zh-CN/ConvertTo-Kitty.md
schema: 2.0.0
---

# ConvertTo-Kitty

## SYNOPSIS

将图像和控制操作转换为 Kitty 图形转义序列（预览）。

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

Windows PowerShell 5.1 使用 System.Drawing 处理 BMP、GIF、JPEG、PNG 和 TIFF 输入，
包括 GIF 动画。PowerShell 7 保留 ImageSharp 及其额外的编解码器。
Kitty 传输控制、原始 RGB/RGBA 输出和 PNG 输出在两种引擎上均可用。

一个独立的 Kitty 图形协议预览编码器。ConvertTo-Sixel 及其现有的 Kitty 路径保持不变。
以字符串形式返回图像/控制序列。第一次符合条件的 cmdlet 执行会探测终端功能和尺寸；
使用 Force 可阻止这次终端 I/O。导入、发现、补全和被动读取函数不会探测。
接收端终端决定它实现了哪些协议功能。

功能检查使用 [Sixel.Terminal.Compatibility]::GetKittyCapabilities()。探测还会发送功能查询
（a=q,i=<id>,e=<action>，与 Contour 的实现一致），涵盖放置（裁剪 x/y/w/h、Unicode 占位符 U、
相对放置 P/Q/H/V）、传输、动画（a 和 f）以及帧合成（c）。当终端以 e=<action> 应答时，
它回显的键决定 SourceRectangle、UnicodePlaceholders、RelativePlacements、Animation 和
FrameComposition，并覆盖终端/版本表；Evidence 列出所用的回复。忽略 e 或应答中不含 e= 的终端
沿用表中的值。未知功能需要 Force，但源矩形、图像编号和文件传输仅在已知不支持时才会被拒绝。

解码后的图像在像素空间中缩放，保持宽高比，且不添加透明的单元格填充。
原生放置使用精确的像素尺寸，避免把不足一个单元格的部分取整为更大的光栅。
输出属性 Width/Height 按所选单元格尺寸描述占用的单元格；PixelWidth/PixelHeight 描述实际光栅。
显式的高级放置控制可以独立于这些光栅尺寸改变显示范围。

使用 Command 进行显示、删除、动画控制、合成、查询或原始传输。
区分大小写的字符串条目可在 PowerShell 中保留 i/I、x/X 和 s/S 等成对的键。
使用下面的示例比较图像传输、放置和动画。

## EXAMPLES

### 示例 1：显示指定大小的图像

```powershell
ConvertTo-Kitty ./assets/cog.png -Width 20 -CellWidth 10 -CellHeight 20
```

以 20 列宽显示图像。

### 示例 2：上传一次，放置两次

```powershell
ConvertTo-Kitty ./assets/cog.png -Action Transmit -ImageId 42
ConvertTo-Kitty -Command 'a=p','i=42','p=1','c=10'
ConvertTo-Kitty -Command 'a=p','i=42','p=2','c=20'
```

为同一个已上传的图像创建两个独立的放置。

### 示例 3：原生 GIF 动画

```powershell
ConvertTo-Kitty ./animation.gif -ImageId 42 -Width 30 -Animate
ConvertTo-Kitty -Command 'a=a','i=42','s=1'
ConvertTo-Kitty -Command 'a=d','d=I','i=42'
```

启动动画、停止动画，然后删除其放置和图像数据。

### 示例 4：通过 tmux 使用 Unicode 占位符

```powershell
ConvertTo-Kitty ./assets/cog.png -ImageId 42 -Width 20 -UnicodePlaceholder -Tmux
```

需要终端支持占位符，并配置 tmux 透传。

## PARAMETERS

### -Force

跳过终端功能兼容性检查和自动终端探测。传输格式校验仍然有效。不使用 Force 时，终端的功能查询回复或版本标记为 Unsupported（对大多数扩展而言还包括 Unknown）的功能会被拒绝。在无法获得测量值时使用缓存/环境信息和后备单元格尺寸。之后不带 Force 的调用仍可执行本会话的第一次探测。

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

### -Command

完整的、区分大小写的传输控制，例如 'a=p','i=42','c=20'。编码器负责分块封装，并默认使用 q=2（a=q 时为 q=0）。Contour 功能查询（带 e=<action> 的 a=q，例如 'a=q','i=1','e=p','c=1','z=1'）以占位值列出键，不携带负载，并跳过值检查。可与 Payload、Tmux 和 Force 一起使用。

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

直接原始命令的未压缩图像字节，或引用命令的 UTF-8 文件名/共享内存名称。如果直接传输指定了 o=z，编码器会压缩这些字节。

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

终端侧的文件名或共享内存名称。不会创建、读取、检查或删除本地文件。临时文件和共享内存传输允许终端删除所引用的资源。

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

引用传输介质：File（默认）、TemporaryFile 或 SharedMemory。

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

图像/引用操作：TransmitAndDisplay（默认）、Transmit、Query 或 Frame。其他操作请使用 Command。Query 需要图像 ID，并默认使用 Quiet 0，以便终端回复。

```yaml
Type: String
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: TransmitAndDisplay
Accept pipeline input: False
Accept wildcard characters: False
```

### -Format

Png、Rgb（24 位）或 Rgba（32 位）。RGB 丢弃 alpha；RGBA 为每个像素携带红、绿、蓝和透明度字节。省略时，解码后的图像使用 PNG。引用数据必须已是该格式，默认为 PNG。

```yaml
Type: String
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: Png
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width

解码图像的最大光栅宽度（单元格）。与 Height 组合时，在保持宽高比的前提下同时满足两个限制。对于引用，设置放置列数 c，不读取图像数据。

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height

解码图像的最大光栅高度（单元格）。对于引用，设置放置行数 r。

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -CellWidth

计算尺寸时使用的单元格像素宽度。默认使用缓存的被动终端尺寸或模块后备值。不会查询终端。

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

计算尺寸时使用的单元格像素高度。默认使用缓存的被动终端尺寸或模块后备值。

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

### -ImageId

显式的图像标识符。Animate 和 UnicodePlaceholder 需要它。与 ImageNumber 互斥。

```yaml
Type: UInt32
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImageNumber

客户端图像编号 I。终端会分配自己的 ID。与 ImageId 互斥。

```yaml
Type: UInt32
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -PlacementId

放置标识符。占位符文本仅支持低 24 位范围。

```yaml
Type: UInt32
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 0
Accept pipeline input: False
Accept wildcard characters: False
```

### -Quiet

2 抑制对编码后图像命令的回复（默认值，Query 除外，其默认值为 0）；1 允许错误回复；0 允许所有回复。转换器会读取它自己的延迟探测回复，但不会读取对返回的图像/控制序列的回复。只有在你的应用程序拥有终端输入时才请求这些回复。

```yaml
Type: Int32
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: 2
Accept pipeline input: False
Accept wildcard characters: False
```

### -Compress

在直接传输前应用 RFC 1950 zlib 压缩。引用则改用 Control 'o=z' 传递已压缩的数据。

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Animate

将合并后的动画帧转换为原生 Kitty 动画，保留帧延迟和循环次数。两种运行时都支持 GIF，PowerShell 7 还支持动画 PNG/WebP。需要 ImageId。其他来源可通过 Frame/Command 提交。

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UnicodePlaceholder

创建虚拟放置并追加其 Unicode 文本网格。需要 ImageId。结束于网格下方的第一列，并重置前景色/下划线颜色。最多 297 行和 297 列。

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoCursorMove

为放置设置 C=1。不能与 UnicodePlaceholder 组合使用。否则普通放置遵循终端的光标移动规则。

```yaml
Type: SwitchParameter
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tmux

将每个图形数据包包装在 tmux 透传中。占位符文本保留在包装之外。tmux 会话必须允许透传。

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

每个直接传输数据包中 base64 字符的最大数量。必须是 4 到 4096 之间的 4 的倍数。较小的值可适应传输限制；引用名称始终是单个数据包。这不会启用 GNU Screen 透传。

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

图像/引用命令的附加控制，区分大小写的 key=value。重复、格式错误以及对该操作无效的控制会被拒绝。可使用协议中描述的裁剪、放置、帧、相对放置和临时键。

```yaml
Type: String[]
Parameter Sets: Path, Stream, Url, Base64, Reference
Aliases:
Required: False
Position: Named
Default value: None
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

Kitty 转义序列。解码图像的输出还带有 Width、Height、PixelWidth、PixelHeight 和 ImageId 注释属性。

## NOTES

预览 API。导入和补全是被动的。第一次符合条件的 ConsoleHost 执行会探测一次；待处理的输入、重定向、已知的输入读取调用栈或 Force 会跳过探测。显式调用 RefreshTerminalInfo 可以刷新缓存。异常延迟的回复和并发的输入读取器仍然是已知限制。
非零图像 ID 由调用方管理；保留多个图像时请使用不同的 ID。
KittyTransfer 提供可释放的本地资源创建。KittyResponse 和 KittySession 支持确认解析和调用方拥有的交换；请参阅预览指南。输入路由和实时终端状态仍由应用程序负责。

## RELATED LINKS

[Kitty 图形协议](https://sw.kovidgoyal.net/kitty/graphics-protocol/)

[预览指南](../kitty-preview.md)