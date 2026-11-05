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
      Confere("Trabalho ausente vira \"sem\"", l[1].Estado, "sem");
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
          using (Bitmap b = Desenho.Pilula(casos[i], 1.5, 60, false)) b.Save(Path.Combine(a[0], "exe-" + i + ".png"));
        using (Bitmap b = Desenho.Pilula(casos[5], 1.5, 60, true)) b.Save(Path.Combine(a[0], "exe-compacta.png"));
      }
      Console.WriteLine();
      Console.WriteLine(falhas > 0 ? falhas + " FALHA(S)" : "Tudo confere.");
      return falhas > 0 ? 1 : 0;
    }
  }
}
