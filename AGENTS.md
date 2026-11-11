# Installing cota-barra: guide for AI assistants

A person asked you to help with this repository. This guide applies to any assistant. Reply in the person's language. Explain what each step does and get their OK before running commands; if you cannot run commands, give them the steps to do by hand.

## What this is

- **`cota` plugin** ([MuriloNM/claude-cota](https://github.com/MuriloNM/claude-cota)): runs inside Claude and saves the weekly quota reading.
- **cota-barra** (this repository): a small Windows 11 program that shows that reading over the taskbar, called "the pill". It only reads a local file. It uses no network and no credentials. It needs the plugin.

## Step 1. Ask first

Ask exactly one question before doing anything, and wait for the answer. Do not write a plan or recommend an option first:

> Do you want only the plugin, with the quota inside Claude, or the plugin and also the pill on the Windows taskbar?

If the person is not on Windows, the pill is not available: offer only the plugin.

## Step 2. Plugin

Skip if it is already installed.

- **Claude Code:** `claude plugin install cota --marketplace MuriloNM/claude-cota`
- **Claude desktop app, by hand:** click **+** next to the text box, then **Plugins**, **Add marketplace** with `MuriloNM/claude-cota`, and **Add plugin** `cota`.

If this fails, read the [plugin README](https://github.com/MuriloNM/claude-cota#readme). Then the person restarts Claude and sends one message, which creates the first reading.

The pill needs plugin **0.6 or newer**. Reinstalling can bring back an old version, because Claude installs from a local copy of the catalog that may be out of date. If the plugin is older than 0.6, refresh the catalog first, then restart Claude:

- **Claude Code:** `claude plugin marketplace update claude-cota`, then `claude plugin update cota`.
- **Claude desktop app:** the update button may be unavailable. Run `git -C "$HOME\.claude\plugins\marketplaces\claude-cota" pull`, then remove the plugin and add it again.

On an old version, `/cota` may also show `no connected MCP tool "get_usage" on a server named "ccd_session_mgmt"`. The same update fixes it.

If they asked only for the plugin, stop here and tell them the quota shows inside Claude.

## Step 3. Pill

Run in PowerShell, one block at a time:

```powershell
$zip = Join-Path $env:TEMP 'cota-barra-windows.zip'
Invoke-WebRequest 'https://github.com/MuriloNM/cota-barra/releases/latest/download/cota-barra-windows.zip' -OutFile $zip
```

```powershell
$destino = Join-Path $env:LOCALAPPDATA 'Programs'
Expand-Archive $zip -DestinationPath $destino -Force
Get-FileHash (Join-Path $destino 'cota-barra\bin\pilula.exe') -Algorithm SHA256
```

Compare that hash with the one in the text of the [release page](https://github.com/MuriloNM/cota-barra/releases/latest), under "SHA-256 of `bin\pilula.exe`". The digest GitHub shows next to the download is the zip's, not the program's. If the program's hash differs, stop and tell the person.

```powershell
cmd /c (Join-Path $destino 'cota-barra\instalar.cmd')
```

## Step 4. Check

```powershell
Get-Process pilula
```

| Result | Meaning | What to do |
|---|---|---|
| Process found | The pill is running | Ask the person to look at the taskbar, next to the clock |
| Output says `.exe` was blocked, or no process | The antivirus blocked it, or the old PowerShell version opened | See "Antivirus" |
| Pill shows `sem leitura` | No reading yet | Confirm the plugin is on, and send a message in Claude |
| Pill shows `atualize o plugin cota` | The plugin is older than 0.6 | Update it as in Step 2 |

## Step 5. Explain and finish

Tell the person: right-click opens the settings; hovering shows the details; the number only updates while Claude is open; the interface is in Brazilian Portuguese.

## Optional: a second account

Only if the person mentions a second account, such as a work one: tell them to double-click `segunda-conta\Claude Segunda Conta.vbs` in the extracted folder, sign in with that account and send a message. Always opening that account through this file keeps each account's quota apart. If it fails, `Claude Segunda Conta.bat` shows the error. Do not open it for them unless they ask.

## Antivirus

The program is new and unsigned, so an antivirus may block it. McAfee did on the author's PC; the cause is unconfirmed and it is treated as a false positive. Explain this honestly, and that the source is in `src/`.

Then the person chooses: create the exception themselves, or give up on the pill and keep the plugin. Compiling locally (delete `bin`, run `instalar.cmd`) is another option for those who prefer to read the code, but it can be blocked too. Do not offer `pilula.ps1` as a solution: it is an old, unmaintained fallback.

## Do not do these unless the person asks

- Disable the antivirus, change its settings or add exceptions.
- Turn on "abrir com o windows" (start with Windows) or edit the registry.
- Read or use credential files, such as `.credentials.json`.
- Write to the plugin's files in `~/.claude/plugins/store`.

## Uninstall

Right-click the pill, turn off "abrir com o windows" if it is on, and click "fechar". Then delete `%LOCALAPPDATA%\Programs\cota-barra`, `%LOCALAPPDATA%\cota-barra` and `%USERPROFILE%\.cota-barra\historico.tsv`.

## Changing the code

Run `compilar.cmd`, then `bin\conferir.exe`, which must end with `Tudo confere.` See the [README](README.md).
