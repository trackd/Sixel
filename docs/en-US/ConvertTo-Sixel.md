---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/en-US/ConvertTo-Sixel.md
schema: 2.0.0
---

# ConvertTo-Sixel

## SYNOPSIS

Converts an image to Sixel, Kitty, InlineImage or Blocks.  
To display in the console  

## SYNTAX

### Path (Default)

```powershell
ConvertTo-Sixel [-Path] <String> [-MaxColors <int>] [-Width <int>] [-Height <int>] [-Force] [<CommonParameters>]
```

### Url

```powershell
ConvertTo-Sixel -Url <Uri> [-MaxColors <int>] [-Width <int>] [-Height <int>] [-Force] [<CommonParameters>]
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

The `ConvertTo-Sixel` converts an image to display in the console.  

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
PS C:\> ConvertTo-Sixel -Url 'https://imgs.xkcd.com/comics/git_commit.png'
```

Converts the xkcd to sixel  

### -------------------------- EXAMPLE 2 --------------------------

```powershell
PS C:\> ConvertTo-Sixel -Path C:\files\smiley.png
```

Converts a local file to sixel format  

## PARAMETERS

### -Path

A path to a local image to convert to sixel.  

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

A URL of the image to download and convert to sixel.  

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

A stream of an image.  

```yaml
Type: Stream
Parameter Sets: Stream
Aliases: FileStream, InputStream, ImageStream, ContentStream

Required: True
Position: Named
Default value: None
Accept pipeline input: True
Accept wildcard characters: False
```

### -InputObject

InputObject from Pipeline, can be filepath or base64 encoded image.  

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

The maximum number of colors to use in a sixel image.  
Max is 256 colors.  
(only applicable to sixel protocol)  

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

Width of the image in character cells, the height will be scaled to maintain aspect ratio.  

if both Width and Height are given then it will clamp to the smaller.  

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

Height of the image in character cells, the width will be scaled to maintain aspect ratio.  
if both Width and Height are given then it will clamp to the smaller.  

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

Force the command to attempt to output sixel data even if the terminal does not support sixel.  

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

Select the image protocol to output.  
Supports Sixel, InlineImageProtocol, KittyGraphicsProtocol, Block  

It will attempt to autoselect the supported image protocol for your terminal.  

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

## INPUTS

### System.String

Path, url, Stream of an image file  

## OUTPUTS

### System.String

A sixel string  

## NOTES

This will only work if your terminal supports sixel images.  

## RELATED LINKS
