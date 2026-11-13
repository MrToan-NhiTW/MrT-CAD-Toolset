$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$sandbox=Join-Path $root 'evidence\density-2013-profile'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$script=Join-Path $sandbox 'density.scr'
$testDll=(Join-Path $root 'bin\DensityMixedRuntime2013.dll').Replace('\','/')
[IO.File]::WriteAllLines($script,@('_NETLOAD',('"'+$testDll+'"'),'MRTDENSITY2013TEST','_QUIT','_N'),[Text.Encoding]::ASCII)
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName='C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe'
$psi.Arguments='/s "'+$script+'" /l en-US /isolate MrTDensity2013 "'+$sandbox+'"'
$psi.WorkingDirectory=$sandbox
$psi.UseShellExecute=$false
$psi.CreateNoWindow=$true
$psi.RedirectStandardOutput=$true
$psi.RedirectStandardError=$true
$psi.StandardOutputEncoding=[Text.Encoding]::Unicode
$psi.EnvironmentVariables['MRT_DENSITY_TEST_DWG']=(Join-Path (Split-Path -Parent $root) 'medical-gas-check-source.dwg')
$psi.EnvironmentVariables['TEMP']=$sandbox
$psi.EnvironmentVariables['TMP']=$sandbox
$proc=[Diagnostics.Process]::Start($psi)
$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync()
if(!$proc.WaitForExit(45000)){$proc.Kill();$proc.WaitForExit();Write-Output 'Native density fixture timed out.'}
$log=$out.Result+[Environment]::NewLine+$err.Result
[IO.File]::WriteAllText((Join-Path $root 'evidence\density-2013-console.txt'),$log,[Text.UTF8Encoding]::new($true))
if($log -match 'RESULT: PASS'){Write-Output 'Native DWG fixture: PASS'}
elseif($log -match 'registry keys|current profile'){Write-Output 'Native DWG fixture could not start: AutoCAD profile/registry initialization failed.'}
else{Write-Output ('Native DWG fixture did not pass; exit code '+$proc.ExitCode)}
