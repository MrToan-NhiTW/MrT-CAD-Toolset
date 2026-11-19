param([string]$AutoCADPath='C:\Program Files\Autodesk\AutoCAD 2024',[switch]$SkipNativeFixtures)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$profileHashBefore=(Get-FileHash -LiteralPath (Join-Path $projectRoot "profiles/mrt-default.json")).Hash
$previousTemp=$env:TEMP
$previousTmp=$env:TMP
$buildTemp=Join-Path $projectRoot '.build-temp'
New-Item -ItemType Directory -Path $buildTemp -Force | Out-Null
$env:TEMP=$buildTemp
$env:TMP=$buildTemp
try {
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$binaryRoot=Join-Path $projectRoot 'bin'
New-Item -ItemType Directory -Path $binaryRoot -Force | Out-Null
foreach($assembly in @('AcCoreMgd.dll','AcDbMgd.dll','AcMgd.dll')) {
    if(!(Test-Path -LiteralPath (Join-Path $AutoCADPath $assembly))) { throw "Missing AutoCAD 2024 API assembly: $assembly" }
}
function Compile-Module([string]$Name,[string[]]$Sources,[string[]]$References,[string]$Target='library') {
    if($SkipNativeFixtures -and $Name -like '*RegressionTests.dll'){Write-Output ('Preserved native fixture pending/loaded in CAD: '+$Name);return}
    $arguments=@('/nologo','/optimize+','/platform:x64',('/target:'+$Target),('/out:'+(Join-Path $binaryRoot $Name)), '/utf8output', '/warn:4', '/codepage:65001')
    foreach($reference in $References) { $arguments+=('/reference:'+$reference) }
    $arguments+=$Sources
    $arguments+=(Join-Path $projectRoot 'src\TargetFramework.cs')
    $arguments+=(Join-Path $projectRoot 'src\AssemblyInfo.cs')
    & $compiler @arguments
    if($LASTEXITCODE -ne 0) { throw "Build failed: $Name" }
}
$common=@('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xaml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')
Compile-Module 'MedGas2024.Core.dll' @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Core') -Filter *.cs | Select-Object -ExpandProperty FullName) $common
$core=Join-Path $binaryRoot 'MedGas2024.Core.dll'
Compile-Module 'RoomRulesTests.exe' @((Join-Path $projectRoot 'tests\RoomRulesTests.cs')) ($common+@($core)) 'exe'
& (Join-Path $binaryRoot 'RoomRulesTests.exe')
if($LASTEXITCODE -ne 0){throw 'Room ID tests failed.'}
Compile-Module 'RNameRulesTests.exe' @((Join-Path $projectRoot 'tests\RNameRulesTests.cs')) ($common+@($core)) 'exe'
& (Join-Path $binaryRoot 'RNameRulesTests.exe')
if($LASTEXITCODE -ne 0){throw 'R linetype naming tests failed.'}
Compile-Module 'QuickJoinTests.exe' @((Join-Path $projectRoot 'tests\QuickJoinTests.cs')) ($common+@($core)) 'exe'
& (Join-Path $binaryRoot 'QuickJoinTests.exe')
if($LASTEXITCODE -ne 0){throw 'Quick join planner tests failed.'}
Compile-Module 'NativeDirectionRulesTests.exe' @((Join-Path $projectRoot 'tests\NativeDirectionRulesTests.cs')) ($common+@($core)) 'exe'
& (Join-Path $binaryRoot 'NativeDirectionRulesTests.exe')
if($LASTEXITCODE -ne 0){throw 'Native R direction rules tests failed.'}
$api=@((Join-Path $AutoCADPath 'AcCoreMgd.dll'),(Join-Path $AutoCADPath 'AcDbMgd.dll'))
$wpf=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll') | ForEach-Object { Join-Path $env:WINDIR ('Microsoft.NET\Framework64\v4.0.30319\WPF\'+$_) }
Compile-Module 'MedGas2024.Engine.dll' @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Engine') -Filter *.cs | Select-Object -ExpandProperty FullName) ($common+@($core)+$api)
Compile-Module 'MedGas2024.UI.dll' @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\UI') -Filter *.cs | Select-Object -ExpandProperty FullName) ($common+@($core,'System.Windows.Forms.dll','System.Drawing.dll'))
Compile-Module 'RoomWorkbookTests.exe' @((Join-Path $projectRoot 'tests\RoomWorkbookTests.cs')) ($common+@($core)) 'exe'
& (Join-Path $binaryRoot 'RoomWorkbookTests.exe') (Join-Path $projectRoot 'tests\room_fixture.xlsx')
if($LASTEXITCODE -ne 0){throw 'Room workbook tests failed.'}
Compile-Module 'RoomUITests.exe' @((Join-Path $projectRoot 'tests\RoomUITests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'RoomUITests.exe') (Join-Path $projectRoot 'tests\room_fixture.xlsx')
if($LASTEXITCODE -ne 0){throw 'Room UI tests failed.'}
Compile-Module 'RNameUiTests.exe' @((Join-Path $projectRoot 'tests\RNameUiTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'RNameUiTests.exe')
if($LASTEXITCODE -ne 0){throw 'R linetype naming UI tests failed.'}
Compile-Module 'MrT.CAD.Toolset.dll' @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\Plugin') -Filter *.cs | Select-Object -ExpandProperty FullName) ($common+$api+$wpf+@($core,(Join-Path $AutoCADPath 'AcMgd.dll'),(Join-Path $AutoCADPath 'AdWindows.dll'),(Join-Path $AutoCADPath 'AcWindows.dll'),(Join-Path $binaryRoot 'MedGas2024.Engine.dll'),(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll'))
Compile-Module 'DensityTests.exe' @((Join-Path $projectRoot 'tests\DensityTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'DensityTests.exe')
if($LASTEXITCODE -ne 0){throw 'Density math and UI tests failed.'}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'tests\CoreTests.cs')) {
    Compile-Module 'CoreTests.exe' @((Join-Path $projectRoot 'tests\CoreTests.cs')) ($common+@($core)) 'exe'
    & (Join-Path $binaryRoot 'CoreTests.exe') $projectRoot
    if($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'profiles\mrt-default.json')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'profiles\mrt-default.json') -Destination (Join-Path $binaryRoot 'default-profile.json') -Force
}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'tests\EngineTests.cs')) {
    Compile-Module 'EngineTests.dll' @((Join-Path $projectRoot 'tests/EngineTests.cs'),(Join-Path $projectRoot 'tests/ToolsetRuntimeTests.cs'),(Join-Path $projectRoot 'tests/DrawingRuntimeTests.cs'),(Join-Path $projectRoot 'tests/ScaleRuntimeTests.cs'),(Join-Path $projectRoot 'tests/DirectionRuntimeTests.cs'),(Join-Path $projectRoot 'tests/BatchRuntimeTests.cs'),(Join-Path $projectRoot 'tests/LabelRuntimeTests.cs')) ($common+$api+@($core,(Join-Path $binaryRoot 'MedGas2024.Engine.dll')))
}
if(Test-Path -LiteralPath (Join-Path $projectRoot 'tests\UIPreview.cs')) {
    Compile-Module 'UIPreview.exe' @((Join-Path $projectRoot 'tests\UIPreview.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
    & (Join-Path $binaryRoot 'UIPreview.exe') $projectRoot
    if($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
}
Compile-Module 'DrawingCoreTests.exe' @((Join-Path $projectRoot 'tests\DrawingCoreTests.cs')) ($common+@($core)) 'exe'
& (Join-Path $binaryRoot 'DrawingCoreTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Drawing core tests failed.'}
Compile-Module 'DrawingUITests.exe' @((Join-Path $projectRoot 'tests\DrawingUITests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'DrawingUITests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Drawing UI tests failed.'}
# Compile the focused native reader/assignment regression with its own production sources,
# avoiding an older installed add-in resolving the Engine/Core assemblies during NETLOAD.
Compile-Module 'ReadRegressionTests.dll' (@((Join-Path $projectRoot 'tests/ReadRegressionTests.cs'),(Join-Path $projectRoot 'src/Engine/DrawingService.cs'),(Join-Path $projectRoot 'src/Engine/ScaleService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)
Compile-Module 'GeneralToolTests.exe' @((Join-Path $projectRoot 'tests/GeneralToolTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'GeneralToolTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'General-purpose tool tests failed.'}
if((Get-FileHash -LiteralPath (Join-Path $projectRoot "profiles/mrt-default.json")).Hash -ne $profileHashBefore){throw "Tests modified the shipped profile."}

Compile-Module 'DirectionTests.exe' @((Join-Path $projectRoot 'tests/DirectionTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'DirectionTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Direction rules/UI tests failed.'}
Compile-Module 'DirectionRegressionTests.dll' (@((Join-Path $projectRoot 'tests/DirectionRuntimeTests.cs'),(Join-Path $projectRoot 'src/Engine/DirectionService.cs'),(Join-Path $projectRoot 'src/Engine/ScaleService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)


Compile-Module 'BatchTests.exe' @((Join-Path $projectRoot 'tests/BatchTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'BatchTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Batch core/UI tests failed.'}
Compile-Module 'BatchRegressionTests.dll' (@((Join-Path $projectRoot 'tests/BatchRuntimeTests.cs'),(Join-Path $projectRoot 'src/Engine/BatchService.cs'),(Join-Path $projectRoot 'src/Engine/DrawingService.cs'),(Join-Path $projectRoot 'src/Engine/ScaleService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)


Compile-Module 'LabelTests.exe' @((Join-Path $projectRoot 'tests/LabelTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'LabelTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Label-group core/UI tests failed.'}


Compile-Module 'LabelRegressionTests.dll' (@((Join-Path $projectRoot 'tests/LabelRuntimeTests.cs'),(Join-Path $projectRoot 'src/Engine/LabelService.cs'),(Join-Path $projectRoot 'src/Engine/BatchService.cs'),(Join-Path $projectRoot 'src/Engine/DirectionService.cs'),(Join-Path $projectRoot 'src/Engine/DrawingService.cs'),(Join-Path $projectRoot 'src/Engine/ScaleService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)

Compile-Module 'SelectionTests.exe' @((Join-Path $projectRoot 'tests/SelectionTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'SelectionTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Selection/filter/embedded creation tests failed.'}
Compile-Module 'SelectionRegressionTests.dll' (@((Join-Path $projectRoot 'tests/SelectionRuntimeTests.cs'),(Join-Path $projectRoot 'src/Engine/PipeSelection.cs'),(Join-Path $projectRoot 'src/Engine/LabelService.cs'),(Join-Path $projectRoot 'src/Engine/BatchService.cs'),(Join-Path $projectRoot 'src/Engine/DirectionService.cs'),(Join-Path $projectRoot 'src/Engine/DrawingService.cs'),(Join-Path $projectRoot 'src/Engine/ScaleService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)
Compile-Module 'VisibilityTests.exe' @((Join-Path $projectRoot 'tests/VisibilityTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'VisibilityTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Visibility/assignment/sort UI tests failed.'}
Compile-Module 'VisibilityRegressionTests.dll' (@((Join-Path $projectRoot 'tests/VisibilityRuntimeTests.cs'),(Join-Path $projectRoot 'src/Engine/IsolationLayers.cs'),(Join-Path $projectRoot 'src/Engine/DrawingService.cs'),(Join-Path $projectRoot 'src/Engine/ScaleService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)
Compile-Module 'FamilyTests.exe' @((Join-Path $projectRoot 'tests/FamilyTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'FamilyTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Family inheritance and picker UI tests failed.'}
Compile-Module 'FamilyRegressionTests.dll' (@((Join-Path $projectRoot 'tests/FamilyRuntimeTests.cs'),(Join-Path $projectRoot 'src/Engine/FamilyService.cs'),(Join-Path $projectRoot 'src/Engine/FamilyNaming.cs'),(Join-Path $projectRoot 'src/Engine/LabelService.cs'),(Join-Path $projectRoot 'src/Engine/BatchService.cs'),(Join-Path $projectRoot 'src/Engine/DirectionService.cs'),(Join-Path $projectRoot 'src/Engine/DrawingService.cs'),(Join-Path $projectRoot 'src/Engine/ScaleService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)
Compile-Module 'CombinedPickTests.exe' @((Join-Path $projectRoot 'tests/CombinedPickTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'CombinedPickTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Combined pick and family failure reporting tests failed.'}
Compile-Module 'LegendTests.exe' @((Join-Path $projectRoot 'tests/LegendTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'LegendTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Legend layout/UI tests failed.'}
Compile-Module 'LegendRegressionTests.dll' (@((Join-Path $projectRoot 'tests/LegendRuntimeTests.cs'),(Join-Path $projectRoot 'src/Engine/LegendService.cs'))+@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src/Core') -Filter *.cs | Select-Object -ExpandProperty FullName)) ($common+$api)
Compile-Module 'IsolationAssignmentTests.exe' @((Join-Path $projectRoot 'tests/IsolationAssignmentTests.cs')) ($common+@($core)) 'exe'
& (Join-Path $binaryRoot 'IsolationAssignmentTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Isolation assignment retention tests failed.'}
Compile-Module 'HotfixTests.exe' @((Join-Path $projectRoot 'tests/HotfixTests.cs')) ($common+@($core,(Join-Path $binaryRoot 'MedGas2024.UI.dll'),'System.Windows.Forms.dll','System.Drawing.dll')) 'exe'
& (Join-Path $binaryRoot 'HotfixTests.exe') $projectRoot
if($LASTEXITCODE -ne 0){throw 'Hotfix scale/unit/POINT tests failed.'}
Compile-Module 'RoomNativeRuntime.dll' @((Join-Path $projectRoot 'tests/RoomNativeRuntime.cs')) ($common+$api+@($core,(Join-Path $binaryRoot 'MedGas2024.Engine.dll')))
Compile-Module 'LegendPipeSyncRuntime.dll' @((Join-Path $projectRoot 'tests/LegendPipeSyncRuntime.cs'),(Join-Path $projectRoot 'tests/FlattenRuntime2020.cs')) ($common+$api+@($core,(Join-Path $binaryRoot 'MedGas2024.Engine.dll')))
Write-Output 'Build complete: bin\MrT.CAD.Toolset.dll'
}
finally {
    $env:TEMP=$previousTemp
    $env:TMP=$previousTmp
}
