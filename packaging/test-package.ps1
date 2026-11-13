$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$oldTemp=$env:TEMP;$oldTmp=$env:TMP
try{
    $env:TEMP=Join-Path $root '.build-temp';$env:TMP=$env:TEMP
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $compiler /nologo /target:exe /platform:x64 /reference:System.Core.dll /reference:System.Xml.Linq.dll ('/out:'+(Join-Path $root 'bin\PackagingTests.exe')) (Join-Path $root 'tests\PackagingTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Packaging tests did not compile'}
    & (Join-Path $root 'bin\PackagingTests.exe') $root
    if($LASTEXITCODE -ne 0){throw 'Packaging checks failed; see evidence/packaging-tests.txt'}
    $unpacked=Join-Path $root 'evidence\cabinet-check'
    New-Item -ItemType Directory -Path $unpacked -Force | Out-Null
    & (Join-Path $env:WINDIR 'System32\expand.exe') '-F:*' (Join-Path $root 'evidence\embedded-payload.cab') $unpacked | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Cabinet extraction failed'}
    $map=Get-Content -LiteralPath (Join-Path $root 'packaging\msi-work\file-map.json') -Raw | ConvertFrom-Json
    $results=@($map | ForEach-Object {
        $original=Join-Path (Join-Path $root 'dist\MrT.CAD.Toolset.bundle') $_.Relative
        $expanded=Join-Path $unpacked $_.Id
        $originalHash=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
        $expandedHash=(Get-FileHash -LiteralPath $expanded -Algorithm SHA256).Hash
        if($originalHash -ne $expandedHash){throw ('Corrupt embedded payload: '+$_.Relative)}
        [pscustomobject]@{File=$_.Relative;SHA256=$originalHash;EmbeddedBytesMatch=$true}
    })
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'evidence\cabinet-integrity.json') -Encoding utf8
    Write-Output ('PASS: extracted all '+$results.Count+' embedded files and verified SHA256 against the bundle.')
}finally{$env:TEMP=$oldTemp;$env:TMP=$oldTmp}
