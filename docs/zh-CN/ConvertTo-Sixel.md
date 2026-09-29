---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/zh-CN/ConvertTo-Sixel.md
schema: 2.0.0
---

# ConvertTo-Sixel

## SYNOPSIS

将图像转换为 Sixel、Kitty、InlineImage 或 Blocks。
用于在控制台中显示

## SYNTAX

### Path (Default)

```powershell
ConvertTo-Sixel [-Path] <String> [-MaxColors <int>] [-Width <int>] [-Height <int>] [-Force] [<CommonParameters>]
```

### Url

```powershell
ConvertTo-Sixel -Url <Uri> [-MaxColors <int>] [-Width <int>] [-Height <int>] [-Force] [-Timeout <TimeSpan>] [<CommonParameters>]
```

### Stream

```powershell
ConvertTo-Sixel -Stream <Stream> [-MaxColors <int>] [-Width <int>] [-Height <int>] [-Force] [<CommonParameters>]
```

### InputObject

```powershell
ConvertTo-Sixel -InputObject <String> [-MaxColors <int>] [-Width <int>] [-Height <int>] [-Force] [<CommonParameters>]
```

## DESCRIPTION

`ConvertTo-Sixel` 将图像转换为可在控制台中显示的形式。

## EXAMPLES

### -------------------------- 示例 1 --------------------------

```powershell
PS C:\> ConvertTo-Sixel -Url 'https://imgs.xkcd.com/comics/git_commit.png'
```

转换 xkcd 图像。

### -------------------------- 示例 2 --------------------------

```powershell
PS C:\> ConvertTo-Sixel -Path C:\files\smiley.png
```

转换本地文件。

## PARAMETERS

### -Path

本地图像的路径。

```yaml
Type: String
Parameter Sets: Path
Aliases: FullName

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Url

图像的 URL。

```yaml
Type: Uri
Parameter Sets: Url
Aliases: Uri

Required: True
Position: Named
Default value: None
Accept pipeline input: True
Accept wildcard characters: False
```

### -Stream

图像的流。

```yaml
Type: Stream
Parameter Sets: Stream
Aliases: RawContentStream, FileStream, InputStream, ContentStream

Required: True
Position: Named
Default value: None
Accept pipeline input: True
Accept wildcard characters: False
```

### -InputObject

来自管道的 InputObject，可以是文件路径或图像的 base64 编码字符串。

```yaml
Type: String
Parameter Sets: InputObject
Aliases: 

Required: True
Position: Named
Default value: None
Accept pipeline input: True
Accept wildcard characters: False
```

### -MaxColors

在 sixel 图像中使用的最大颜色数。
最大为 256 色。
（仅适用于 sixel 协议）

```yaml
Type: int
Parameter Sets: (All)
Aliases: None

Required: False
Position: Named
Default value: 256
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width

以字符单元为单位的图像宽度，高度将按比例缩放以保持宽高比。

如果同时指定了 Width 和 Height，则会限制到其中较小的值。

```yaml
Type: int
Parameter Sets: (All)
Aliases: CellWidth

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height

以字符单元为单位的图像高度，宽度将按比例缩放以保持宽高比。
如果同时指定了 Width 和 Height，则会限制到其中较小的值。

```yaml
Type: int
Parameter Sets: (All)
Aliases: CellWidth

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force

即使终端不支持 sixel，也强制命令尝试输出 sixel 数据。

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

### -Protocol

选择要输出的图像协议。
支持 Sixel、InlineImageProtocol、KittyGraphicsProtocol、Block

它会尝试为你的终端自动选择支持的图像协议。

```yaml
Type: ImageProtocol
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: ImageProtocol.Auto
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout

Web 请求的超时时间

```yaml
Type: TimeSpan
Parameter Sets: Url
Aliases:

Required: False
Position: Named
Default value: 15
Accept pipeline input: False
Accept wildcard characters: False
```

## INPUTS

### System.String

图像文件的路径、URL、Base64 字符串、流

## OUTPUTS

### System.String

一个 sixel 字符串

## NOTES

仅当你的终端支持 sixel 图像时，此命令才有效。

## RELATED LINKS
