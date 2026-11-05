# Mede CPU, memória e identificadores dos protótipos abertos. Uso: medir-cpu.ps1 -Minutos 10
param([double]$Minutos = 10, [string]$Saida)
Add-Type -Namespace M -Name U -MemberDefinition '[DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr p, int f);'
$nucleos = [Environment]::ProcessorCount
$alvos = foreach ($n in 'pilula', 'bandeja') {
    $arq = Join-Path $env:LOCALAPPDATA "cota-barra\$n.pid"
    if (Test-Path $arq) {
        $p = Get-Process -Id ([int](Get-Content $arq)) -ErrorAction SilentlyContinue
        if ($p) { @{ Nome = $n; P = $p; Cpu0 = $p.TotalProcessorTime; Ws0 = $p.WorkingSet64; Pv0 = $p.PrivateMemorySize64; Gdi0 = [M.U]::GetGuiResources($p.Handle, 0); Usr0 = [M.U]::GetGuiResources($p.Handle, 1) } }
    }
}
$sw = [Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds ($Minutos * 60)
$seg = $sw.Elapsed.TotalSeconds
$linhas = foreach ($a in $alvos) {
    $p = $a.P; $p.Refresh()
    if ($p.HasExited) { "$($a.Nome): processo terminou durante a medição"; continue }
    $cpu = ($p.TotalProcessorTime - $a.Cpu0).TotalSeconds
    "{0}: CPU {1:N1} s em {2:N0} s = {3:N3}% de um núcleo = {4:N4}% do PC ({5} núcleos lógicos) | RAM {6:N0} -> {7:N0} MB (privada {8:N0} -> {9:N0} MB) | GDI {10} -> {11} | USER {12} -> {13}" -f $a.Nome, $cpu, $seg, (100 * $cpu / $seg), (100 * $cpu / $seg / $nucleos), $nucleos, ($a.Ws0 / 1MB), ($p.WorkingSet64 / 1MB), ($a.Pv0 / 1MB), ($p.PrivateMemorySize64 / 1MB), $a.Gdi0, [M.U]::GetGuiResources($p.Handle, 0), $a.Usr0, [M.U]::GetGuiResources($p.Handle, 1)
}
$linhas | ForEach-Object { Write-Host $_ }
if ($Saida) { $linhas | Set-Content $Saida -Encoding UTF8 }
