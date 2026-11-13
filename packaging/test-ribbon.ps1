param([string]$AutoCADPath='C:\Program Files\Autodesk\AutoCAD 2024')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$oldTemp=$env:TEMP;$oldTmp=$env:TMP
try{
 $env:TEMP=Join-Path $root '.build-temp';$env:TMP=$env:TEMP
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 $arguments=@('/nologo','/target:exe','/platform:x64','/codepage:65001','/reference:System.Core.dll','/reference:System.Xaml.dll',('/out:'+(Join-Path $root 'bin\RibbonTests.exe')),('/reference:'+(Join-Path $AutoCADPath 'AdWindows.dll')))
 foreach($name in @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')){$arguments+=('/reference:'+(Join-Path $env:WINDIR ('Microsoft.NET\Framework64\v4.0.30319\WPF\'+$name)))}
 $arguments+=@((Join-Path $root 'tests\RibbonTests.cs'),(Join-Path $root 'src\Plugin\RibbonFactory.cs'),(Join-Path $root 'src\Plugin\RibbonCommandHandler.cs'))
 & $compiler @arguments
 if($LASTEXITCODE -ne 0){throw 'Ribbon object tests did not compile'}
 & (Join-Path $root 'bin\RibbonTests.exe') $root $AutoCADPath
 if($LASTEXITCODE -ne 0){throw 'Ribbon object tests failed; inspect evidence/ribbon-tests.txt'}
}finally{$env:TEMP=$oldTemp;$env:TMP=$oldTmp}
