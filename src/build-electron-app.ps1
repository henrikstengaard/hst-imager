# kill any running hst imager guiapp processes
Get-Process | Where-Object { $_.ProcessName -match 'Hst.Imager' } | Foreach-Object { Stop-Process -Id $_.Id }

# update neutralinojs
Push-Location 'Hst.Imager.GuiApp'
dotnet electronize build /target win
Pop-Location