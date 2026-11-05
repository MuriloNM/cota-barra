# Protótipo 2: pílula sobre a barra de tarefas.
# Clique: alterna as contas. Arrastar: muda de lugar. Botão direito: menu.
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'nucleo.ps1')
[CotaBarra.Nativo]::SerCienteDoDpi()

$novo = $false
$script:Mutex = New-Object Threading.Mutex($true, 'Local\CotaBarra.pilula', [ref]$novo)
if (-not $novo) { exit 0 }
$script:SinalFechar = New-Object Threading.EventWaitHandle($false, 'ManualReset', 'Local\CotaBarra.pilula.fechar')
$script:SinalFechar.Reset() | Out-Null
[void][IO.Directory]::CreateDirectory($script:PastaEstado)
Set-Content (Join-Path $script:PastaEstado 'pilula.pid') $PID

$cfg = Get-Config 'pilula'
$script:Vista   = if ($cfg -and $cfg.vista) { $cfg.vista } else { 'ambas' }   # ambas | Padrão | Trabalho
$script:Manual  = if ($cfg -and $null -ne $cfg.deslocManual) { [int]$cfg.deslocManual } else { $null }
$script:EscondidaAte = [datetime]::MinValue
$script:Ocupado = @{ WidgetsDir = 0; IconesEsq = $null; IconesDir = $null; Bandeja = 0 }
$script:Assinatura = ''
$script:UltimaVarredura = [datetime]::MinValue
$script:UltimoDado = [datetime]::MinValue
$script:Arrasto = $null
$script:Visivel = $false
$script:PosAtual = $null

function Save-Estado { Save-Config 'pilula' @{ vista = $script:Vista; deslocManual = $script:Manual } }

# Onde estão os ícones. Passa pela Automação da Interface; roda pouco e só em segundo plano de evento.
function Update-Ocupado {
    $script:UltimaVarredura = Get-Date
    try {
        Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
        $raiz = [System.Windows.Automation.AutomationElement]::FromHandle([CotaBarra.Nativo]::Barra())
        $todos = $raiz.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        $esq = $null; $dir = $null; $wid = 0; $band = 0
        foreach ($e in $todos) {
            $c = $e.Current; $id = $c.AutomationId; $b = $c.BoundingRectangle
            if ($b.IsEmpty) { continue }
            if ($id -eq 'WidgetsButton') { $wid = [int]$b.Right; continue }
            if ($id -eq 'SystemTrayIcon') { if (-not $band -or $b.Left -lt $band) { $band = [int]$b.Left }; continue }
            if ($c.ClassName -like '*TaskListButton*' -or $id -in 'StartButton', 'SearchButton', 'TaskViewButton', 'CopilotButton') {
                if ($null -eq $esq -or $b.Left -lt $esq) { $esq = [int]$b.Left }
                if ($null -eq $dir -or $b.Right -gt $dir) { $dir = [int]$b.Right }
            }
        }
        $script:Ocupado = @{ WidgetsDir = $wid; IconesEsq = $esq; IconesDir = $dir; Bandeja = $band }
    } catch { Write-Registro 'pilula' "varredura: $_" }
}

function Get-ListaVisivel {
    $contas = @(Get-Contas)
    if ($script:Vista -eq 'ambas') { return $contas }
    @($contas | Where-Object { $_.Rotulo -eq $script:Vista })
}

