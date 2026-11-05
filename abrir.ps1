# Abre um protótipo sem janela de console. Uso: abrir.ps1 pilula | bandeja
param([ValidateSet('pilula', 'bandeja')][string]$Nome = 'pilula')
$pidArq = Join-Path $env:LOCALAPPDATA "cota-barra\$Nome.pid"
if ((Test-Path $pidArq) -and (Get-Process -Id ([int](Get-Content $pidArq)) -ErrorAction SilentlyContinue)) { Write-Host "$Nome já está aberta."; exit 0 }
Remove-Item $pidArq -ErrorAction SilentlyContinue
$alvo = Join-Path $PSScriptRoot "$Nome.ps1"
$args2 = "-NoProfile -ExecutionPolicy Bypass -STA -File `"$alvo`""
Start-Process powershell.exe -WindowStyle Hidden -ArgumentList $args2
for ($i = 0; $i -lt 40; $i++) {
    if (Test-Path $pidArq) { Write-Host "$Nome aberta (PID $(Get-Content $pidArq))."; exit 0 }
    Start-Sleep -Milliseconds 250
}
Write-Host "$Nome não confirmou a abertura. Veja $env:LOCALAPPDATA\cota-barra\$Nome.log"
exit 1
