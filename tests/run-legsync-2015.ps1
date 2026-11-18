$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$sandbox=Join-Path $root 'evidence\legsync-2015-profile'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$scr=Join-Path $sandbox 'legsync.scr'
$dll=(Join-Path $root 'bin\LegendPipeSyncRuntime.dll').Replace('\','/')
[IO.File]::WriteAllLines($scr,@('_NETLOAD',('"'+$dll+'"'),'MRTZ0PL2020TEST','MRTLEGSYNC2015TEST','_QUIT','_N'),[Text.Encoding]::ASCII)
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName='C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe'
$psi.Arguments='/s "'+$scr+'" /l en-US /isolate MrTLegendSync2015 "'+$sandbox+'"'
$psi.WorkingDirectory=$sandbox;$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true;$psi.StandardOutputEncoding=[Text.Encoding]::Unicode
$psi.EnvironmentVariables['TEMP']=$sandbox;$psi.EnvironmentVariables['TMP']=$sandbox
$proc=[Diagnostics.Process]::Start($psi);$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync()
if(!$proc.WaitForExit(45000)){$proc.Kill();$proc.WaitForExit();Write-Output 'Native fixture timed out.'}
$log=$out.Result+[Environment]::NewLine+$err.Result
[IO.File]::WriteAllText((Join-Path $root 'evidence\legsync-2015-console.txt'),$log,[Text.UTF8Encoding]::new($true))
if($log -match 'RESULT: PASS' -and $log -match 'MRTZ0PL2020TEST PASS'){Write-Output 'Native legend and Z0 fixtures: PASS'}elseif($log -match 'registry keys|current profile'){Write-Output 'Native legend fixture could not start: AutoCAD profile/registry initialization failed.'}else{Write-Output ('Native legend fixture did not pass; exit code '+$proc.ExitCode)}
