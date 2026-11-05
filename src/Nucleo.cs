// Leitura do arquivo, fórmula, posição e desenho da pílula e do cartão de detalhe.
// Só LÊ o arquivo de cota. Não usa rede nem credenciais.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CotaBarra {
  public class Leitura { public double? Usado; public string ResetsAt; public long? Lido; }

  public class Paleta {
    public Color Fundo, Superficie, Texto, Fraco, Destaque, Verde, Amarelo, Vermelho, Alerta;
    static Color H(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
    static Paleta P(int f, int s, int t, int w, int d, int g, int y, int r, int a) {
      return new Paleta { Fundo = H(f), Superficie = H(s), Texto = H(t), Fraco = H(w), Destaque = H(d), Verde = H(g), Amarelo = H(y), Vermelho = H(r), Alerta = H(a) };
    }
    public static Paleta De(string nome, bool claro) {
      switch (nome) {
        case "catppuccin": return claro
          ? P(0xeff1f5, 0xccd0da, 0x4c4f69, 0x6c6f85, 0x8839ef, 0x40a02b, 0xdf8e1d, 0xd20f39, 0xfe640b)
          : P(0x1e1e2e, 0x313244, 0xcdd6f4, 0x9399b2, 0xcba6f7, 0xa6e3a1, 0xf9e2af, 0xf38ba8, 0xfab387);
        case "terminal": return claro
          ? P(0xf5f5f3, 0xe4e4e0, 0x1e1e1e, 0x5f5f5f, 0x168039, 0x168039, 0x9a6700, 0xcf222e, 0xaa5f00)
          : P(0x0c0c0c, 0x262626, 0xe1e1e1, 0x8c8c8c, 0x3fb950, 0x3fb950, 0xe3b341, 0xf85149, 0xf0aa46);
        default: return claro   // Tokyo Night
          ? P(0xe1e2e7, 0xd0d1d8, 0x343b58, 0x6172b0, 0x2e7de9, 0x587539, 0x8c6c3e, 0xc64343, 0xb15c00)
          : P(0x1a1b26, 0x2a2e42, 0xc0caf5, 0x7a83a8, 0x7aa2f7, 0x9ece6a, 0xe0af68, 0xf7768e, 0xff9e64);
      }
    }
    public Color DoRitmo(string cor) { return cor == "verde" ? Verde : cor == "amarelo" ? Amarelo : cor == "vermelho" ? Vermelho : Fraco; }
  }

  // Como a pílula é desenhada. AlfaFundo 1 = transparente, mas ainda recebe o mouse (com 0 o clique atravessaria).
  public class Aparencia {
    public string Fonte = "JetBrains Mono", NomePaleta = "tokyo";
    public double Letra = 10; public int AlfaFundo = 1; public bool Claro, Barras = true;
    public Paleta Cores { get { return Paleta.De(NomePaleta, Claro); } }
  }

  public class Conta {
    public string Rotulo, Estado = "sem", Cor = "cinza", Idade = "", Detalhe, RestaTxt = "", LidaTxt = "";   // Estado: ok | velha | vencida | sem
    public int Usado, Esperado, Projecao, Diferenca;
    public string Assinatura() { return Rotulo + "|" + Estado + "|" + Cor + "|" + Usado + "|" + Esperado + "|" + Projecao + "|" + Idade; }
  }

  public static class Cota {
    public static readonly string Arquivo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".claude\plugins\store\cota_inline-633892c87774.json");
    public const double SemanaMs = 604800000.0;
    public const int VelhaDepoisMin = 120;   // leitura com mais de 2 h aparece como velha
    static readonly string[,] Chaves = { { "ultima:padrao", "Padrão" }, { "ultima:Trabalho", "Trabalho" } };

    public static int Inteiro(double v) { return (int)Math.Round(v, 0, MidpointRounding.AwayFromZero); }

    public static string FormatIdade(int min) {
      if (min < 1) return "agora";
      if (min < 60) return min + " min";
      if (min < 2880) return (min / 60) + " h";
      return (min / 1440) + " d";
    }

    // Fórmula igual à do plugin.
    public static Conta Calcular(Leitura l, string rotulo, DateTimeOffset agora) {
      Conta c = new Conta { Rotulo = rotulo, Detalhe = rotulo + ": sem leitura ainda. Abra o Claude nesta conta." };
      if (l == null || l.Usado == null || string.IsNullOrEmpty(l.ResetsAt) || l.Lido == null) return c;
      DateTimeOffset reset;
      if (!DateTimeOffset.TryParse(l.ResetsAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out reset)) return c;

      long agoraMs = agora.ToUnixTimeMilliseconds();
      int min = (int)Math.Max(Math.Floor((agoraMs - l.Lido.Value) / 60000.0), 0);
      c.Idade = FormatIdade(min);
      c.LidaTxt = c.Idade == "agora" ? "lida agora" : "lida há " + c.Idade;
      if (reset <= agora) {
        c.Estado = "vencida";
        c.Detalhe = rotulo + ": leitura vencida (a semana já virou). Abra o Claude nesta conta.";
        return c;
      }
      double usado = l.Usado.Value;
      double resta = (reset - agora).TotalMilliseconds;
      double decorrido = Math.Min(Math.Max(1 - resta / SemanaMs, 0), 1);
      double esperado = decorrido * 100;
      double projecao = decorrido > 0.02 ? usado / decorrido : usado;
      double dif = usado - esperado;
      c.Cor = dif < -5 ? "verde" : dif > 5 ? "vermelho" : "amarelo";
      c.Usado = Inteiro(usado); c.Esperado = Inteiro(esperado); c.Projecao = Inteiro(projecao); c.Diferenca = Inteiro(dif);
      c.Estado = min > VelhaDepoisMin ? "velha" : "ok";

      double dias = resta / 86400000;
      c.RestaTxt = dias < 1 ? Inteiro(dias * 24) + " h" : dias.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " dias";
      c.Detalhe = rotulo + ": " + c.Usado + "% usado · esperado " + c.Esperado + "% · projeção " + c.Projecao + "%\n"
                + "reseta em " + c.RestaTxt + " · " + c.LidaTxt + (c.Estado == "velha" ? " (VELHA: abra o Claude)" : "");
      return c;
    }

    // Lê sem bloquear quem grava. Só relê quando a data muda; se vier quebrado, mantém o último bom.
    static string cacheCaminho; static DateTime cacheData; static Dictionary<string, object> cacheJson;
    static Dictionary<string, object> LerArquivo(string caminho) {
      try {
        DateTime data = File.GetLastWriteTimeUtc(caminho);
        if (caminho == cacheCaminho && data == cacheData && cacheJson != null) return cacheJson;
        string texto;
        using (FileStream fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8)) texto = sr.ReadToEnd();
        Dictionary<string, object> json = new JavaScriptSerializer().DeserializeObject(texto) as Dictionary<string, object>;
        if (json != null) { cacheJson = json; cacheData = data; cacheCaminho = caminho; }
      } catch { }
      return caminho == cacheCaminho ? cacheJson : null;
    }

    static Leitura Extrair(Dictionary<string, object> json, string chave) {
      object o;
      if (json == null || !json.TryGetValue(chave, out o)) return null;
      Dictionary<string, object> d = o as Dictionary<string, object>;
      if (d == null) return null;
      Leitura l = new Leitura();
      try {
        if (d.TryGetValue("percentUsed", out o) && o != null) l.Usado = Convert.ToDouble(o, CultureInfo.InvariantCulture);
        if (d.TryGetValue("resetsAt", out o) && o != null) l.ResetsAt = Convert.ToString(o, CultureInfo.InvariantCulture);
        if (d.TryGetValue("lido", out o) && o != null) l.Lido = Convert.ToInt64(o, CultureInfo.InvariantCulture);
      } catch { return null; }
      return l;
    }

    public static List<Conta> Contas(DateTimeOffset agora, string caminho) {
      Dictionary<string, object> json = LerArquivo(caminho);
      List<Conta> lista = new List<Conta>();
      for (int i = 0; i < Chaves.GetLength(0); i++) lista.Add(Calcular(Extrair(json, Chaves[i, 0]), Chaves[i, 1], agora));
      return lista;
    }
    public static List<Conta> Contas() { return Contas(DateTimeOffset.Now, Arquivo); }

    // Ordem: posição escolhida pelo usuário, à esquerda da bandeja, à direita dos widgets. null = não cabe.
    public static int? Posicao(int barraEsq, int barraDir, int bandejaEsq, int widgetsDir, int? iconesEsq, int? iconesDir, int largura, int margem, int? manual, out string origem) {
      int min = Math.Max(barraEsq, widgetsDir) + margem, max = bandejaEsq - margem;
      Func<int, bool> livre = x => {
        if (x < min || x + largura > max) return false;
        if (iconesEsq == null) return true;
        return x + largura + margem <= iconesEsq.Value || x - margem >= iconesDir.Value;
      };
      if (manual != null) {
        int xm = barraDir - manual.Value - largura;
        if (livre(xm)) { origem = "manual"; return xm; }
      }
      if (livre(max - largura)) { origem = "direita"; return max - largura; }
      if (livre(min)) { origem = "esquerda"; return min; }
      origem = null; return null;
    }
  }

  public static class Desenho {
    class Seg { public string Rotulo = "", Usado = "", Resto = "", Idade = ""; public Conta Conta; public bool TemBarra; }

    static bool TemNumero(Conta c) { return c.Estado == "ok" || c.Estado == "velha"; }

    static Seg Segmentos(Conta c, bool rotulo, bool compacto, bool barras) {
      Seg s = new Seg { Conta = c, Rotulo = rotulo ? c.Rotulo.ToLowerInvariant() : "" };
      if (!TemNumero(c)) {
        s.Usado = "--";
        if (!compacto) s.Resto = c.Estado == "sem" ? "sem leitura" : "vencida";
        return s;
      }
      s.Usado = c.Usado + "%";
      if (compacto) { s.Rotulo = ""; return s; }
      s.TemBarra = barras;
      s.Resto = barras ? "→ " + c.Projecao + "%" : "/ " + c.Esperado + "% → " + c.Projecao + "%";
      s.Idade = c.Estado == "velha" ? "velha " + c.Idade : c.Idade;
      return s;
    }
    static int Medir(Graphics g, string t, Font f, StringFormat fmt) {
      return t.Length == 0 ? 0 : (int)Math.Ceiling(g.MeasureString(t, f, 4000, fmt).Width);
    }
    // A primeira fonte instalada da lista. new Font() trocaria em silêncio por uma sem serifa.
    public static string FonteMono(string preferida) {
      foreach (string nome in new[] { preferida, "JetBrains Mono", "Cascadia Mono", "Consolas" }) {
        if (string.IsNullOrEmpty(nome)) continue;
        try { using (FontFamily f = new FontFamily(nome)) return f.Name; } catch (ArgumentException) { }
      }
      return FontFamily.GenericMonospace.Name;
    }
    // Fontes de largura fixa instaladas, sem variações de peso nem fontes de símbolos ou só de CJK.
    public static List<string> FontesMono() {
      Regex fora = new Regex(@"(ExtraLight|SemiLight|Light|Medium|SemiBold|ExtraBold|Thin|Black)$|Marlett|MT Extra|Outlook|Icons|MDL2|Wingdings|Webdings|Symbol|MingLiU|SimSun|Gothic|Holo", RegexOptions.IgnoreCase);
      List<string> lista = new List<string>();
      using (InstalledFontCollection fontes = new InstalledFontCollection())
      using (Bitmap b = new Bitmap(1, 1)) using (Graphics g = Graphics.FromImage(b)) {
        foreach (FontFamily fam in fontes.Families) {
          if (fora.IsMatch(fam.Name) || !fam.IsStyleAvailable(FontStyle.Regular)) continue;
          using (Font f = new Font(fam, 20, FontStyle.Regular, GraphicsUnit.Pixel)) {
            float i = g.MeasureString("iiiiiiii", f, 4000, StringFormat.GenericTypographic).Width;
            float w = g.MeasureString("WWWWWWWW", f, 4000, StringFormat.GenericTypographic).Width;
            if (Math.Abs(i - w) < 1) lista.Add(fam.Name);
          }
        }
      }
      return lista;
    }
    // O sinal no lugar do prompt: > leitura boa, ~ leitura velha, x sem número válido.
    static string Prompt(Conta c) { return c.Estado == "ok" ? ">" : c.Estado == "velha" ? "~" : "x"; }

    // Barra em células: cheia até o usado, apagada até a projeção, trilha no resto e um risco no esperado de hoje.
    public static void Barra(Graphics g, int x, int y, int cel, int vao, int alto, int n, Conta c, Color cor, Color trilha, Color risco) {
      SmoothingMode antes = g.SmoothingMode;
      g.SmoothingMode = SmoothingMode.None;
      int cheias = Math.Min(n, Cota.Inteiro(c.Usado / 100.0 * n));
      int proj = Math.Max(cheias, Math.Min(n, Cota.Inteiro(c.Projecao / 100.0 * n)));
      using (SolidBrush bCor = new SolidBrush(cor)) using (SolidBrush bProj = new SolidBrush(Color.FromArgb(95, cor))) using (SolidBrush bTrilha = new SolidBrush(trilha)) {
        for (int i = 0; i < n; i++)
          g.FillRectangle(i < cheias ? bCor : i < proj ? bProj : bTrilha, x + i * (cel + vao), y, cel, alto);
      }
      int total = n * cel + (n - 1) * vao;
      int xr = x + Math.Min(total - 1, (int)(c.Esperado / 100.0 * total));
      int folga = Math.Max(1, alto / 4);
      using (Pen p = new Pen(risco, Math.Max(1, cel / 3))) g.DrawLine(p, xr, y - folga, xr, y + alto + folga - 1);
      g.SmoothingMode = antes;
    }
    public static int LarguraBarra(int cel, int vao, int n) { return n * cel + (n - 1) * vao; }

    static void Moldura(Graphics g, int w, int h, int raioPx, Color fundo, Color borda, bool comBorda) {
      int d = raioPx * 2, w1 = w - 1, h1 = h - 1;
      using (GraphicsPath cam = new GraphicsPath()) {
        cam.AddArc(0, 0, d, d, 180, 90); cam.AddArc(w1 - d, 0, d, d, 270, 90);
        cam.AddArc(w1 - d, h1 - d, d, d, 0, 90); cam.AddArc(0, h1 - d, d, d, 90, 90); cam.CloseFigure();
        using (SolidBrush b = new SolidBrush(fundo)) g.FillPath(b, cam);
        if (comBorda) using (Pen p = new Pen(borda)) g.DrawPath(p, cam);
      }
    }

    public static Bitmap Pilula(IList<Conta> lista, double esc, int alturaMax, bool compacto, Aparencia a) {
      bool duas = lista.Count > 1;
      Paleta p = a.Cores;
      float px = (float)((duas ? a.Letra : a.Letra + 2) * esc);
      string fonte = FonteMono(a.Fonte);

      using (Font fNormal = new Font(fonte, px, FontStyle.Regular, GraphicsUnit.Pixel))
      using (Font fForte = new Font(fonte, px, FontStyle.Bold, GraphicsUnit.Pixel))
      using (StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone()) {
        fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        List<Seg> linhas = new List<Seg>();
        foreach (Conta c in lista) linhas.Add(Segmentos(c, duas, compacto, a.Barras));
        const int Celulas = 20;
        int cel = Math.Max(2, (int)(px * 0.30)), vaoCel = Math.Max(1, (int)(px * 0.10));
        bool algumaBarra = false;
        int wRot = 0, wUso = 0, wResto = 0, wIdade = 0, wPrompt, altura;
        using (Bitmap med = new Bitmap(1, 1)) using (Graphics gm = Graphics.FromImage(med)) {
          foreach (Seg s in linhas) {
            algumaBarra |= s.TemBarra;
            wRot = Math.Max(wRot, Medir(gm, s.Rotulo, fNormal, fmt));
            wUso = Math.Max(wUso, Medir(gm, s.Usado, fForte, fmt));
            wResto = Math.Max(wResto, Medir(gm, s.Resto, fNormal, fmt));
            wIdade = Math.Max(wIdade, Medir(gm, s.Idade, fNormal, fmt));
          }
          wPrompt = Medir(gm, ">", fForte, fmt);
          // A altura acompanha a letra, sem passar do que cabe na barra.
          altura = Math.Min(alturaMax, (int)Math.Ceiling(fNormal.GetHeight(gm) * linhas.Count + 2 * (int)(4 * esc)));
        }
        int pad = (int)(9 * esc), vao = (int)(6 * esc), wBarra = algumaBarra ? LarguraBarra(cel, vaoCel, Celulas) : 0;
        int x1 = pad + wPrompt + vao;
        int xB = x1 + (wRot > 0 ? wRot + vao : 0);
        int x2 = xB + (wBarra > 0 ? wBarra + vao : 0);
        int x3 = x2 + wUso + (wResto > 0 ? (int)(4 * esc) : 0);
        int x4 = x3 + wResto + (wIdade > 0 ? vao * 2 : 0);
        int largura = x4 + wIdade + pad;

        Bitmap bmp = new Bitmap(largura, altura, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
          g.SmoothingMode = SmoothingMode.AntiAlias;
          g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
          Moldura(g, largura, altura, (int)(4 * esc), Color.FromArgb(a.AlfaFundo, p.Fundo), Color.FromArgb(80, p.Fraco), a.AlfaFundo > 1);
          float altLinha = fNormal.GetHeight(g), topo = (altura - altLinha * linhas.Count) / 2;
          Color trilha = a.AlfaFundo > 200 ? p.Superficie : Color.FromArgb(70, p.Fraco);
          using (SolidBrush bForte = new SolidBrush(p.Texto)) using (SolidBrush bFraco = new SolidBrush(p.Fraco)) using (SolidBrush bAlerta = new SolidBrush(p.Alerta)) {
            for (int i = 0; i < linhas.Count; i++) {
              Seg s = linhas[i]; Conta c = s.Conta;
              float y = topo + i * altLinha;
              bool ok = c.Estado == "ok";
              Color cor = ok ? p.DoRitmo(c.Cor) : p.Fraco;   // só a leitura boa fica colorida: a cor de uma leitura velha não é a do agora
              using (SolidBrush bCor = new SolidBrush(cor)) {
                g.DrawString(Prompt(c), fForte, c.Estado == "velha" ? bAlerta : bCor, pad, y, fmt);
                g.DrawString(s.Usado, fForte, bCor, x2 + wUso - Medir(g, s.Usado, fForte, fmt), y, fmt);   // à direita, colado no que vem depois
              }
              if (s.TemBarra) {
                int altoBarra = (int)(px * 0.62);
                Barra(g, xB, (int)(y + (altLinha - altoBarra) / 2), cel, vaoCel, altoBarra, Celulas, c, cor, trilha, p.Texto);
              }
              if (s.Rotulo.Length > 0) g.DrawString(s.Rotulo, fNormal, bFraco, x1, y, fmt);
              if (s.Resto.Length > 0) g.DrawString(s.Resto, fNormal, ok ? bForte : bFraco, x3, y, fmt);
              if (s.Idade.Length > 0) g.DrawString(s.Idade, fNormal, c.Estado == "velha" ? bAlerta : bFraco, x4, y, fmt);
            }
          }
        }
        return bmp;
      }
    }

    // O cartão que aparece ao passar o mouse: sempre com fundo sólido, para ler bem sobre qualquer coisa.
    public static Bitmap Cartao(IList<Conta> lista, double esc, Aparencia a) {
      Paleta p = a.Cores;
      float px = (float)(13 * esc);
      string fonte = FonteMono(a.Fonte);
      using (Font fN = new Font(fonte, px, FontStyle.Regular, GraphicsUnit.Pixel))
      using (Font fF = new Font(fonte, px, FontStyle.Bold, GraphicsUnit.Pixel))
      using (Font fP = new Font(fonte, (float)(11 * esc), FontStyle.Regular, GraphicsUnit.Pixel))
      using (StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone()) {
        fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        int pad = (int)(14 * esc), entre = (int)(14 * esc), n = 36, cel = (int)(6 * esc), vao = Math.Max(1, (int)(1.5 * esc)), altoBarra = (int)(9 * esc);
        int wBarra = LarguraBarra(cel, vao, n);
        const string Legenda = "cheio: usado · claro: projeção · risco: esperado";
        const int Sep = 3;
        Func<Conta, string> linhaRitmo = c => {
          if (c.Estado == "sem") return "sem leitura: abra o Claude nesta conta";
          if (c.Estado == "vencida") return "semana vencida: abra o Claude nesta conta";
          string r = c.Diferenca < -5 ? (-c.Diferenca) + " pts de folga" : c.Diferenca > 5 ? c.Diferenca + " pts acima do ritmo" : "no ritmo";
          return c.Estado == "velha" ? "leitura velha, " + c.LidaTxt + ": abra o Claude" : r + " · " + c.LidaTxt;
        };
        int larg = wBarra, alt;
        float hN, hP;
        using (Bitmap med = new Bitmap(1, 1)) using (Graphics gm = Graphics.FromImage(med)) {
          hN = fN.GetHeight(gm); hP = fP.GetHeight(gm);
          foreach (Conta c in lista) larg = Math.Max(larg, Medir(gm, linhaRitmo(c), fN, fmt));
          larg = Math.Max(larg, Medir(gm, Legenda, fP, fmt));
        }
        alt = pad * 2 + (int)(hP + 10 * esc);
        foreach (Conta c in lista)
          alt += (int)(hN * 2) + (int)(4 * esc) + entre + (TemNumero(c) ? (int)hN + altoBarra + (int)(8 * esc) : 0);
        alt -= entre;
        int w = larg + pad * 2;
        Bitmap bmp = new Bitmap(w, alt, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
          g.SmoothingMode = SmoothingMode.AntiAlias;
          g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
          Moldura(g, w, alt, (int)(6 * esc), Color.FromArgb(250, p.Fundo), Color.FromArgb(150, p.Destaque), true);
          using (SolidBrush bT = new SolidBrush(p.Texto)) using (SolidBrush bW = new SolidBrush(p.Fraco)) using (SolidBrush bA = new SolidBrush(p.Alerta)) {
            float y = pad;
            foreach (Conta c in lista) {
              Color cor = c.Estado == "ok" ? p.DoRitmo(c.Cor) : p.Fraco;
              g.DrawString(c.Rotulo.ToLowerInvariant(), fF, bT, pad, y, fmt);
              if (TemNumero(c)) {
                string dir = "reseta em " + c.RestaTxt;
                g.DrawString(dir, fN, bW, w - pad - Medir(g, dir, fN, fmt), y, fmt);
              }
              y += hN + (int)(4 * esc);
              if (TemNumero(c)) {
                Barra(g, pad, (int)y, cel, vao, altoBarra, n, c, cor, p.Superficie, p.Texto);
                y += altoBarra + (int)(6 * esc);
                using (SolidBrush bC = new SolidBrush(cor)) {
                  float x = pad;
                  string u = c.Usado + "% usado", e = c.Esperado + "% esperado", q = c.Projecao + "% projeção";
                  g.DrawString(u, fF, bC, x, y, fmt);
                  x = pad + (larg - Medir(g, e, fN, fmt)) / 2f;
                  g.DrawString(e, fN, bW, x, y, fmt);
                  g.DrawString(q, fN, bT, w - pad - Medir(g, q, fN, fmt), y, fmt);
                }
                y += hN + (int)(2 * esc);
              }
              g.DrawString(linhaRitmo(c), fN, c.Estado == "velha" ? bA : bW, pad, y, fmt);
              y += hN + entre;
            }
            y -= entre - (int)(Sep * esc);
            g.DrawString(Legenda, fP, bW, pad, y, fmt);
          }
        }
        return bmp;
      }
    }
  }
}
