# Núcleo comum aos dois protótipos: leitura do arquivo, fórmula, textos e desenho.
# Só LÊ o arquivo de cota. Não usa rede nem credenciais.

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$script:ArquivoCota    = Get-ChildItem (Join-Path $env:USERPROFILE '.claude\plugins\store') -Filter 'cota_*.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
$script:PastaEstado    = Join-Path $env:LOCALAPPDATA 'cota-barra'
$script:SemanaMs       = 604800000.0
$script:VelhaDepoisMin = 120      # leitura com mais de 2 h aparece como velha
$script:Contas         = @(
    @{ Chave = 'ultima:padrao';   Rotulo = 'Padrão' },
    @{ Chave = 'ultima:Trabalho'; Rotulo = 'Trabalho' }
)

if (-not ('CotaBarra.Nativo' -as [type])) {
    Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CotaBarra {
  public static class Nativo {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }
    [DllImport("user32.dll")] static extern IntPtr SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string a, string b);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr c, string a, string b);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint f);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO i);
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int i, uint dpi);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr p, int f);

    public static void SerCienteDoDpi() { try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } }
    public static IntPtr Barra() { return FindWindow("Shell_TrayWnd", null); }
    public static Rectangle Retangulo(IntPtr h) {
      RECT r; if (!GetWindowRect(h, out r)) return Rectangle.Empty;
      return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }
    public static Rectangle Monitor(IntPtr h) {
      MONITORINFO i = new MONITORINFO(); i.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
      if (!GetMonitorInfo(MonitorFromWindow(h, 2), ref i)) return Rectangle.Empty;
      return Rectangle.FromLTRB(i.rcMonitor.Left, i.rcMonitor.Top, i.rcMonitor.Right, i.rcMonitor.Bottom);
    }
    public static int BandejaEsquerda(IntPtr barra) {
      IntPtr n = FindWindowEx(barra, IntPtr.Zero, "TrayNotifyWnd", null);
      if (n == IntPtr.Zero) return -1;
      return Retangulo(n).Left;
    }
    // A janela em primeiro plano ocupa a tela inteira do monitor desta barra?
    public static bool TelaCheia(IntPtr barra, IntPtr janela) {
      if (janela == IntPtr.Zero || !IsWindowVisible(janela)) return false;
      StringBuilder s = new StringBuilder(64); GetClassName(janela, s, 64);
      switch (s.ToString()) {
        case "Progman": case "WorkerW": case "Shell_TrayWnd": case "Shell_SecondaryTrayWnd":
        case "XamlExplorerHostIslandWindow": case "Windows.UI.Core.CoreWindow": case "TopLevelWindowForOverflowXamlIsland":
          return false;
      }
      if (MonitorFromWindow(janela, 2) != MonitorFromWindow(barra, 2)) return false;
      Rectangle m = Monitor(janela), r = Retangulo(janela);
      return r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
    }
  }

  // Avisa quando a janela em primeiro plano muda, sem varredura: dispara um timer curto.
  public class Vigia : IDisposable {
    delegate void Proc(IntPtr h, uint e, IntPtr j, int o, int c, uint t, uint ms);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint a, uint b, IntPtr m, Proc p, uint pid, uint tid, uint f);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
    readonly Proc proc; readonly IntPtr gancho; readonly Timer gatilho;
    public Vigia(Timer g) { gatilho = g; proc = Chamada; gancho = SetWinEventHook(3, 3, IntPtr.Zero, proc, 0, 0, 2); }
    void Chamada(IntPtr h, uint e, IntPtr j, int o, int c, uint t, uint ms) { gatilho.Stop(); gatilho.Start(); }
    public void Dispose() { UnhookWinEvent(gancho); }
  }

  // A pílula: janela sem borda, sempre visível, que não rouba o foco e tem bordas suaves.
  public class FormaPilula : Form {
    [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; public PT(int a, int b) { x = a; y = b; } }
    [StructLayout(LayoutKind.Sequential)] struct SZ { public int w, h; public SZ(int a, int b) { w = a; h = b; } }
    [StructLayout(LayoutKind.Sequential, Pack=1)] struct BLEND { public byte op, flags, alpha, fmt; }
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref PT p, ref SZ s, IntPtr src, ref PT sp, int key, ref BLEND b, int f);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);

    public FormaPilula() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None; }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
      get { CreateParams c = base.CreateParams; c.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000 | 0x00000008; return c; }  // LAYERED, TOOLWINDOW, NOACTIVATE, TOPMOST
    }
    protected override void WndProc(ref Message m) {
      if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }  // WM_MOUSEACTIVATE: não ativar
      base.WndProc(ref m);
    }
    public void Pintar(Bitmap b, int x, int y) {
      IntPtr tela = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(tela), hb = b.GetHbitmap(Color.FromArgb(0)), velho = SelectObject(mem, hb);
      try {
        PT p = new PT(x, y), o = new PT(0, 0); SZ s = new SZ(b.Width, b.Height);
        BLEND f = new BLEND(); f.alpha = 255; f.fmt = 1;
        UpdateLayeredWindow(Handle, tela, ref p, ref s, mem, ref o, 0, ref f, 2);
      } finally { SelectObject(mem, velho); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, tela); }
    }
    public void Mover(int x, int y) { SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010); }
    public void Reforcar() { SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); }
  }
}
'@
}

