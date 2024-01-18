# kill any running hst imager guiapp processes
Get-Process | Where-Object { $_.ProcessName -match 'Hst.Imager' } | Foreach-Object { Stop-Process -Id $_.Id }

# run neutralinojs
Push-Location 'Hst.Imager.GuiApp/build/neutralinojs'

# neu.cmd can't be used to run as it adds '--neu-dev-auto-reload' arg, which causes reloads of the application
#neu.cmd run -- --window-enable-inspector --disable-auto-reload

bin/neutralino-win_x64.exe  --load-dir-res --path=. --export-auth-info --neu-dev-extension --window-enable-inspector --disable-auto-reload

Pop-Location