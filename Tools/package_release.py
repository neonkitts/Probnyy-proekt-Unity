"""Package the complete Windows player, excluding personal saves and settings."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

project = Path(__file__).resolve().parents[1]
build = project / 'Builds' / 'Windows'
required = ['FairyBeer.exe', 'UnityPlayer.dll', 'FairyBeer_Data', 'MonoBleedingEdge']
for name in required:
    if not (build / name).exists():
        raise SystemExit(f'Missing player dependency: {name}')
release = project / 'Releases' / 'FairyBeer-Windows.zip'
release.parent.mkdir(exist_ok=True)
excluded = ('.save.json', '.settings.json', '.log', '.pdb')
with ZipFile(release, 'w', ZIP_DEFLATED, compresslevel=6) as z:
    for p in sorted(build.rglob('*')):
        if not p.is_file() or p.name.endswith(excluded):
            continue
        if p.name.startswith('Chibi') or 'BackUpThisFolder' in str(p):
            continue
        z.write(p, p.relative_to(build))
    z.writestr('START_HERE.txt', 'Fairy Beer 2.2.0 — Windows x64\nExtract every file before running FairyBeer.exe.\nControls: A=right, D=left, S=jump, W=crouch, Space/J=attack, Esc=pause.\nUse Settings for graphics and the full control reference.\n')
with ZipFile(release) as z:
    assert z.testzip() is None
    assert 'FairyBeer.exe' in z.namelist()
    assert 'FairyBeer_Data/Managed/Assembly-CSharp.dll' in z.namelist()
    assert not any(n.endswith(('.save.json', '.settings.json')) for n in z.namelist())
print(f'Verified release: {release.name} ({release.stat().st_size:,} bytes)')
