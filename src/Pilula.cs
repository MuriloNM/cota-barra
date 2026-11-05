// A pílula sobre a barra de tarefas.
// Clique: alterna as contas. Arrastar: muda de lugar. Botão direito: menu.
using System;
using System.Collections.Generic;
using System.Drawing;
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

  // Janela sem borda, sempre visível, que não rouba o foco e tem bordas suaves.
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

    public FormaPilula() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None; Text = "Cota da semana"; }
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

  class Aplicacao {
    static readonly string Pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cota-barra");
    static readonly string[] Ordem = { "ambas", "Padrão", "Trabalho" };

    readonly FormaPilula forma = new FormaPilula();
    readonly ToolTip dica = new ToolTip { ShowAlways = true };
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly Temporizador relogio = new Temporizador { Interval = 5000 };
    readonly Temporizador gatilho = new Temporizador { Interval = 200 };
    readonly Temporizador arrasto = new Temporizador { Interval = 30 };
    readonly EventWaitHandle sinalFechar;
    Vigia vigia;
    ToolStripMenuItem itemAmbas, itemPad, itemTrab;

    string vista = "ambas"; int? manual;
    string tema = "auto", tamanho = "normal", fundo = "opaco", fonte = "JetBrains Mono", monitor;   // monitor null = o principal
    ToolStripMenuItem subMonitor, subFonte;
    readonly List<Action> marcas = new List<Action>();
    DateTime escondidaAte = DateTime.MinValue, ultimaVarredura = DateTime.MinValue, ultimoDado = DateTime.MinValue;
    int widgetsDir, bandejaUia; int? iconesEsq, iconesDir;
    string assinatura = "", textoDica = "";
    bool visivel;
    // Posição atual
    int posX, posY, posL, posMargem, posBandeja; Rectangle posBarra;
    // Arrasto
    bool arrastando, moveu; int arrInicio, arrX0, arrXAtual;

    Aplicacao(EventWaitHandle sinal) {
      sinalFechar = sinal;
      LerConfig();
      MontarMenu();
      forma.MouseEnter += delegate {
        int alto = TextRenderer.MeasureText(textoDica, SystemFonts.StatusFont).Height + 24;
        dica.Show(textoDica, forma, 0, -alto, 15000);   // acima da pílula, sem cobrir o texto nem a bandeja
      };
      forma.MouseLeave += delegate { dica.Hide(forma); };
      forma.MouseDown += AoPressionar;
      forma.MouseUp += delegate(object s, MouseEventArgs e) {
        if (e.Button != MouseButtons.Right) return;
        Nativo.SetForegroundWindow(forma.Handle);   // o menu só fecha ao clicar fora com o processo em primeiro plano
        menu.Show(Cursor.Position);
      };
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
        if (d.TryGetValue("vista", out o) && o is string) vista = (string)o;
        if (d.TryGetValue("deslocManual", out o) && o != null) manual = Convert.ToInt32(o);
        if (d.TryGetValue("tema", out o) && o is string) tema = (string)o;
        if (d.TryGetValue("tamanho", out o) && o is string) tamanho = (string)o;
        if (d.TryGetValue("fundo", out o) && o is string) fundo = (string)o;
        if (d.TryGetValue("fonte", out o) && o is string) fonte = (string)o;
        if (d.TryGetValue("monitor", out o) && o is string) monitor = (string)o;
      } catch { }
    }
    void SalvarConfig() {
      try {
        Directory.CreateDirectory(Pasta);
        Dictionary<string, object> d = new Dictionary<string, object>();
        d["vista"] = vista; d["deslocManual"] = manual; d["tema"] = tema; d["tamanho"] = tamanho; d["fundo"] = fundo; d["fonte"] = fonte; d["monitor"] = monitor;
        File.WriteAllText(Path.Combine(Pasta, "pilula.json"), new JavaScriptSerializer().Serialize(d), new UTF8Encoding(true));   // com BOM, para a versão em PowerShell ler os acentos
      } catch { }
    }
    public static void Registrar(string t) {
      try { Directory.CreateDirectory(Pasta); File.AppendAllText(Path.Combine(Pasta, "pilula.log"), DateTime.Now.ToString("s") + " " + t + Environment.NewLine); } catch { }
    }

    void MontarMenu() {
      itemAmbas = new ToolStripMenuItem("As duas", null, delegate { MudarVista("ambas"); });
      itemPad = new ToolStripMenuItem("Só Padrão", null, delegate { MudarVista("Padrão"); });
      itemTrab = new ToolStripMenuItem("Só Trabalho", null, delegate { MudarVista("Trabalho"); });
      ToolStripMenuItem subContas = new ToolStripMenuItem("Contas");
      subContas.DropDownItems.AddRange(new ToolStripItem[] { itemAmbas, itemPad, itemTrab });
      subMonitor = new ToolStripMenuItem("Monitor");
      subFonte = new ToolStripMenuItem("Fonte");
      menu.Items.Add(subContas);
      menu.Items.Add(subMonitor);
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(Opcoes("Tema", new[] { "auto", "Igual ao Windows", "escuro", "Escuro", "claro", "Claro" }, () => tema, v => tema = v));
      menu.Items.Add(Opcoes("Tamanho", new[] { "pequena", "Pequeno", "media", "Médio", "normal", "Normal", "grande", "Grande" }, () => tamanho, v => tamanho = v));
      menu.Items.Add(Opcoes("Fundo", new[] { "opaco", "Sólido", "translucido", "Translúcido", "transparente", "Transparente" }, () => fundo, v => fundo = v));
      menu.Items.Add(subFonte);
      menu.Items.Add("Voltar ao visual padrão", null, delegate {
        tema = "auto"; tamanho = "normal"; fundo = "opaco"; fonte = "JetBrains Mono";
        foreach (Action m in marcas) m();
        SalvarConfig(); Atualizar(true);
      });
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add("Esconder por 30 min", null, delegate { escondidaAte = DateTime.Now.AddMinutes(30); AtualizarVisibilidade(); });
      menu.Items.Add("Voltar à posição automática", null, delegate { manual = null; SalvarConfig(); Atualizar(true); });
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add("Fechar", null, delegate { Fechar(); });
      // Monitores podem entrar e sair: a lista é refeita a cada abertura. As fontes, só na primeira.
      menu.Opening += delegate { MontarMonitores(); if (subFonte.DropDownItems.Count == 0) MontarFontes(); };
      subMonitor.DropDownItems.Add("-");   // para aparecer a seta do submenu
      subFonte.DropDownItems.Clear();
      MarcarMenu();
    }
    void MarcarMenu() { itemAmbas.Checked = vista == "ambas"; itemPad.Checked = vista == "Padrão"; itemTrab.Checked = vista == "Trabalho"; }
    void MudarVista(string v) { vista = v; SalvarConfig(); MarcarMenu(); Atualizar(true); }

    // Submenu de escolha única. pares = valor, rótulo, valor, rótulo...
    ToolStripMenuItem Opcoes(string titulo, string[] pares, Func<string> ler, Action<string> gravar) {
      ToolStripMenuItem sub = new ToolStripMenuItem(titulo);
      List<ToolStripMenuItem> itens = new List<ToolStripMenuItem>();
      Action marcar = delegate { for (int i = 0; i < itens.Count; i++) itens[i].Checked = pares[i * 2] == ler(); };
      for (int i = 0; i < pares.Length; i += 2) {
        string valor = pares[i];
        itens.Add(new ToolStripMenuItem(pares[i + 1], null, delegate { gravar(valor); marcar(); SalvarConfig(); Atualizar(true); }));
      }
      sub.DropDownItems.AddRange(itens.ToArray());
      marcas.Add(marcar);
      marcar();
      return sub;
    }

    void MontarFontes() {
      List<string> nomes = Desenho.FontesMono();
      nomes.Add("Segoe UI");
      string[] pares = new string[nomes.Count * 2];
      for (int i = 0; i < nomes.Count; i++) { pares[i * 2] = nomes[i]; pares[i * 2 + 1] = nomes[i]; }
      ToolStripMenuItem pronto = Opcoes("Fonte", pares, () => fonte, v => fonte = v);
      while (pronto.DropDownItems.Count > 0) subFonte.DropDownItems.Add(pronto.DropDownItems[0]);
    }

    // Lista os monitores que o Windows enxerga agora. Sem barra de tarefas não há onde pousar: fica desabilitado.
    void MontarMonitores() {
      subMonitor.DropDownItems.Clear();
      string atual = Nativo.Dispositivo(BarraAtual());
      Screen[] telas = Screen.AllScreens;
      Array.Sort(telas, (a, b) => string.CompareOrdinal(a.DeviceName, b.DeviceName));
      Screen principal = Screen.PrimaryScreen;
      foreach (Screen t in telas) {
        string dev = t.DeviceName;
        string lado = t.Primary ? "principal" : t.Bounds.Left >= principal.Bounds.Right ? "à direita" : t.Bounds.Right <= principal.Bounds.Left ? "à esquerda" : t.Bounds.Top < principal.Bounds.Top ? "acima" : "abaixo";
        bool temBarra = Nativo.BarraDe(dev) != IntPtr.Zero;
        ToolStripMenuItem it = new ToolStripMenuItem("Monitor " + dev.Replace(@"\\.\DISPLAY", "") + " (" + lado + ", " + t.Bounds.Width + "×" + t.Bounds.Height + ")" + (temBarra ? "" : " sem barra de tarefas"), null, delegate { MudarMonitor(dev); });
        it.Checked = dev == atual;
        it.Enabled = temBarra;
        subMonitor.DropDownItems.Add(it);
      }
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
      Aparencia a = new Aparencia { Fonte = fonte };
      a.Letra = tamanho == "pequena" ? 10 : tamanho == "media" ? 11 : tamanho == "grande" ? 13.5 : 12;
      a.AlfaFundo = fundo == "transparente" ? 1 : fundo == "translucido" ? 150 : 245;
      a.Claro = tema == "claro" || (tema == "auto" && WindowsClaro());
      return a;
    }
    static bool WindowsClaro() {
      try { object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0); return v is int && (int)v == 1; } catch { return false; }
    }
    void Fechar() { relogio.Stop(); gatilho.Stop(); vigia.Dispose(); Application.Exit(); }

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
        foreach (Conta c in Cota.Contas()) if (vista == "ambas" || vista == c.Rotulo) lista.Add(c);
        StringBuilder sb = new StringBuilder();
        foreach (Conta c in lista) sb.Append(c.Assinatura()).Append(';');
        string sig = sb + "#" + r + "#" + dpi + "#" + ap.Claro + "#" + widgetsDir + "#" + iconesEsq + "#" + iconesDir + "#" + bandejaUia + "#" + manual;
        if (!forcar && sig == assinatura) return;
        assinatura = sig;

        sb.Clear();
        foreach (Conta c in lista) { if (sb.Length > 0) sb.Append("\n\n"); sb.Append(c.Detalhe); }
        textoDica = sb.ToString();

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
          posL = bmp.Width; posBarra = r; posMargem = margem; posBandeja = bandeja;
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
          if (mostrar) { forma.Show(); Atualizar(true); } else forma.Hide();
        }
        if (mostrar) forma.Reforcar();
      } catch (Exception ex) { Registrar("visibilidade: " + ex.Message); }
    }

    // Clique alterna; arrastar move. O movimento é lido por um timer só enquanto o botão está preso.
    void AoPressionar(object s, MouseEventArgs e) {
      dica.Hide(forma);
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
      else MudarVista(Ordem[(Array.IndexOf(Ordem, vista) + 1) % Ordem.Length]);
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
