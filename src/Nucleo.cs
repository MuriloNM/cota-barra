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
    public float Onda = Desenho.SemOnda;   // onde a onda de cor está na barra: 0 é o começo, 1 é o fim
    public Paleta Cores { get { return Paleta.De(NomePaleta, Claro); } }
  }

  public class Conta {
    public string Rotulo, Estado = "sem", Cor = "cinza", Idade = "", Detalhe, RestaTxt = "", LidaTxt = "";   // Estado: ok | velha | vencida | sem
    public int Usado, Esperado, Projecao, Diferenca, ProjecaoMedia;
    public int DiasHistorico;                  // dias completos de histórico, de qualquer semana
    public double VelDia, DiasAte100 = -1;     // dia típico em pontos, dias até 100% antes do reset (-1: não chega)
    // Janela de 5 h: só existe se o arquivo trouxer. No plano sem essa janela, nada disso aparece.
    public bool Tem5h, Velha5h; public int Uso5h; public string Reset5hTxt = "";
    public double Vel5hHora, Acaba5hMin = -1;  // pontos por hora agora, minutos até 100% antes do reset (-1: não acaba)
    public List<Conta> Modelos = new List<Conta>();   // cotas semanais por modelo, como a do Fable; mesma fórmula da geral
    public string Assinatura() { return Rotulo + "|" + Estado + "|" + Cor + "|" + Usado + "|" + Esperado + "|" + Projecao + "|" + Idade + "|" + Uso5h + "|" + Velha5h + "|" + Cota.Inteiro(Acaba5hMin / 5) + "|" + string.Join(",", Modelos.ConvertAll(m => m.Assinatura()).ToArray()); }
  }

  public class Ponto { public long Lido; public double Usado; public DateTimeOffset Reset; }

  // Histórico das leituras, em arquivo próprio: o arquivo de cota guarda só a última e nunca é alterado.
  public static class Historico {
    const int GuardarDias = 56;   // o aprendizado atravessa as semanas
    static readonly Dictionary<string, List<Ponto>> pontos = new Dictionary<string, List<Ponto>>();
    static string arquivo;

    public static void Carregar(string caminho, DateTimeOffset agora) {
      arquivo = caminho; pontos.Clear();
      long corte = agora.AddDays(-GuardarDias).ToUnixTimeMilliseconds();
      List<string> mantidas = new List<string>(); bool podou = false;
      try {
        if (!File.Exists(caminho)) return;
        foreach (string linha in File.ReadAllLines(caminho)) {
          string[] p = linha.Split('\t');
          long lido; double usado; DateTimeOffset reset;
          if (p.Length != 4 || !long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out lido) || !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out usado) || !DateTimeOffset.TryParse(p[3], CultureInfo.InvariantCulture, DateTimeStyles.None, out reset)) { podou = true; continue; }
          if (lido < corte) { podou = true; continue; }
          Lista(p[0]).Add(new Ponto { Lido = lido, Usado = usado, Reset = reset });
          mantidas.Add(linha);
        }
        if (podou) { string tmp = caminho + ".novo"; File.WriteAllLines(tmp, mantidas); File.Copy(tmp, caminho, true); File.Delete(tmp); }
      } catch { }
    }
    static List<Ponto> Lista(string conta) {
      List<Ponto> l;
      if (!pontos.TryGetValue(conta, out l)) { l = new List<Ponto>(); pontos[conta] = l; }
      return l;
    }
    public static List<Ponto> De(string conta) { return Lista(conta); }
    // Só grava quando a leitura é nova.
    public static void Registrar(string conta, Leitura l) {
      if (arquivo == null || l == null || l.Usado == null || l.Lido == null) return;
      DateTimeOffset reset;
      if (!DateTimeOffset.TryParse(l.ResetsAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out reset)) return;
      List<Ponto> lista = Lista(conta);
      if (lista.Count > 0) {
        Ponto u = lista[lista.Count - 1];
        if (u.Lido >= l.Lido.Value) return;
        // Leitura igual à anterior, no mesmo dia, não ensina nada: não ocupa espaço.
        bool mesmoDia = DateTimeOffset.FromUnixTimeMilliseconds(u.Lido).ToLocalTime().Date == DateTimeOffset.FromUnixTimeMilliseconds(l.Lido.Value).ToLocalTime().Date;
        if (mesmoDia && u.Usado == l.Usado.Value && Math.Abs((u.Reset - reset).TotalMinutes) <= 5) return;
      }
      lista.Add(new Ponto { Lido = l.Lido.Value, Usado = l.Usado.Value, Reset = reset });
      try { File.AppendAllText(arquivo, conta + "\t" + l.Lido.Value + "\t" + l.Usado.Value.ToString(CultureInfo.InvariantCulture) + "\t" + l.ResetsAt + Environment.NewLine); } catch { }
    }
  }

  public static class Cota {
    public const double SemanaMs = 604800000.0;
    public const int VelhaDepoisMin = 120;   // leitura com mais de 2 h aparece como velha
    public const int DiasParaConfiar = 7;    // com 7 dias completos de histórico, a projeção semanal vale só pelo dia típico

    // Dia típico: quantos pontos a pessoa usa num dia, aprendido entre semanas. Conta todo dia desde a primeira
    // leitura, inclusive os sem uso; a partir de 3 dias ignora o maior, para um pico isolado não puxar a previsão.
    public static double DiaTipico(IList<Ponto> historico, DateTimeOffset agora, out int dias) {
      dias = 0;
      if (historico == null || historico.Count == 0) return 0;
      List<Ponto> pts = new List<Ponto>(historico);
      pts.Sort((a, b) => a.Lido.CompareTo(b.Lido));
      Func<long, DateTime> dia = ms => DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().Date;
      DateTime hoje = agora.ToLocalTime().Date, primeiro = dia(pts[0].Lido);
      Dictionary<DateTime, double> porDia = new Dictionary<DateTime, double>();
      for (int i = 1; i < pts.Count; i++) {
        bool mesmaSemana = Math.Abs((pts[i].Reset - pts[i - 1].Reset).TotalMinutes) <= 5;
        double delta = mesmaSemana ? Math.Max(pts[i].Usado - pts[i - 1].Usado, 0) : pts[i].Usado;   // semana nova começa do zero
        DateTime d = dia(pts[i].Lido); double v;
        porDia.TryGetValue(d, out v); porDia[d] = v + delta;
      }
      List<double> valores = new List<double>();
      for (DateTime d = hoje.AddDays(-1); d > primeiro && valores.Count < 28; d = d.AddDays(-1)) {   // hoje está pela metade; o primeiro dia também
        double v; porDia.TryGetValue(d, out v); valores.Add(v);
      }
      dias = valores.Count;
      if (dias == 0) return 0;
      valores.Sort();
      int usar = dias >= 3 ? dias - 1 : dias;
      double soma = 0;
      for (int i = 0; i < usar; i++) soma += valores[i];
      return soma / usar;
    }

    // Janela de 5 h: a velocidade de agora é a da última hora; sem ela, a média desde o início da janela.
    public const double Janela5hHoras = 5;
    public static void Calcular5h(Conta c, Leitura l, DateTimeOffset agora, IList<Ponto> historico) {
      if (l == null || l.Usado == null || l.Lido == null) return;
      DateTimeOffset reset;
      if (!DateTimeOffset.TryParse(l.ResetsAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out reset) || reset <= agora) return;
      long agoraMs = agora.ToUnixTimeMilliseconds();
      double usado = l.Usado.Value, restaH = (reset - agora).TotalHours;
      c.Tem5h = true; c.Uso5h = Inteiro(usado);
      int restaMin = (int)Math.Ceiling(restaH * 60);
      c.Reset5hTxt = restaMin < 60 ? restaMin + " min" : (restaMin / 60) + " h " + (restaMin % 60).ToString("00");
      c.Velha5h = agoraMs - l.Lido.Value > 30 * 60000;   // velha aqui é bem mais cedo que na semana
      if (c.Velha5h) return;
      double porHora = -1;
      if (historico != null) {
        Ponto base1 = null;
        foreach (Ponto p in historico) {
          if (Math.Abs((p.Reset - reset).TotalMinutes) > 5 || p.Lido >= l.Lido.Value || l.Lido.Value - p.Lido > 3600000) continue;
          if (base1 == null || p.Lido < base1.Lido) base1 = p;
        }
        if (base1 != null && l.Lido.Value - base1.Lido >= 10 * 60000)
          porHora = Math.Max(usado - base1.Usado, 0) / ((l.Lido.Value - base1.Lido) / 3600000.0);
      }
      if (porHora < 0) {
        double passou = Janela5hHoras - restaH;
        if (passou >= 0.25) porHora = usado / passou;
      }
      if (porHora < 0) return;
      c.Vel5hHora = porHora;
      if (usado >= 100) { c.Acaba5hMin = 0; return; }
      if (porHora > 0 && (100 - usado) / porHora < restaH) c.Acaba5hMin = (100 - usado) / porHora * 60;
    }

    // O nome do arquivo muda de instalação para instalação: vale o mais recente.
    public static string Arquivo() {
      string teste = Environment.GetEnvironmentVariable("COTA_ARQUIVO");   // só para testar com um arquivo de exemplo
      return !string.IsNullOrEmpty(teste) ? teste : MaisRecente("cota_inline-*.json");
    }
    // A janela de 5 h pode vir de um arquivo à parte, gravado pelo mod de teste. Só lido.
    static string Arquivo5h() {
      return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COTA_ARQUIVO")) ? null : MaisRecente("cota-5h_*.json");
    }
    static string MaisRecente(string padrao) {
      try {
        string pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".claude\plugins\store");
        string melhor = null; DateTime data = DateTime.MinValue;
        foreach (string f in Directory.GetFiles(pasta, padrao)) {
          DateTime d = File.GetLastWriteTimeUtc(f);
          if (d > data) { data = d; melhor = f; }
        }
        return melhor;
      } catch { return null; }
    }

    public static int Inteiro(double v) { return (int)Math.Round(v, 0, MidpointRounding.AwayFromZero); }

    public static string FormatIdade(int min) {
      if (min < 1) return "agora";
      if (min < 60) return min + " min";
      if (min < 2880) return (min / 60) + " h";
      return (min / 1440) + " d";
    }

    // Fórmula igual à do plugin.
    public static Conta Calcular(Leitura l, string rotulo, DateTimeOffset agora) { return Calcular(l, rotulo, agora, null); }
    public static Conta Calcular(Leitura l, string rotulo, DateTimeOffset agora, IList<Ponto> historico) {
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
      c.Usado = Inteiro(usado); c.Esperado = Inteiro(esperado); c.Diferenca = Inteiro(dif); c.ProjecaoMedia = Inteiro(projecao);
      c.Estado = min > VelhaDepoisMin ? "velha" : "ok";

      // Dia típico aprendido: o peso cresce com os dias de histórico, para a projeção sair da média sem dar saltos.
      if (historico != null) {
        int diasHist;
        double porDia = DiaTipico(historico, agora, out diasHist);
        c.DiasHistorico = diasHist;
        if (diasHist > 0) {
          c.VelDia = porDia;
          double restaDias = resta / 86400000;
          double peso = Math.Min(diasHist / (double)DiasParaConfiar, 1);
          projecao = Math.Max(usado, peso * (usado + porDia * restaDias) + (1 - peso) * projecao);
          if (porDia > 0 && usado < 100 && (100 - usado) / porDia < restaDias) c.DiasAte100 = (100 - usado) / porDia;
        }
      }
      c.Projecao = Inteiro(projecao);

      double dias = resta / 86400000;
      c.RestaTxt = dias < 1 ? Inteiro(dias * 24) + " h" : dias.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " dias";
      c.Detalhe = rotulo + ": " + c.Usado + "% usado · esperado " + c.Esperado + "% · projeção " + c.Projecao + "%\n"
                + "reseta em " + c.RestaTxt + " · " + c.LidaTxt + (c.Estado == "velha" ? " (VELHA: abra o Claude)" : "");
      return c;
    }

    // Lê sem bloquear quem grava. Só relê quando a data muda; se vier quebrado, mantém o último bom.
    class Cache { public DateTime Data; public Dictionary<string, object> Json; }
    static readonly Dictionary<string, Cache> caches = new Dictionary<string, Cache>();
    static Dictionary<string, object> LerArquivo(string caminho) {
      Cache cache;
      if (!caches.TryGetValue(caminho, out cache)) { cache = new Cache(); caches[caminho] = cache; }
      try {
        DateTime data = File.GetLastWriteTimeUtc(caminho);
        if (data == cache.Data && cache.Json != null) return cache.Json;
        string texto;
        using (FileStream fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8)) texto = sr.ReadToEnd();
        Dictionary<string, object> json = new JavaScriptSerializer().DeserializeObject(texto) as Dictionary<string, object>;
        if (json != null) { cache.Json = json; cache.Data = data; }
      } catch { }
      return cache.Json;
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

    // Cotas por modelo guardadas como { lido, janelas: [{ nome, percentUsed, resetsAt }] }. Vencida não aparece.
    static List<Conta> LerModelos(Dictionary<string, object> json, string conta, DateTimeOffset agora) {
      List<Conta> l = new List<Conta>();
      object o;
      if (json == null || !json.TryGetValue("modelos:" + conta, out o)) return l;
      Dictionary<string, object> d = o as Dictionary<string, object>;
      object lido, janelas;
      if (d == null || !d.TryGetValue("lido", out lido) || !d.TryGetValue("janelas", out janelas) || !(janelas is object[])) return l;
      foreach (object item in (object[])janelas) {
        Dictionary<string, object> j = item as Dictionary<string, object>;
        object nome, usado, reset;
        if (j == null || !j.TryGetValue("nome", out nome) || !j.TryGetValue("percentUsed", out usado) || !j.TryGetValue("resetsAt", out reset)) continue;
        try {
          Conta m = Calcular(new Leitura { Usado = Convert.ToDouble(usado, CultureInfo.InvariantCulture), ResetsAt = Convert.ToString(reset, CultureInfo.InvariantCulture), Lido = Convert.ToInt64(lido, CultureInfo.InvariantCulture) }, Convert.ToString(nome, CultureInfo.InvariantCulture), agora);
          if (m.Estado == "ok" || m.Estado == "velha") l.Add(m);
        } catch { }
      }
      return l;
    }

    // Cada conta é uma chave "ultima:<nome>"; "padrao" vem primeiro. A chave antiga "ultima" não vale.
    static string RotuloDe(string nome) { return nome == "padrao" ? "Padrão" : nome; }
    public static List<Conta> Contas(DateTimeOffset agora, string caminho) { return Contas(agora, caminho, false); }
    static List<Conta> Contas(DateTimeOffset agora, string caminho, bool usarHistorico) {
      Dictionary<string, object> json = string.IsNullOrEmpty(caminho) ? null : LerArquivo(caminho);
      List<string> nomes = new List<string>();
      if (json != null) foreach (string k in json.Keys) if (k.StartsWith("ultima:", StringComparison.Ordinal)) nomes.Add(k.Substring(7));
      nomes.Sort((a, b) => a == "padrao" ? (b == "padrao" ? 0 : -1) : b == "padrao" ? 1 : string.CompareOrdinal(a, b));
      List<Conta> lista = new List<Conta>();
      foreach (string n in nomes) {
        Leitura l = Extrair(json, "ultima:" + n);
        if (usarHistorico) Historico.Registrar(n, l);
        Conta c = Calcular(l, RotuloDe(n), agora, usarHistorico ? Historico.De(n) : null);
        // Janela de 5 h, se o arquivo trouxer. Quem não tem essa janela no plano segue só com a semana.
        Leitura l5 = Extrair(json, "cinco_horas:" + n);
        if (l5 == null && usarHistorico) {
          string a5 = Arquivo5h();
          if (a5 != null) l5 = Extrair(LerArquivo(a5), "cinco_horas:" + n);
        }
        if (usarHistorico) Historico.Registrar(n + "#5h", l5);
        Calcular5h(c, l5, agora, usarHistorico ? Historico.De(n + "#5h") : null);
        c.Modelos = LerModelos(json, n, agora);
        if (c.Modelos.Count == 0 && usarHistorico) {
          string a5m = Arquivo5h();
          if (a5m != null) c.Modelos = LerModelos(LerArquivo(a5m), n, agora);
        }
        lista.Add(c);
      }
      if (lista.Count == 0) lista.Add(Calcular(null, "Claude", agora));
      return lista;
    }
    public static List<Conta> Contas() { return Contas(DateTimeOffset.Now, Arquivo(), true); }

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
    class Seg { public string Rotulo = "", Usado = "", Resto = "", Cinco = "", Modelo = "", Idade = ""; public Conta Conta, ContaModelo; public bool TemBarra; }

    public static string TextoAcaba5h(double min) {
      if (min <= 0) return "esgotada";
      int m = (int)Math.Round(min);
      return "acaba em " + (m < 60 ? m + " min" : (m / 60) + " h " + (m % 60).ToString("00"));
    }

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
      if (c.Tem5h) s.Cinco = "5h " + c.Uso5h + "%";
      if (c.Modelos.Count > 0) { s.ContaModelo = c.Modelos[0]; s.Modelo = c.Modelos[0].Rotulo.ToLowerInvariant() + " " + c.Modelos[0].Usado + "%"; }
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
    public const float SemOnda = -9f;
    const float MeioOnda = 2f, BordaOnda = 2f, ForcaOnda = 0.90f;   // 5 células fortes de uma vez, mais 2 se apagando de cada lado
    static Color Misturar(Color a, Color b, float k) {
      return Color.FromArgb(a.A, (int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k));
    }
    public static void Barra(Graphics g, int x, int y, int cel, int vao, int alto, int n, Conta c, Color cor, Color trilha, Color risco) {
      Barra(g, x, y, cel, vao, alto, n, c, cor, trilha, risco, SemOnda, Color.Empty);
    }
    public static void Barra(Graphics g, int x, int y, int cel, int vao, int alto, int n, Conta c, Color cor, Color trilha, Color risco, float onda, Color corOnda) {
      SmoothingMode antes = g.SmoothingMode;
      g.SmoothingMode = SmoothingMode.None;
      int cheias = Math.Min(n, Cota.Inteiro(c.Usado / 100.0 * n));
      int proj = Math.Max(cheias, Math.Min(n, Cota.Inteiro(c.Projecao / 100.0 * n)));
      Color cProj = Color.FromArgb(95, cor);
      for (int i = 0; i < n; i++) {
        Color base1 = i < cheias ? cor : i < proj ? cProj : trilha;
        if (onda != SemOnda) {
          float d = Math.Abs(i - onda * (n - 1));   // distância em células, para valer igual em qualquer barra
          if (d < MeioOnda + BordaOnda) {
            float k = d <= MeioOnda ? 1 : 1 - (d - MeioOnda) / BordaOnda;
            base1 = Misturar(base1, corOnda, k * ForcaOnda);
            base1 = Color.FromArgb(Math.Max((int)base1.A, (int)(230 * k)), base1);   // as células apagadas também acendem
          }
        }
        using (SolidBrush b = new SolidBrush(base1)) g.FillRectangle(b, x + i * (cel + vao), y, cel, alto);
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
        int wRot = 0, wUso = 0, wResto = 0, wCinco = 0, wModelo = 0, wIdade = 0, wPrompt, altura;
        using (Bitmap med = new Bitmap(1, 1)) using (Graphics gm = Graphics.FromImage(med)) {
          foreach (Seg s in linhas) {
            algumaBarra |= s.TemBarra;
            wRot = Math.Max(wRot, Medir(gm, s.Rotulo, fNormal, fmt));
            wUso = Math.Max(wUso, Medir(gm, s.Usado, fForte, fmt));
            wResto = Math.Max(wResto, Medir(gm, s.Resto, fNormal, fmt));
            wCinco = Math.Max(wCinco, Medir(gm, s.Cinco, fNormal, fmt));
            wModelo = Math.Max(wModelo, Medir(gm, s.Modelo, fNormal, fmt));
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
        int x5 = x3 + wResto + (wCinco > 0 ? vao * 2 : 0);
        int x6 = x5 + wCinco + (wModelo > 0 ? vao * 2 : 0);
        int x4 = x6 + wModelo + (wIdade > 0 ? vao * 2 : 0);
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
                float ondaLinha = a.Onda == SemOnda ? SemOnda : a.Onda - i * 0.08f;   // a segunda linha vem um pouco atrás
                Barra(g, xB, (int)(y + (altLinha - altoBarra) / 2), cel, vaoCel, altoBarra, Celulas, c, cor, trilha, p.Texto, ondaLinha, p.Destaque);
              }
              if (s.Rotulo.Length > 0) g.DrawString(s.Rotulo, fNormal, bFraco, x1, y, fmt);
              if (s.Resto.Length > 0) g.DrawString(s.Resto, fNormal, ok ? bForte : bFraco, x3, y, fmt);
              if (s.Cinco.Length > 0) g.DrawString(s.Cinco, fNormal, c.Velha5h ? bFraco : c.Acaba5hMin >= 0 ? bAlerta : bForte, x5, y, fmt);
              if (s.Modelo.Length > 0)
                using (SolidBrush bM = new SolidBrush(s.ContaModelo.Estado == "ok" ? p.DoRitmo(s.ContaModelo.Cor) : p.Fraco)) g.DrawString(s.Modelo, fNormal, bM, x6, y, fmt);
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
      string fonte = FonteMono(null);   // a fonte escolhida vale só para a pílula; o cartão fica sempre igual
      using (Font fN = new Font(fonte, px, FontStyle.Regular, GraphicsUnit.Pixel))
      using (Font fF = new Font(fonte, px, FontStyle.Bold, GraphicsUnit.Pixel))
      using (Font fP = new Font(fonte, (float)(11 * esc), FontStyle.Regular, GraphicsUnit.Pixel))
      using (StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone()) {
        fmt.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        int pad = (int)(14 * esc), entre = (int)(14 * esc), n = 36, cel = (int)(6 * esc), vao = Math.Max(1, (int)(1.5 * esc)), altoBarra = (int)(9 * esc);
        int wBarra = LarguraBarra(cel, vao, n);
        const string Legenda = "cheio: usado · claro: projeção · risco: esperado";
        Func<Conta, string> linha5h = c => {
          string t = "5h: " + c.Uso5h + "% · reseta em " + c.Reset5hTxt;
          if (c.Velha5h) return t + " · leitura velha";
          if (c.Acaba5hMin >= 0) return t + " · " + TextoAcaba5h(c.Acaba5hMin);
          return t;
        };
        const int Sep = 3;
        Func<Conta, string> linhaRitmo = c => {
          if (c.Estado == "sem") return "sem leitura: abra o Claude nesta conta";
          if (c.Estado == "vencida") return "semana vencida: abra o Claude nesta conta";
          string r = c.Diferenca < -5 ? (-c.Diferenca) + " pts de folga" : c.Diferenca > 5 ? c.Diferenca + " pts acima do ritmo" : "no ritmo";
          return c.Estado == "velha" ? "leitura velha, " + c.LidaTxt + ": abra o Claude" : r + " · " + c.LidaTxt;
        };
        CultureInfo br = CultureInfo.GetCultureInfo("pt-BR");
        Func<Conta, string> linhaVel = c => {
          if (c.DiasHistorico == 0) return "dia típico: aprendendo, projeção pela média";
          string v = "dia típico " + c.VelDia.ToString("0.0", br) + " pts";
          if (c.DiasAte100 >= 0) return v + " · 100% em " + c.DiasAte100.ToString("0.0", br) + " dias";
          if (c.DiasHistorico < Cota.DiasParaConfiar) return v + " · aprendendo, " + c.DiasHistorico + " de " + Cota.DiasParaConfiar + " dias";
          return v + " · pela média: " + c.ProjecaoMedia + "%";
        };
        Func<Conta, string> linhaModelo = m => m.Rotulo.ToLowerInvariant() + ": " + m.Usado + "% · esperado " + m.Esperado + "% · projeção " + m.Projecao + "%";
        int larg = wBarra, alt;
        float hN, hP;
        using (Bitmap med = new Bitmap(1, 1)) using (Graphics gm = Graphics.FromImage(med)) {
          hN = fN.GetHeight(gm); hP = fP.GetHeight(gm);
          foreach (Conta c in lista) {
            larg = Math.Max(larg, Medir(gm, linhaRitmo(c), fN, fmt));
            if (TemNumero(c)) larg = Math.Max(larg, Medir(gm, linhaVel(c), fN, fmt));
            if (c.Tem5h) larg = Math.Max(larg, Medir(gm, linha5h(c), fN, fmt));
            foreach (Conta m in c.Modelos) larg = Math.Max(larg, Medir(gm, linhaModelo(m), fN, fmt));
          }
          larg = Math.Max(larg, Medir(gm, Legenda, fP, fmt));
        }
        alt = pad * 2 + (int)(hP + 10 * esc);
        foreach (Conta c in lista)
          alt += (int)(hN * 2) + (int)(4 * esc) + entre + (TemNumero(c) ? (int)(hN * 2) + altoBarra + (int)(8 * esc) : 0) + (c.Tem5h ? (int)hN : 0) + c.Modelos.Count * (int)hN;
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
                g.DrawString(linhaVel(c), fN, c.DiasAte100 >= 0 ? bA : bW, pad, y, fmt);
                y += hN;
              }
              g.DrawString(linhaRitmo(c), fN, c.Estado == "velha" ? bA : bW, pad, y, fmt);
              y += hN;
              if (c.Tem5h) {
                g.DrawString(linha5h(c), fN, c.Velha5h ? bW : c.Acaba5hMin >= 0 ? bA : bT, pad, y, fmt);
                y += hN;
              }
              foreach (Conta m in c.Modelos) {
                using (SolidBrush bM = new SolidBrush(m.Estado == "ok" ? p.DoRitmo(m.Cor) : p.Fraco)) g.DrawString(linhaModelo(m), fN, bM, pad, y, fmt);
                y += hN;
              }
              y += entre;
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
