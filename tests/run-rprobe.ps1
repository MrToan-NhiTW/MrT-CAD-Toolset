param([string]$AutoCADPath='C:\Program Files\Autodesk\AutoCAD 2024')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$sandbox=Join-Path $taskRoot 'evidence\legend-coreconsole-profile'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$script=Join-Path $PSScriptRoot 'run-rprobe.scr'
$testDll=(Join-Path $taskRoot 'bin\NativeRProbeRuntime.dll').Replace('\','/')
[IO.File]::WriteAllLines($script,@('_NETLOAD',('"'+$testDll+'"'),'MRT_RPROBE_TEST','_QUIT','_N'),[Text.Encoding]::ASCII)
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName=Join-Path $AutoCADPath 'accoreconsole.exe'
$psi.Arguments='/s "'+$script+'" /l en-US'
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
[IO.File]::WriteAllText((Join-Path $taskRoot 'evidence\rprobe-coreconsole-log.txt'),$out.Result+[Environment]::NewLine+$err.Result,[Text.UTF8Encoding]::new($true))
$result=Join-Path $taskRoot 'evidence\rprobe-runtime.txt'
if(!(Test-Path -LiteralPath $result) -or (Get-Item -LiteralPath $result).LastWriteTimeUtc -lt $started){throw 'Native fixture did not run; see rprobe-coreconsole-log.txt.'}
Get-Content -LiteralPath $result
if(!(Select-String -LiteralPath $result -Pattern '^RESULT: PASS')){throw 'Native legend fixture failed.'}
