$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$sandbox=Join-Path $root 'evidence\rname-2014-profile'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$scr=Join-Path $sandbox 'rname.scr'
$dll=(Join-Path $root 'bin\RNameRuntime2014.dll').Replace('\','/')
[IO.File]::WriteAllLines($scr,@('_NETLOAD',('"'+$dll+'"'),'MRTRNAME2014TEST','_QUIT','_N'),[Text.Encoding]::ASCII)
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName='C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe'
$psi.Arguments='/s "'+$scr+'" /l en-US /isolate MrTRName2014 "'+$sandbox+'"'
$psi.WorkingDirectory=$sandbox;$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true;$psi.StandardOutputEncoding=[Text.Encoding]::Unicode
$psi.EnvironmentVariables['MRT_RNAME_TEST_DWG']=(Join-Path (Split-Path -Parent $root) 'medical-gas-check-source.dwg')
$psi.EnvironmentVariables['TEMP']=$sandbox;$psi.EnvironmentVariables['TMP']=$sandbox
$proc=[Diagnostics.Process]::Start($psi);$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync()
if(!$proc.WaitForExit(45000)){$proc.Kill();$proc.WaitForExit();Write-Output 'Native fixture timed out.'}
$log=$out.Result+[Environment]::NewLine+$err.Result
[IO.File]::WriteAllText((Join-Path $root 'evidence\rname-2014-console.txt'),$log,[Text.UTF8Encoding]::new($true))
if($log -match 'RESULT: PASS'){Write-Output 'Native DWG fixture: PASS'}elseif($log -match 'registry keys|current profile'){Write-Output 'Native DWG fixture could not start: AutoCAD profile/registry initialization failed.'}else{Write-Output ('Native DWG fixture did not pass; exit code '+$proc.ExitCode)}
