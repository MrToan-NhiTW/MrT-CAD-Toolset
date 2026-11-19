param([Parameter(Mandatory=$true)][string]$BundlePath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$BundlePath=[IO.Path]::GetFullPath($BundlePath)
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
$scratch=Join-Path $PSScriptRoot 'msi-work'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$cabPath=Join-Path $scratch 'MrTToolset.cab'
$files=@(Get-ChildItem -LiteralPath $BundlePath -File -Recurse | Sort-Object FullName)
$fileMap=@()
$directoryMap=@{''='INSTALLFOLDER'}
$index=0
foreach($file in $files){
    $index++
    $relative=$file.FullName.Substring($BundlePath.Length+1)
    $folder=[IO.Path]::GetDirectoryName($relative)
    $fileMap+=[pscustomobject]@{Id=('F{0:000}' -f $index);Component=('C{0:000}' -f $index);File=$file;Relative=$relative;Folder=$folder;Sequence=$index}
    while($folder -and !$directoryMap.ContainsKey($folder)){$directoryMap[$folder]='D'+$directoryMap.Count;$folder=[IO.Path]::GetDirectoryName($folder)}
}
$ddf=@('.OPTION EXPLICIT','.Set CabinetNameTemplate=MrTToolset.cab',('.Set DiskDirectoryTemplate="'+$scratch+'"'),'.Set CompressionType=MSZIP','.Set Cabinet=ON','.Set Compress=ON','.Set MaxDiskSize=0',('.Set RptFileName="'+(Join-Path $scratch 'cab-report.txt')+'"'),('.Set InfFileName="'+(Join-Path $scratch 'cab-inf.txt')+'"'))
$ddf+=@($fileMap | ForEach-Object {'"'+$_.File.FullName+'" '+$_.Id})
$ddfPath=Join-Path $scratch 'payload.ddf'
[IO.File]::WriteAllLines($ddfPath,$ddf,[Text.Encoding]::Default)
& (Join-Path $env:WINDIR 'System32\makecab.exe') /F $ddfPath | Out-Null
if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $cabPath)){throw 'Cabinet creation failed'}
if(Test-Path -LiteralPath $OutputPath){Remove-Item -LiteralPath $OutputPath -Force}
function Invoke-Com($object,[string]$method,[object[]]$arguments=@()){$object.GetType().InvokeMember($method,[Reflection.BindingFlags]::InvokeMethod,$null,$object,$arguments)}
function Set-Com($object,[string]$property,[object[]]$arguments){$object.GetType().InvokeMember($property,[Reflection.BindingFlags]::SetProperty,$null,$object,$arguments) | Out-Null}
$installer=New-Object -ComObject WindowsInstaller.Installer
$database=Invoke-Com $installer 'OpenDatabase' @($OutputPath,3)
function Sql([string]$sql){$view=Invoke-Com $database 'OpenView' @($sql);try{Invoke-Com $view 'Execute' | Out-Null}finally{Invoke-Com $view 'Close' | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null}}
function Row([string]$table,[string[]]$columns,[object[]]$values){
    $quoted=($columns | ForEach-Object {'`'+$_+'`'}) -join ','
    $marks=($columns | ForEach-Object {'?'}) -join ','
    $view=Invoke-Com $database 'OpenView' @('INSERT INTO `'+$table+'` ('+$quoted+') VALUES ('+$marks+')')
    $record=Invoke-Com $installer 'CreateRecord' @($values.Count)
    try{for($i=0;$i -lt $values.Count;$i++){if($null -eq $values[$i]){continue};if($values[$i] -is [int]){Set-Com $record 'IntegerData' @(($i+1),$values[$i])}else{Set-Com $record 'StringData' @(($i+1),[string]$values[$i])}};Invoke-Com $view 'Execute' @($record) | Out-Null}
    finally{Invoke-Com $view 'Close' | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null}
}
function Stream-Row([string]$table,[string]$key,[string]$path){
    $view=Invoke-Com $database 'OpenView' @('INSERT INTO `'+$table+'` (`Name`,`Data`) VALUES (?,?)')
    $record=Invoke-Com $installer 'CreateRecord' @(2)
    try{Set-Com $record 'StringData' @(1,$key);Invoke-Com $record 'SetStream' @(2,$path) | Out-Null;Invoke-Com $view 'Execute' @($record) | Out-Null}
    finally{Invoke-Com $view 'Close' | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) | Out-Null}
}
try{
    $tables=@(
    'CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` CHAR(0) LOCALIZABLE PRIMARY KEY `Property`)',
    'CREATE TABLE `Directory` (`Directory` CHAR(72) NOT NULL, `Directory_Parent` CHAR(72), `DefaultDir` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Directory`)',
    'CREATE TABLE `Component` (`Component` CHAR(72) NOT NULL, `ComponentId` CHAR(38), `Directory_` CHAR(72) NOT NULL, `Attributes` SHORT NOT NULL, `Condition` CHAR(255), `KeyPath` CHAR(72) PRIMARY KEY `Component`)',
    'CREATE TABLE `Feature` (`Feature` CHAR(38) NOT NULL, `Feature_Parent` CHAR(38), `Title` CHAR(64) LOCALIZABLE, `Description` CHAR(255) LOCALIZABLE, `Display` SHORT, `Level` SHORT NOT NULL, `Directory_` CHAR(72), `Attributes` SHORT NOT NULL PRIMARY KEY `Feature`)',
    'CREATE TABLE `FeatureComponents` (`Feature_` CHAR(38) NOT NULL, `Component_` CHAR(72) NOT NULL PRIMARY KEY `Feature_`,`Component_`)',
    'CREATE TABLE `File` (`File` CHAR(72) NOT NULL, `Component_` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL LOCALIZABLE, `FileSize` LONG NOT NULL, `Version` CHAR(72), `Language` CHAR(20), `Attributes` SHORT, `Sequence` LONG NOT NULL PRIMARY KEY `File`)',
    'CREATE TABLE `Media` (`DiskId` SHORT NOT NULL, `LastSequence` LONG NOT NULL, `DiskPrompt` CHAR(64) LOCALIZABLE, `Cabinet` CHAR(255), `VolumeLabel` CHAR(32), `Source` CHAR(72) PRIMARY KEY `DiskId`)',
    'CREATE TABLE `MsiFileHash` (`File_` CHAR(72) NOT NULL, `Options` SHORT NOT NULL, `HashPart1` LONG NOT NULL, `HashPart2` LONG NOT NULL, `HashPart3` LONG NOT NULL, `HashPart4` LONG NOT NULL PRIMARY KEY `File_`)',
    'CREATE TABLE `InstallExecuteSequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `InstallUISequence` (`Action` CHAR(72) NOT NULL, `Condition` CHAR(255), `Sequence` SHORT PRIMARY KEY `Action`)',
    'CREATE TABLE `LaunchCondition` (`Condition` CHAR(255) NOT NULL, `Description` CHAR(255) NOT NULL LOCALIZABLE PRIMARY KEY `Condition`)',
    'CREATE TABLE `Upgrade` (`UpgradeCode` CHAR(38) NOT NULL, `VersionMin` CHAR(20), `VersionMax` CHAR(20), `Language` CHAR(255), `Attributes` LONG NOT NULL, `Remove` CHAR(255), `ActionProperty` CHAR(72) NOT NULL PRIMARY KEY `UpgradeCode`,`VersionMin`,`VersionMax`,`Language`,`Attributes`)',
    'CREATE TABLE `CustomAction` (`Action` CHAR(72) NOT NULL, `Type` SHORT NOT NULL, `Source` CHAR(72), `Target` CHAR(255) PRIMARY KEY `Action`)',
    'CREATE TABLE `Icon` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)',
    'CREATE TABLE `AppSearch` (`Property` CHAR(72) NOT NULL, `Signature_` CHAR(72) NOT NULL PRIMARY KEY `Property`,`Signature_`)',
    'CREATE TABLE `Signature` (`Signature` CHAR(72) NOT NULL, `FileName` CHAR(255) NOT NULL, `MinVersion` CHAR(20), `MaxVersion` CHAR(20), `MinSize` LONG, `MaxSize` LONG, `MinDate` LONG, `MaxDate` LONG, `Languages` CHAR(255) PRIMARY KEY `Signature`)',
    'CREATE TABLE `RegLocator` (`Signature_` CHAR(72) NOT NULL, `Root` SHORT NOT NULL, `Key` CHAR(255) NOT NULL, `Name` CHAR(255), `Type` SHORT PRIMARY KEY `Signature_`)',
    'CREATE TABLE `TextStyle` (`TextStyle` CHAR(72) NOT NULL, `FaceName` CHAR(32) NOT NULL, `Size` SHORT NOT NULL, `Color` LONG, `StyleBits` SHORT PRIMARY KEY `TextStyle`)',
    'CREATE TABLE `Dialog` (`Dialog` CHAR(72) NOT NULL, `HCentering` SHORT NOT NULL, `VCentering` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Title` CHAR(128) LOCALIZABLE, `Control_First` CHAR(50) NOT NULL, `Control_Default` CHAR(50), `Control_Cancel` CHAR(50) PRIMARY KEY `Dialog`)',
    'CREATE TABLE `Control` (`Dialog_` CHAR(72) NOT NULL, `Control` CHAR(50) NOT NULL, `Type` CHAR(20) NOT NULL, `X` SHORT NOT NULL, `Y` SHORT NOT NULL, `Width` SHORT NOT NULL, `Height` SHORT NOT NULL, `Attributes` LONG, `Property` CHAR(72), `Text` CHAR(0) LOCALIZABLE, `Control_Next` CHAR(50), `Help` CHAR(50) LOCALIZABLE PRIMARY KEY `Dialog_`,`Control`)',
    'CREATE TABLE `ControlEvent` (`Dialog_` CHAR(72) NOT NULL, `Control_` CHAR(50) NOT NULL, `Event` CHAR(50) NOT NULL, `Argument` CHAR(255) NOT NULL, `Condition` CHAR(255), `Ordering` SHORT PRIMARY KEY `Dialog_`,`Control_`,`Event`,`Argument`,`Condition`)'
    )
    foreach($ddl in $tables){Sql $ddl}
    $product='{C8EB58E7-6BC6-4F8D-8CCA-A8B71D500D91}';$upgrade='{71842E5F-26DC-4C7F-93D6-F51A7EF6E89A}'
    $properties=@{ProductCode=$product;UpgradeCode=$upgrade;ProductName='Mr.T CAD tool set';ProductVersion='2.0.2.4';ProductLanguage='1033';Manufacturer='Mr.Toan-NhiTW';ALLUSERS='1';INSTALLLEVEL='1';ARPNOMODIFY='1';ARPPRODUCTICON='MrTIcon';ARPCOMMENTS='Layer + Linetype tools for AutoCAD 2024 x64';DefaultUIFont='Body';SecureCustomProperties='OLDPRODUCTS;NEWERPRODUCTS';MSIRESTARTMANAGERCONTROL='Disable';REBOOT='ReallySuppress'}
    foreach($key in $properties.Keys){Row 'Property' @('Property','Value') @($key,$properties[$key])}
    Row 'LaunchCondition' @('Condition','Description') @('VersionNT64','This package requires 64-bit Windows and full AutoCAD 2024. AutoCAD LT is not supported.')
    Row 'LaunchCondition' @('Condition','Description') @('Installed OR NOT NEWERPRODUCTS','A newer version of Mr.T CAD tool set is already installed.')
    Row 'LaunchCondition' @('Condition','Description') @('Installed OR NET48RELEASE >= "#528040"','.NET Framework 4.8 or later is required. Install the AutoCAD 2024 prerequisites first.')
    Row 'AppSearch' @('Property','Signature_') @('NET48RELEASE','DotNet48')
    Row 'RegLocator' @('Signature_','Root','Key','Name','Type') @('DotNet48',2,'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','Release',18)
    Row 'Upgrade' @('UpgradeCode','VersionMin','VersionMax','Language','Attributes','Remove','ActionProperty') @($upgrade,$null,'2.0.2.4',$null,512,$null,'OLDPRODUCTS')
    Row 'Upgrade' @('UpgradeCode','VersionMin','VersionMax','Language','Attributes','Remove','ActionProperty') @($upgrade,'2.0.2.4',$null,$null,2,$null,'NEWERPRODUCTS')
    Row 'Directory' @('Directory','Directory_Parent','DefaultDir') @('TARGETDIR',$null,'SourceDir')
    Row 'Directory' @('Directory','Directory_Parent','DefaultDir') @('ProgramFiles64Folder','TARGETDIR','.')
    Row 'Directory' @('Directory','Directory_Parent','DefaultDir') @('AutodeskFolder','ProgramFiles64Folder','Autodesk')
    Row 'Directory' @('Directory','Directory_Parent','DefaultDir') @('PluginsFolder','AutodeskFolder','ApplicationPlugins')
    Row 'Directory' @('Directory','Directory_Parent','DefaultDir') @('INSTALLFOLDER','PluginsFolder','MrT.CAD.Toolset.bundle')
    foreach($folder in @($directoryMap.Keys | Where-Object {$_} | Sort-Object Length)){
        Row 'Directory' @('Directory','Directory_Parent','DefaultDir') @($directoryMap[$folder],$directoryMap[[IO.Path]::GetDirectoryName($folder)],[IO.Path]::GetFileName($folder))
    }
    Row 'Feature' @('Feature','Feature_Parent','Title','Description','Display','Level','Directory_','Attributes') @('Toolset',$null,'Mr.T CAD tool set','Layer and Linetype manager for AutoCAD 2024',1,1,'INSTALLFOLDER',0)
    foreach($item in $fileMap){
        $md5=[Security.Cryptography.MD5]::Create();try{$digest=$md5.ComputeHash([Text.Encoding]::UTF8.GetBytes('MrT.CAD.Toolset.1|'+$item.Relative.ToLowerInvariant()))}finally{$md5.Dispose()}
        $guid=([Guid]::new($digest)).ToString('B').ToUpperInvariant()
        Row 'Component' @('Component','ComponentId','Directory_','Attributes','Condition','KeyPath') @($item.Component,$guid,$directoryMap[$item.Folder],256,$null,$item.Id)
        Row 'FeatureComponents' @('Feature_','Component_') @('Toolset',$item.Component)
        $version=$null;$language=$null
        if($item.File.Extension -eq '.dll'){$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($item.File.FullName).FileVersion;$language='0'}
        Row 'File' @('File','Component_','FileName','FileSize','Version','Language','Attributes','Sequence') @($item.Id,$item.Component,$item.File.Name,[int]$item.File.Length,$version,$language,16384,$item.Sequence)
        if(!$version){$md5=[Security.Cryptography.MD5]::Create();try{$bytes=[IO.File]::ReadAllBytes($item.File.FullName);$hash=$md5.ComputeHash($bytes)}finally{$md5.Dispose()};Row 'MsiFileHash' @('File_','Options','HashPart1','HashPart2','HashPart3','HashPart4') @($item.Id,0,[BitConverter]::ToInt32($hash,0),[BitConverter]::ToInt32($hash,4),[BitConverter]::ToInt32($hash,8),[BitConverter]::ToInt32($hash,12))}
    }
    Row 'Media' @('DiskId','LastSequence','DiskPrompt','Cabinet','VolumeLabel','Source') @(1,$files.Count,$null,'#MrTToolset.cab',$null,$null)
    Stream-Row '_Streams' 'MrTToolset.cab' $cabPath
    Stream-Row 'Icon' 'MrTIcon' (Join-Path $BundlePath 'Contents\Win64\Resources\MrT.ico')
    Row 'CustomAction' @('Action','Type','Source','Target') @('SetInstallLocation',51,'ARPINSTALLLOCATION','[INSTALLFOLDER]')
    $execute=@{FindRelatedProducts=25;AppSearch=50;LaunchConditions=100;ValidateProductID=700;CostInitialize=800;FileCost=900;CostFinalize=1000;SetInstallLocation=1100;InstallValidate=1400;InstallInitialize=1500;RemoveExistingProducts=1550;ProcessComponents=1600;UnpublishFeatures=1800;RemoveFiles=3500;RemoveFolders=3600;CreateFolders=3700;InstallFiles=4000;RegisterUser=6000;RegisterProduct=6100;PublishFeatures=6300;PublishProduct=6400;InstallFinalize=6600}
    foreach($action in $execute.Keys){Row 'InstallExecuteSequence' @('Action','Condition','Sequence') @($action,$null,[int]$execute[$action])}
    $ui=@{FindRelatedProducts=25;AppSearch=50;LaunchConditions=100;ValidateProductID=700;CostInitialize=800;FileCost=900;CostFinalize=1000;ExecuteAction=1300}
    foreach($action in $ui.Keys){Row 'InstallUISequence' @('Action','Condition','Sequence') @($action,$null,[int]$ui[$action])}
    Row 'TextStyle' @('TextStyle','FaceName','Size','Color','StyleBits') @('Body','Segoe UI',10,0,0)
    Row 'TextStyle' @('TextStyle','FaceName','Size','Color','StyleBits') @('Title','Segoe UI',17,0,1)
    function Control([string]$dialog,[string]$name,[string]$type,[int]$x,[int]$y,[int]$w,[int]$h,[string]$text,[string]$next=$null){$text=$text.Replace('\n',"`r`n");Row 'Control' @('Dialog_','Control','Type','X','Y','Width','Height','Attributes','Property','Text','Control_Next','Help') @($dialog,$name,$type,$x,$y,$w,$h,3,$null,$text,$next,$null)}
    function Event([string]$dialog,[string]$control,[string]$event,[string]$argument,[int]$order=1){Row 'ControlEvent' @('Dialog_','Control_','Event','Argument','Condition','Ordering') @($dialog,$control,$event,$argument,'1',$order)}
    function Dialog([string]$name,[string]$first,[string]$default,[string]$cancel){Row 'Dialog' @('Dialog','HCentering','VCentering','Width','Height','Attributes','Title','Control_First','Control_Default','Control_Cancel') @($name,50,50,400,280,3,'[ProductName] Setup',$first,$default,$cancel);Control $name 'Title' 'Text' 22 18 356 30 '{\Title}Mr.T CAD tool set';Control $name 'Footer' 'Text' 22 242 350 14 'workflow by Mr.Toan-NhiTW'}
    Dialog 'WelcomeDlg' 'Next' 'Next' 'Cancel'
    Control 'WelcomeDlg' 'Intro' 'Text' 22 62 354 130 'Install Mr.T CAD tool set 2.0.2.4 trial for full AutoCAD 2024 (64-bit).\n\nIncludes Layer + Linetype management and the new Legend table tool.\n\nSave your drawings and close every AutoCAD window before continuing.'
    Control 'WelcomeDlg' 'Next' 'PushButton' 230 210 70 23 'Next >' 'Cancel'
    Control 'WelcomeDlg' 'Cancel' 'PushButton' 308 210 70 23 'Cancel' 'Next'
    Event 'WelcomeDlg' 'Next' 'NewDialog' 'ReadyDlg';Event 'WelcomeDlg' 'Cancel' 'EndDialog' 'Exit'
    Dialog 'ReadyDlg' 'Install' 'Install' 'Cancel'
    Control 'ReadyDlg' 'Intro' 'Text' 22 62 354 130 'Ready to install. Windows may request administrator permission.\n\n[INSTALLFOLDER]\n\nAfter installation, open AutoCAD and type MRTTOOLS. Your saved JSON/DWG files are kept separately.'
    Control 'ReadyDlg' 'Install' 'PushButton' 230 210 70 23 'Install' 'Cancel'
    Control 'ReadyDlg' 'Cancel' 'PushButton' 308 210 70 23 'Cancel' 'Install'
    Event 'ReadyDlg' 'Install' 'EndDialog' 'Return';Event 'ReadyDlg' 'Cancel' 'EndDialog' 'Exit'
    Dialog 'MaintenanceDlg' 'Repair' 'Repair' 'Cancel'
    Control 'MaintenanceDlg' 'Intro' 'Text' 22 62 354 130 'Mr.T CAD tool set is installed. Close AutoCAD before continuing.\n\nRepair restores installed program files. Remove uninstalls this bundle. JSON and DWG files saved outside the install folder are kept.'
    Control 'MaintenanceDlg' 'Repair' 'PushButton' 146 210 70 23 'Repair' 'Remove'
    Control 'MaintenanceDlg' 'Remove' 'PushButton' 224 210 70 23 'Remove' 'Cancel'
    Control 'MaintenanceDlg' 'Cancel' 'PushButton' 302 210 70 23 'Cancel' 'Repair'
    Event 'MaintenanceDlg' 'Repair' '[REINSTALLMODE]' 'omus' 1;Event 'MaintenanceDlg' 'Repair' '[REINSTALL]' 'ALL' 2;Event 'MaintenanceDlg' 'Repair' 'EndDialog' 'Return' 3
    Event 'MaintenanceDlg' 'Remove' '[REMOVE]' 'ALL' 1;Event 'MaintenanceDlg' 'Remove' 'EndDialog' 'Return' 2;Event 'MaintenanceDlg' 'Cancel' 'EndDialog' 'Exit'
    foreach($name in @('ExitDlg','FatalDlg','CancelledDlg')){Dialog $name 'Finish' 'Finish' 'Finish';Control $name 'Finish' 'PushButton' 308 210 70 23 'Finish' 'Finish';Event $name 'Finish' 'EndDialog' 'Return'}
    Control 'ExitDlg' 'Intro' 'Text' 22 62 354 130 'Setup completed.\n\nIf installed: open AutoCAD 2024, select Mr.T CAD tool set or type MRTTOOLS. Use MRTHELP for the local guide.\n\nIf removed: the bundle has been uninstalled.'
    Control 'FatalDlg' 'Intro' 'Text' 22 62 354 130 'Setup could not finish. Windows Installer will roll back changes.\n\nClose AutoCAD, confirm administrator access and try again. For diagnostics run msiexec /i "package.msi" /L*v "setup.log".'
    Control 'CancelledDlg' 'Intro' 'Text' 22 62 354 130 'Setup was cancelled. No installation was completed.'
    Row 'InstallUISequence' @('Action','Condition','Sequence') @('WelcomeDlg','NOT Installed',1230)
    Row 'InstallUISequence' @('Action','Condition','Sequence') @('MaintenanceDlg','Installed AND NOT REMOVE',1240)
    Row 'InstallUISequence' @('Action','Condition','Sequence') @('ExitDlg',$null,-1)
    Row 'InstallUISequence' @('Action','Condition','Sequence') @('CancelledDlg',$null,-2)
    Row 'InstallUISequence' @('Action','Condition','Sequence') @('FatalDlg',$null,-3)
    $summary=$database.GetType().InvokeMember('SummaryInformation',[Reflection.BindingFlags]::GetProperty,$null,$database,@(20))
    try{
        foreach($pair in @(@(1,1252),@(2,'Mr.T CAD tool set Setup'),@(3,'AutoCAD 2024 Layer and Linetype tools'),@(4,'Mr.Toan-NhiTW'),@(7,'x64;1033'),@(9,([Guid]::NewGuid().ToString('B').ToUpperInvariant())),@(14,500),@(15,2),@(18,'Mr.T CAD tool set packaging'))){Set-Com $summary 'Property' $pair}
        Invoke-Com $summary 'Persist' | Out-Null
    }finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary) | Out-Null}
    Invoke-Com $database 'Commit' | Out-Null
    $fileMap | Select-Object Id,Component,Relative,Folder,Sequence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $scratch 'file-map.json') -Encoding utf8
    Write-Output ('MSI built: '+$OutputPath+' | '+$files.Count+' payload files')
}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null}
