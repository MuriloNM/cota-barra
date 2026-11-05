// Leitura do arquivo, fórmula, posição e desenho da pílula.
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

  // Como a pílula é desenhada. AlfaFundo 1 = transparente, mas ainda recebe o mouse (com 0 o clique atravessaria).
  public class Aparencia { public string Fonte = "JetBrains Mono"; public double Letra = 12; public int AlfaFundo = 245; public bool Claro; }

  public class Conta {
    public string Rotulo, Estado = "sem", Cor = "cinza", Idade = "", Detalhe;   // Estado: ok | velha | vencida | sem
    public int Usado, Esperado, Projecao;
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
      c.Usado = Inteiro(usado); c.Esperado = Inteiro(esperado); c.Projecao = Inteiro(projecao);
      c.Estado = min > VelhaDepoisMin ? "velha" : "ok";

      double dias = resta / 86400000;
      string txtResta = dias < 1 ? Inteiro(dias * 24) + " h" : dias.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " dias";
      string lida = c.Idade == "agora" ? "lida agora" : "lida há " + c.Idade;
      c.Detalhe = rotulo + ": " + c.Usado + "% usado · esperado " + c.Esperado + "% · projeção " + c.Projecao + "%\n"
                + "reseta em " + txtResta + " · " + lida + (c.Estado == "velha" ? " (VELHA: abra o Claude)" : "");
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
    static Color CorDe(string c, bool claro) {
      switch (c) {
        case "verde": return claro ? Color.FromArgb(22, 128, 57) : Color.FromArgb(63, 185, 80);
        case "amarelo": return claro ? Color.FromArgb(154, 103, 0) : Color.FromArgb(227, 179, 65);
        case "vermelho": return claro ? Color.FromArgb(207, 34, 46) : Color.FromArgb(248, 81, 73);
        default: return Color.FromArgb(140, 140, 140);
      }
    }
    class Seg { public string Rotulo = "", Usado = "", Resto = "", Idade = ""; public Conta Conta; }

    static Seg Segmentos(Conta c, bool rotulo, bool compacto) {
      Seg s = new Seg { Conta = c, Rotulo = rotulo ? c.Rotulo.ToLowerInvariant() : "" };
      if (c.Estado == "sem" || c.Estado == "vencida") {
        s.Usado = "--";
        if (!compacto) s.Resto = c.Estado == "sem" ? "sem leitura" : "vencida";
        return s;
      }
      s.Usado = c.Usado + "%";
      if (compacto) { s.Rotulo = ""; return s; }
      s.Resto = "/ " + c.Esperado + "% → " + c.Projecao + "%";
      s.Idade = c.Estado == "velha" ? "velha " + c.Idade : c.Idade;
      return s;
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
    static int Medir(Graphics g, string t, Font f, StringFormat fmt) {
      return t.Length == 0 ? 0 : (int)Math.Ceiling(g.MeasureString(t, f, 4000, fmt).Width);
    }

    // Visual de terminal: sempre escuro, cantos quase retos, fonte mono, prompt e número na cor do ritmo.
    public static Bitmap Pilula(IList<Conta> lista, double esc, int alturaMax, bool compacto, Aparencia a) {
      bool duas = lista.Count > 1, claro = a.Claro;
      Color fundo = claro ? Color.FromArgb(a.AlfaFundo, 245, 245, 243) : Color.FromArgb(a.AlfaFundo, 12, 12, 12);
      Color borda = claro ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(70, 255, 255, 255);
      Color forte = claro ? Color.FromArgb(30, 30, 30) : Color.FromArgb(225, 225, 225);
      Color fraco = claro ? Color.FromArgb(95, 95, 95) : Color.FromArgb(140, 140, 140);
      Color alerta = claro ? Color.FromArgb(170, 95, 0) : Color.FromArgb(240, 170, 70);
      float px = (float)((duas ? a.Letra : a.Letra + 2) * esc);
      string fonte = FonteMono(a.Fonte);

      using (Font fNormal = new Font(fonte, px, FontStyle.Regular, GraphicsUnit.Pixel))
      using (Font fForte = new Font(fonte, px, FontStyle.Bold, GraphicsUnit.Pixel))
      using (StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone()) {
        fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        List<Seg> linhas = new List<Seg>();
        foreach (Conta c in lista) linhas.Add(Segmentos(c, duas, compacto));
        int wRot = 0, wUso = 0, wResto = 0, wIdade = 0;
        using (Bitmap med = new Bitmap(1, 1)) using (Graphics gm = Graphics.FromImage(med)) {
          foreach (Seg s in linhas) {
            wRot = Math.Max(wRot, Medir(gm, s.Rotulo, fNormal, fmt));
            wUso = Math.Max(wUso, Medir(gm, s.Usado, fForte, fmt));
            wResto = Math.Max(wResto, Medir(gm, s.Resto, fNormal, fmt));
            wIdade = Math.Max(wIdade, Medir(gm, s.Idade, fNormal, fmt));
          }
        }
        int pad = (int)(9 * esc), vao = (int)(6 * esc), wPrompt, altura;
        using (Bitmap med = new Bitmap(1, 1)) using (Graphics gm = Graphics.FromImage(med)) {
          wPrompt = Medir(gm, ">", fForte, fmt);
          // A altura acompanha a letra, sem passar do que cabe na barra.
          altura = Math.Min(alturaMax, (int)Math.Ceiling(fNormal.GetHeight(gm) * linhas.Count + 2 * (int)(4 * esc)));
        }
        int x1 = pad + wPrompt + vao;
        int x2 = x1 + (wRot > 0 ? wRot + vao : 0);
        int x3 = x2 + wUso + (wResto > 0 ? (int)(4 * esc) : 0);
        int x4 = x3 + wResto + (wIdade > 0 ? vao * 2 : 0);
        int largura = x4 + wIdade + pad;

        Bitmap bmp = new Bitmap(largura, altura, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(bmp)) {
          g.SmoothingMode = SmoothingMode.AntiAlias;
          g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
          int raio = (int)(3 * esc) * 2, w1 = largura - 1, h1 = altura - 1;
          using (GraphicsPath cam = new GraphicsPath()) {
            cam.AddArc(0, 0, raio, raio, 180, 90); cam.AddArc(w1 - raio, 0, raio, raio, 270, 90);
            cam.AddArc(w1 - raio, h1 - raio, raio, raio, 0, 90); cam.AddArc(0, h1 - raio, raio, raio, 90, 90); cam.CloseFigure();
            using (SolidBrush b = new SolidBrush(fundo)) g.FillPath(b, cam);
            if (a.AlfaFundo > 1) using (Pen p = new Pen(borda)) g.DrawPath(p, cam);
          }
          float altLinha = fNormal.GetHeight(g), topo = (altura - altLinha * linhas.Count) / 2;
          using (SolidBrush bForte = new SolidBrush(forte)) using (SolidBrush bFraco = new SolidBrush(fraco)) using (SolidBrush bAlerta = new SolidBrush(alerta)) {
            for (int i = 0; i < linhas.Count; i++) {
              Seg s = linhas[i]; Conta c = s.Conta;
              float y = topo + i * altLinha;
              bool ok = c.Estado == "ok";
              // Só a leitura boa fica colorida: a cor de uma leitura velha não é a do agora.
              using (SolidBrush bCor = new SolidBrush(ok ? CorDe(c.Cor, claro) : fraco)) {
                g.DrawString(Prompt(c), fForte, c.Estado == "velha" ? bAlerta : bCor, pad, y, fmt);
                g.DrawString(s.Usado, fForte, bCor, x2 + wUso - Medir(g, s.Usado, fForte, fmt), y, fmt);   // à direita, colado na barra "/"
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
  }
}
