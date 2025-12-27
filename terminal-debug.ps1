# testfile copied to /assets folder
# assets/nord-apple.jpg
# 1280 x 720 pixels 18.8 KB 25 dpi 24 bit

## Windows
## [Sixel.Terminal.Compatibility]::GetTerminalInfo()
<#

>>> Windows Terminal
$f = cts .\dotfiles\apps\images\nord-apple.jpg -Protocol Sixel
 $f.Width
128
 $f.Height
36
 -join $f[0..100] | escapeansi
␛P0;1q"1;1;1280;720#0;2;0;0;0#1;2;18;20;25#2;2;17;20;24#3;2;18;21;25#4;2;18;21;28#5;2;19;21;28#6;2;19
 $host.UI.RawUI.WindowSize

Width Height
----- ------
  170     58

 [Sixel.Terminal.Compatibility]::GetCellSize()

PixelWidth PixelHeight
---------- -----------
        10          20

>>> WezTerm
$f = cts .\dotfiles\apps\images\nord-apple.jpg -Protocol Sixel
 $f.Width
128
 $f.Height
33
 -join $f[0..100] | escapeansi
␛P0;1q"1;1;1280;726#0;2;0;0;0#1;2;18;20;25#2;2;17;20;24#3;2;18;21;25#4;2;18;21;28#5;2;19;21;28#6;2;19
$host.UI.RawUI.WindowSize

Width Height
----- ------
  169     60

 [Sixel.Terminal.Compatibility]::GetCellSize()


PixelWidth PixelHeight
---------- -----------
        10          22


#>



#### Mac OSX
<#
ghostty
[Sixel.Terminal.Compatibility]::GetCellSize()

PixelWidth PixelHeight
---------- -----------
        15          32


wezterm
[Sixel.Terminal.Compatibility]::GetCellSize()

PixelWidth PixelHeight
---------- -----------
         6           4

kitty
[Sixel.Terminal.Compatibility]::GetCellSize()

PixelWidth PixelHeight
---------- -----------
        10          20

#>


<#
wezterm
 $f = cts ./Code/dotfiles/apps/images/nord-apple.jpg -Protocol Sixel
 -join $f[0..100] | escapeansi
␛P0;1q"1;1;1284;720#0;2;0;0;0#1;2;18;20;25#2;2;0;0;0#3;2;17;20;24#4;2;18;21;25#5;2;18;21;28#6;2;18;21
 $f.Width
214
 $f.Height
180

   $host.UI.RawUI.WindowSize

Width Height
----- ------
  130     40

 $f = cts ./Code/dotfiles/apps/images/nord-apple.jpg -Protocol Sixel
 $f.Height
180
 $f.Width
214

#>
