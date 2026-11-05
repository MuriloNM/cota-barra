# Protótipo 1: ícone na bandeja, perto do relógio. Passar o mouse mostra o detalhe.
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'nucleo.ps1')
[CotaBarra.Nativo]::SerCienteDoDpi()

$novo = $false
$script:Mutex = New-Object Threading.Mutex($true, 'Local\CotaBarra.bandeja', [ref]$novo)
if (-not $novo) { exit 0 }
$script:SinalFechar = New-Object Threading.EventWaitHandle($false, 'ManualReset', 'Local\CotaBarra.bandeja.fechar')
$script:SinalFechar.Reset() | Out-Null
[void][IO.Directory]::CreateDirectory($script:PastaEstado)
Set-Content (Join-Path $script:PastaEstado 'bandeja.pid') $PID

$cfg = Get-Config 'bandeja'
$script:Vista = if ($cfg -and $cfg.vista) { $cfg.vista } else { 'ambas' }

# O NotifyIcon do .NET recusa dica acima de 63 caracteres; o Windows aceita 127.
function Set-Dica($Icone, [string]$Texto) {
    if ($Texto.Length -gt 127) { $Texto = $Texto.Substring(0, 127) }
    try {
        $t = [Windows.Forms.NotifyIcon]; $f = [Reflection.BindingFlags]'NonPublic,Instance'
        $t.GetField('text', $f).SetValue($Icone, $Texto)
        if ($t.GetField('added', $f).GetValue($Icone)) { [void]$t.GetMethod('UpdateIcon', $f).Invoke($Icone, @($true)) }
    } catch { $Icone.Text = $Texto.Substring(0, [Math]::Min(63, $Texto.Length)) }
}

$script:Itens = @{}
foreach ($d in $script:Contas) {
    $ni = New-Object Windows.Forms.NotifyIcon
    $script:Itens[$d.Rotulo] = @{ Icone = $ni; Alca = [IntPtr]::Zero; Sig = ''; Circulo = ($d.Rotulo -eq 'Trabalho') }
}

function Update-Icones {
    try {
        $dpi = [CotaBarra.Nativo]::GetDpiForWindow([CotaBarra.Nativo]::Barra())
        if (-not $dpi) { $dpi = 96 }
        $tam = [CotaBarra.Nativo]::GetSystemMetricsForDpi(49, $dpi)   # SM_CXSMICON
        foreach ($c in (Get-Contas)) {
            $it = $script:Itens[$c.Rotulo]
            $it.Icone.Visible = ($script:Vista -eq 'ambas' -or $script:Vista -eq $c.Rotulo)
            $sig = "$($c.Estado)|$($c.Cor)|$($c.Usado)|$tam"
            if ($sig -ne $it.Sig -or $it.Alca -eq [IntPtr]::Zero) {
                $it.Sig = $sig
                $bmp = New-BitmapIcone -Conta $c -Tam $tam -Circulo $it.Circulo
                $novaAlca = $bmp.GetHicon(); $bmp.Dispose()
                $it.Icone.Icon = [Drawing.Icon]::FromHandle($novaAlca)
                if ($it.Alca -ne [IntPtr]::Zero) { [void][CotaBarra.Nativo]::DestroyIcon($it.Alca) }
                $it.Alca = $novaAlca
            }
            Set-Dica $it.Icone $c.Detalhe
        }
    } catch { Write-Registro 'bandeja' "ícones: $_" }
}

function Close-Bandeja {
    $script:Relogio.Stop()
    foreach ($it in $script:Itens.Values) { $it.Icone.Visible = $false; $it.Icone.Dispose() }
    [Windows.Forms.Application]::Exit()
}
function Set-Vista([string]$v) {
    $script:Vista = $v; Save-Config 'bandeja' @{ vista = $v }
    $script:ItemAmbas.Checked = $v -eq 'ambas'; $script:ItemPad.Checked = $v -eq 'Padrão'; $script:ItemTrab.Checked = $v -eq 'Trabalho'
    Update-Icones
}

$script:Menu = New-Object Windows.Forms.ContextMenuStrip
$script:ItemAmbas = $script:Menu.Items.Add('Mostrar as duas contas'); $script:ItemAmbas.Add_Click({ Set-Vista 'ambas' })
$script:ItemPad   = $script:Menu.Items.Add('Só Padrão');   $script:ItemPad.Add_Click({ Set-Vista 'Padrão' })
$script:ItemTrab  = $script:Menu.Items.Add('Só Trabalho'); $script:ItemTrab.Add_Click({ Set-Vista 'Trabalho' })
[void]$script:Menu.Items.Add('-')
$i = $script:Menu.Items.Add('Fechar'); $i.Add_Click({ Close-Bandeja })
foreach ($it in $script:Itens.Values) { $it.Icone.ContextMenuStrip = $script:Menu }
$script:ItemAmbas.Checked = $script:Vista -eq 'ambas'; $script:ItemPad.Checked = $script:Vista -eq 'Padrão'; $script:ItemTrab.Checked = $script:Vista -eq 'Trabalho'

Update-Icones

# 5 s para atender ao pedido de fechar; 15 s para olhar o arquivo. Fora isso, parado.
$script:Passo = 0
$script:Relogio = New-Object Windows.Forms.Timer
$script:Relogio.Interval = 5000
$script:Relogio.Add_Tick({
    if ($script:SinalFechar.WaitOne(0)) { Close-Bandeja; return }
    $script:Passo = ($script:Passo + 1) % 3
    if ($script:Passo -eq 0) { Update-Icones }
})
$script:Relogio.Start()

try { [Windows.Forms.Application]::Run() } catch { Write-Registro 'bandeja' "fim: $_" }
try { $script:Mutex.ReleaseMutex() } catch { }
Remove-Item (Join-Path $script:PastaEstado 'bandeja.pid') -ErrorAction SilentlyContinue
