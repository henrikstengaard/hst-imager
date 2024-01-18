:: https://imagemagick.org/script/download.php#windows

set path=%PATH%;c:\Program Files\ImageMagick-7.1.1-Q16-HDRI
magick convert hst-imager.png -define icon:auto-resize=16,32,48,64,96,128,256 -compress zip hst-imager.ico
pause