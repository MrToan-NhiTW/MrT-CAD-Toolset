param([string]$AutoCADPath='C:\Program Files\Autodesk\AutoCAD 2024')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$scratch=Join-Path $taskRoot '.build-temp'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$generated=@()
foreach($pair in @(@('LegendRuntimeTests.cs','MRTLEGENDTEST','MRTHOTLEG194'),@('LabelRuntimeTests.cs','MRTLABELTEST','MRTHOTR194'))){$file=Join-Path $scratch $pair[0];[IO.File]::WriteAllText($file,[IO.File]::ReadAllText((Join-Path $PSScriptRoot $pair[0])).Replace($pair[1],$pair[2]),[Text.UTF8Encoding]::new($true));$generated+=$file}
$oldTemp=$env:TEMP;$oldTmp=$env:TMP
try{
 $env:TEMP=$scratch;$env:TMP=$scratch
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 $arguments=@('/nologo','/platform:x64','/target:library','/codepage:65001','/reference:System.dll','/reference:System.Core.dll','/reference:System.Runtime.Serialization.dll',('/reference:'+(Join-Path $AutoCADPath 'AcCoreMgd.dll')),('/reference:'+(Join-Path $AutoCADPath 'AcDbMgd.dll')),('/out:'+(Join-Path $taskRoot 'bin\LegendNativeTests194.dll')))
 $arguments+=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src\Core') -Filter *.cs | Select-Object -ExpandProperty FullName)
 $arguments+=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src\Engine') -Filter *.cs | Select-Object -ExpandProperty FullName)
 $arguments+=$generated
 $arguments+=@((Join-Path $taskRoot 'src\AssemblyInfo.cs'),(Join-Path $taskRoot 'src\TargetFramework.cs'))
 & $compiler @arguments
 if($LASTEXITCODE -ne 0){throw 'Current native fixture compilation failed'}
 Write-Output 'Compiled current backend into LegendNativeTests194.dll. Commands: MRTHOTLEG194 and MRTHOTR194. No production assemblies need to be reloaded.'
}finally{$env:TEMP=$oldTemp;$env:TMP=$oldTmp}
