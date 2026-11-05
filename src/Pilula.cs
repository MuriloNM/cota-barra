// A pílula sobre a barra de tarefas.
// Clique: alterna as contas. Arrastar: muda de lugar. Mouse em cima: cartão de detalhe. Botão direito: painel de ajustes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using System.Windows.Forms;
using Temporizador = System.Windows.Forms.Timer;

namespace CotaBarra {
  static class Nativo {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice; }
    [DllImport("user32.dll")] public static extern IntPtr SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string a, string b);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr p, IntPtr c, string a, string b);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr m, ref MONITORINFO i);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    public static IntPtr Barra() { return FindWindow("Shell_TrayWnd", null); }
    public static string Dispositivo(IntPtr h) {
      MONITORINFO i = new MONITORINFO(); i.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
      return GetMonitorInfo(MonitorFromWindow(h, 2), ref i) ? i.szDevice : "";
    }
    // A barra de um monitor; IntPtr.Zero se ele não tiver barra. Os outros monitores usam outra classe de janela.
    public static IntPtr BarraDe(string dispositivo) {
      IntPtr p = Barra();
      if (p != IntPtr.Zero && Dispositivo(p) == dispositivo) return p;
      IntPtr s = IntPtr.Zero;
      while ((s = FindWindowEx(IntPtr.Zero, s, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        if (Dispositivo(s) == dispositivo) return s;
      return IntPtr.Zero;
    }
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
      return n == IntPtr.Zero ? -1 : Retangulo(n).Left;
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
  class Vigia : IDisposable {
    delegate void Proc(IntPtr h, uint e, IntPtr j, int o, int c, uint t, uint ms);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint a, uint b, IntPtr m, Proc p, uint pid, uint tid, uint f);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
    readonly Proc proc; readonly IntPtr gancho; readonly Temporizador gatilho;
    public Vigia(Temporizador g) { gatilho = g; proc = Chamada; gancho = SetWinEventHook(3, 3, IntPtr.Zero, proc, 0, 0, 2); }
    void Chamada(IntPtr h, uint e, IntPtr j, int o, int c, uint t, uint ms) { gatilho.Stop(); gatilho.Start(); }
    public void Dispose() { UnhookWinEvent(gancho); }
  }

  // Janela sem borda, sempre visível, que não rouba o foco e tem bordas suaves. O cartão também deixa o mouse atravessar.
  class FormaPilula : Form {
    [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; public PT(int a, int b) { x = a; y = b; } }
    [StructLayout(LayoutKind.Sequential)] struct SZ { public int w, h; public SZ(int a, int b) { w = a; h = b; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alpha, fmt; }
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref PT p, ref SZ s, IntPtr src, ref PT sp, int key, ref BLEND b, int f);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);

    readonly bool atravessar;
    public FormaPilula(bool atravessarMouse) {
      atravessar = atravessarMouse;
      FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None; Text = "Cota da semana";
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
      get {
        CreateParams c = base.CreateParams;
        c.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000 | 0x00000008;  // LAYERED, TOOLWINDOW, NOACTIVATE, TOPMOST
        if (atravessar) c.ExStyle |= 0x00000020;                          // TRANSPARENT
        return c;
      }
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

  class Chip { public string Texto; public Func<bool> Ativo; public Action Acao; public bool Perigo; public Rectangle R; }
  class Secao { public string Titulo; public List<Chip> Chips = new List<Chip>(); }

  // Painel de ajustes no lugar do menu branco do Windows. Cada clique vale na hora; clicar fora ou Esc fecha.
  class PainelAjustes : Form {
    readonly Func<List<Secao>> montar; readonly Func<Aparencia> aparencia; readonly Action<PainelAjustes> posicionar; readonly double esc;
    List<Secao> secoes; Chip sobre; bool fechando;

    public PainelAjustes(Func<List<Secao>> montarSecoes, Func<Aparencia> ap, Action<PainelAjustes> pos, double escala) {
      montar = montarSecoes; aparencia = ap; posicionar = pos; esc = escala;
      FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
      TopMost = true; DoubleBuffered = true; KeyPreview = true; Text = "Ajustes da cota";
      secoes = montar(); Dispor();
      Deactivate += delegate { Encerrar(); };
      KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Encerrar(); };
    }
    int D(double v) { return (int)(v * esc); }
    public void Encerrar() { if (fechando) return; fechando = true; Close(); }
    protected override void OnHandleCreated(EventArgs e) {
      base.OnHandleCreated(e);
      int redondo = 2; Nativo.DwmSetWindowAttribute(Handle, 33, ref redondo, 4);   // cantos e sombra do Windows 11
    }
    Font Fonte(FontStyle st, double tam) { return new Font(Desenho.FonteMono(aparencia().Fonte), (float)(tam * esc), st, GraphicsUnit.Pixel); }

    void Dispor() {
      int largura = D(470), xChips = D(96), pad = D(14), alturaChip = D(26), vaoChip = D(6), y = D(44);
      using (Font f = Fonte(FontStyle.Regular, 12.5)) using (Graphics g = CreateGraphics()) {
        foreach (Secao s in secoes) {
          int x = xChips;
          foreach (Chip c in s.Chips) {
            int w = TextRenderer.MeasureText(g, c.Texto, f, Size.Empty, TextFormatFlags.NoPadding).Width + D(18);
            if (x + w > largura - pad && x > xChips) { x = xChips; y += alturaChip + vaoChip; }
            c.R = new Rectangle(x, y, w, alturaChip);
            x += w + vaoChip;
          }
          y += alturaChip + D(10);
        }
      }
      ClientSize = new Size(largura, y + D(4));
      string teste = Environment.GetEnvironmentVariable("COTA_TESTE");
      if (!string.IsNullOrEmpty(teste) && IsHandleCreated) {
        StringBuilder sb = new StringBuilder();
        foreach (Secao s in secoes) foreach (Chip c in s.Chips) {
          Rectangle t = RectangleToScreen(c.R);
          sb.AppendLine(s.Titulo + "|" + c.Texto + "|" + (t.X + t.Width / 2) + "|" + (t.Y + t.Height / 2));
        }
        File.WriteAllText(teste, sb.ToString(), new UTF8Encoding(true));
      }
    }
    public void Refazer() { secoes = montar(); Dispor(); posicionar(this); Dispor(); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e) {
      Aparencia ap = aparencia(); Paleta p = ap.Cores; Graphics g = e.Graphics;
      g.Clear(p.Fundo); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
      using (Font fT = Fonte(FontStyle.Bold, 14)) using (Font fN = Fonte(FontStyle.Regular, 12.5))
      using (SolidBrush bD = new SolidBrush(p.Destaque)) using (SolidBrush bW = new SolidBrush(p.Fraco)) using (SolidBrush bX = new SolidBrush(p.Texto)) using (Pen borda = new Pen(Color.FromArgb(150, p.Destaque))) {
        g.DrawRectangle(borda, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        g.DrawString("> ajustes", fT, bD, D(14), D(11));
        string esq = "esc fecha";
        g.DrawString(esq, fN, bW, ClientSize.Width - D(14) - g.MeasureString(esq, fN).Width, D(12));
        foreach (Secao s in secoes) {
          if (s.Chips.Count == 0) continue;
          g.DrawString(s.Titulo, fN, bW, D(14), s.Chips[0].R.Y + D(4));
          foreach (Chip c in s.Chips) {
            bool ativo = c.Ativo != null && c.Ativo();
            Color fundo = ativo ? p.Destaque : c == sobre ? Color.FromArgb(Math.Min(255, p.Superficie.R + 22), Math.Min(255, p.Superficie.G + 22), Math.Min(255, p.Superficie.B + 22)) : p.Superficie;
            using (GraphicsPath cam = Arredondado(c.R, D(4))) using (SolidBrush b = new SolidBrush(fundo)) g.FillPath(b, cam);
            Color txt = ativo ? p.Fundo : c.Perigo ? p.Vermelho : p.Texto;
            TextRenderer.DrawText(g, c.Texto, fN, c.R, txt, fundo, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
          }
        }
      }
    }
    static GraphicsPath Arredondado(Rectangle r, int raio) {
      int d = raio * 2; GraphicsPath c = new GraphicsPath();
      c.AddArc(r.X, r.Y, d, d, 180, 90); c.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
      c.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90); c.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90); c.CloseFigure();
      return c;
    }
    Chip ChipEm(Point pt) { foreach (Secao s in secoes) foreach (Chip c in s.Chips) if (c.R.Contains(pt)) return c; return null; }
    protected override void OnMouseMove(MouseEventArgs e) {
      Chip c = ChipEm(e.Location);
      if (c != sobre) { sobre = c; Cursor = c != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
      base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { sobre = null; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) {
      Chip c = ChipEm(e.Location);
      if (c != null && e.Button == MouseButtons.Left) { sobre = null; c.Acao(); if (!fechando && !IsDisposed) Refazer(); }
      base.OnMouseClick(e);
    }
  }

  class Aplicacao {
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cota-barra");
    const string TamanhoPadrao = "pequena", FundoPadrao = "transparente", EstiloPadrao = "barras", PaletaPadrao = "tokyo";

    readonly FormaPilula forma = new FormaPilula(false);
    readonly FormaPilula cartao = new FormaPilula(true);
    readonly Temporizador relogio = new Temporizador { Interval = 5000 };
    readonly Temporizador gatilho = new Temporizador { Interval = 200 };
    readonly Temporizador arrasto = new Temporizador { Interval = 30 };
    readonly EventWaitHandle sinalFechar;
    Vigia vigia;
    PainelAjustes painel;
    List<string> fontes;

    string vista = "ambas"; int? manual;
    string tema = "auto", paleta = PaletaPadrao, estilo = EstiloPadrao, tamanho = TamanhoPadrao, fundo = FundoPadrao, fonte = "JetBrains Mono", monitor;   // monitor null = o principal
    DateTime escondidaAte = DateTime.MinValue, ultimaVarredura = DateTime.MinValue, ultimoDado = DateTime.MinValue;
    int widgetsDir, bandejaUia; int? iconesEsq, iconesDir;
    string assinatura = "";
    List<Conta> listaAtual = new List<Conta>();
    bool visivel;
    // Posição atual
    int posX, posY, posL, posH, posMargem, posBandeja; Rectangle posBarra; double posEsc = 1;
    // Arrasto
    bool arrastando, moveu; int arrInicio, arrX0, arrXAtual;

    Aplicacao(EventWaitHandle sinal) {
      sinalFechar = sinal;
      LerConfig();
      Historico.Carregar(Path.Combine(Pasta, "historico.tsv"), DateTimeOffset.Now);
      forma.MouseEnter += delegate { MostrarCartao(); };
      forma.MouseLeave += delegate { EsconderCartao(); };
      forma.MouseDown += AoPressionar;
      forma.MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right) AbrirPainel(); };
      arrasto.Tick += delegate { AoArrastar(); };
      gatilho.Tick += delegate {
        gatilho.Stop();
        AtualizarVisibilidade();
        if ((DateTime.Now - ultimaVarredura).TotalSeconds >= 15) { Varrer(); if (visivel) Atualizar(false); }
      };
      vigia = new Vigia(gatilho);
      relogio.Tick += delegate {
        if (sinalFechar.WaitOne(0)) { Fechar(); return; }
        AtualizarVisibilidade();
        DateTime agora = DateTime.Now;
        if (visivel && (agora - ultimaVarredura).TotalSeconds >= 60) Varrer();
        if (visivel && (agora - ultimoDado).TotalSeconds >= 15) { ultimoDado = agora; Atualizar(false); }
      };
      Varrer();
      forma.Shown += delegate { Atualizar(true); visivel = true; forma.Reforcar(); relogio.Start(); AtualizarVisibilidade(); };
    }

    void LerConfig() {
      try {
        Dictionary<string, object> d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(Pasta, "pilula.json"))) as Dictionary<string, object>;
        object o;
        if (d.TryGetValue("deslocManual", out o) && o != null) manual = Convert.ToInt32(o);
        vista = Texto(d, "vista", vista); tema = Texto(d, "tema", tema); paleta = Texto(d, "paleta", paleta); estilo = Texto(d, "estilo", estilo);
        tamanho = Texto(d, "tamanho", tamanho); fundo = Texto(d, "fundo", fundo); fonte = Texto(d, "fonte", fonte); monitor = Texto(d, "monitor", monitor);
      } catch { }
    }
    static string Texto(Dictionary<string, object> d, string chave, string padrao) { object o; return d.TryGetValue(chave, out o) && o is string ? (string)o : padrao; }
    void SalvarConfig() {
      try {
        Directory.CreateDirectory(Pasta);
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["vista"] = vista; d["deslocManual"] = manual; d["tema"] = tema; d["paleta"] = paleta; d["estilo"] = estilo; d["tamanho"] = tamanho; d["fundo"] = fundo; d["fonte"] = fonte; d["monitor"] = monitor;
        File.WriteAllText(Path.Combine(Pasta, "pilula.json"), new JavaScriptSerializer().Serialize(d), new UTF8Encoding(true));   // com BOM, para a versão em PowerShell ler os acentos
      } catch { }
    }
    public static void Registrar(string t) {
      try { Directory.CreateDirectory(Pasta); File.AppendAllText(Path.Combine(Pasta, "pilula.log"), DateTime.Now.ToString("s") + " " + t + Environment.NewLine); } catch { }
    }

    // ---------- Painel de ajustes ----------
    Chip Opcao(string texto, string valor, Func<string> ler, Action<string> gravar) {
      return new Chip { Texto = texto, Ativo = () => ler() == valor, Acao = delegate { gravar(valor); SalvarConfig(); Atualizar(true); } };
    }
    Secao Grupo(string titulo, Func<string> ler, Action<string> gravar, params string[] pares) {
      Secao s = new Secao { Titulo = titulo };
      for (int i = 0; i < pares.Length; i += 2) s.Chips.Add(Opcao(pares[i + 1], pares[i], ler, gravar));
      return s;
    }
    List<Secao> MontarSecoes() {
      List<Secao> l = new List<Secao>();
      List<Conta> contas = Cota.Contas();
      Secao sc = new Secao { Titulo = "contas" };
      sc.Chips.Add(Opcao(contas.Count == 2 ? "as duas" : "todas", "ambas", () => vista, v => vista = v));
      foreach (Conta c in contas) sc.Chips.Add(Opcao(c.Rotulo.ToLowerInvariant(), c.Rotulo, () => vista, v => vista = v));
      l.Add(sc);
      l.Add(MontarMonitores());
      l.Add(Grupo("estilo", () => estilo, v => estilo = v, "barras", "barras", "texto", "só texto"));
      l.Add(Grupo("paleta", () => paleta, v => paleta = v, "tokyo", "tokyo night", "catppuccin", "catppuccin", "terminal", "terminal"));
      l.Add(Grupo("tema", () => tema, v => tema = v, "auto", "igual ao windows", "escuro", "escuro", "claro", "claro"));
      l.Add(Grupo("tamanho", () => tamanho, v => tamanho = v, "pequena", "p", "media", "m", "normal", "g", "grande", "gg"));
      l.Add(Grupo("fundo", () => fundo, v => fundo = v, "opaco", "sólido", "translucido", "translúcido", "transparente", "transparente"));
      if (fontes == null) { fontes = Desenho.FontesMono(); fontes.Add("Segoe UI"); }
      Secao f = new Secao { Titulo = "fonte" };
      foreach (string n in fontes) f.Chips.Add(Opcao(n, n, () => fonte, v => fonte = v));
      l.Add(f);
      Secao acoes = new Secao { Titulo = "ações" };
      acoes.Chips.Add(new Chip { Texto = "abrir com o windows", Ativo = AbreComWindows, Acao = delegate { MudarInicio(!AbreComWindows()); } });
      acoes.Chips.Add(new Chip { Texto = "visual padrão", Acao = delegate {
        tema = "auto"; paleta = PaletaPadrao; estilo = EstiloPadrao; tamanho = TamanhoPadrao; fundo = FundoPadrao; fonte = "JetBrains Mono"; SalvarConfig(); Atualizar(true);
      } });
      acoes.Chips.Add(new Chip { Texto = "posição automática", Acao = delegate { manual = null; SalvarConfig(); Atualizar(true); } });
      acoes.Chips.Add(new Chip { Texto = "esconder 30 min", Acao = delegate { escondidaAte = DateTime.Now.AddMinutes(30); if (painel != null) painel.Encerrar(); AtualizarVisibilidade(); } });
      acoes.Chips.Add(new Chip { Texto = "fechar", Perigo = true, Acao = delegate { Fechar(); } });
      l.Add(acoes);
      return l;
    }
    // Monitores podem entrar e sair: a lista é refeita sempre. Sem barra de tarefas não há onde pousar, então não entra.
    Secao MontarMonitores() {
      Secao s = new Secao { Titulo = "monitor" };
      string atual = Nativo.Dispositivo(BarraAtual());
      Screen[] telas = Screen.AllScreens;
      Array.Sort(telas, (a, b) => string.CompareOrdinal(a.DeviceName, b.DeviceName));
      Screen principal = Screen.PrimaryScreen;
      foreach (Screen t in telas) {
        string dev = t.DeviceName;
        if (Nativo.BarraDe(dev) == IntPtr.Zero) continue;
        string lado = t.Primary ? "principal" : t.Bounds.Left >= principal.Bounds.Right ? "à direita" : t.Bounds.Right <= principal.Bounds.Left ? "à esquerda" : t.Bounds.Top < principal.Bounds.Top ? "acima" : "abaixo";
        s.Chips.Add(new Chip { Texto = dev.Replace(@"\\.\DISPLAY", "") + " " + lado, Ativo = () => dev == atual, Acao = delegate { MudarMonitor(dev); } });
      }
      return s;
    }
    void AbrirPainel() {
      EsconderCartao();
      if (painel != null && !painel.IsDisposed) { painel.Encerrar(); return; }
      painel = new PainelAjustes(MontarSecoes, AparenciaAtual, Posicionar, posEsc);
      painel.FormClosed += delegate { painel = null; };
      Posicionar(painel);
      painel.Show();
      painel.Activate();
      Nativo.SetForegroundWindow(painel.Handle);
      painel.Refazer();
    }
    // Acima da pílula, alinhado à direita dela, sem sair do monitor.
    void Posicionar(PainelAjustes p) {
      Rectangle m = Nativo.Monitor(BarraAtual());
      int x = Math.Min(posX + posL, m.Right - 8) - p.Width;
      p.Location = new Point(Math.Max(m.Left + 8, x), posY - p.Height - (int)(10 * posEsc));
    }

    // A posição arrastada vale para uma barra; na outra, volta para a automática.
    void MudarMonitor(string dev) {
      if (dev == Nativo.Dispositivo(BarraAtual())) return;
      monitor = dev; manual = null; SalvarConfig();
      Varrer(); Atualizar(true); AtualizarVisibilidade();
    }
    // O monitor escolhido; se ele foi desconectado, o principal (a escolha fica guardada para quando voltar).
    IntPtr BarraAtual() {
      if (monitor != null) { IntPtr b = Nativo.BarraDe(monitor); if (b != IntPtr.Zero) return b; }
      return Nativo.Barra();
    }
    Aparencia AparenciaAtual() {
      Aparencia a = new Aparencia { Fonte = fonte, NomePaleta = paleta, Barras = estilo != "texto" };
      a.Letra = tamanho == "pequena" ? 10 : tamanho == "media" ? 11 : tamanho == "grande" ? 13.5 : 12;
      a.AlfaFundo = fundo == "transparente" ? 1 : fundo == "translucido" ? 150 : 245;
      a.Claro = tema == "claro" || (tema == "auto" && WindowsClaro());
      return a;
    }
    // Só do usuário atual: a entrada "CotaBarra" em Run, que também aparece em Aplicativos de inicialização do Windows.
    const string ChaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static bool AbreComWindows() {
      try { using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ChaveRun)) return k != null && k.GetValue("CotaBarra") != null; } catch { return false; }
    }
    static void MudarInicio(bool ligar) {
      try {
        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ChaveRun, true)) {
          if (ligar) k.SetValue("CotaBarra", "\"" + Application.ExecutablePath + "\""); else k.DeleteValue("CotaBarra", false);
        }
      } catch (Exception ex) { Registrar("início com o Windows: " + ex.Message); }
    }
    static bool WindowsClaro() {
      try { object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0); return v is int && (int)v == 1; } catch { return false; }
    }
    void MudarVista(string v) { vista = v; SalvarConfig(); Atualizar(true); }
    void Fechar() { relogio.Stop(); gatilho.Stop(); vigia.Dispose(); Application.Exit(); }

    // ---------- Cartão de detalhe ----------
    void MostrarCartao() {
      if (arrastando || painel != null) return;
      try {
        using (Bitmap b = Desenho.Cartao(listaAtual, posEsc, AparenciaAtual())) {
          Rectangle m = Nativo.Monitor(BarraAtual());
          int x = Math.Max(m.Left + 8, Math.Min(posX + posL, m.Right - 8) - b.Width);
          if (!cartao.Visible) cartao.Show();
          cartao.Pintar(b, x, posY - b.Height - (int)(10 * posEsc));
          cartao.Reforcar();
        }
      } catch (Exception ex) { Registrar("cartão: " + ex.Message); }
    }
    void EsconderCartao() { if (cartao.Visible) cartao.Hide(); }

    // Onde estão os ícones. Roda pouco: a cada 60 s, ou no máximo a cada 15 s quando a janela da frente muda.
    void Varrer() {
      ultimaVarredura = DateTime.Now;
      try {
        AutomationElement raiz = AutomationElement.FromHandle(BarraAtual());
        int? esq = null, dir = null; int wid = 0, band = 0;
        foreach (AutomationElement e in raiz.FindAll(TreeScope.Descendants, Condition.TrueCondition)) {
          AutomationElement.AutomationElementInformation c = e.Current;
          System.Windows.Rect b = c.BoundingRectangle;
          if (b.IsEmpty) continue;
          string id = c.AutomationId ?? "";
          if (id == "WidgetsButton") { wid = (int)b.Right; continue; }
          if (id == "SystemTrayIcon") { if (band == 0 || b.Left < band) band = (int)b.Left; continue; }
          if ((c.ClassName ?? "").Contains("TaskListButton") || id == "StartButton" || id == "SearchButton" || id == "TaskViewButton" || id == "CopilotButton") {
            if (esq == null || b.Left < esq) esq = (int)b.Left;
            if (dir == null || b.Right > dir) dir = (int)b.Right;
          }
        }
        widgetsDir = wid; iconesEsq = esq; iconesDir = dir; bandejaUia = band;
      } catch (Exception ex) { Registrar("varredura: " + ex.Message); }
    }

    // Desenha e posiciona. Só redesenha se algo visível mudou.
    void Atualizar(bool forcar) {
      try {
        IntPtr barra = BarraAtual();
        if (barra == IntPtr.Zero) return;
        Aparencia ap = AparenciaAtual();
        Rectangle r = Nativo.Retangulo(barra);
        uint dpi = Nativo.GetDpiForWindow(barra);
        double esc = dpi / 96.0;
        List<Conta> lista = new List<Conta>();
        List<Conta> todas = Cota.Contas();
        foreach (Conta c in todas) if (vista == "ambas" || vista == c.Rotulo) lista.Add(c);
        if (lista.Count == 0) lista = todas;   // a conta escolhida deixou de existir
        listaAtual = lista;
        StringBuilder sb = new StringBuilder();
        foreach (Conta c in lista) sb.Append(c.Assinatura()).Append(';');
        string sig = sb + "#" + r + "#" + dpi + "#" + ap.Claro + "#" + widgetsDir + "#" + iconesEsq + "#" + iconesDir + "#" + bandejaUia + "#" + manual;
        if (!forcar && sig == assinatura) return;
        assinatura = sig;

        int margem = (int)(8 * esc), alt = r.Height - 2 * (int)(4 * esc);
        int bandeja = Nativo.BandejaEsquerda(barra);
        if (bandejaUia > 0 && (bandeja < 0 || bandejaUia < bandeja)) bandeja = bandejaUia;
        if (bandeja < 0) bandeja = r.Right - (int)(240 * esc);

        Bitmap bmp = null; int? x = null; string origem;
        foreach (bool compacto in new[] { false, true }) {
          if (bmp != null) bmp.Dispose();
          bmp = Desenho.Pilula(lista, esc, alt, compacto, ap);
          x = Cota.Posicao(r.Left, r.Right, bandeja, widgetsDir, iconesEsq, iconesDir, bmp.Width, margem, manual, out origem);
          if (x != null) break;
        }
        using (bmp) {
          posX = x ?? bandeja - margem - bmp.Width;   // sem vão nenhum: fica colada na bandeja
          posY = r.Top + (r.Height - bmp.Height) / 2;
          posL = bmp.Width; posH = bmp.Height; posBarra = r; posMargem = margem; posBandeja = bandeja; posEsc = esc;
          forma.Pintar(bmp, posX, posY);
        }
      } catch (Exception ex) { Registrar("desenho: " + ex.Message); }
    }

    // Some em tela cheia, com a barra escondida ou a pedido; senão garante que está por cima.
    void AtualizarVisibilidade() {
      try {
        IntPtr barra = BarraAtual();
        bool mostrar = barra != IntPtr.Zero;
        if (mostrar) {
          Rectangle r = Nativo.Retangulo(barra), m = Nativo.Monitor(barra);
          if (r.Top >= m.Bottom - 4) mostrar = false;   // barra em ocultar automaticamente
          else if (Nativo.TelaCheia(barra, Nativo.GetForegroundWindow())) mostrar = false;
          else if (DateTime.Now < escondidaAte) mostrar = false;
        }
        if (mostrar != visivel) {
          visivel = mostrar;
          if (mostrar) { forma.Show(); Atualizar(true); } else { EsconderCartao(); forma.Hide(); }
        }
        if (mostrar) forma.Reforcar();
      } catch (Exception ex) { Registrar("visibilidade: " + ex.Message); }
    }

    // Clique alterna; arrastar move. O movimento é lido por um timer só enquanto o botão está preso.
    void AoPressionar(object s, MouseEventArgs e) {
      EsconderCartao();
      if (e.Button != MouseButtons.Left) return;
      arrastando = true; moveu = false; arrInicio = Cursor.Position.X; arrX0 = posX; arrXAtual = posX;
      arrasto.Start();
    }
    void AoArrastar() {
      if (!arrastando) { arrasto.Stop(); return; }
      int cx = Cursor.Position.X;
      if ((Control.MouseButtons & MouseButtons.Left) != 0) {
        if (Math.Abs(cx - arrInicio) > 4) moveu = true;
        if (moveu) {
          arrXAtual = Math.Max(posBarra.Left, Math.Min(arrX0 + cx - arrInicio, posBandeja - posMargem - posL));
          forma.Mover(arrXAtual, posY);
        }
        return;
      }
      arrasto.Stop(); arrastando = false;
      if (moveu) { manual = posBarra.Right - arrXAtual - posL; SalvarConfig(); Atualizar(true); }
      else {
        List<string> ordem = new List<string> { "ambas" };
        foreach (Conta c in Cota.Contas()) ordem.Add(c.Rotulo);
        MudarVista(ordem[(ordem.IndexOf(vista) + 1) % ordem.Count]);
      }
    }

    [STAThread]
    static void Main() {
      try { Nativo.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
      bool novo;
      using (Mutex mutex = new Mutex(true, @"Local\CotaBarra.pilula", out novo)) {
        if (!novo) return;
        using (EventWaitHandle sinal = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\CotaBarra.pilula.fechar")) {
          sinal.Reset();
          string arqPid = Path.Combine(Pasta, "pilula.pid");
          Directory.CreateDirectory(Pasta);
          File.WriteAllText(arqPid, System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
          Application.EnableVisualStyles();
          Application.SetCompatibleTextRenderingDefault(false);
          Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
          Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { Registrar("exceção: " + e.Exception); };
          try { Application.Run(new Aplicacao(sinal).forma); }
          catch (Exception ex) { Registrar("fim: " + ex); }
          try { File.Delete(arqPid); } catch { }
          mutex.ReleaseMutex();
        }
      }
    }
  }
}
