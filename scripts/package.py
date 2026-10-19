#!/usr/bin/env python3
"""Executed by the VPS build container, never by onboarding."""
from concurrent.futures import ThreadPoolExecutor
import hashlib
import pathlib
import shutil
import subprocess
import sys
import uuid
import time
import zipfile
import xml.etree.ElementTree as ET

out, documentation = map(pathlib.Path, sys.argv[1:])
artifacts = pathlib.Path('/artifacts')
artifacts.mkdir(parents=True, exist_ok=True)
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
    shutil.copytree(documentation, folder, dirs_exist_ok=True)
    wix = ET.Element('{'+NS+'}Wix')
    product = element(wix, 'Product', Id=stable(name+'/product'), Name=name, Language='1033', Version='0.1.0', Manufacturer='RemoteAdmin', UpgradeCode=stable(name+'/upgrade'))
    element(product, 'Package', InstallerVersion='500', Compressed='yes', InstallScope='perMachine', Platform='x64', Description=name+' interactive application')
    element(product, 'Media', Id='1', Cabinet='application.cab', EmbedCab='yes')
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
        element(component, 'File', Id='F'+str(index), Source=str(file), Name=file.name, KeyPath='yes', DiskId='1')
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
    subprocess.run(['wixl', '-a', 'x64', '-o', str(artifacts/(name+'-0.1.0-win-x64.msi')), str(manifest)], check=True)
    print(f'[{name}] MSI done in {time.monotonic()-started:.1f}s; preparing ZIP', flush=True)
    with zipfile.ZipFile(artifacts/(name+'-0.1.0-win-x64.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        for file in sorted(folder.rglob('*')):
            if file.is_file():
                archive.write(file, str(pathlib.Path(name)/file.relative_to(folder)))
    print(f'[{name}] packaging complete in {time.monotonic()-started:.1f}s', flush=True)

# Separate MSI and ZIP work for Server/Client can use CPU concurrently.
with ThreadPoolExecutor(max_workers=2) as pool:
    list(pool.map(package_app, ('Server', 'Client')))

with (artifacts/'SHA256SUMS').open('w') as checksums:
    for file in sorted(artifacts.iterdir()):
        if file.suffix in ('.zip', '.msi'):
            with file.open('rb') as stream:
                checksums.write(hashlib.file_digest(stream, 'sha256').hexdigest()+'  '+file.name+'\n')
