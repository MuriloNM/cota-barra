# Fecha um protótipo com calma (para o ícone não ficar fantasma na bandeja). Uso: fechar.ps1 pilula | bandeja
param([ValidateSet('pilula', 'bandeja')][string]$Nome = 'pilula')
$pidArq = Join-Path $env:LOCALAPPDATA "cota-barra\$Nome.pid"
$alvo = if (Test-Path $pidArq) { Get-Process -Id ([int](Get-Content $pidArq)) -ErrorAction SilentlyContinue } else { $null }
if (-not $alvo) { Write-Host "$Nome não estava aberta."; Remove-Item $pidArq -ErrorAction SilentlyContinue; exit 0 }
$sinal = $null
if ([Threading.EventWaitHandle]::TryOpenExisting("Local\CotaBarra.$Nome.fechar", [ref]$sinal)) { [void]$sinal.Set(); $sinal.Dispose() }
if (-not $alvo.WaitForExit(9000)) { Stop-Process -Id $alvo.Id -Force; Write-Host "$Nome fechada à força." } else { Write-Host "$Nome fechada." }
Remove-Item $pidArq -ErrorAction SilentlyContinue
