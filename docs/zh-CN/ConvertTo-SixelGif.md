---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/zh-CN/ConvertTo-SixelGif.md
schema: 2.0.0
---

# ConvertTo-SixelGif

## SYNOPSIS

将 gif 转换为 sixel 动画。

此 cmdlet 仅支持 Sixel。

## SYNTAX

### Path (Default)

```powershell
ConvertTo-SixelGif [-Path] <string> [-MaxColors <int>] [-Width <int>] [-Force] [-LoopCount <int>] [<CommonParameters>]
```

### Url

```powershell
ConvertTo-SixelGif -Url <uri> [-MaxColors <int>] [-Width <int>] [-Force] [-LoopCount <int>] [-Timeout <TimeSpan>] [<CommonParameters>]
```

### Stream

```powershell
ConvertTo-SixelGif -Stream <stream> [-MaxColors <int>] [-Width <int>] [-Force] [-LoopCount <int>] [<CommonParameters>]
```

### InputObject

```powershell
ConvertTo-SixelGif -InputObject <string> [-MaxColors <int>] [-Width <int>] [-Force] [-LoopCount <int>] [<CommonParameters>]
```

## DESCRIPTION

`ConvertTo-SixelGif` 获取一个 gif 并将其转换为 sixel 动画

## EXAMPLES

### -------------------------- 示例 1 --------------------------

```powershell
PS C:\> ConvertTo-SixelGif -Url 'https://i.gifer.com/10j2.gif'
```

### -------------------------- 示例 2 --------------------------

```powershell
PS C:\> ConvertTo-SixelGif -Path $env:USERPROFILE\desktop\hello.gif
```

将本地文件转换为 sixel 格式

## PARAMETERS

### -Path

要转换为 sixel 的本地 gif 的路径。

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

要下载并转换为 sixel 的 gif 的 URL。

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

### -LoopCount

gif 循环播放的次数。

```yaml
Type: int
Parameter Sets: (All)
Aliases: None

Required: False
Position: Named
Default value: 3
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxColors

在图像中使用的最大颜色数。
最大为 256。

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

```yaml
Type: int
Parameter Sets: (All)
Aliases: 

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

gif 文件的路径或 URL

## OUTPUTS

### System.String

一个 sixel 动画

## NOTES

仅当你的终端支持 sixel 图像时，此命令才有效。
需要 Windows Terminal 1.22 或更高版本

## RELATED LINKS
