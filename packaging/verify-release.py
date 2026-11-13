"""Read package bytes, preserve references, and make a source archive. Never run Setup."""
import hashlib
import json
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[1]
setup = root / 'installer/MrT_CAD_Toolset_1.9.3_Setup.exe'
msi = root / 'installer/MrT_CAD_Toolset_1.9.3_AutoCAD2024_x64.msi'
assert msi.read_bytes() in setup.read_bytes(), 'Setup does not contain exact MSI payload'
for ref in json.loads((root/'evidence/source-manifest.json').read_text(encoding='utf-8-sig')):
    data = (root/'reference-originals'/ref['Name']).read_bytes()
    assert hashlib.sha256(data).hexdigest().upper() == ref['SHA256'].upper(), ref['Name']
doc = root/'docs/BANG_KY_HIEU_1.9.3.md'
doc.write_text(doc.read_text(encoding='utf-8-sig').replace('evidence/build-legend.log','evidence/build-final.log'),encoding='utf-8-sig')
verification = dict(Version='1.9.3',ProductCode='{3C08E0B8-8631-4B00-B649-9B483DD195F9}',
    SetupSHA256=hashlib.sha256(setup.read_bytes()).hexdigest(),
    Build='PASS',LegendLayoutUI='PASS',ScaleAndPointLogic='PASS',IsolationMembership='PASS',RibbonObjectTests='PASS',
    MSIContentsHashes='PASS',SetupContainsExactMSI=True,OriginalLINSamples='10 unchanged',
    NativeAutoCAD='NOT RUN for 1.9.3: compiled fixture only; previous CoreConsole attempt could not initialize profile',InstalledByAgent=False,
    Commands=['MRTLEGEND','MRTLEGEDIT','MRTR0','MRTRESTORERAW'],
    KnownLimits=['Associative HATCH capture, reactor detachment, source preservation, Undo and PDF require native AutoCAD testing',
    'WIPEOUT, draw order, nested/dynamic blocks, DIMENSION, MLEADER and PDF need AutoCAD GUI testing',
    'Native TABLE/picking/font/Undo/PDF/isolation visibility need AutoCAD GUI testing',
    'Isolation retention tracks groups created by Mr.T in the current session',
    'TABLE update replaces entity identity and may reset custom cell formatting', 'R0 preserves names and updates all shared definition references; preview first', 'Legacy hashed recovery skips ambiguous mappings'])
(root/'evidence/release-verification.json').write_text(json.dumps(verification,ensure_ascii=False,indent=2),encoding='utf-8')
output = root.parent/'MrT_CAD_Toolset_1.9.3_Source.zip'
assert not output.exists(), 'Source archive already exists; inspect before replacing'
omit = {'.build-temp','msi-work','cabinet-check','corrupt-icons','legend-coreconsole-profile'}
omit_files = {'msi-costing-log.txt','embedded-payload.cab','LegendRegressionTests.dll','LabelRegressionTests.dll'}
count = 0
with zipfile.ZipFile(output,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for source in sorted(root.rglob('*')):
        relative = source.relative_to(root)
        if not source.is_file() or omit.intersection(relative.parts) or source.name in omit_files:
            continue
        archive.write(source,root.name+'/'+relative.as_posix())
        count += 1
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None, 'Source archive CRC failed'
print(json.dumps(dict(setup=str(setup),setup_sha256=verification['SetupSHA256'],
    source_zip=str(output),source_files=count,source_bytes=output.stat().st_size,
    result='PASS: exact embedded MSI, 10 LIN hashes, source ZIP CRC'),ensure_ascii=False,indent=2))
