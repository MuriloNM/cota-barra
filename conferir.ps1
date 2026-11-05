# Confere fórmula, estados e posição. Com -Amostras, grava PNGs dos estados em -Pasta.
# Não grava no arquivo de cota; os casos de arquivo quebrado usam cópias temporárias.
param([switch]$Amostras, [string]$Pasta = (Join-Path $PSScriptRoot 'prints'))
. (Join-Path $PSScriptRoot 'nucleo.ps1')

$script:falhas = 0
function Confere($Nome, $Obtido, $Esperado) {
    $ok = "$Obtido" -eq "$Esperado"
    if (-not $ok) { $script:falhas++ }
    Write-Host ("[{0}] {1}: {2}{3}" -f $(if ($ok) { 'ok' } else { 'FALHOU' }), $Nome, $Obtido, $(if ($ok) { '' } else { " (esperado $Esperado)" }))
}

$agora = [DateTimeOffset]::Parse('2026-11-04T23:48:00Z')
function Leitura($usado, $resetEmHoras, $idadeMin) {
    [pscustomobject]@{ percentUsed = $usado; resetsAt = $agora.AddHours($resetEmHoras).ToString('o'); lido = $agora.AddMinutes(-$idadeMin).ToUnixTimeMilliseconds() }
}

Write-Host '--- Fórmula, com contas feitas à mão (84 h = meia semana)'
$c = Get-Conta (Leitura 50 84 1) 'x' $agora
Confere 'meia semana: esperado' $c.Esperado 50
Confere 'meia semana: projeção' $c.Projecao 100
Confere 'meia semana: amarelo' $c.Cor 'amarelo'
Confere 'diferença -5 exata é amarelo' (Get-Conta (Leitura 45 84 1) 'x' $agora).Cor 'amarelo'
Confere 'diferença +5 exata é amarelo' (Get-Conta (Leitura 55 84 1) 'x' $agora).Cor 'amarelo'
Confere 'diferença -5,1 é verde' (Get-Conta (Leitura 44.9 84 1) 'x' $agora).Cor 'verde'
Confere 'diferença +5,1 é vermelho' (Get-Conta (Leitura 55.1 84 1) 'x' $agora).Cor 'vermelho'
$ini = Get-Conta (Leitura 3 167 1) 'x' $agora   # decorrido 0,6% < 2%: sem projeção por divisão
Confere 'início da semana: projeção = usado' $ini.Projecao 3

Write-Host '--- Estados'
Confere 'até 2 h é ok' (Get-Conta (Leitura 10 84 120) 'x' $agora).Estado 'ok'
Confere 'acima de 2 h é velha' (Get-Conta (Leitura 10 84 121) 'x' $agora).Estado 'velha'
Confere 'reset vencido não vale' (Get-Conta (Leitura 10 -1 1) 'x' $agora).Estado 'vencida'
Confere 'sem chave' (Get-Conta $null 'x' $agora).Estado 'sem'

Write-Host '--- Arquivo (cópias temporárias)'
$tmp = Join-Path $env:TEMP "cota-barra-teste-$PID.json"
'{"ultima":{"percentUsed":99,"resetsAt":"2030-01-01T00:00:00Z","lido":1},"ultima:padrao":{"percentUsed":18,"resetsAt":"2026-11-09T19:00:00Z","lido":1793835996553}}' | Set-Content $tmp -Encoding UTF8
$lista = @(Get-Contas -Agora $agora -Caminho $tmp)
Confere 'ignora a chave antiga "ultima"' $lista[0].Usado 18
Confere 'Trabalho ausente vira "sem"' $lista[1].Estado 'sem'
'{ quebrado' | Set-Content $tmp -Encoding UTF8
[IO.File]::SetLastWriteTimeUtc($tmp, [datetime]::UtcNow.AddMinutes(1))
Confere 'JSON quebrado mantém o último bom' (@(Get-Contas -Agora $agora -Caminho $tmp))[0].Usado 18
Remove-Item $tmp