# ---------- Fórmula (igual à do plugin) ----------

function Get-Inteiro([double]$v) { [int][Math]::Round($v, 0, [MidpointRounding]::AwayFromZero) }

function Get-Ritmo {
    param([double]$Usado, [DateTimeOffset]$ResetsAt, [DateTimeOffset]$Agora)
    $resta = ($ResetsAt - $Agora).TotalMilliseconds
    $decorrido = [Math]::Min([Math]::Max(1 - $resta / $script:SemanaMs, 0), 1)
    $esperado = $decorrido * 100
    $projecao = if ($decorrido -gt 0.02) { $Usado / $decorrido } else { $Usado }
    $dif = $Usado - $esperado
    $cor = if ($dif -lt -5) { 'verde' } elseif ($dif -gt 5) { 'vermelho' } else { 'amarelo' }
    [pscustomobject]@{ Decorrido = $decorrido; Esperado = $esperado; Projecao = $projecao; Diferenca = $dif; Cor = $cor; RestaDias = $resta / 86400000 }
}

function Format-Idade([int]$Min) {
    if ($Min -lt 1) { return 'agora' }
    if ($Min -lt 60) { return "$Min min" }
    if ($Min -lt 2880) { return "$([Math]::Floor($Min / 60)) h" }
    return "$([Math]::Floor($Min / 1440)) d"
}

