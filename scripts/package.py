#!/usr/bin/env python3
"""Executed by the VPS build container, never by onboarding."""
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import uuid
import time
import zipfile
import xml.etree.ElementTree as ET

out, documentation = (pathlib.Path(value).resolve() for value in sys.argv[1:])
artifacts = pathlib.Path('/artifacts')
artifacts.mkdir(parents=True, exist_ok=True)
# Application assemblies and release artifacts share one version source.
VERSION = ET.parse(pathlib.Path(__file__).resolve().parents[1] / 'desktop' / 'Directory.Build.props').findtext('./PropertyGroup/Version')
if not VERSION or len(VERSION.split('.')) != 3 or not all(part.isdigit() for part in VERSION.split('.')):
    raise ValueError('Expected a three-part release Version in Directory.Build.props')
NS = 'http://schemas.microsoft.com/wix/2006/wi'
ET.register_namespace('', NS)
def element(parent, tag, **attrs):
    return ET.SubElement(parent, '{'+NS+'}'+tag, attrs)
def stable(value):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, 'remoteadmin/0.1/'+value)).upper()
def package_app(app):
    started = time.monotonic()
    name = 'RemoteAdmin'+app
    print(f'[{name}] preparing MSI', flush=True)
    folder = out / name
    # Documentation belongs in its own artifact, not beside every runtime DLL.
    payload = list(folder.iterdir())
    unexpected = [p.name for p in payload if p.name != name+'.exe']
    if unexpected or not (folder/(name+'.exe')).is_file():
        raise RuntimeError(f'{name}: expected single EXE, unexpected publish output: {unexpected}')
    icon_source = pathlib.Path(__file__).resolve().parents[1]/'desktop'/'Assets'/('server.ico' if app=='Server' else 'viewer.ico')
    shutil.copyfile(icon_source, out/(name+'.ico'))
    wix = ET.Element('{'+NS+'}Wix')
    product = element(wix, 'Product', Id=stable(name+'/product/'+VERSION), Name=name, Language='1033', Version=VERSION, Manufacturer='RemoteAdmin', UpgradeCode=stable(name+'/upgrade'))
    element(product, 'Package', InstallerVersion='500', Compressed='yes', InstallScope='perMachine', Description=name+' interactive application')
    element(product, 'Icon', Id='AppIcon', SourceFile=name+'.ico')
    element(product, 'Property', Id='ARPPRODUCTICON', Value='AppIcon')
    element(product, 'Media', Id='1', Cabinet=name+'.cab', EmbedCab='yes')
    # Keep the original UpgradeCode and component GUID namespace across releases.
    upgrade = element(product, 'Upgrade', Id=stable(name+'/upgrade'))
    element(upgrade, 'UpgradeVersion', Minimum='0.0.0', Maximum=VERSION, IncludeMinimum='yes', IncludeMaximum='no', Property='PREVIOUSVERSIONSINSTALLED')
    element(upgrade, 'UpgradeVersion', Minimum=VERSION, IncludeMinimum='no', OnlyDetect='yes', Property='NEWERVERSIONDETECTED')
    element(product, 'Property', Id='PREVIOUSVERSIONSINSTALLED', Secure='yes')
    element(product, 'Property', Id='NEWERVERSIONDETECTED', Secure='yes')
    condition = element(product, 'Condition', Message='A newer version of this application is already installed.')
    condition.text = 'Installed OR NOT NEWERVERSIONDETECTED'
    sequence = element(product, 'InstallExecuteSequence')
    # Install the new files before retiring the previous product. Stable component
    # GUIDs protect shared files; the installer transaction can roll back on error.
    remove = element(sequence, 'RemoveExistingProducts', After='InstallExecute')
    remove.text = 'PREVIOUSVERSIONSINSTALLED'

    target = element(product, 'Directory', Id='TARGETDIR', Name='SourceDir')
    program = element(target, 'Directory', Id='ProgramFiles64Folder')
    install = element(program, 'Directory', Id='INSTALLDIR', Name=name)
    directories = {pathlib.PurePath('.'): install}
    feature = element(product, 'Feature', Id='Complete', Title=name, Level='1')
    for index, file in enumerate(sorted(folder.rglob('*'))):
        if not file.is_file():
            continue
        relative = file.relative_to(folder)
        current = pathlib.PurePath('.')
        parent = install
        for part in relative.parts[:-1]:
            current = current / part
            if current not in directories:
                directories[current] = element(parent, 'Directory', Id='D'+hashlib.sha256(str(current).encode()).hexdigest()[:24], Name=part)
            parent = directories[current]
        component_id = 'C'+str(index)
        component = element(parent, 'Component', Id=component_id, Guid=stable(name+'/'+relative.as_posix()), Win64='yes')
        element(component, 'File', Id='F'+str(index), Source=file.relative_to(out).as_posix(), Name=file.name, KeyPath='yes', DiskId='1')
        element(feature, 'ComponentRef', Id=component_id)
    # Shortcuts are in the common Start Menu; application is launched by the user,
    # never from a SYSTEM installer action or a hidden service.
    menu_root = element(target, 'Directory', Id='ProgramMenuFolder')
    menu = element(menu_root, 'Directory', Id='MenuDir', Name=name)
    comp = element(menu, 'Component', Id='ShortcutComponent', Guid=stable(name+'/shortcut'), Win64='yes')
    element(comp, 'Shortcut', Id='StartShortcut', Name=name, Target='[INSTALLDIR]'+name+'.exe', WorkingDirectory='INSTALLDIR')
    element(comp, 'RemoveFolder', Id='RemoveMenu', On='uninstall')
    element(comp, 'RegistryValue', Root='HKLM', Key='Software\\RemoteAdmin\\'+app, Name='Installed', Type='integer', Value='1', KeyPath='yes')
    element(feature, 'ComponentRef', Id='ShortcutComponent')
    manifest = out / (name+'.wxs')
    ET.ElementTree(wix).write(manifest, encoding='utf-8', xml_declaration=True)
    subprocess.run(['wixl', '-a', 'x64', '-o', str(artifacts/(name+'-'+VERSION+'-win-x64.msi')), manifest.name], cwd=out, check=True)
    print(f'[{name}] MSI done in {time.monotonic()-started:.1f}s; preparing ZIP', flush=True)
    with zipfile.ZipFile(artifacts/(name+'-'+VERSION+'-win-x64.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for file in sorted(folder.rglob('*')):
            if file.is_file():
                archive.write(file, str(pathlib.Path(name)/file.relative_to(folder)))
    setup = artifacts / (name+'-'+VERSION+'-Setup.exe')
    subprocess.run(['makensis', '-V2', '-INPUTCHARSET', 'UTF8',
                    '-DAPPNAME='+name, '-DVERSION='+VERSION,
                    '-DICON_PATH='+str(pathlib.Path(__file__).resolve().parents[1]/'desktop'/'Assets'/('server.ico' if app=='Server' else 'viewer.ico')),
                    '-DUPGRADE_CODE={'+stable(name+'/upgrade')+'}',
                    '-DMSI_PATH='+str(artifacts/(name+'-'+VERSION+'-win-x64.msi')),
                    '-DSETUP_OUTPUT='+str(setup),
                    str(pathlib.Path(__file__).resolve().parents[1]/'installer'/'setup.nsi')], check=True)
    print(f'[{name}] packaging complete in {time.monotonic()-started:.1f}s', flush=True)

# Separate MSI and ZIP work for Server/Client can use CPU concurrently.
with ThreadPoolExecutor(max_workers=2) as pool:
    list(pool.map(package_app, ('Server', 'Client')))

with zipfile.ZipFile(artifacts/('RemoteAdmin-'+VERSION+'-docs.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for file in sorted(documentation.rglob('*')):
        if file.is_file():
            archive.write(file, file.relative_to(documentation))

with (artifacts/'SHA256SUMS').open('w') as checksums:
    for file in sorted(artifacts.iterdir()):
        if file.suffix in ('.zip', '.msi', '.exe'):
            with file.open('rb') as stream:
                checksums.write(hashlib.file_digest(stream, 'sha256').hexdigest()+'  '+file.name+'\n')

# Actual sizes are measured only after the VPS has built both applications.
report = {'version': VERSION, 'applications': {}}
lines = [f'RemoteAdmin {VERSION}: actual VPS artifact sizes',
         'MiB = 1048576 bytes. Each EXE includes its own .NET runtime.', '']
for app in ('Server', 'Client'):
    name = 'RemoteAdmin'+app
    files = {'installed_exe': out/name/(name+'.exe'),
             'portable_zip': artifacts/(name+'-'+VERSION+'-win-x64.zip'),
             'msi': artifacts/(name+'-'+VERSION+'-win-x64.msi'),
             'setup_exe': artifacts/(name+'-'+VERSION+'-Setup.exe')}
    sizes = {label: file.stat().st_size for label, file in files.items()}
    report['applications'][app] = {'installed_files': 1, 'bytes': sizes}
    lines.append(name+' (1 installed EXE):')
    for label, size in sizes.items():
        lines.append(f'  {label}: {size:,} bytes / {size/1048576:.2f} MiB')
server = report['applications']['Server']['bytes']['installed_exe']
client = report['applications']['Client']['bytes']['installed_exe']
report['server_minus_client_bytes'] = server-client
lines.extend(['', f'Server minus Client EXE: {server-client:+,} bytes / {(server-client)/1048576:+.2f} MiB',
              'Similar sizes are expected: both applications embed the Windows Desktop .NET runtime.',
              'Native runtime libraries are extracted on first launch; this report does not measure extraction/cache disk usage.'])
(artifacts/'SIZE-REPORT.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
(artifacts/'SIZE-REPORT.txt').write_text('\n'.join(lines)+'\n', encoding='utf-8')
print('\n'.join(lines), flush=True)
