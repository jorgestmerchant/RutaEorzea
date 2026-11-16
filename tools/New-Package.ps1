param(
    [string]$Repository = 'jorgestmerchant/RutaEorzea',
    [string]$Version,
    [string]$Output = (Join-Path $PSScriptRoot '../artifacts')
)
$ErrorActionPreference = 'Stop'
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Nombre de repositorio no válido.' }
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dll = Join-Path $project 'bin/Release/RutaEorzea.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Compila la versión Release antes de empaquetar.' }
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
if (!$Version) { $Version = $assemblyVersion.ToString(4) }
if ([version]$Version -ne $assemblyVersion) { throw "Versión de paquete distinta de la DLL: $assemblyVersion" }
$Version = ([version]$Version).ToString(4)
$outputPath = [IO.Path]::GetFullPath($Output)
$staging = Join-Path $outputPath "RutaEorzea-$Version"
[IO.Directory]::CreateDirectory((Join-Path $staging 'images')) | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $project 'RutaEorzea.json') -Raw | ConvertFrom-Json -AsHashtable
$manifest.AssemblyVersion = $Version
$manifest.RepoUrl = "https://github.com/$Repository"
$manifest.IconUrl = "https://raw.githubusercontent.com/$Repository/main/images/icon.png"
$manifest.DownloadLinkInstall = "https://github.com/$Repository/releases/download/v$Version/latest.zip"
$manifest.DownloadLinkUpdate = $manifest.DownloadLinkInstall
$manifest.DownloadLinkTesting = $manifest.DownloadLinkInstall
$manifest.LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
$manifest.IsHide = $false
$manifest.IsTestingExclusive = $false
Copy-Item -LiteralPath $dll -Destination $staging
Copy-Item -LiteralPath (Join-Path $project 'images/icon.png') -Destination (Join-Path $staging 'images/icon.png')
foreach ($file in @('README.md','LEEME.md','INSTALAR.txt','GUIA-DISCORD.txt')) {
    Copy-Item -LiteralPath (Join-Path $project $file) -Destination $staging
}
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $staging 'RutaEorzea.json') -Encoding utf8NoBOM
ConvertTo-Json -InputObject @($manifest) -Depth 20 | Set-Content -LiteralPath (Join-Path $outputPath 'pluginmaster.json') -Encoding utf8NoBOM
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $staging | Select-Object -ExpandProperty FullName) -DestinationPath (Join-Path $outputPath 'latest.zip') -Force
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $outputPath 'latest.zip'))
try {
    foreach ($required in @('RutaEorzea.dll','RutaEorzea.json','images/icon.png','README.md')) {
        if (!$zip.GetEntry($required)) { throw "Falta $required en el paquete." }
    }
} finally { $zip.Dispose() }
Write-Output "Paquete validado: $Version"
