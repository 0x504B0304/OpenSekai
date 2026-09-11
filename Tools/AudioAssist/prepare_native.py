"""Developer-only conversion, native compilation and Unity asset assembly.

Run with the analysis development venv. No Python files enter the player.
"""
import argparse
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import urllib.request
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / '.audio-assist-native'
PIN = '81fa192e6fcc88e35e887f6e6ccce91227f4e6f5'
FILES = ['htdemucs.onnx', 'mms_fa.int8.onnx', 'tokens.json', 'ja-reading.tsv', 'ja-roman.tsv', 'zh-reading.tsv']


def run(*args):
    subprocess.run([str(a) for a in args], cwd=ROOT, check=True)


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def dependency(name, url, sha):
    archive = CACHE / 'deps' / (name + '.zip')
    archive.parent.mkdir(parents=True, exist_ok=True)
    if not archive.exists():
        urllib.request.urlretrieve(url, archive)
    if digest(archive) != sha:
        raise ValueError('Dependency checksum mismatch: ' + name)
    dest = archive.with_suffix('')
    if not dest.exists():
        with zipfile.ZipFile(archive) as package:
            package.extractall(dest)
    return dest


def plugin(source, platform):
    dest = ROOT / 'Assets/Plugins/AudioAssistNative' / platform / source.name
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, dest)
    win = platform == 'Windows/x86_64'
    meta = f'''fileFormatVersion: 2
guid: {uuid.uuid5(uuid.NAMESPACE_URL, dest.relative_to(ROOT).as_posix()).hex}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  isPreloaded: 0
  isOverridable: 0
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: {int(win)}
      settings:
        CPU: x86_64
        OS: Windows
        DefaultValueInitialized: true
  - first:
      Standalone: Win64
    second:
      enabled: {int(win)}
      settings:
        CPU: x86_64
  - first:
      Android: Android
    second:
      enabled: {int(not win)}
      settings:
        CPU: ARM64
  userData:
  assetBundleName:
  assetBundleVariant:
'''
    dest.with_name(dest.name + '.meta').write_text(meta, encoding='utf-8')


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--vs', type=Path, required=True, help='Visual Studio installation with Desktop C++')
    parser.add_argument('--unity', type=Path, required=True, help='Unity installation, containing Editor/')
    parser.add_argument('--models-ready', action='store_true', help='Reuse existing converted models')
    args = parser.parse_args()
    ort = dependency('ort', 'https://api.nuget.org/v3-flatcontainer/microsoft.ml.onnxruntime/1.22.1/microsoft.ml.onnxruntime.1.22.1.nupkg',
                     '7b091e012dcfef2f9cc965f2252041101b1840c748ffbc1c18ba047bd6508b1c')
    android = dependency('ort-android', 'https://repo.maven.apache.org/maven2/com/microsoft/onnxruntime/onnxruntime-android/1.22.0/onnxruntime-android-1.22.0.aar',
                         '04a4617a9c797cf49225595e45b5546081cb34c86ac817581141577d3b7dbfe2')
    fork = ROOT / '.utmp/demucs-onnx'
    if not fork.exists():
        run('git', 'clone', 'https://github.com/sevagh/demucs.onnx', fork)
    run('git', '-C', fork, 'checkout', '--detach', PIN)
    if not args.models_ready:
        for model in ['separator', 'aligner']:
            run(sys.executable, ROOT / 'Tools/AudioAssist/export_native_models.py', model)
        run(sys.executable, ROOT / 'Tools/AudioAssist/export_pronunciations.py')
    cmake = args.vs / 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
    ninja = args.vs / 'Common7/IDE/CommonExtensions/Microsoft/CMake/Ninja/ninja.exe'
    # Ninja plus the VS developer environment works across VS releases.
    # The selected installation determines the supported VS generator.
    generator = 'Visual Studio 18 2026' if (args.vs / 'VC/Tools/MSVC').exists() and '18' in str(args.vs) else 'Visual Studio 17 2022'
    win = CACHE / 'build-win'
    include = ort / 'build/native/include'
    run(cmake, '-S', ROOT / 'Tools/AudioAssist/Native', '-B', win, '-G', generator, '-A', 'x64',
        '-DCMAKE_GENERATOR_INSTANCE=' + args.vs.as_posix(), '-DORT_INCLUDE=' + include.as_posix(),
        '-DORT_LIBRARY=' + (ort / 'runtimes/win-x64/native/onnxruntime.lib').as_posix())
    run(cmake, '--build', win, '--config', 'Release')
    player = args.unity / 'Editor/Data/PlaybackEngines/AndroidPlayer'
    acmake = player / 'SDK/cmake/3.22.1/bin/cmake.exe'
    arm = CACHE / 'build-android'
    run(acmake, '-S', ROOT / 'Tools/AudioAssist/Native', '-B', arm, '-G', 'Ninja',
        '-DCMAKE_MAKE_PROGRAM=' + ninja.as_posix(),
        '-DCMAKE_TOOLCHAIN_FILE=' + (player / 'NDK/build/cmake/android.toolchain.cmake').as_posix(),
        '-DANDROID_ABI=arm64-v8a', '-DANDROID_PLATFORM=android-26', '-DANDROID_STL=c++_static', '-DCMAKE_BUILD_TYPE=Release',
        '-DORT_INCLUDE=' + include.as_posix(), '-DORT_LIBRARY=' + (android / 'jni/arm64-v8a/libonnxruntime.so').as_posix())
    run(acmake, '--build', arm)
    plugin(win / 'Release/opensekai_audio.dll', 'Windows/x86_64')
    for file in (ort / 'runtimes/win-x64/native').glob('*.dll'):
        plugin(file, 'Windows/x86_64')
    crt = sorted((args.vs / 'VC/Redist/MSVC').glob('*/x64/Microsoft.VC14*.CRT'))[-1]
    for file in crt.glob('*.dll'):
        plugin(file, 'Windows/x86_64')
    plugin(arm / 'libopensekai_audio.so', 'Android/arm64-v8a')
    plugin(android / 'jni/arm64-v8a/libonnxruntime.so', 'Android/arm64-v8a')
    dest = ROOT / 'Assets/StreamingAssets/AudioAssistNative'
    dest.mkdir(parents=True, exist_ok=True)
    entries = []
    for name in FILES:
        source = CACHE / 'models' / name
        shutil.copy2(source, dest / name)
        entries.append(dict(path=name, bytes=source.stat().st_size, sha256=digest(source)))
    version = 'native-v1-' + hashlib.sha256(json.dumps(entries, sort_keys=True).encode()).hexdigest()[:16]
    (dest / 'manifest.json').write_text(json.dumps(dict(schema=1, version=version, files=entries), indent=2), encoding='utf-8')
    licenses = dest / 'licenses'
    licenses.mkdir(exist_ok=True)
    for source, name in [(ort / 'LICENSE', 'onnxruntime-MIT.txt'), (ort / 'ThirdPartyNotices.txt', 'onnxruntime-third-party.txt'),
                         (fork / 'LICENSE', 'demucs-onnx-MIT.txt')]:
        shutil.copy2(source, licenses / name)
    for package in ['pykakasi', 'pypinyin', 'demucs']:
        dist = importlib.metadata.distribution(package)
        for file in dist.files:
            if any(word in file.name.upper() for word in ['LICENSE', 'COPYING', 'AUTHORS']):
                shutil.copy2(dist.locate_file(file), licenses / (package + '-' + file.name))
    shutil.copy2(ROOT / 'Tools/AudioAssist/THIRD_PARTY_NOTICES.md', dest / 'THIRD_PARTY_NOTICES.md')
    print('Windows x64 and Android ARM64 native assets ready:', version, flush=True)


if __name__ == '__main__':
    main()
