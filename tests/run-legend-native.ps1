param([string]$AutoCADPath='C:\Program Files\Autodesk\AutoCAD 2024')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$sandbox=Join-Path $taskRoot 'evidence\legend-coreconsole-profile'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$script=Join-Path $PSScriptRoot 'run-legend-native.scr'
$testDll=(Join-Path $taskRoot 'bin\LegendRegressionTests.dll').Replace('\','/')
[IO.File]::WriteAllLines($script,@('_NETLOAD',('"'+$testDll+'"'),'MRTLEGENDTEST','_QUIT','_N'),[Text.Encoding]::ASCII)
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName=Join-Path $AutoCADPath 'accoreconsole.exe'
$psi.Arguments='/s "'+$script+'" /l en-US /isolate MrTLegendTrial "'+$sandbox+'"'
$psi.WorkingDirectory=$sandbox
$psi.UseShellExecute=$false
$psi.CreateNoWindow=$true
$psi.RedirectStandardOutput=$true
$psi.RedirectStandardError=$true
$psi.StandardOutputEncoding=[Text.Encoding]::Unicode
$psi.EnvironmentVariables['TEMP']=$sandbox
$psi.EnvironmentVariables['TMP']=$sandbox
$started=[DateTime]::UtcNow
$process=[Diagnostics.Process]::Start($psi)
$out=$process.StandardOutput.ReadToEndAsync()
$err=$process.StandardError.ReadToEndAsync()
if(!$process.WaitForExit(45000)){$process.Kill();$process.WaitForExit();Write-Output 'CoreConsole timed out after 45 seconds; stopped this test helper only.'}
[IO.File]::WriteAllText((Join-Path $taskRoot 'evidence\legend-coreconsole-log.txt'),$out.Result+[Environment]::NewLine+$err.Result,[Text.UTF8Encoding]::new($true))
$result=Join-Path $taskRoot 'evidence\legend-runtime-tests.txt'
if(!(Test-Path -LiteralPath $result) -or (Get-Item -LiteralPath $result).LastWriteTimeUtc -lt $started){throw 'Native fixture did not run; see legend-coreconsole-log.txt.'}
Get-Content -LiteralPath $result
if(!(Select-String -LiteralPath $result -Pattern '^RESULT: PASS')){throw 'Native legend fixture failed.'}
