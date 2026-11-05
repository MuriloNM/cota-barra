# cota-barra (protótipo)

Mostra a cota semanal do Claude fora do Claude, no Windows 11, numa janelinha com cara de terminal sobre a barra de tarefas.

## Instalar

Precisa de Windows 11 e do plugin `cota` no Claude: é ele que guarda a leitura que a pílula mostra.

1. **Plugin:** no app, clique em **+** ao lado do campo de texto, depois **Plugins**, **Add marketplace** com `MuriloNM/claude-cota` e **Add plugin** `cota`. Reinicie o Claude e mande uma mensagem, para gerar a primeira leitura.
2. **Pílula:** extraia esta pasta num lugar fixo, como `C:\cota-barra`, e dê dois cliques em `instalar.cmd`. Ele compila com o que já vem no Windows, não instala nada e abre a pílula.
3. **Uso:** botão direito na pílula abre os ajustes, inclusive "abrir com o windows".

**Antivírus:** o programa é compilado na sua máquina e não tem assinatura, então alguns antivírus podem barrar. Ele não usa rede nem credenciais, e o código está em `src/`. Só crie uma exceção se confiar em quem te passou. Se ele for barrado, a abertura cai numa versão antiga em PowerShell, que pode não achar a cota.

Só atualiza com o Claude aberto. A cota por modelo e a janela de 5 h ainda dependem de um mod de teste que não vem neste pacote.
## Os dois formatos

A pílula é o formato principal; a bandeja é uma alternativa antiga.

| | Abrir | Fechar |
|---|---|---|
| Pílula (escolhida) | `abrir-pilula.cmd` | `fechar-pilula.cmd` |
| Ícone na bandeja | `abrir-bandeja.cmd` | `fechar-bandeja.cmd` |

Também dá para fechar pelo botão direito. Para a pílula abrir com o Windows, clique em "abrir com o windows" no painel dela; clique de novo para desligar. Vale só para o seu usuário. A bandeja não abre sozinha.

## Como usar

- **Pílula:** clique alterna entre as duas contas e cada uma sozinha; arrastar muda o lugar; passar o mouse abre o cartão de detalhe. O botão direito abre o painel de ajustes: contas, monitor, estilo (barras ou só texto), paleta (Tokyo Night, Catppuccin, Terminal), onda nas barras (desligada por padrão; 5 s a 15 min; 3 pontos, na cor atual da barra), tema (igual ao Windows, escuro, claro), tamanho, fundo (sólido, translúcido, transparente), fonte (as de largura fixa instaladas e a Segoe UI), abrir com o Windows, visual padrão (pequeno, transparente e com barras), posição automática, esconder 30 min e fechar. Tudo fica salvo em `%LOCALAPPDATA%\cota-barra\pilula.json`.
- **Bandeja:** um ícone por conta (Padrão = quadrado, Trabalho = círculo). O Windows põe ícones novos na área oculta (`^`): arraste os dois para a barra.

## Como é feita

- **Pílula:** C# em `src/`, compilado para `bin/pilula.exe` por `compilar.cmd`, com o compilador que já vem no Windows. Não instala nada. `bin/` não vai para o git: rode `compilar.cmd` depois de clonar.
- **McAfee:** ele barra o `.exe` compilado como possível vírus, e a exceção no próprio McAfee parece valer só para aquele arquivo exato. O que funcionou depois de cada `compilar.cmd`: deixar o McAfee barrar, restaurar o arquivo da quarentena e manter `bin\pilula.exe` em Arquivos excluídos, nessa ordem, sem recompilar no meio.
- **Semana:** cada leitura nova vai para `%LOCALAPPDATA%\cota-barra\historico.tsv` (56 dias). O dia típico é a média de pontos por dia, entre semanas, contando dias sem uso e ignorando o maior a partir de 3 dias. A projeção mistura dia típico e média da semana, com peso de 0 a 100% até 7 dias de histórico. A cor e o esperado seguem a fórmula do plugin.
- **Janela de 5 h:** aparece só se o arquivo trouxer a chave `cinco_horas:<conta>`, com `percentUsed`, `resetsAt` e `lido`. A decisão é pela presença da janela, não pelo nome do plano. A velocidade é a da última hora; sem ela, a média desde o início da janela. **O plugin ainda não grava essa chave.**
- **Reserva:** se o `.exe` não existir ou for barrado, `abrir-pilula.cmd` usa `pilula.ps1`, que tem o visual antigo.
- **Fonte:** JetBrains Mono, se instalada; senão Cascadia Mono, depois Consolas.
- **Bandeja:** PowerShell com WinForms (`bandeja.ps1`, `nucleo.ps1`).

## Regras

- **Dados:** só lê o arquivo `cota_*.json` em `~/.claude/plugins/store`, chaves `ultima:padrao` e `ultima:Trabalho`. Nunca grava nele, não usa rede nem credenciais. Grava só as preferências, em `%LOCALAPPDATA%\cota-barra`.
- **Fórmula:** igual à do plugin. `decorrido = 1 - resta/semana` (limitado a 0..1); `esperado = decorrido*100`; `projeção = usado/decorrido` se `decorrido > 0.02`, senão `usado`; verde se `usado - esperado < -5`, vermelho se `> 5`, amarelo no resto.
- **Sinal no começo da linha:** `>` leitura boa, na cor do ritmo; `~` leitura com mais de 2 h, com aviso "velha" em laranja e número cinza; `x` semana vencida ou sem leitura, sem número.

## Pílula: casos tratados

- **Barra por cima:** volta ao topo quando a janela em primeiro plano muda e a cada 5 s.
- **Ícones:** procura espaço à esquerda da bandeja; se os ícones crescerem, passa para a direita dos widgets; sem espaço, encolhe para só os percentuais. Soltar em cima dos ícones é recusado.
- **Tela cheia:** some enquanto uma janela em tela cheia estiver em primeiro plano no monitor da pílula.
- **Barra oculta automaticamente:** some junto (não testado: exigiria mudar a configuração do Windows).
- **Monitores:** o menu lista os monitores que o Windows enxerga, e o que não tem barra de tarefas fica desabilitado. Se o escolhido for desconectado, a pílula vai para o principal e volta quando ele reconectar. Trocar de monitor zera a posição arrastada.

Limites: só atualiza enquanto o Claude grava o arquivo. Tema claro com fundo transparente sobre barra escura fica ilegível: a escolha é sua.

## Conferir e medir

```
compilar.cmd
bin\conferir.exe
powershell -NoProfile -ExecutionPolicy Bypass -File medir-cpu.ps1 -Minutos 10
```
