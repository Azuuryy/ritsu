"""Package Ritsu publish outputs into release archives.

The publish outputs are produced by Package.ps1 in .cache/Ritsu-<version>-<rid>.
This script stages the documentation and licences next to each build, preserves
the executable bits required by Linux and macOS, and writes a SHA-256 sidecar
for every archive.
"""

import argparse
import hashlib
import plistlib
import shutil
import stat
import tarfile
import zipfile
from pathlib import Path

PLATFORMS = {
    'win-x64': 'Windows x64',
    'linux-x64': 'Linux x64',
    'osx-x64': 'macOS x86_64',
    'osx-arm64': 'macOS arm64',
}

DOCUMENTS = ['GUIDE.md', 'ROADMAP.md', 'VALIDATION.md', 'CHANGELOG.md', 'VERSION', 'dependencies.json']

PREVIEW_NOTE = '''Ritsu {version} preview build for {platform}.

This package was cross-published from Windows and has not been launched or validated on {platform}. It is provided for volunteer compatibility feedback, not as a confirmed supported build.

Ritsu is an offline local-play client. Online services remain disabled. Report the OS version, CPU architecture, launch result, and torii-crash.txt if startup fails.

macOS package: unsigned and not notarized. macOS may display a Gatekeeper warning.
'''


def licenses(root):
    return {
        'LICENCE-client.txt': root / 'client/LICENCE',
        'LICENCE-Modosu.txt': root / 'client/LICENCE-Modosu.txt',
        'LICENCE-framework.txt': root / 'toriirefresh-framework/LICENCE',
        'LICENCE-resources.md': root / 'toriirefresh-resources/LICENCE.md',
        'THIRD-PARTY-NOTICES.txt': root / 'client/THIRD-PARTY-NOTICES.txt',
    }


def stage(root, rid, platform, version, staging):
    source = root / '.cache' / f'Ritsu-{version}-{rid}'
    executable = 'torii' if rid.startswith('linux') or rid.startswith('osx') else 'torii.exe'
    if not source.is_dir() or not (source / executable).exists():
        raise SystemExit(f'Missing publish output for {rid}: {source} (run Package.ps1 without -SkipPublish)')

    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
    shutil.copytree(source, staging, dirs_exist_ok=True)

    for name in DOCUMENTS:
        shutil.copy2(root / name, staging / name)
    for name, path in licenses(root).items():
        shutil.copy2(path, staging / name)
    shutil.copy2(root / 'README.md', staging / 'RITSU-README.md')
    return staging


def write_preview_note(staging, version, platform):
    (staging / 'PREVIEW-PLATFORMS.txt').write_text(PREVIEW_NOTE.format(version=version, platform=platform), encoding='utf-8')


def hash_asset(path):
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    Path(str(path) + '.sha256').write_text(f'{digest}  {path.name}\n', encoding='ascii')
    return digest


def package_windows(staging, output, version):
    (staging / 'Launch Ritsu.cmd').write_bytes(b'@echo off\r\nstart "" "%~dp0torii.exe" %*\r\n')
    name = f'Ritsu-{version}-win-x64'
    asset = output / f'{name}.zip'
    with zipfile.ZipFile(asset, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as zf:
        for path in sorted(staging.rglob('*')):
            rel = path.relative_to(staging).as_posix()
            if path.is_dir():
                zf.writestr(f'{name}/{rel}/', b'')
            else:
                zf.write(path, f'{name}/{rel}')
    return asset


def package_linux(staging, output, version):
    write_preview_note(staging, version, 'Linux x64')
    (staging / 'Launch Ritsu.sh').write_text(
        '#!/bin/sh\nSCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)\ncd "$SCRIPT_DIR"\nexec ./torii "$@"\n',
        encoding='utf-8',
        newline='\n',
    )
    name = f'Ritsu-{version}-linux-x64'
    asset = output / f'{name}.tar.gz'

    def tar_filter(info):
        base = Path(info.name).name
        if info.isdir():
            info.mode = 0o755
        elif base in {'torii', 'Launch Ritsu.sh'}:
            info.mode = 0o755
        else:
            info.mode = 0o644
        return info

    with tarfile.open(asset, 'w:gz', format=tarfile.PAX_FORMAT) as tf:
        tf.add(staging, arcname=name, filter=tar_filter)
    return asset


def package_macos(staging, output, root, version, rid, arch):
    app = staging / 'Torii Nova.app'
    contents = app / 'Contents'
    macos = contents / 'MacOS'
    resources = contents / 'Resources'
    macos.mkdir(parents=True)
    resources.mkdir(parents=True)

    write_preview_note(staging, version, f'macOS {arch}')
    for item in list(staging.iterdir()):
        if item == app or item.name == 'PREVIEW-PLATFORMS.txt' or item.name == 'Torii Nova.app':
            continue
        shutil.move(str(item), macos / item.name)
    shutil.copy2(root / 'client/osu.Desktop/torii.icns', resources / 'torii.icns')

    plist = {
        'CFBundleName': 'Torii Nova',
        'CFBundleDisplayName': 'Torii Nova',
        'CFBundleIdentifier': 'com.shikkesora.torii',
        'CFBundleVersion': version,
        'CFBundleShortVersionString': version,
        'CFBundlePackageType': 'APPL',
        'CFBundleSignature': '????',
        'CFBundleExecutable': 'torii',
        'CFBundleIconFile': 'torii',
        'NSPrincipalClass': 'NSApplication',
        'NSHighResolutionCapable': True,
    }
    with (contents / 'Info.plist').open('wb') as f:
        plistlib.dump(plist, f, sort_keys=False)

    name = f'Ritsu-{version}-{rid}'
    asset = output / f'{name}.zip'
    with zipfile.ZipFile(asset, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as zf:
        for path in sorted(staging.rglob('*')):
            rel = path.relative_to(staging).as_posix()
            arc = f'{name}/{rel}' + ('/' if path.is_dir() else '')
            mode = (stat.S_IFDIR | 0o755) if path.is_dir() else (stat.S_IFREG | (0o755 if path.name == 'torii' else 0o644))
            info = zipfile.ZipInfo(arc)
            info.external_attr = mode << 16
            if path.is_dir():
                zf.writestr(info, b'')
            else:
                zf.writestr(info, path.read_bytes())
    return asset


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parent.parent)
    parser.add_argument('--output-dir', type=Path, default=None)
    parser.add_argument('--platforms', default=','.join(PLATFORMS))
    parser.add_argument('--version', default=None)
    args = parser.parse_args()

    root = args.root.resolve()
    output = (args.output_dir or root / 'artifacts').resolve()
    output.mkdir(parents=True, exist_ok=True)
    version = args.version or (root / 'VERSION').read_text(encoding='utf-8').strip()
    platforms = [item.strip() for item in args.platforms.split(',') if item.strip()]

    unknown = [item for item in platforms if item not in PLATFORMS]
    if unknown:
        raise SystemExit(f'Unknown platform(s): {", ".join(unknown)}')

    staging_root = root / '.cache' / 'packaging' / version
    for rid in platforms:
        staging = stage(root, rid, PLATFORMS[rid], version, staging_root / rid)
        if rid == 'win-x64':
            asset = package_windows(staging, output, version)
        elif rid == 'linux-x64':
            asset = package_linux(staging, output, version)
        else:
            arch = 'arm64' if rid == 'osx-arm64' else 'x86_64'
            asset = package_macos(staging, output, root, version, rid, arch)
        print(f'{asset.name} SHA256 {hash_asset(asset)}')


if __name__ == '__main__':
    main()
