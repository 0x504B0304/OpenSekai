"""Developer-only assembly of the self-contained Windows analysis payload.

Run with the development analysis environment. End users never run pip/setup.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
VERSION = '3.12.10'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / '.audio-assist-bundle')
    parser.add_argument('--crt-root', type=Path, help='Visual Studio redistributable x64/Microsoft.VC143.CRT directory')
    parser.add_argument('--install-to', type=Path, help='Refresh AudioAssistEngine beside an already built OpenSekai.exe')
    args = parser.parse_args()
    dest = args.output.resolve()
    dest.mkdir(parents=True, exist_ok=True)
    runtime = dest / 'runtime'
    runtime.mkdir(exist_ok=True)
    if not (runtime / 'python.exe').exists():
        archive = dest / 'python-embed.zip'
        urllib.request.urlretrieve(f'https://www.python.org/ftp/python/{VERSION}/python-{VERSION}-embed-amd64.zip', archive)
        with zipfile.ZipFile(archive) as package:
            package.extractall(runtime)
        archive.unlink()
    (runtime / 'python312._pth').write_text('python312.zip\n.\nLib/site-packages\nimport site\n', encoding='utf-8')
    crt = args.crt_root
    if crt is None:
        candidates = sorted(Path(os.environ.get('ProgramFiles', 'C:/Program Files')).glob(
            'Microsoft Visual Studio/*/*/VC/Redist/MSVC/*/x64/Microsoft.VC14*.CRT'))
        crt = candidates[-1] if candidates else None
    if crt is None or not (crt / 'msvcp140.dll').exists():
        raise RuntimeError('Provide --crt-root pointing to the Visual Studio x64 redistributable CRT (not System32).')
    for library in crt.glob('*.dll'):
        shutil.copy2(library, runtime / library.name)
    packages = runtime / 'Lib/site-packages'
    temporary = ROOT / 'Logs/AudioAssistPackagingTemp'
    temporary.mkdir(parents=True, exist_ok=True)
    install_env = dict(os.environ, TEMP=str(temporary), TMP=str(temporary))
    if not (dest / 'dependencies.lock.json').exists():
        # Pin torch in this resolution too: never pull a second/CUDA torch wheel.
        subprocess.run([sys.executable, '-m', 'pip', 'install', '--target', str(packages),
                        '--upgrade', 'torch==2.8.0+cpu', 'torchaudio==2.8.0+cpu',
                        '--extra-index-url', 'https://download.pytorch.org/whl/cpu',
                        '-r', str(ROOT / 'Tools/AudioAssist/Legacy/requirements.txt')], check=True, env=install_env)
        listing = subprocess.check_output([str(runtime / 'python.exe'), '-I', '-m', 'pip', 'list', '--format=json']) if (packages / 'pip').exists() else subprocess.check_output([
            str(runtime / 'python.exe'), '-I', '-c',
            'import importlib.metadata as m,json;print(json.dumps(sorted([(d.metadata["Name"],d.version) for d in m.distributions()])))'])
        (dest / 'dependencies.lock.json').write_bytes(listing)
    models = dest / 'models'
    models.mkdir(exist_ok=True)
    import torch
    if not (models / 'htdemucs.pt').exists():
        from demucs.pretrained import get_model
        model = get_model('htdemucs')
        model = model.models[0] if hasattr(model, 'models') else model
        positional, kwargs = model._init_args_kwargs
        kwargs = dict(kwargs)
        kwargs['segment'] = float(kwargs['segment'])
        (models / 'htdemucs.json').write_text(json.dumps(dict(args=positional, kwargs=kwargs)), encoding='utf-8')
        torch.save(model.state_dict(), models / 'htdemucs.pt')
        del model
    if not (models / 'mms_fa.pt').exists():
        import torchaudio
        # Bundle the processed state dictionary to avoid network/cache loaders at runtime.
        model = torchaudio.pipelines.MMS_FA.get_model()
        torch.save(model.model.state_dict(), models / 'mms_fa.pt')
        del model
    decoder = dest / 'decoder'
    decoder.mkdir(exist_ok=True)
    for source in (ROOT / 'ffmpeg').iterdir():
        if source.is_file() and (source.suffix.lower() == '.dll' or source.name.lower() == 'ffmpeg.exe' or 'license' in source.name.lower()):
            shutil.copy2(source, decoder / source.name)
    license_source = ROOT / 'Builds/Release/win_amd64_ffmpeg/ffmpeg/LICENSE'
    if license_source.exists():
        shutil.copy2(license_source, decoder / 'LICENSE.txt')
    elif not (decoder / 'LICENSE.txt').exists():
        urllib.request.urlretrieve('https://www.gnu.org/licenses/gpl-3.0.txt', decoder / 'LICENSE.txt')
    version = subprocess.check_output([str(decoder / 'ffmpeg.exe'), '-version'])
    (decoder / 'BUILD.txt').write_bytes(version + b'\nSource/build recipes: https://github.com/BtbN/FFmpeg-Builds\nFFmpeg source: https://github.com/FFmpeg/FFmpeg\n')
    shutil.copy2(ROOT / 'Tools/AudioAssist/Legacy/worker.py', dest / 'worker.py')
    shutil.copy2(ROOT / 'Tools/AudioAssist/THIRD_PARTY_NOTICES.md', dest / 'THIRD_PARTY_NOTICES.md')
    # Embedded Python includes its LICENSE; wheels retain dist-info licenses.
    manifest = dict(schema=1, python=VERSION, torch='2.8.0+cpu', torchaudio='2.8.0+cpu', files=[])
    for path in sorted(dest.rglob('*')):
        if path.is_file() and path.name != 'manifest.json' and '__pycache__' not in path.parts and path.suffix.lower() not in {'.lib', '.pdb', '.h', '.hpp', '.cuh'}:
            manifest['files'].append(dict(path=path.relative_to(dest).as_posix(), bytes=path.stat().st_size,
                                          sha256=digest(path)))
    (dest / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(f'Packaged {len(manifest["files"])} files in {dest}', flush=True)
    if args.install_to:
        target = args.install_to.resolve()
        if target.name != 'AudioAssistEngine' or not (target.parent / 'OpenSekai.exe').exists() or target == dest:
            raise ValueError('--install-to must be AudioAssistEngine beside a built OpenSekai.exe')
        def owned_path(relative):
            path = (target / relative).resolve()
            if not path.is_relative_to(target):
                raise ValueError('Invalid manifest path')
            return path
        old_path = target / 'manifest.json'
        old = json.loads(old_path.read_text(encoding='utf-8')) if old_path.exists() else dict(files=[])
        names = {entry['path'] for entry in manifest['files']}
        # Remove only files explicitly owned by the previous generated manifest.
        for entry in old['files']:
            if entry['path'] not in names:
                owned_path(entry['path']).unlink(missing_ok=True)
        for entry in manifest['files']:
            path = owned_path(entry['path'])
            path.parent.mkdir(parents=True, exist_ok=True)
            if not path.exists() or path.stat().st_size != entry['bytes'] or digest(path) != entry['sha256']:
                shutil.copy2(dest / entry['path'], path)
            if digest(path) != entry['sha256']:
                raise RuntimeError('Installed file verification failed: ' + str(path))
        shutil.copy2(dest / 'manifest.json', old_path)
        print(f'Installed and verified {sum(e["bytes"] for e in manifest["files"]):,} bytes at {target}', flush=True)


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


if __name__ == '__main__':
    main()
