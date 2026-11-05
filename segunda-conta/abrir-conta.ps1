# Opens a separate copy of Claude Desktop for a second account.
# Login, conversations and settings of this copy stay in %USERPROFILE%\ClaudePerfis\<name>.
# Called by the .vbs and .bat files next to it. -SoConferir only finds the app, without opening it.
param([string]$Nome = 'Trabalho', [switch]$SoConferir)

$Nome = $Nome -replace '[^\w.-]+', '_'
if (-not $Nome) { Write-Host 'Invalid account name.'; exit 1 }

# The app can come from its own installer or from the Microsoft Store.
$exe = $null
$versao = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'AnthropicClaude') -Directory -Filter 'app-*' -ErrorAction SilentlyContinue |
    Sort-Object { try { [version]$_.Name.Substring(4) } catch { [version]'0.0' } } -Descending |
    Select-Object -First 1
if ($versao) { $exe = Join-Path $versao.FullName 'claude.exe' }
if (-not $exe) {
    $pacote = Get-AppxPackage -Name '*Claude*' -ErrorAction SilentlyContinue |
        Where-Object { $_.Publisher -like '*Anthropic*' } |
        Select-Object -First 1
    if ($pacote) {
        $achado = Get-ChildItem $pacote.InstallLocation -Recurse -Filter 'claude.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($achado) { $exe = $achado.FullName }
    }
}
if (-not $exe) { Write-Host 'Claude Desktop not found.'; exit 1 }

$dados = Join-Path $env:USERPROFILE "ClaudePerfis\$Nome"
Write-Host "Opening: $exe"
Write-Host "Account: $Nome ($dados)"
if ($SoConferir) { exit 0 }

[void][IO.Directory]::CreateDirectory($dados)
$env:CLAUDE_PERFIL = $Nome   # the cota plugin uses this to keep each account's quota apart
try {
    Start-Process -FilePath $exe -ArgumentList ('--user-data-dir="' + $dados + '"') -ErrorAction Stop
} catch {
    Write-Host "Could not open: $($_.Exception.Message)"
    exit 1
}
