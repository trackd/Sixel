﻿# Sixel

A module that lets you convert images to sixel.  

Supports converting bmp, gif, jpeg, pbm, png, tiff, tga, webp to sixel.  

This code was originally meant to be added to [PwshSpectreConsole](https://github.com/ShaunLawrie/PwshSpectreConsole)  
But we wanted to get something out quickly for people who wanted to test sixel.  

It will probably be added as an experimental feature there as well soon.  

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