# Transforma uma leitura em tudo o que as telas precisam. Estado: ok | velha | vencida | sem
function Get-Conta {
    param($Leitura, [string]$Rotulo, [DateTimeOffset]$Agora)
    $c = [ordered]@{ Rotulo = $Rotulo; Estado = 'sem'; Cor = 'cinza'; Usado = 0; Esperado = 0; Projecao = 0; Idade = ''; Detalhe = "$Rotulo`: sem leitura ainda. Abra o Claude nesta conta." }
    if ($null -eq $Leitura -or $null -eq $Leitura.percentUsed -or -not $Leitura.resetsAt -or -not $Leitura.lido) { return [pscustomobject]$c }
    try { $reset = [DateTimeOffset]::Parse([string]$Leitura.resetsAt, [Globalization.CultureInfo]::InvariantCulture) } catch { return [pscustomobject]$c }
    $min = [Math]::Max([Math]::Floor(($Agora.ToUnixTimeMilliseconds() - [long]$Leitura.lido) / 60000), 0)
    $idade = Format-Idade $min
    $c.Idade = $idade
    $prefixo = if ($idade -eq 'agora') { 'lida agora' } else { "lida há $idade" }
    if ($reset -le $Agora) {
        $c.Estado = 'vencida'
        $c.Detalhe = "$Rotulo`: leitura vencida (a semana já virou). Abra o Claude nesta conta."
        return [pscustomobject]$c
    }
    $r = Get-Ritmo -Usado ([double]$Leitura.percentUsed) -ResetsAt $reset -Agora $Agora
    $c.Cor = $r.Cor; $c.Usado = Get-Inteiro $Leitura.percentUsed; $c.Esperado = Get-Inteiro $r.Esperado; $c.Projecao = Get-Inteiro $r.Projecao
    $c.Estado = if ($min -gt $script:VelhaDepoisMin) { 'velha' } else { 'ok' }
    $resta = if ($r.RestaDias -lt 1) { "$(Get-Inteiro ($r.RestaDias * 24)) h" } else { "$($r.RestaDias.ToString('0.0', [Globalization.CultureInfo]::GetCultureInfo('pt-BR'))) dias" }
    $linha2 = "reseta em $resta · $prefixo"
    if ($c.Estado -eq 'velha') { $linha2 += ' (VELHA: abra o Claude)' }
    $c.Detalhe = "$Rotulo`: $($c.Usado)% usado · esperado $($c.Esperado)% · projeção $($c.Projecao)%`n$linha2"
    [pscustomobject]$c
}

