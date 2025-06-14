---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/en-US/ConvertTo-SixelGif.md
schema: 2.0.0
---

# ConvertTo-SixelGif

## SYNOPSIS

Converts a gif to a sixel animation.  

this cmdlet only supports Sixel.  

## SYNTAX

### Path (Default)

```powershell
ConvertTo-SixelGif [-Path] <string> [-MaxColors <int>] [-Width <int>] [-Force] [-LoopCount <int>] [<CommonParameters>]
```

### Url

```powershell
ConvertTo-SixelGif -Url <uri> [-MaxColors <int>] [-Width <int>] [-Force] [-LoopCount <int>] [<CommonParameters>]
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

The `ConvertTo-SixelGif` takes a gif and converts it to sixel animation  

## EXAMPLES

### -------------------------- EXAMPLE 1 --------------------------

```powershell
PS C:\> ConvertTo-SixelGif -Url 'https://i.gifer.com/10j2.gif'
```

### -------------------------- EXAMPLE 2 --------------------------

```powershell
PS C:\> ConvertTo-SixelGif -Path $env:USERPROFILE\desktop\hello.gif
```

Converts a local file to sixel format  

## PARAMETERS

### -Path

A path to a local gif to convert to sixel.  

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

A URL of the gif to download and convert to sixel.  

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

### -LoopCount

The amount of times the gif will loop.  

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

The maximum number of colors to use in the image.  
Max is 256.  

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

## INPUTS

### System.String

Path or url to an gif file  

## OUTPUTS

### System.String

a sixel animation  

## NOTES

This will only work if your terminal supports sixel images.  
Windows Terminal version 1.22 or newer  

## RELATED LINKS