# Desenha e posiciona. Só redesenha se algo visível mudou.
function Update-Pilula {
    param([switch]$Forcar)
    try {
        $barra = [CotaBarra.Nativo]::Barra()
        if ($barra -eq [IntPtr]::Zero) { return }
        $r = [CotaBarra.Nativo]::Retangulo($barra)
        $dpi = [CotaBarra.Nativo]::GetDpiForWindow($barra)
        $esc = $dpi / 96.0
        $lista = Get-ListaVisivel
        $claro = Get-ModoClaro
        $texto = ($lista | ForEach-Object { "$($_.Rotulo)|$($_.Estado)|$($_.Cor)|$($_.Usado)|$($_.Esperado)|$($_.Projecao)|$($_.Idade)" }) -join ';'
        $o = $script:Ocupado
        $sig = "$texto#$r#$dpi#$claro#$($o.WidgetsDir)#$($o.IconesEsq)#$($o.IconesDir)#$($o.Bandeja)#$($script:Manual)"
        if (-not $Forcar -and $sig -eq $script:Assinatura) { return }
        $script:Assinatura = $sig

        $script:Dica.SetToolTip($script:Forma, (($lista | ForEach-Object { $_.Detalhe }) -join "`n`n"))

        $margem = [int](8 * $esc)
        $alt = $r.Height - 2 * [int](4 * $esc)
        $bandeja = [CotaBarra.Nativo]::BandejaEsquerda($barra)
        if ($o.Bandeja -gt 0 -and ($bandeja -lt 0 -or $o.Bandeja -lt $bandeja)) { $bandeja = $o.Bandeja }
        if ($bandeja -lt 0) { $bandeja = $r.Right - [int](240 * $esc) }

        foreach ($compacto in $false, $true) {
            $bmp = New-BitmapPilula -Lista $lista -Escala $esc -Altura $alt -Compacto $compacto -Claro $claro
            $pos = Select-Posicao -BarraEsq $r.Left -BarraDir $r.Right -BandejaEsq $bandeja -WidgetsDir $o.WidgetsDir -IconesEsq $o.IconesEsq -IconesDir $o.IconesDir -Largura $bmp.Width -Margem $margem -DeslocManual $script:Manual
            if ($pos -or $compacto) { break }
            $bmp.Dispose()
        }
        $x = if ($pos) { $pos.X } else { $bandeja - $margem - $bmp.Width }   # sem vão nenhum: fica colada na bandeja
        $y = $r.Top + [int](($r.Height - $alt) / 2)
        $script:PosAtual = @{ X = $x; Y = $y; L = $bmp.Width; Barra = $r; Margem = $margem; Bandeja = $bandeja }
        $script:Forma.Pintar($bmp, $x, $y)
        $bmp.Dispose()
    } catch { Write-Registro 'pilula' "desenho: $_" }
}

# Some em tela cheia, com a barra escondida ou a pedido; senão garante que está por cima.
function Update-Visibilidade {
    try {
        $barra = [CotaBarra.Nativo]::Barra()
        $mostrar = $barra -ne [IntPtr]::Zero
        if ($mostrar) {
            $r = [CotaBarra.Nativo]::Retangulo($barra); $m = [CotaBarra.Nativo]::Monitor($barra)
            if ($r.Top -ge ($m.Bottom - 4)) { $mostrar = $false }   # barra em ocultar automaticamente
            elseif ([CotaBarra.Nativo]::TelaCheia($barra, [CotaBarra.Nativo]::GetForegroundWindow())) { $mostrar = $false }
            elseif ((Get-Date) -lt $script:EscondidaAte) { $mostrar = $false }
        }
        if ($mostrar -ne $script:Visivel) {
            $script:Visivel = $mostrar
            if ($mostrar) { $script:Forma.Show(); Update-Pilula -Forcar } else { $script:Forma.Hide() }
        }
        if ($mostrar) { $script:Forma.Reforcar() }
    } catch { Write-Registro 'pilula' "visibilidade: $_" }
}

function Set-Vista([string]$v) { $script:Vista = $v; Save-Estado; Update-Menu; Update-Pilula -Forcar }
function Close-Pilula {
    $script:Relogio.Stop(); $script:Gatilho.Stop()
    $script:Vigia.Dispose()
    [Windows.Forms.Application]::Exit()
}

# ---------- Janela e menu ----------
[Windows.Forms.Application]::EnableVisualStyles()
$script:Forma = New-Object CotaBarra.FormaPilula
$script:Forma.Text = 'Cota da semana'
$script:Dica = New-Object Windows.Forms.ToolTip
$script:Dica.ShowAlways = $true; $script:Dica.InitialDelay = 300; $script:Dica.AutoPopDelay = 15000

$script:Menu = New-Object Windows.Forms.ContextMenuStrip
$script:ItemAmbas = $script:Menu.Items.Add('Mostrar as duas contas'); $script:ItemAmbas.Add_Click({ Set-Vista 'ambas' })
$script:ItemPad   = $script:Menu.Items.Add('Só Padrão');   $script:ItemPad.Add_Click({ Set-Vista 'Padrão' })
$script:ItemTrab  = $script:Menu.Items.Add('Só Trabalho'); $script:ItemTrab.Add_Click({ Set-Vista 'Trabalho' })
[void]$script:Menu.Items.Add('-')
$i = $script:Menu.Items.Add('Esconder por 30 min'); $i.Add_Click({ $script:EscondidaAte = (Get-Date).AddMinutes(30); Update-Visibilidade })
$i = $script:Menu.Items.Add('Voltar à posição automática'); $i.Add_Click({ $script:Manual = $null; Save-Estado; Update-Pilula -Forcar })
[void]$script:Menu.Items.Add('-')
$i = $script:Menu.Items.Add('Fechar'); $i.Add_Click({ Close-Pilula })
function Update-Menu {
    $script:ItemAmbas.Checked = $script:Vista -eq 'ambas'
    $script:ItemPad.Checked   = $script:Vista -eq 'Padrão'
    $script:ItemTrab.Checked  = $script:Vista -eq 'Trabalho'
}
Update-Menu