Write-Host '--- Posição (barra 0..3840, bandeja 3525, widgets até 237, largura 500, margem 12)'
$base = @{ BarraEsq = 0; BarraDir = 3840; BandejaEsq = 3525; WidgetsDir = 237; Largura = 500; Margem = 12 }
Confere 'normal: à esquerda da bandeja' (Select-Posicao @base -IconesEsq 1322 -IconesDir 2518 -DeslocManual $null).X 3013
Confere 'ícones crescem: passa para a esquerda' (Select-Posicao @base -IconesEsq 1000 -IconesDir 3100 -DeslocManual $null).X 249
Confere 'origem nesse caso' (Select-Posicao @base -IconesEsq 1000 -IconesDir 3100 -DeslocManual $null).Origem 'esquerda'
Confere 'sem espaço: devolve nada (vira compacta)' (($null -eq (Select-Posicao @base -IconesEsq 400 -IconesDir 3400 -DeslocManual $null))) $true
Confere 'posição manual livre é respeitada' (Select-Posicao @base -IconesEsq 1322 -IconesDir 2518 -DeslocManual 800).X 2540
Confere 'manual sobre os ícones é ignorada' (Select-Posicao @base -IconesEsq 1322 -IconesDir 2518 -DeslocManual 1500).Origem 'direita'
Confere 'sem dados dos ícones: ainda respeita a bandeja' (Select-Posicao @base -IconesEsq $null -IconesDir $null -DeslocManual $null).X 3013

Write-Host '--- Cota real agora, com conta independente'
$json = Get-Content $script:ArquivoCota -Raw | ConvertFrom-Json
$real = [DateTimeOffset]::Now
foreach ($conta in (Get-Contas -Agora $real)) {
    $chave = if ($conta.Rotulo -eq 'Padrão') { 'ultima:padrao' } else { 'ultima:Trabalho' }
    $l = $json.$chave
    $rs = ([DateTimeOffset]::Parse($l.resetsAt, [Globalization.CultureInfo]::InvariantCulture) - $real).TotalHours
    $dec = 1 - $rs / 168
    $esp = $dec * 100; $proj = $l.percentUsed / $dec
    Write-Host ("{0}: usado={1} resta={2:N2} h decorrido={3:N4} esperado={4:N2} projeção={5:N2} dif={6:N2}" -f $conta.Rotulo, $l.percentUsed, $rs, $dec, $esp, $proj, ($l.percentUsed - $esp))
    Confere "  $($conta.Rotulo) esperado" $conta.Esperado ([Math]::Round($esp, 0, 'AwayFromZero'))
    Confere "  $($conta.Rotulo) projeção" $conta.Projecao ([Math]::Round($proj, 0, 'AwayFromZero'))
    Write-Host "  tela: $($conta.Estado) $($conta.Cor) $($conta.Usado)% / $($conta.Esperado)% -> $($conta.Projecao)% ($($conta.Idade))"
}

if ($Amostras) {
    [void][IO.Directory]::CreateDirectory($Pasta)
    $casos = [ordered]@{
        verde    = Get-Conta (Leitura 18 115 3) 'Padrão' $agora
        amarelo  = Get-Conta (Leitura 50 84 40) 'Padrão' $agora
        vermelho = Get-Conta (Leitura 100 10 28) 'Trabalho' $agora
        velha    = Get-Conta (Leitura 18 115 300) 'Padrão' $agora
        vencida  = Get-Conta (Leitura 18 -3 300) 'Trabalho' $agora
        sem      = Get-Conta $null 'Trabalho' $agora
    }
    foreach ($k in $casos.Keys) {
        $b = New-BitmapPilula -Lista @($casos[$k]) -Escala 1.5 -Altura 60 -Compacto $false -Claro $false
        $b.Save((Join-Path $Pasta "amostra-pilula-$k.png")); $b.Dispose()
        $b = New-BitmapIcone -Conta $casos[$k] -Tam 96 -Circulo ($casos[$k].Rotulo -eq 'Trabalho')
        $b.Save((Join-Path $Pasta "amostra-icone-$k.png")); $b.Dispose()
    }
    $b = New-BitmapPilula -Lista @($casos.verde, $casos.velha) -Escala 1.5 -Altura 60 -Compacto $false -Claro $false
    $b.Save((Join-Path $Pasta 'amostra-pilula-duas.png')); $b.Dispose()
    $b = New-BitmapPilula -Lista @($casos.verde, $casos.vermelho) -Escala 1.5 -Altura 60 -Compacto $true -Claro $false
    $b.Save((Join-Path $Pasta 'amostra-pilula-compacta.png')); $b.Dispose()
    $b = New-BitmapPilula -Lista @($casos.verde, $casos.vermelho) -Escala 1.5 -Altura 60 -Compacto $false -Claro $true
    $b.Save((Join-Path $Pasta 'amostra-pilula-clara.png')); $b.Dispose()
    Write-Host "Amostras em $Pasta"
}

Write-Host ''
if ($script:falhas) { Write-Host "$script:falhas FALHA(S)"; exit 1 }
Write-Host 'Tudo confere.'
