param([string]$AutoCADPath='C:\Program Files\Autodesk\AutoCAD 2024')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$binPath=(Join-Path $projectRoot 'bin').Replace('\','/')
$scriptPath=Join-Path $PSScriptRoot 'run-engine.scr'
$lines=@('(setvar "FILEDIA" 0)','_NETLOAD',('"'+$binPath+'/EngineTests.dll"'),'MRTTEST','_QUIT','_N')
[IO.File]::WriteAllLines($scriptPath,$lines,[Text.Encoding]::ASCII)
$processInfo=New-Object Diagnostics.ProcessStartInfo
$processInfo.FileName=Join-Path $AutoCADPath 'accoreconsole.exe'
$processInfo.Arguments='/s "'+$scriptPath+'" /l en-US'
$processInfo.UseShellExecute=$false
$processInfo.CreateNoWindow=$true
$processInfo.RedirectStandardOutput=$true
$processInfo.RedirectStandardError=$true
$processInfo.StandardOutputEncoding=[Text.Encoding]::Unicode
$testStarted=[DateTime]::UtcNow
$process=[Diagnostics.Process]::Start($processInfo)
$outputTask=$process.StandardOutput.ReadToEndAsync()
$errorTask=$process.StandardError.ReadToEndAsync()
$process.WaitForExit()
[IO.File]::WriteAllText((Join-Path $projectRoot 'evidence\coreconsole-log.txt'),$outputTask.Result,[Text.Encoding]::UTF8)
Write-Output $errorTask.Result
$resultPath=Join-Path $projectRoot 'evidence\toolset-v1-autocad-tests.txt'
if($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $resultPath) -or (Get-Item -LiteralPath $resultPath).LastWriteTimeUtc -lt $testStarted) { throw 'AutoCAD did not complete the tests. Inspect evidence\coreconsole-log.txt.' }
if(!(Select-String -LiteralPath $resultPath -Pattern '^RESULT: PASS')) { throw 'AutoCAD integration assertions failed. Inspect evidence\toolset-v1-autocad-tests.txt.' }
Get-Content -LiteralPath $resultPath
