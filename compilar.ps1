# Compila a pílula com o compilador que já vem no Windows (.NET Framework 4). Não instala nada.
$fw = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
$wpf = Join-Path $fw 'WPF'
$src = Join-Path $PSScriptRoot 'src'
$bin = Join-Path $PSScriptRoot 'bin'
[void][IO.Directory]::CreateDirectory($bin)
$comum = @('/nologo', '/optimize+', '/platform:x64', '/codepage:65001', '/utf8output', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll')

& $csc @comum '/target:winexe' "/out:$bin\pilula.novo.exe" "/lib:$wpf" '/r:System.Windows.Forms.dll' '/r:UIAutomationClient.dll' '/r:UIAutomationTypes.dll' '/r:WindowsBase.dll' "$src\Nucleo.cs" "$src\Pilula.cs"
if ($LASTEXITCODE) { Write-Host 'Falhou: pilula.exe'; exit 1 }
# O antivírus pode segurar o .exe por um tempo depois de ele fechar: compila ao lado e troca quando liberar.
$trocou = $false
for ($i = 0; $i -lt 60 -and -not $trocou; $i++) {
    try { Move-Item "$bin\pilula.novo.exe" "$bin\pilula.exe" -Force -ErrorAction Stop; $trocou = $true } catch { Start-Sleep -Seconds 1 }
}
if (-not $trocou) { Write-Host 'Compilou em bin\pilula.novo.exe, mas bin\pilula.exe está em uso. Feche a pílula e rode de novo.'; exit 1 }
& $csc @comum '/target:exe' "/out:$bin\conferir.exe" "$src\Nucleo.cs" "$src\Conferir.cs"
if ($LASTEXITCODE) { Write-Host 'Falhou: conferir.exe'; exit 1 }
Write-Host "Compilado em $bin"
