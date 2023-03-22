magick convert -resize x16 -gravity center -crop 16x16+0+0 -flatten -colors 256 hst-imager.png hst-imager-16x16.ico
magick convert -resize x32 -gravity center -crop 32x32+0+0 -flatten -colors 256 hst-imager.png hst-imager-32x32.ico
magick convert hst-imager-16x16.ico hst-imager-32x32.ico hst-imager-favicon.ico
pause