$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$sandbox=Join-Path $root 'evidence\group-2012-profile'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$scr=Join-Path $sandbox 'group-2012.scr'
$dll=(Join-Path $root 'bin\LegendGroupRuntime2012.dll').Replace('\','/')
[IO.File]::WriteAllLines($scr,@('_NETLOAD',('"'+$dll+'"'),'MRTLEGEND2012TEST','_QUIT','_N'),[Text.Encoding]::ASCII)
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName='C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe'
$psi.Arguments='/s "'+$scr+'" /l en-US /isolate MrTGroup2012 "'+$sandbox+'"'
$psi.WorkingDirectory=$sandbox
$psi.UseShellExecute=$false
$psi.CreateNoWindow=$true
$psi.RedirectStandardOutput=$true
$psi.RedirectStandardError=$true
$psi.StandardOutputEncoding=[Text.Encoding]::Unicode
$psi.EnvironmentVariables['TEMP']=$sandbox
$psi.EnvironmentVariables['TMP']=$sandbox
$proc=[Diagnostics.Process]::Start($psi)
$stdout=$proc.StandardOutput.ReadToEndAsync()
$stderr=$proc.StandardError.ReadToEndAsync()
if(!$proc.WaitForExit(45000)){$proc.Kill();$proc.WaitForExit();Write-Output 'Native fixture timed out.'}
$output=$stdout.Result+[Environment]::NewLine+$stderr.Result
[IO.File]::WriteAllText((Join-Path $root 'evidence\legend-group-2012-console.txt'),$output,[Text.UTF8Encoding]::new($true))
if($output -match 'RESULT: PASS'){Write-Output 'Native group fixture: PASS'}
elseif($output -match 'FATAL ERROR: Problem with setting up current profile'){Write-Output 'Native group fixture did not run: AutoCAD could not initialize its profile.'}
else{Write-Output ('Native group fixture did not pass; exit code: '+$proc.ExitCode)}