# Lê o arquivo sem bloquear quem grava nele. Só relê quando a data muda; se vier quebrado, mantém o último bom.
$script:CacheData = [datetime]::MinValue
$script:CacheJson = $null
function Read-ArquivoCota {
    param([string]$Caminho = $script:ArquivoCota)
    try {
        $data = [IO.File]::GetLastWriteTimeUtc($Caminho)
        if ($data -eq $script:CacheData -and $script:CacheJson) { return $script:CacheJson }
        $fs = New-Object IO.FileStream($Caminho, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        try { $sr = New-Object IO.StreamReader($fs, [Text.Encoding]::UTF8); $texto = $sr.ReadToEnd() } finally { $fs.Dispose() }
        $script:CacheJson = $texto | ConvertFrom-Json
        $script:CacheData = $data
    } catch { }
    $script:CacheJson
}

function Get-Contas {
    param([DateTimeOffset]$Agora = [DateTimeOffset]::Now, [string]$Caminho = $script:ArquivoCota)
    $json = Read-ArquivoCota -Caminho $Caminho
    foreach ($d in $script:Contas) {
        $leitura = if ($json) { $json.($d.Chave) } else { $null }
        Get-Conta -Leitura $leitura -Rotulo $d.Rotulo -Agora $Agora
    }
}

# ---------- Posição da pílula (decisão pura, testada em conferir.ps1) ----------
# Ordem: posição escolhida pelo usuário, à esquerda da bandeja, à direita dos widgets. $null = não cabe.
function Select-Posicao {
    param([int]$BarraEsq, [int]$BarraDir, [int]$BandejaEsq, [int]$WidgetsDir, $IconesEsq, $IconesDir, [int]$Largura, [int]$Margem, $DeslocManual)
    $min = [Math]::Max($BarraEsq, $WidgetsDir) + $Margem
    $max = $BandejaEsq - $Margem
    $livre = {
        param($x)
        if ($x -lt $min -or ($x + $Largura) -gt $max) { return $false }
        if ($null -eq $IconesEsq) { return $true }
        (($x + $Largura + $Margem) -le $IconesEsq) -or (($x - $Margem) -ge $IconesDir)
    }
    if ($null -ne $DeslocManual) {
        $x = $BarraDir - [int]$DeslocManual - $Largura
        if (& $livre $x) { return [pscustomobject]@{ X = $x; Origem = 'manual' } }
    }
    $x = $max - $Largura
    if (& $livre $x) { return [pscustomobject]@{ X = $x; Origem = 'direita' } }
    if (& $livre $min) { return [pscustomobject]@{ X = $min; Origem = 'esquerda' } }
    $null
}

# ---------- Desenho ----------

$script:Paleta = @{
    verde = [Drawing.Color]::FromArgb(63, 185, 80); amarelo = [Drawing.Color]::FromArgb(227, 179, 65)
    vermelho = [Drawing.Color]::FromArgb(248, 81, 73); cinza = [Drawing.Color]::FromArgb(140, 140, 140)
}

function Get-Medida($Grafico, [string]$Texto, $Fonte, $Formato) {
    if (-not $Texto) { return 0 }
    [Math]::Ceiling($Grafico.MeasureString($Texto, $Fonte, 4000, $Formato).Width)
}

# Monta as colunas de cada linha da pílula.
function Get-Segmentos($Conta, [bool]$MostrarRotulo, [bool]$Compacto) {
    $rotulo = if ($MostrarRotulo) { $Conta.Rotulo } else { '' }
    if ($Conta.Estado -in 'sem', 'vencida') {
        $msg = if ($Conta.Estado -eq 'sem') { 'sem leitura' } else { 'vencida' }
        return @{ Rotulo = $rotulo; Usado = '—'; Resto = $(if ($Compacto) { '' } else { $msg }); Idade = '' }
    }
    if ($Compacto) { return @{ Rotulo = ''; Usado = "$($Conta.Usado)%"; Resto = ''; Idade = '' } }
    $idade = if ($Conta.Estado -eq 'velha') { "velha · $($Conta.Idade)" } else { $Conta.Idade }
    @{ Rotulo = $rotulo; Usado = "$($Conta.Usado)%"; Resto = "/ $($Conta.Esperado)% → $($Conta.Projecao)%"; Idade = $idade }
}

function New-BitmapPilula {
    param($Lista, [double]$Escala, [int]$Altura, [bool]$Compacto, [bool]$Claro)
    $Lista = @($Lista)
    $duas = $Lista.Count -gt 1
    $fundo   = if ($Claro) { [Drawing.Color]::FromArgb(240, 250, 250, 250) } else { [Drawing.Color]::FromArgb(240, 32, 32, 32) }
    $borda   = if ($Claro) { [Drawing.Color]::FromArgb(50, 0, 0, 0) } else { [Drawing.Color]::FromArgb(50, 255, 255, 255) }
    $forte   = if ($Claro) { [Drawing.Color]::FromArgb(25, 25, 25) } else { [Drawing.Color]::FromArgb(245, 245, 245) }
    $fraco   = if ($Claro) { [Drawing.Color]::FromArgb(100, 100, 100) } else { [Drawing.Color]::FromArgb(170, 170, 170) }
    $alerta  = if ($Claro) { [Drawing.Color]::FromArgb(170, 95, 0) } else { [Drawing.Color]::FromArgb(240, 170, 70) }

    $px = if ($duas) { 12.5 * $Escala } else { 15 * $Escala }
    $fNormal = New-Object Drawing.Font('Segoe UI', [single]$px, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    $fForte  = New-Object Drawing.Font('Segoe UI Semibold', [single]$px, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
    $fmt = [Drawing.StringFormat]::GenericTypographic.Clone()
    $fmt.FormatFlags = $fmt.FormatFlags -bor [Drawing.StringFormatFlags]::MeasureTrailingSpaces -bor [Drawing.StringFormatFlags]::NoWrap

    $linhas = @(foreach ($c in $Lista) { @{ Conta = $c; Seg = (Get-Segmentos $c $duas $Compacto) } })
    $med = New-Object Drawing.Bitmap(1, 1)
    $gm = [Drawing.Graphics]::FromImage($med)
    $wRot = 0; $wUso = 0; $wResto = 0; $wIdade = 0
    foreach ($l in $linhas) {
        $wRot   = [Math]::Max($wRot,   (Get-Medida $gm $l.Seg.Rotulo $fNormal $fmt))
        $wUso   = [Math]::Max($wUso,   (Get-Medida $gm $l.Seg.Usado  $fForte  $fmt))
        $wResto = [Math]::Max($wResto, (Get-Medida $gm $l.Seg.Resto  $fNormal $fmt))
        $wIdade = [Math]::Max($wIdade, (Get-Medida $gm $l.Seg.Idade  $fNormal $fmt))
    }
    $gm.Dispose(); $med.Dispose()

    $pad = [int](10 * $Escala); $vao = [int](6 * $Escala); $bola = [int]($px * 0.62)
    $x1 = $pad + $bola + $vao
    $x2 = $x1 + $(if ($wRot) { $wRot + $vao } else { 0 })
    $x3 = $x2 + $wUso + $(if ($wResto) { [int](4 * $Escala) } else { 0 })
    $x4 = $x3 + $wResto + $(if ($wIdade) { $vao * 2 } else { 0 })
    $largura = [int]($x4 + $wIdade + $pad)

    $bmp = New-Object Drawing.Bitmap($largura, $Altura, [Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'

    $raio = [int](8 * $Escala) * 2
    $cam = New-Object Drawing.Drawing2D.GraphicsPath
    $w1 = $largura - 1; $h1 = $Altura - 1
    $cam.AddArc(0, 0, $raio, $raio, 180, 90); $cam.AddArc($w1 - $raio, 0, $raio, $raio, 270, 90)
    $cam.AddArc($w1 - $raio, $h1 - $raio, $raio, $raio, 0, 90); $cam.AddArc(0, $h1 - $raio, $raio, $raio, 90, 90); $cam.CloseFigure()
    $pincel = New-Object Drawing.SolidBrush($fundo); $g.FillPath($pincel, $cam); $pincel.Dispose()
    $caneta = New-Object Drawing.Pen($borda); $g.DrawPath($caneta, $cam); $caneta.Dispose(); $cam.Dispose()

    $altLinha = $fNormal.GetHeight($g)
    $topo = ($Altura - ($altLinha * $linhas.Count)) / 2
    $bForte = New-Object Drawing.SolidBrush($forte); $bFraco = New-Object Drawing.SolidBrush($fraco); $bAlerta = New-Object Drawing.SolidBrush($alerta)
    $i = 0
    foreach ($l in $linhas) {
        $y = $topo + $i * $altLinha
        $c = $l.Conta; $s = $l.Seg
        $cor = $script:Paleta[$c.Cor]
        $yb = $y + ($altLinha - $bola) / 2
        if ($c.Estado -eq 'ok') {
            $b = New-Object Drawing.SolidBrush($cor); $g.FillEllipse($b, [single]$pad, [single]$yb, $bola, $bola); $b.Dispose()
        } else {
            # Bolinha vazada: a cor é de uma leitura velha, não do agora.
            $p = New-Object Drawing.Pen($cor, [single](1.5 * $Escala)); $g.DrawEllipse($p, [single]$pad, [single]$yb, $bola, $bola); $p.Dispose()
        }
        $apagado = $c.Estado -ne 'ok'
        if ($s.Rotulo) { $g.DrawString($s.Rotulo, $fNormal, $bFraco, [single]$x1, [single]$y, $fmt) }
        $xUso = $x2 + $wUso - (Get-Medida $g $s.Usado $fForte $fmt)   # alinha à direita, colado na barra "/"
        $g.DrawString($s.Usado, $fForte, $(if ($apagado) { $bFraco } else { $bForte }), [single]$xUso, [single]$y, $fmt)
        if ($s.Resto) { $g.DrawString($s.Resto, $fNormal, $(if ($apagado) { $bFraco } else { $bForte }), [single]$x3, [single]$y, $fmt) }
        if ($s.Idade) { $g.DrawString($s.Idade, $fNormal, $(if ($c.Estado -eq 'velha') { $bAlerta } else { $bFraco }), [single]$x4, [single]$y, $fmt) }
        $i++
    }
    $bForte.Dispose(); $bFraco.Dispose(); $bAlerta.Dispose(); $fNormal.Dispose(); $fForte.Dispose(); $fmt.Dispose(); $g.Dispose()
    $bmp
}

# Ícone da bandeja: número sobre fundo colorido. Padrão = quadrado; Trabalho = círculo.
function New-BitmapIcone {
    param($Conta, [int]$Tam, [bool]$Circulo)
    $bmp = New-Object Drawing.Bitmap($Tam, $Tam, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $vivo = $Conta.Estado -eq 'ok'
    $corFundo = if ($vivo) { $script:Paleta[$Conta.Cor] } else { [Drawing.Color]::FromArgb(95, 95, 95) }
    $b = New-Object Drawing.SolidBrush($corFundo)
    if ($Circulo) {
        $g.FillEllipse($b, 0, 0, $Tam - 1, $Tam - 1)
    } else {
        $d = [int]($Tam * 0.36); $w = $Tam - 1
        $cam = New-Object Drawing.Drawing2D.GraphicsPath
        $cam.AddArc(0, 0, $d, $d, 180, 90); $cam.AddArc($w - $d, 0, $d, $d, 270, 90)
        $cam.AddArc($w - $d, $w - $d, $d, $d, 0, 90); $cam.AddArc(0, $w - $d, $d, $d, 90, 90); $cam.CloseFigure()
        $g.FillPath($b, $cam); $cam.Dispose()
    }
    $b.Dispose()
    $texto = if ($Conta.Estado -in 'sem', 'vencida') { '—' } else { "$($Conta.Usado)" }
    $corTexto = if ($vivo -and $Conta.Cor -eq 'vermelho') { [Drawing.Color]::White } elseif ($vivo) { [Drawing.Color]::FromArgb(15, 15, 15) } else { [Drawing.Color]::White }
    $fmt = [Drawing.StringFormat]::GenericTypographic.Clone()
    $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'; $fmt.FormatFlags = $fmt.FormatFlags -bor [Drawing.StringFormatFlags]::NoWrap
    $px = $Tam * $(if ($texto.Length -ge 3) { 0.56 } else { 0.72 })
    $f = New-Object Drawing.Font('Segoe UI', [single]$px, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
    $bt = New-Object Drawing.SolidBrush($corTexto)
    $g.DrawString($texto, $f, $bt, (New-Object Drawing.RectangleF(0, 0, $Tam, $Tam)), $fmt)
    $bt.Dispose(); $f.Dispose(); $fmt.Dispose(); $g.Dispose()
    $bmp
}

# ---------- Estado e instância ----------

function Get-Config([string]$Nome) {
    try { Get-Content (Join-Path $script:PastaEstado "$Nome.json") -Raw -ErrorAction Stop | ConvertFrom-Json } catch { $null }
}
function Save-Config([string]$Nome, $Dados) {
    try {
        [void][IO.Directory]::CreateDirectory($script:PastaEstado)
        $Dados | ConvertTo-Json | Set-Content (Join-Path $script:PastaEstado "$Nome.json") -Encoding UTF8
    } catch { }
}
function Write-Registro([string]$Nome, [string]$Texto) {
    try {
        [void][IO.Directory]::CreateDirectory($script:PastaEstado)
        Add-Content (Join-Path $script:PastaEstado "$Nome.log") "$(Get-Date -Format s) $Texto" -Encoding UTF8
    } catch { }
}
function Get-ModoClaro {
    try { (Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name SystemUsesLightTheme -ErrorAction Stop).SystemUsesLightTheme -eq 1 } catch { $false }
}
