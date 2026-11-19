param([string]$AutoCADPath='C:\Program Files\Autodesk\AutoCAD 2024',[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
if(!$SkipBuild){& (Join-Path $projectRoot 'build.ps1') -AutoCADPath $AutoCADPath}
$defaultProfile=Get-Content -LiteralPath (Join-Path $projectRoot 'bin/default-profile.json') -Raw | ConvertFrom-Json
if(@($defaultProfile.Rows).Count -ne 0){throw 'Release default library must be empty.'}
$bundle=Join-Path $projectRoot 'dist\MrT.CAD.Toolset.bundle'
$runtime=Join-Path $bundle 'Contents\Win64'
$resources=Join-Path $runtime 'Resources'
$help=Join-Path $runtime 'Help'
New-Item -ItemType Directory -Path $resources,$help,(Join-Path $projectRoot 'installer') -Force | Out-Null
foreach($oldIcon in @('manage32.png','assign32.png','direction32.png','pick32.png','help32.png','legend32.png')){ $oldPath=Join-Path $resources $oldIcon; if(Test-Path -LiteralPath $oldPath){Remove-Item -LiteralPath $oldPath} }
foreach($file in @('MrT.CAD.Toolset.dll','MedGas2024.Core.dll','MedGas2024.Engine.dll','MedGas2024.UI.dll','default-profile.json')){Copy-Item -LiteralPath (Join-Path $projectRoot ('bin\'+$file)) -Destination (Join-Path $runtime $file) -Force}
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\MrT_Z0Clean.lsp') -Destination (Join-Path $runtime 'MrT_Z0Clean.lsp') -Force
$fontBase=[IO.File]::ReadAllText((Join-Path $projectRoot 'packaging\MrT_ABC2Font.lsp'))
$fontSafe=[IO.File]::ReadAllText((Join-Path $projectRoot 'packaging\MrT_FontSafe2006.lsp'))
[IO.File]::WriteAllText((Join-Path $runtime 'MrT_ABC2Font.lsp'),($fontBase+"`r`n"+$fontSafe),(New-Object Text.UTF8Encoding($true)))
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\PackageContents.xml') -Destination (Join-Path $bundle 'PackageContents.xml') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\help.html') -Destination (Join-Path $help 'index.html') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-label-manager.png') -Destination (Join-Path $help 'ui-preview.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-batch-editor.png') -Destination (Join-Path $help 'ui-batch-editor.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-label-group-edit.png') -Destination (Join-Path $help 'ui-label-group-edit.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-embedded-creation.png') -Destination (Join-Path $help 'ui-creation.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-assign-isolate.png') -Destination (Join-Path $help 'ui-assign-isolate.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-add-family-sizes.png') -Destination (Join-Path $help 'ui-family-sizes.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-pick-mode.png') -Destination (Join-Path $help 'ui-pick-mode.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-pick-combined.png') -Destination (Join-Path $help 'ui-pick-combined.png') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'evidence\ui-legend.png') -Destination (Join-Path $help 'ui-legend.png') -Force
$oldTemp=$env:TEMP;$oldTmp=$env:TMP
try{
    $env:TEMP=Join-Path $projectRoot '.build-temp';$env:TMP=$env:TEMP
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /platform:x64 /codepage:65001 ('/out:'+(Join-Path $projectRoot 'bin\Assets.exe')) /reference:System.Drawing.dll ('/reference:'+(Join-Path $projectRoot 'bin\MedGas2024.UI.dll')) (Join-Path $projectRoot 'packaging\Assets.cs')
    if($LASTEXITCODE -ne 0){throw 'Asset build failed'}
    & (Join-Path $projectRoot 'bin\Assets.exe') $resources
    if($LASTEXITCODE -ne 0){throw 'Asset generation failed'}
    & (Join-Path $projectRoot 'packaging\build-msi.ps1') -BundlePath $bundle -OutputPath (Join-Path $projectRoot 'installer\MrT_CAD_Toolset_2.0.2.5_AutoCAD2024_x64.msi')
    & (Join-Path $projectRoot 'packaging\build-setup.ps1')
}finally{$env:TEMP=$oldTemp;$env:TMP=$oldTmp}
