# cota-barra (protótipo)

Mostra a cota semanal do Claude fora do Claude, no Windows 11. Dois formatos para escolher:

| | Abrir | Fechar |
|---|---|---|
| Pílula sobre a barra | `abrir-pilula.cmd` | `fechar-pilula.cmd` |
| Ícone na bandeja | `abrir-bandeja.cmd` | `fechar-bandeja.cmd` |

Também dá para fechar pelo botão direito em cima de cada um. Nada abre sozinho com o Windows.

## Como usar

- **Pílula:** clique alterna entre as duas contas e cada uma sozinha; arrastar muda o lugar; botão direito abre o menu (contas, esconder 30 min, posição automática, fechar). Passar o mouse mostra o detalhe.
- **Bandeja:** um ícone por conta (Padrão = quadrado, Trabalho = círculo). Passar o mouse mostra o detalhe; botão direito escolhe as contas ou fecha.
  O Windows põe ícones novos na área oculta (`^`): arraste os dois para a barra para vê-los sempre.

## Regras

- **Dados:** só lê `~/.claude/plugins/store/cota_inline-633892c87774.json`, chaves `ultima:padrao` e `ultima:Trabalho`. Nunca grava nele, não usa rede nem credenciais. Grava só as preferências, em `%LOCALAPPDATA%\cota-barra`.
- **Fórmula:** igual à do plugin. `decorrido = 1 - resta/semana` (limitado a 0..1); `esperado = decorrido*100`; `projeção = usado/decorrido` se `decorrido > 0.02`, senão `usado`; verde se `usado - esperado < -5`, vermelho se `> 5`, amarelo no resto.
- **Idade da leitura:** sempre visível na pílula e na dica. Acima de 2 h vira "velha": bolinha vazada e aviso laranja (na bandeja, ícone cinza). Com `resetsAt` vencido, mostra "vencida", sem número.

## Pílula: casos tratados

- **Barra por cima:** volta ao topo quando a janela em primeiro plano muda e a cada 5 s.
- **Ícones:** procura espaço à esquerda da bandeja; se os ícones crescerem, passa para a direita dos widgets; sem espaço, encolhe para só os percentuais. Soltar em cima dos ícones é recusado.
- **Tela cheia:** some enquanto uma janela em tela cheia estiver em primeiro plano no monitor principal.
- **Barra oculta automaticamente:** some junto (não testado: exigiria mudar a configuração do Windows).
- **Tema claro:** acompanha o tema do Windows.

Limites: fica só no monitor principal; só atualiza enquanto o Claude grava o arquivo.

## Conferir e medir

```
powershell -NoProfile -ExecutionPolicy Bypass -File conferir.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File medir-cpu.ps1 -Minutos 10
```
