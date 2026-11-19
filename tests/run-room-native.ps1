$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$sandbox=Join-Path $root 'evidence\rooms-2016-profile'
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$scr=Join-Path $sandbox 'rooms.scr'
$dll=(Join-Path $root 'bin\RoomNativeRuntime.dll').Replace('\','/')
# The console runs in its own isolated profile (/isolate below). With SECURELOAD=1 it refuses
# NETLOAD from bin, so only that throwaway profile trusts the bin folder; the user's AutoCAD
# profile and its SECURELOAD are not touched.
$bin=(Join-Path $root 'bin').Replace('\','/')
Remove-Item -LiteralPath (Join-Path $root 'bin\RoomNativeUndo.txt') -ErrorAction SilentlyContinue
[IO.File]::WriteAllLines($scr,@(('(setvar "TRUSTEDPATHS" "'+$bin+'")'),'_NETLOAD',('"'+$dll+'"'),'MRTROOMS2016TEST','MRTROOMSUNDOFIX','MRTROOMSUNDOAPPLY','_.UNDO','1','MRTROOMSUNDOCHECK','_QUIT','_Y'),[Text.Encoding]::ASCII)
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName='C:\Program Files\Autodesk\AutoCAD 2024\accoreconsole.exe'
$psi.Arguments='/s "'+$scr+'" /l en-US /isolate MrTRoom2016 "'+$sandbox+'"'
$psi.WorkingDirectory=$sandbox;$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true;$psi.StandardOutputEncoding=[Text.Encoding]::Unicode
$psi.EnvironmentVariables['TEMP']=$sandbox;$psi.EnvironmentVariables['TMP']=$sandbox
$proc=[Diagnostics.Process]::Start($psi);$out=$proc.StandardOutput.ReadToEndAsync();$err=$proc.StandardError.ReadToEndAsync()
if(!$proc.WaitForExit(150000)){$proc.Kill();$proc.WaitForExit();Write-Output 'Native fixture timed out.'}
$log=$out.Result+[Environment]::NewLine+$err.Result
[IO.File]::WriteAllText((Join-Path $root 'evidence\rooms-2016-console.txt'),$log,[Text.UTF8Encoding]::new($true))
if(($log -match 'RESULT: PASS — create') -and ($log -match 'RESULT: PASS UNDO')){Write-Output 'Native room fixture: PASS (incl. block labels and UNDO)'}elseif($log -match 'registry keys|current profile'){Write-Output 'Native room fixture could not start: AutoCAD profile/registry initialization failed.'}else{Write-Output ('Native room fixture did not pass; exit code '+$proc.ExitCode)}
