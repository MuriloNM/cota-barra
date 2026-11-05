// Confere fórmula, estados, arquivo e posição do programa compilado. Com um caminho de pasta, grava amostras PNG.
// Não grava no arquivo de cota; os casos de arquivo quebrado usam cópias temporárias.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CotaBarra {
  static class Conferir {
    static int falhas;
    static readonly DateTimeOffset Agora = DateTimeOffset.Parse("2026-11-04T23:48:00Z");
    static void Confere(string nome, object obtido, object esperado) {
      bool ok = Convert.ToString(obtido) == Convert.ToString(esperado);
      if (!ok) falhas++;
      Console.WriteLine("[" + (ok ? "ok" : "FALHOU") + "] " + nome + ": " + obtido + (ok ? "" : " (esperado " + esperado + ")"));
    }
    static Leitura L(double usado, double resetHoras, double idadeMin) {
      return new Leitura { Usado = usado, ResetsAt = Agora.AddHours(resetHoras).ToString("o"), Lido = Agora.AddMinutes(-idadeMin).ToUnixTimeMilliseconds() };
    }
    static Conta C(double usado, double resetHoras, double idadeMin) { return Cota.Calcular(L(usado, resetHoras, idadeMin), "x", Agora); }
    static string Rastro(int? iconesEsq, int? iconesDir, int? manual) {
      string o; int? x = Cota.Posicao(0, 3840, 3525, 237, iconesEsq, iconesDir, 500, 12, manual, out o);
      return x == null ? "nada" : o + " " + x;
    }

    static int Diferentes(Bitmap a, Bitmap b) {
      int n = 0;
      for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) if (a.GetPixel(x, y) != b.GetPixel(x, y)) n++;
      return n;
    }
    static Conta Vel(double usado, List<Ponto> h) { return Cota.Calcular(L(usado, 84, 1), "x", Agora, h); }
    // Dois pontos: um há "horas" com "antes" e um agora com "usado".
    static List<Ponto> H(double usado, double horas, double antes) {
      DateTimeOffset r = Agora.AddHours(84);
      return new List<Ponto> {
        new Ponto { Lido = Agora.AddHours(-horas).ToUnixTimeMilliseconds(), Usado = antes, Reset = r },
        new Ponto { Lido = Agora.ToUnixTimeMilliseconds(), Usado = usado, Reset = r }
      };
    }
    // Histórico com um dia por valor, terminando ontem. O primeiro ponto só marca o começo.
    static List<Ponto> Dias(params double[] porDia) {
      DateTimeOffset r = Agora.AddHours(84);
      List<Ponto> l = new List<Ponto>();
      double soma = 0;
      l.Add(new Ponto { Lido = Agora.AddDays(-(porDia.Length + 1)).ToUnixTimeMilliseconds(), Usado = 0, Reset = r });
      for (int i = 0; i < porDia.Length; i++) {
        soma += porDia[i];
        l.Add(new Ponto { Lido = Agora.AddDays(-(porDia.Length - i)).ToUnixTimeMilliseconds(), Usado = soma, Reset = r });
      }
      return l;
    }
    static Conta V5(Leitura l, List<Ponto> h) { Conta c = new Conta { Rotulo = "x" }; Cota.Calcular5h(c, l, Agora, h); return c; }
    // Um ponto de 5 h: "usado" há "minutos", na janela que reseta em "resetHoras".
    static List<Ponto> P5(double usado, double minutos, double resetHoras) {
      return new List<Ponto> { new Ponto { Lido = Agora.AddMinutes(-minutos).ToUnixTimeMilliseconds(), Usado = usado, Reset = Agora.AddHours(resetHoras) } };
    }
    // Conta as células pintadas com a cor cheia, desenhando a barra de verdade.
    static int Contar(Conta c, int n) {
      using (Bitmap b = new Bitmap(n * 4 + 2, 12)) using (Graphics g = Graphics.FromImage(b)) {
        g.Clear(Color.Black);
        Desenho.Barra(g, 0, 2, 3, 1, 8, n, c, Color.FromArgb(0, 255, 0), Color.FromArgb(40, 40, 40), Color.Black);
        int t = 0;
        for (int i = 0; i < n; i++) { Color p = b.GetPixel(i * 4 + 1, 2); if (p.G == 255) t++; }
        return t;
      }
    }

    static int Main(string[] a) {
      Console.OutputEncoding = System.Text.Encoding.UTF8;
      Console.WriteLine("--- Fórmula, com contas feitas à mão (84 h = meia semana)");
      Conta m = C(50, 84, 1);
      Confere("meia semana: esperado", m.Esperado, 50);
      Confere("meia semana: projeção", m.Projecao, 100);
      Confere("meia semana: amarelo", m.Cor, "amarelo");
      Confere("diferença -5 exata é amarelo", C(45, 84, 1).Cor, "amarelo");
      Confere("diferença +5 exata é amarelo", C(55, 84, 1).Cor, "amarelo");
      Confere("diferença -5,1 é verde", C(44.9, 84, 1).Cor, "verde");
      Confere("diferença +5,1 é vermelho", C(55.1, 84, 1).Cor, "vermelho");
      Confere("início da semana: projeção = usado", C(3, 167, 1).Projecao, 3);

      Console.WriteLine("--- Estados");
      Confere("até 2 h é ok", C(10, 84, 120).Estado, "ok");
      Confere("acima de 2 h é velha", C(10, 84, 121).Estado, "velha");
      Confere("reset vencido não vale", C(10, -1, 1).Estado, "vencida");
      Confere("sem leitura", Cota.Calcular(null, "x", Agora).Estado, "sem");

      Console.WriteLine("--- Arquivo (cópias temporárias)");
      string tmp = Path.Combine(Path.GetTempPath(), "cota-barra-teste-" + Guid.NewGuid().ToString("N") + ".json");
      File.WriteAllText(tmp, "{\"ultima\":{\"percentUsed\":99,\"resetsAt\":\"2030-01-01T00:00:00Z\",\"lido\":1},\"ultima:padrao\":{\"percentUsed\":18,\"resetsAt\":\"2026-11-09T19:00:00Z\",\"lido\":1793835996553}}");
      List<Conta> l = Cota.Contas(Agora, tmp);
      Confere("ignora a chave antiga \"ultima\"", l[0].Usado, 18);
      Confere("só aparece a conta que existe", l.Count, 1);
      string tmpVelho = Path.Combine(Path.GetTempPath(), "cota-barra-teste-" + Guid.NewGuid().ToString("N") + ".json");
      File.WriteAllText(tmpVelho, "{\"ultima\":{\"percentUsed\":42,\"resetsAt\":\"2026-11-09T19:00:00Z\",\"lido\":1793835996553}}");
      Confere("só a chave antiga: plugin antigo", Cota.Contas(Agora, tmpVelho)[0].Estado, "plugin-antigo");
      File.WriteAllText(tmpVelho, "{}");
      File.SetLastWriteTimeUtc(tmpVelho, DateTime.UtcNow.AddMinutes(1));
      Confere("arquivo sem chave nenhuma: sem leitura", Cota.Contas(Agora, tmpVelho)[0].Estado, "sem");
      File.Delete(tmpVelho);
      File.WriteAllText(tmp, "{ quebrado");
      File.SetLastWriteTimeUtc(tmp, DateTime.UtcNow.AddMinutes(1));
      Confere("JSON quebrado mantém o último bom", Cota.Contas(Agora, tmp)[0].Usado, 18);
      File.Delete(tmp);

      Console.WriteLine("--- Posição (barra 0..3840, bandeja 3525, widgets até 237, largura 500, margem 12)");
      Confere("normal: à esquerda da bandeja", Rastro(1322, 2518, null), "direita 3013");
      Confere("ícones crescem: passa para a esquerda", Rastro(1000, 3100, null), "esquerda 249");
      Confere("sem espaço: devolve nada (vira compacta)", Rastro(400, 3400, null), "nada");
      Confere("posição manual livre é respeitada", Rastro(1322, 2518, 800), "manual 2540");
      Confere("manual sobre os ícones é ignorada", Rastro(1322, 2518, 1500), "direita 3013");
      Confere("sem dados dos ícones: ainda respeita a bandeja", Rastro(null, null, null), "direita 3013");

      Console.WriteLine("--- Aparência");
      List<Conta> duas = new List<Conta> { C(18, 115, 3), C(100, 10, 28) };
      using (Bitmap pq = Desenho.Pilula(duas, 1.5, 60, false, new Aparencia { Letra = 10 }))
      using (Bitmap gr = Desenho.Pilula(duas, 1.5, 60, false, new Aparencia { Letra = 13.5 })) {
        Confere("letra menor deixa a pílula mais estreita", pq.Width < gr.Width, true);
        Confere("letra menor deixa a pílula mais baixa", pq.Height < gr.Height, true);
        Confere("nunca passa da altura da barra", gr.Height <= 60, true);
      }
      using (Bitmap pp = Desenho.Pilula(duas, 1.5, 60, false, new Aparencia { Letra = 8 }))
      using (Bitmap p10 = Desenho.Pilula(duas, 1.5, 60, false, new Aparencia { Letra = 10 })) {
        Confere("tamanho mínimo é mais estreito", pp.Width < p10.Width, true);
        Confere("tamanho mínimo é mais baixo", pp.Height < p10.Height, true);
      }
      using (Bitmap c7 = Desenho.Cartao(duas, 1.5 * 0.7, new Aparencia()))
      using (Bitmap c10 = Desenho.Cartao(duas, 1.5, new Aparencia()))
        Confere("cartão pequeno é menor", c7.Width < c10.Width && c7.Height < c10.Height, true);
      Confere("fonte que não existe cai numa instalada", Desenho.FonteMono("Fonte Que Nao Existe") != "Fonte Que Nao Existe", true);
      Confere("JetBrains Mono vem com o programa", Desenho.Embutida("JetBrains Mono") != null, true);
      using (Font fb = Desenho.NovaFonte("JetBrains Mono", 14, FontStyle.Bold))
        Confere("fonte embutida tem negrito", fb.Bold && fb.FontFamily.Name == "JetBrains Mono", true);
      List<string> fontes = Desenho.FontesMono();
      Confere("menu de fontes traz a JetBrains Mono", fontes.Contains("JetBrains Mono"), true);
      Console.WriteLine("fontes no menu: " + string.Join(", ", fontes));
      Confere("lista de fontes não traz símbolos", fontes.Contains("Marlett") || fontes.Contains("Wingdings 2"), false);
      Confere("lista de fontes não traz variações de peso", fontes.Contains("JetBrains Mono Light"), false);
      using (Bitmap t = Desenho.Pilula(duas, 1.5, 60, false, new Aparencia { AlfaFundo = 1 }))
        Confere("fundo transparente ainda recebe o mouse", t.GetPixel(t.Width - 8, t.Height / 2).A > 0, true);
      Confere("barra: 50% de 20 células enche 10", Contar(C(50, 84, 1), 20), 10);
      Confere("barra: 100% enche todas", Contar(C(100, 84, 1), 20), 20);
      Confere("barra: acima de 100% não estoura", Contar(new Conta { Usado = 130, Projecao = 200, Esperado = 50, Estado = "ok" }, 20), 20);
      using (Bitmap so = Desenho.Pilula(duas, 1.5, 60, false, new Aparencia { Barras = false }))
      using (Bitmap ba = Desenho.Pilula(duas, 1.5, 60, false, new Aparencia { Barras = true }))
        Confere("os dois estilos desenham coisas diferentes", so.Width != ba.Width, true);
      Confere("paleta desconhecida cai na Tokyo Night", Paleta.De("nada", false).Fundo, Paleta.De("tokyo", false).Fundo);

      Console.WriteLine("--- Dia típico (reset em 84 h = 3,5 dias; usado 50; pela média daria 100)");
      Confere("sem histórico: fica na média", Vel(50, null).Projecao, 100);
      Confere("só o dia de hoje: ainda na média", Vel(50, H(50, 1, 49.5)).Projecao, 100);
      Conta d7 = Vel(50, Dias(5, 5, 5, 5, 5, 5, 5));
      Confere("7 dias de 5 pts: dia típico", Cota.Inteiro(d7.VelDia), 5);
      Confere("7 dias: projeção só pelo dia típico (50 + 17,5)", d7.Projecao, 68);
      Confere("7 dias: a média continua guardada", d7.ProjecaoMedia, 100);
      Confere("cor não muda com a velocidade", d7.Cor, "amarelo");
      Confere("um dia de pico é ignorado", Cota.Inteiro(Vel(50, Dias(2, 2, 2, 40)).VelDia), 2);
      Confere("com 2 dias ainda não ignora nada", Cota.Inteiro(Vel(50, Dias(4, 6)).VelDia), 5);
      Confere("2 dias: pesa 2/7 sobre a média", Vel(50, Dias(4, 6)).Projecao, 91);
      Confere("dias sem uso entram na conta", Vel(50, Dias(10, 0, 10, 0, 10, 0, 0)).VelDia.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "3.3");
      Confere("sem uso: projeção = usado", Vel(50, Dias(0, 0, 0, 0, 0, 0, 0)).Projecao, 50);
      Confere("avisa quando chega a 100% antes do reset", Vel(90, Dias(20, 20, 20, 20, 20, 20, 20)).DiasAte100, 0.5);
      Confere("não avisa se não chega", d7.DiasAte100, -1);
      List<Ponto> virou = Dias(5, 5, 5, 5, 5, 5, 5);
      for (int i = 0; i < 4; i++) virou[i].Reset = virou[i].Reset.AddDays(-7);
      Conta dv = Vel(50, virou);
      Confere("aprendizado atravessa a semana", Cota.Inteiro(dv.VelDia) >= 5, true);
      Confere("virada da semana não dá dia negativo", dv.VelDia >= 0, true);
      List<Ponto> comHoje = Dias(5, 5, 5, 5, 5, 5, 5);
      comHoje.Add(new Ponto { Lido = Agora.ToUnixTimeMilliseconds(), Usado = 99, Reset = comHoje[0].Reset });
      Confere("hoje, pela metade, não entra", Cota.Inteiro(Vel(50, comHoje).VelDia), 5);

      Console.WriteLine("--- Janela de 5 h");
      Confere("plano sem 5 h: não aparece", V5(null, null).Tem5h, false);
      Confere("reset já passou: não aparece", V5(L(40, -1, 1), null).Tem5h, false);
      Conta m50 = V5(L(50, 3, 0), null);    // 2 h passadas, 25 pts/h: faltam 50, 2 h, reset em 3 h
      Confere("sem histórico: usa a média da janela", Cota.Inteiro(m50.Vel5hHora), 25);
      Confere("avisa quando acaba antes do reset", Cota.Inteiro(m50.Acaba5hMin), 120);
      Confere("não avisa se não acaba", V5(L(40, 3, 0), null).Acaba5hMin, -1);
      Conta r30 = V5(L(40, 3, 0), P5(10, 60, 3));   // subiu 30 na última hora
      Confere("velocidade de agora vale mais que a média", Cota.Inteiro(r30.Vel5hHora), 30);
      Confere("velocidade de agora prevê o fim", Cota.Inteiro(r30.Acaba5hMin), 120);
      Confere("parou de usar: não prevê fim", V5(L(40, 3, 0), P5(40, 60, 3)).Acaba5hMin, -1);
      Confere("pouco histórico: cai na média", Cota.Inteiro(V5(L(40, 3, 0), P5(39, 5, 3)).Vel5hHora), 20);
      Confere("100%: esgotada", V5(L(100, 3, 0), null).Acaba5hMin, 0);
      Confere("leitura de 40 min: velha", V5(L(40, 3, 40), null).Velha5h, true);
      Confere("leitura velha: não prevê", V5(L(50, 3, 40), null).Acaba5hMin, -1);
      Confere("texto do fim", Desenho.TextoAcaba5h(80), "acaba em 1 h 20");
      Confere("5 h: projeção para na velocidade", V5(L(50, 3, 0), null).Proj5h, 100);
      Confere("5 h: risco no tempo que passou", V5(L(50, 3, 0), null).Esp5h, 40);

      Console.WriteLine("--- Velocidade de agora na semana");
      DateTimeOffset rv = Agora.AddDays(3); long lv = Agora.ToUnixTimeMilliseconds();
      Func<double, double, Ponto> PV = (horas, uso) => new Ponto { Lido = lv - (long)(horas * 3600000), Usado = uso, Reset = rv };
      Confere("sem histórico: sem velocidade", Cota.VelocidadeHora(new List<Ponto>(), lv, 20, rv), -1.0);
      Confere("menos de 10 min: sem velocidade", Cota.VelocidadeHora(new List<Ponto> { PV(0.1, 19) }, lv, 20, rv), -1.0);
      Confere("não espera dias: 4 pts em 2 h", Cota.VelocidadeHora(new List<Ponto> { PV(2, 16) }, lv, 20, rv), 2.0);
      Confere("usa a leitura mais antiga da janela", Cota.VelocidadeHora(new List<Ponto> { PV(10, 1), PV(2, 16), PV(1, 18) }, lv, 20, rv), 2.0);
      Confere("parado há horas: zero", Cota.VelocidadeHora(new List<Ponto> { PV(8, 20) }, lv, 20, rv), 0.0);
      Confere("ignora a semana anterior", Cota.VelocidadeHora(new List<Ponto> { new Ponto { Lido = lv - 3600000, Usado = 90, Reset = rv.AddDays(-7) } }, lv, 20, rv), -1.0);
      string tmp5 = Path.Combine(Path.GetTempPath(), "cota-barra-teste-" + Guid.NewGuid().ToString("N") + ".json");
      File.WriteAllText(tmp5, "{\"ultima:padrao\":{\"percentUsed\":18,\"resetsAt\":\"2026-11-09T19:00:00Z\",\"lido\":1793835996553},\"cinco_horas:padrao\":{\"percentUsed\":40,\"resetsAt\":\"2026-11-05T02:00:00Z\",\"lido\":1793835996553}}");
      Confere("lê a janela de 5 h do arquivo", Cota.Contas(Agora, tmp5)[0].Uso5h, 40);
      File.Delete(tmp5);

      Console.WriteLine("--- Onda de cor");
      Func<Aparencia, Bitmap> pil = ap => Desenho.Pilula(new[] { C(50, 84, 1) }, 1.5, 60, false, ap);
      using (Bitmap sem = pil(new Aparencia { AlfaFundo = 245 })) using (Bitmap com = pil(new Aparencia { AlfaFundo = 245, Onda = 0.5f })) using (Bitmap fora = pil(new Aparencia { AlfaFundo = 245, Onda = 2f })) using (Bitmap txt = pil(new Aparencia { AlfaFundo = 245, Barras = false, Onda = 0.5f })) using (Bitmap txt0 = pil(new Aparencia { AlfaFundo = 245, Barras = false })) {
        Confere("não muda o tamanho da pílula", com.Width == sem.Width && com.Height == sem.Height, true);
        Confere("muda as cores no meio da barra", Diferentes(sem, com) > 0, true);
        Confere("fora da barra não muda nada", Diferentes(sem, fora), 0);
        Confere("no estilo só texto não muda nada", Diferentes(txt0, txt), 0);
      }

      Console.WriteLine("--- Cota por modelo (reset em 84 h: esperado 50)");
      long lidoMs = Agora.AddMinutes(-1).ToUnixTimeMilliseconds();
      string reset84 = Agora.AddHours(84).ToString("o"), resetVelho = Agora.AddHours(-1).ToString("o");
      string tmpM = Path.Combine(Path.GetTempPath(), "cota-barra-teste-" + Guid.NewGuid().ToString("N") + ".json");
      File.WriteAllText(tmpM, "{\"ultima:padrao\":{\"percentUsed\":18,\"resetsAt\":\"" + reset84 + "\",\"lido\":" + lidoMs + "},\"modelos:padrao\":{\"lido\":" + lidoMs + ",\"janelas\":[{\"nome\":\"Fable\",\"percentUsed\":60,\"resetsAt\":\"" + reset84 + "\"},{\"nome\":\"Velho\",\"percentUsed\":5,\"resetsAt\":\"" + resetVelho + "\"}]}}");
      Conta cm = Cota.Contas(Agora, tmpM)[0];
      Confere("lê o modelo; vencido não aparece", cm.Modelos.Count, 1);
      Confere("nome do modelo", cm.Modelos[0].Rotulo, "Fable");
      Confere("mesma fórmula: esperado", cm.Modelos[0].Esperado, 50);
      Confere("mesma fórmula: projeção", cm.Modelos[0].Projecao, 120);
      Confere("mesma fórmula: cor", cm.Modelos[0].Cor, "vermelho");
      File.Delete(tmpM);
      using (Bitmap sm = Desenho.Pilula(new[] { C(18, 84, 1) }, 1.5, 60, false, new Aparencia())) using (Bitmap cmb = Desenho.Pilula(new[] { cm }, 1.5, 60, false, new Aparencia()))
        Confere("modelo aparece na pílula", cmb.Width > sm.Width, true);

      Console.WriteLine("--- Cota real agora");
      foreach (Conta c in Cota.Contas())
        Console.WriteLine(c.Rotulo + ": " + c.Estado + " " + c.Cor + " " + c.Usado + "% / " + c.Esperado + "% -> " + c.Projecao + "% (" + c.Idade + ")");

      if (a.Length > 0) {
        Directory.CreateDirectory(a[0]);
        Conta v = Cota.Calcular(L(18, 115, 3), "Padrão", Agora), t = Cota.Calcular(L(100, 10, 28), "Trabalho", Agora);
        Conta[][] casos = {
          new[] { v }, new[] { t }, new[] { Cota.Calcular(L(18, 115, 300), "Padrão", Agora) }, new[] { Cota.Calcular(L(18, -3, 300), "Trabalho", Agora) },
          new[] { Cota.Calcular(null, "Trabalho", Agora) }, new[] { v, t }
        };
        for (int i = 0; i < casos.Length; i++)
          using (Bitmap b = Desenho.Pilula(casos[i], 1.5, 60, false, new Aparencia { AlfaFundo = 245, Letra = 12 })) b.Save(Path.Combine(a[0], "exe-" + i + ".png"));
        using (Bitmap b = Desenho.Pilula(casos[5], 1.5, 60, true, new Aparencia { AlfaFundo = 245, Letra = 12 })) b.Save(Path.Combine(a[0], "exe-compacta.png"));
        foreach (string pal in new[] { "tokyo", "catppuccin", "terminal" })
          foreach (bool claro in new[] { false, true })
            using (Bitmap b = Desenho.Cartao(casos[5], 1.5, new Aparencia { NomePaleta = pal, Claro = claro })) b.Save(Path.Combine(a[0], "cartao-" + pal + (claro ? "-claro" : "") + ".png"));
        using (Bitmap b = Desenho.Cartao(new[] { casos[2][0], casos[3][0], casos[4][0] }, 1.5, new Aparencia())) b.Save(Path.Combine(a[0], "cartao-estados.png"));
        Conta a5 = Vel(50, Dias(5, 5, 5, 5, 5, 5, 5)); Cota.Calcular5h(a5, L(50, 3, 0), Agora, null);
        Conta b5 = Vel(90, Dias(20, 20, 20, 20, 20, 20, 20)); Cota.Calcular5h(b5, L(30, 4.5, 0), Agora, P5(25, 30, 4.5));
        using (Bitmap b = Desenho.Cartao(new[] { a5, b5, Vel(50, Dias(4, 6)), Vel(50, null) }, 1.5, new Aparencia())) b.Save(Path.Combine(a[0], "cartao-velocidade.png"));
        using (Bitmap b = Desenho.Pilula(new[] { a5, b5 }, 1.5, 60, false, new Aparencia { AlfaFundo = 245 })) b.Save(Path.Combine(a[0], "pilula-5h.png"));
      }
      Console.WriteLine();
      Console.WriteLine(falhas > 0 ? falhas + " FALHA(S)" : "Tudo confere.");
      return falhas > 0 ? 1 : 0;
    }
  }
}
