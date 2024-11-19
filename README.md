# Sixel

A module that lets you convert images to Sixel, Inline Images Protocol, Kitty Graphics Protocol.

Supports converting bmp, gif, jpeg, pbm, png, tiff, tga, webp to sixel.  

This code was originally meant to be added to [PwshSpectreConsole](https://github.com/ShaunLawrie/PwshSpectreConsole)  
But we wanted to get something out quickly for people who wanted to test sixel.  

It uses an Assembly load context for the Sixlabors library, from [PowerShell-ALC](https://github.com/jborean93/PowerShell-ALC)  

## Install

```powershell
Install-Module Sixel
# or
Install-PSResource Sixel
```

## Requirements

This module requires Powershell version 7.4+  
We test against the latest Windows Terminal Preview.  

Sixel support has not been added to Windows Terminal Stable branch yet.  

There is an example sixel file in the ./assets folder that can be used for testing  

```powershell
# test sixel
Invoke-RestMethod https://raw.githubusercontent.com/trackd/Sixel/refs/heads/main/assets/chibi.six
```

## Authors

**[@trackd](https://github.com/trackd)**  
**[@ShaunLawrie](https://github.com/ShaunLawrie)**  

## Todo

1. Better docs/help
2. Tests
3. Animated sixels

## libraries

[Sixlabors.Imagesharp](https://github.com/SixLabors/ImageSharp)  

## examples

![Example](./assets/combo_example.png)  
![Example](./assets/cog_xkcd.png)  
