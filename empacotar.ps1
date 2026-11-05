# Builds dist\cota-barra-windows.zip: the source of the current commit plus bin\pilula.exe.
# Does not compile: it packs the same .exe that was tested. Run compilar.cmd first if the code changed.
$raiz = $PSScriptRoot
$exe = Join-Path $raiz 'bin\pilula.exe'
if (-not (Test-Path $exe)) { Write-Host 'Missing bin\pilula.exe. Run compilar.cmd.'; exit 1 }
$dist = Join-Path $raiz 'dist'
[void][IO.Directory]::CreateDirectory($dist)
$zip = Join-Path $dist 'cota-barra-windows.zip'
Remove-Item $zip -ErrorAction SilentlyContinue
& git -C $raiz archive --format=zip --prefix=cota-barra/ -o $zip HEAD
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [IO.Compression.ZipFile]::Open($zip, 'Update')
[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $exe, 'cota-barra/bin/pilula.exe')
$z.Dispose()
Write-Host "Done: $zip ($([int]((Get-Item $zip).Length / 1KB)) KB)"
Write-Host "SHA-256 of pilula.exe: $((Get-FileHash $exe -Algorithm SHA256).Hash)"
