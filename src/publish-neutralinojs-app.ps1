# kill any running hst imager guiapp processes
Get-Process | Where-Object { $_.ProcessName -match 'Hst.Imager.GuiApp' } | Foreach-Object { Stop-Process -Id $_.Id }

# run neutralinojs
Push-Location 'build/neutralinojs'
neu.cmd build
Pop-Location