# Clique alterna; arrastar move. O movimento é lido por um timer só enquanto o botão está preso.
$script:TimerArrasto = New-Object Windows.Forms.Timer
$script:TimerArrasto.Interval = 30
$script:TimerArrasto.Add_Tick({
    $a = $script:Arrasto
    if (-not $a) { $script:TimerArrasto.Stop(); return }
    $cx = [Windows.Forms.Cursor]::Position.X
    if ([Windows.Forms.Control]::MouseButtons -band [Windows.Forms.MouseButtons]::Left) {
        if ([Math]::Abs($cx - $a.Inicio) -gt 4) { $a.Moveu = $true }
        if ($a.Moveu) {
            $p = $script:PosAtual
            $x = [Math]::Max($p.Barra.Left, [Math]::Min($a.X0 + $cx - $a.Inicio, $p.Bandeja - $p.Margem - $p.L))
            $script:Forma.Mover($x, $p.Y); $a.XAtual = $x
        }
        return
    }
    $script:TimerArrasto.Stop(); $script:Arrasto = $null
    if ($a.Moveu) {
        $p = $script:PosAtual
        $script:Manual = $p.Barra.Right - $a.XAtual - $p.L
        Save-Estado; Update-Pilula -Forcar
    } else {
        $ordem = 'ambas', 'Padrão', 'Trabalho'
        Set-Vista $ordem[(([array]::IndexOf($ordem, $script:Vista) + 1) % 3)]
    }
})
$script:Forma.Add_MouseDown({
    param($s, $e)
    if ($e.Button -eq 'Left') {
        $script:Arrasto = @{ Inicio = [Windows.Forms.Cursor]::Position.X; X0 = $script:PosAtual.X; XAtual = $script:PosAtual.X; Moveu = $false }
        $script:TimerArrasto.Start()
    }
})
$script:Forma.Add_MouseUp({
    param($s, $e)
    if ($e.Button -eq 'Right') {
        # O menu só fecha ao clicar fora se o processo estiver em primeiro plano.
        [void][CotaBarra.Nativo]::SetForegroundWindow($script:Forma.Handle)
        $script:Menu.Show([Windows.Forms.Cursor]::Position)
    }
})

# ---------- Ritmo: nada de varredura contínua ----------
# Gatilho: 200 ms depois de a janela em primeiro plano mudar (tela cheia, barra por cima).
$script:Gatilho = New-Object Windows.Forms.Timer
$script:Gatilho.Interval = 200
$script:Gatilho.Add_Tick({
    $script:Gatilho.Stop()
    Update-Visibilidade
    if (((Get-Date) - $script:UltimaVarredura).TotalSeconds -ge 15) { Update-Ocupado; if ($script:Visivel) { Update-Pilula } }
})
$script:Vigia = New-Object CotaBarra.Vigia($script:Gatilho)

# Relógio de 5 s: pedido de fechar, visibilidade. A cada 15 s olha o arquivo; a cada 60 s revê os ícones.
$script:Relogio = New-Object Windows.Forms.Timer
$script:Relogio.Interval = 5000
$script:Relogio.Add_Tick({
    if ($script:SinalFechar.WaitOne(0)) { Close-Pilula; return }
    Update-Visibilidade
    $agora = Get-Date
    if ($script:Visivel -and ($agora - $script:UltimaVarredura).TotalSeconds -ge 60) { Update-Ocupado }
    if ($script:Visivel -and ($agora - $script:UltimoDado).TotalSeconds -ge 15) { $script:UltimoDado = $agora; Update-Pilula }
})

Update-Ocupado
$script:Forma.Add_Shown({ Update-Pilula -Forcar; $script:Visivel = $true; $script:Forma.Reforcar(); $script:Relogio.Start(); Update-Visibilidade })
try {
    [Windows.Forms.Application]::Run($script:Forma)
} catch { Write-Registro 'pilula' "fim: $_" }
try { $script:Mutex.ReleaseMutex() } catch { }
Remove-Item (Join-Path $script:PastaEstado 'pilula.pid') -ErrorAction SilentlyContinue
