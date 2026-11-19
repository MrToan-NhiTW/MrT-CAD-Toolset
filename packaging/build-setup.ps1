$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$msi=Join-Path $root 'installer\MrT_CAD_Toolset_2.0.2.3_AutoCAD2024_x64.msi'
$hash=(Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash
$source='namespace MrT.CAD.Toolset.Setup { internal static class Payload { internal const string Sha256="'+$hash+'"; } }'
$digestSource=Join-Path $root '.build-temp\PayloadDigest.cs'
[IO.File]::WriteAllText($digestSource,$source,[Text.UTF8Encoding]::new($false))
$oldTemp=$env:TEMP;$oldTmp=$env:TMP
try{
    $env:TEMP=Join-Path $root '.build-temp';$env:TMP=$env:TEMP
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ('/out:'+(Join-Path $root 'installer\MrT_CAD_Toolset_2.0.2.3_Setup.exe')) ('/resource:'+$msi+',MrT.Toolset.Payload.msi') ('/win32manifest:'+(Join-Path $PSScriptRoot 'setup.manifest')) ('/win32icon:'+(Join-Path $root 'dist\MrT.CAD.Toolset.bundle\Contents\Win64\Resources\MrT.ico')) (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'SetupProgress.cs') $digestSource (Join-Path $root 'src\AssemblyInfo.cs') (Join-Path $root 'src\TargetFramework.cs')
    if($LASTEXITCODE -ne 0){throw 'Setup.exe build failed'}
    Write-Output 'Built offline Setup.exe with the complete MSI embedded; no network/download dependency.'
}finally{$env:TEMP=$oldTemp;$env:TMP=$oldTmp}
