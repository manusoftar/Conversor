<#
 Verifica que la carpeta de salida de Conversor sea ejecutable por sí sola:
  1. Existen todas las DLL referenciadas con HintPath en el .csproj (FFMpegCore, Instances, System.Text.Json, ...).
  2. Ninguna DLL referencia a otra con una versión distinta a la que está en la carpeta
     sin que exista un bindingRedirect en <exe>.config que la cubra (causa típica de
     "No se pudo cargar el archivo o ensamblado ..." en .NET Framework).
 Uso: pwsh tools/verify-dependencies.ps1 -OutputDir <carpeta> -ProjectFile Conversor/Conversor.csproj
#>
param(
    [Parameter(Mandatory = $true)][string]$OutputDir,
    [Parameter(Mandatory = $true)][string]$ProjectFile,
    [string]$ExeName = 'Conversor.exe'
)

$ErrorActionPreference = 'Stop'
$problems = New-Object System.Collections.Generic.List[string]

function Get-ManagedInfo([string]$path) {
    $stream = [System.IO.File]::OpenRead($path)
    try {
        $pe = New-Object System.Reflection.PortableExecutable.PEReader($stream)
        if (-not $pe.HasMetadata) { return $null }
        $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $def = $md.GetAssemblyDefinition()
        $refs = New-Object System.Collections.Generic.List[object]
        foreach ($h in $md.AssemblyReferences) {
            $r = $md.GetAssemblyReference($h)
            $refs.Add([pscustomobject]@{ Name = $md.GetString($r.Name); Version = $r.Version })
        }
        return [pscustomobject]@{ Name = $md.GetString($def.Name); Version = $def.Version; References = $refs }
    }
    catch [System.BadImageFormatException] { return $null }  # DLL nativa
    finally { $stream.Dispose() }
}

# --- 1) DLL requeridas por el .csproj ------------------------------------------------
[xml]$proj = Get-Content -Raw -LiteralPath $ProjectFile
$required = New-Object System.Collections.Generic.List[string]
foreach ($group in @($proj.Project.ItemGroup)) {
    foreach ($ref in @($group.Reference)) {
        if ($null -ne $ref -and $null -ne $ref.HintPath) {
            $hint = [string]$ref.HintPath
            if ($hint -notmatch 'packages') { continue }  # ignora referencias de plataforma (p. ej. netstandard de Mono)
            $required.Add([System.IO.Path]::GetFileName(($hint -replace '\\', '/')))
        }
    }
}
$required.Add('TextProgressBar.dll')
$required.Add($ExeName)

foreach ($file in ($required | Sort-Object -Unique)) {
    if (-not (Test-Path -LiteralPath (Join-Path $OutputDir $file))) {
        $problems.Add("Falta '$file' en la carpeta de salida.")
    }
}

# --- 2) bindingRedirects del .config -------------------------------------------------
$redirects = @{}
$configPath = Join-Path $OutputDir "$ExeName.config"
if (Test-Path -LiteralPath $configPath) {
    [xml]$cfg = Get-Content -Raw -LiteralPath $configPath
    $ns = New-Object System.Xml.XmlNamespaceManager($cfg.NameTable)
    $ns.AddNamespace('b', 'urn:schemas-microsoft-com:asm.v1')
    foreach ($da in $cfg.SelectNodes('//b:dependentAssembly', $ns)) {
        $id = $da.SelectSingleNode('b:assemblyIdentity', $ns)
        $br = $da.SelectSingleNode('b:bindingRedirect', $ns)
        if ($null -eq $id -or $null -eq $br) { continue }
        $range = $br.GetAttribute('oldVersion') -split '-'
        $key = $id.GetAttribute('name').ToLowerInvariant()
        if (-not $redirects.ContainsKey($key)) { $redirects[$key] = New-Object System.Collections.Generic.List[object] }
        $redirects[$key].Add([pscustomobject]@{
            Lo  = [version]$range[0]
            Hi  = [version]$range[$range.Length - 1]
            New = [version]$br.GetAttribute('newVersion')
        })
    }
}
else {
    $problems.Add("No existe '$ExeName.config' (sin él no hay bindingRedirects).")
}

# --- 3) versiones referenciadas vs. versiones presentes -------------------------------
$present = @{}
$infos = @{}
foreach ($f in Get-ChildItem -LiteralPath $OutputDir -File | Where-Object { $_.Extension -in '.dll', '.exe' }) {
    $info = Get-ManagedInfo $f.FullName
    if ($null -ne $info) {
        $infos[$f.Name] = $info
        $present[$info.Name.ToLowerInvariant()] = $info.Version
    }
}

$notInOutput = New-Object System.Collections.Generic.SortedSet[string]
foreach ($name in $infos.Keys) {
    foreach ($r in $infos[$name].References) {
        $key = $r.Name.ToLowerInvariant()
        if (-not $present.ContainsKey($key)) { [void]$notInOutput.Add($r.Name); continue }

        $actual = $present[$key]
        if ($r.Version -eq $actual) { continue }

        $covered = $false
        if ($redirects.ContainsKey($key)) {
            foreach ($rd in $redirects[$key]) {
                if ($r.Version -ge $rd.Lo -and $r.Version -le $rd.Hi -and $rd.New -eq $actual) { $covered = $true }
            }
        }
        if (-not $covered) {
            $problems.Add("$name referencia $($r.Name) $($r.Version) pero la carpeta contiene la $actual y no hay bindingRedirect que lo cubra.")
        }
    }
}

Write-Host "Ensamblados referenciados que no están en la salida (se asumen del framework/GAC):"
$notInOutput | ForEach-Object { Write-Host "  $_" }

if ($problems.Count -gt 0) {
    Write-Host ""
    foreach ($p in $problems) { Write-Host "::error::$p" }
    exit 1
}
Write-Host "OK: dependencias de '$OutputDir' consistentes."
