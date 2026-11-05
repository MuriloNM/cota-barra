# Abre um protótipo sem janela de console. Uso: abrir.ps1 pilula | bandeja
# A pílula roda o .exe compilado; se ele não existir ou for barrado, cai para a versão em PowerShell.
param([ValidateSet('pilula', 'bandeja')][string]$Nome = 'pilula')
$pidArq = Join-Path $env:LOCALAPPDATA "cota-barra\$Nome.pid"
if ((Test-Path $pidArq) -and (Get-Process -Id ([int](Get-Content $pidArq)) -ErrorAction SilentlyContinue)) { Write-Host "$Nome já está aberta."; exit 0 }
Remove-Item $pidArq -ErrorAction SilentlyContinue

function Wait-Pid {
    for ($i = 0; $i -lt 40; $i++) {
        if (Test-Path $pidArq) { return $true }
        Start-Sleep -Milliseconds 250
    }
    $false
}

$exe = Join-Path $PSScriptRoot "bin\$Nome.exe"
if (Test-Path $exe) {
    # Aberta por um assistente, herdaria o job do Claude e morreria com ele. Criada pelo WMI, nasce fora dele.
    $criado = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = "`"$exe`"" } -ErrorAction SilentlyContinue
    if ($criado -and $criado.ReturnValue -eq 0 -and (Wait-Pid)) { Write-Host "$Nome aberta, .exe (PID $(Get-Content $pidArq))."; exit 0 }
    try {
        Start-Process $exe -ErrorAction Stop
        if (Wait-Pid) { Write-Host "$Nome aberta, .exe (PID $(Get-Content $pidArq))."; exit 0 }
    } catch { Write-Host "O .exe foi barrado: $_" }
}
$alvo = Join-Path $PSScriptRoot "$Nome.ps1"
Start-Process powershell.exe -WindowStyle Hidden -ArgumentList "-NoProfile -ExecutionPolicy Bypass -STA -File `"$alvo`""
if (Wait-Pid) { Write-Host "$Nome aberta, PowerShell (PID $(Get-Content $pidArq))."; exit 0 }
Write-Host "$Nome não confirmou a abertura. Veja $env:LOCALAPPDATA\cota-barra\$Nome.log"
exit 1
