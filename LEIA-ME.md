# cota-barra (protótipo)

Mostra a cota semanal do Claude fora do Claude, no Windows 11, numa janelinha com cara de terminal sobre a barra de tarefas.

**Escolhida:** a pílula (Murilo, 04/11/2026). A bandeja fica no repositório, fechada.

| | Abrir | Fechar |
|---|---|---|
| Pílula (escolhida) | `abrir-pilula.cmd` | `fechar-pilula.cmd` |
| Ícone na bandeja | `abrir-bandeja.cmd` | `fechar-bandeja.cmd` |

Também dá para fechar pelo botão direito. Nada abre sozinho com o Windows.

## Como usar

- **Pílula:** clique alterna entre as duas contas e cada uma sozinha; arrastar muda o lugar; passar o mouse mostra o detalhe. O botão direito abre o menu: contas, monitor, tema (igual ao Windows, escuro, claro), tamanho (pequeno a grande), fundo (sólido, translúcido, transparente), fonte (as de largura fixa instaladas e a Segoe UI), voltar ao visual padrão, esconder 30 min, posição automática e fechar. Tudo fica salvo em `%LOCALAPPDATA%\cota-barra\pilula.json`.
- **Bandeja:** um ícone por conta (Padrão = quadrado, Trabalho = círculo). O Windows põe ícones novos na área oculta (`^`): arraste os dois para a barra.

## Como é feita

- **Pílula:** C# em `src/`, compilado para `bin/pilula.exe` por `compilar.cmd`, com o compilador que já vem no Windows. Não instala nada. `bin/` não vai para o git: rode `compilar.cmd` depois de clonar.
- **Reserva:** se o `.exe` não existir ou for barrado, `abrir-pilula.cmd` usa `pilula.ps1`, que tem o visual antigo.
- **Fonte:** JetBrains Mono, se instalada; senão Cascadia Mono, depois Consolas.
- **Bandeja:** PowerShell com WinForms (`bandeja.ps1`, `nucleo.ps1`).

## Regras

- **Dados:** só lê `~/.claude/plugins/store/cota_inline-633892c87774.json`, chaves `ultima:padrao` e `ultima:Trabalho`. Nunca grava nele, não usa rede nem credenciais. Grava só as preferências, em `%LOCALAPPDATA%\cota-barra`.
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
