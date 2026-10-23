param(
    [string]$PublishDirectory = 'artifacts/single-file'
)
$ErrorActionPreference = 'Stop'
$folder = (Resolve-Path -LiteralPath $PublishDirectory).Path
$entries = @(Get-ChildItem -LiteralPath $folder -Force)
if ($entries.Count -ne 1 -or $entries[0].PSIsContainer -or $entries[0].Name -cne 'MineDock.exe') {
    throw 'The publish directory must contain exactly one file: MineDock.exe.'
}
$exe = $entries[0]
$stream = [IO.File]::OpenRead($exe.FullName)
$reader = [IO.BinaryReader]::new($stream)
try {
    if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'MineDock.exe is not a Windows executable.' }
    $stream.Position = 0x3C
    $peOffset = $reader.ReadInt32()
    if ($peOffset -lt 0x40 -or $peOffset -gt $stream.Length - 6) {
        throw 'MineDock.exe has an invalid PE header.'
    }
    $stream.Position = $peOffset
    if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne 0x8664) {
        throw 'MineDock.exe must be a Windows x64 executable.'
    }
}
finally { $reader.Dispose() }
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'MineDock.App/MineDock.App.csproj') -Raw
$expected = [version]($project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe.FullName)
if ($version.FileMajorPart -ne $expected.Major -or $version.FileMinorPart -ne $expected.Minor -or
    $version.FileBuildPart -ne $expected.Build) {
    throw "Published version $($version.FileVersion) does not match $expected."
}
$hash = (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = Join-Path (Split-Path -Parent $folder) 'SHA256SUMS.txt'
[IO.File]::WriteAllText($checksum, "$hash  MineDock.exe`n", [Text.UTF8Encoding]::new($false))
Write-Output "Verified Windows x64 single EXE: $($exe.Length) bytes, version $($version.FileVersion)"
Write-Output "SHA-256: $hash"
