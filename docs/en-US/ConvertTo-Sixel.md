---
external help file: Sixel.dll-Help.xml
Module Name: Sixel
online version: https://github.com/trackd/Sixel/blob/main/docs/en-US/ConvertTo-Sixel.md
schema: 2.0.0
---

# ConvertTo-Sixel

## SYNOPSIS

Converts an image to sixel  

## SYNTAX

### ByPath (Default)

```powershell
ConvertTo-Sixel [-Path] <string> [-MaxColors <int>] [-Width <int>] [-Force] [<CommonParameters>]
```

### ByUrl

```powershell
ConvertTo-Sixel -Url <string> [-MaxColors <int>] [-Width <int>] [-Force] [<CommonParameters>]
```

## DESCRIPTION

The `ConvertTo-Sixel` takes an image and converts it to sixel  

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

The filepath to an image  

```yaml
Type: String
Parameter Sets: (Path)
Aliases: FullName

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName)
Accept wildcard characters: False
```

### -Url

the url to an image.  
it will download the image to a temporary file and convert it and dispose of it afterwards.  

```yaml
Type: String
Parameter Sets: (Url)
Aliases: Uri

Required: True
Position: Named
Default value: None
Accept pipeline input: True
Accept wildcard characters: False
```

### -MaxColors

Sets the amount of colors to use.  

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

Sets the width of the image, it will resize to fit while keeping the aspect ratio.  

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

Overrides the sixel support detection and output a sixel even if your terminal might not support it.  
Note: this has no effect on the capabilities of the terminal, it will just force sixel output.  

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

Path or url to an image file  

## OUTPUTS

### System.String

a sixel string  

## NOTES

This will only work if your terminal supports sixel images.  
Windows Terminal Preview version 1.22.2702.0 or newer  

## RELATED LINKS
