"""Exercise the shipped engine with empty caches, isolated PATH and audited reads.

python verify_portable.py --engine Builds/Windows-AudioAssist/AudioAssistEngine
    --audio Logs/AudioAssistValidation/source.wav
    --lrc Logs/AudioAssistValidation/lyrics.lrc --output Logs/AudioAssistPortable
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--engine', type=Path, required=True)
    parser.add_argument('--audio', type=Path, required=True)
    parser.add_argument('--lrc', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    engine, output = args.engine.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    audio = output / ('原始音频' + args.audio.suffix)
    shutil.copy2(args.audio, audio)
    command = [str(engine / 'worker.py'), '--models', str(engine / 'models'),
               '--ffmpeg', str(engine / 'decoder/ffmpeg.exe'), '--audio', str(audio),
               '--output', str(output / 'result'), '--language', 'ja']
    if args.lrc:
        shutil.copy2(args.lrc, output / '歌词.lrc')
        command += ['--lrc', str(output / '歌词.lrc')]
    config = dict(roots=[str(engine), str(output)], args=command)
    (output / 'config.json').write_text(json.dumps(config), encoding='utf-8')
    # The audit hook disallows reading developer Python/cache files even if a
    # dependency were to discover one. The worker separately blocks all sockets.
    runner = output / 'probe.py'
    runner.write_text('''import json,os,runpy,sys
from pathlib import Path
root=Path(__file__).parent
config=json.loads((root/'config.json').read_text(encoding='utf-8'))
allowed=[os.path.normcase(os.path.realpath(p))+os.sep for p in config['roots']]
opened=set()
def audit(event,args):
    if event!='open' or not isinstance(args[0],(str,bytes,os.PathLike)):return
    p=os.path.normcase(os.path.realpath(os.fsdecode(args[0])))
    if p==os.path.normcase(os.path.realpath(os.devnull)):return
    if not any(p.startswith(base) for base in allowed):
        raise RuntimeError('External file access during portable test: '+p)
    opened.add(p)
sys.addaudithook(audit)
sys.argv=config['args']
runpy.run_path(sys.argv[0],run_name='__main__')
origins=sorted({str(getattr(m,'__file__','')) for m in sys.modules.values() if getattr(m,'__file__',None)})
report=dict(path=sys.path,modules=origins,opened=sorted(opened))
import ctypes
kernel=ctypes.WinDLL('kernel32',use_last_error=True)
kernel.GetModuleHandleW.argtypes=[ctypes.c_wchar_p];kernel.GetModuleHandleW.restype=ctypes.c_void_p
kernel.GetModuleFileNameW.argtypes=[ctypes.c_void_p,ctypes.c_wchar_p,ctypes.c_uint]
crt={}
for name in ['msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll']:
    handle=kernel.GetModuleHandleW(name)
    assert handle, name+' not loaded'
    buffer=ctypes.create_unicode_buffer(32768)
    assert kernel.GetModuleFileNameW(handle,buffer,len(buffer))
    assert os.path.normcase(buffer.value).startswith(allowed[0]),buffer.value
    crt[name]=buffer.value
report['crt']=crt
(root/'isolation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
import soundfile as sf,numpy as np
data,sr=sf.read(root/'result/input.wav')
stems=[sf.read(root/'result'/name)[0] for name in ['drums.wav','bass.wav','other.wav','vocals.wav']]
assert all(s.shape==data.shape for s in stems)
assert np.corrcoef(sum(stems).ravel(),data.ravel())[0,1]>.98
result=json.loads((root/'result/result.json').read_text(encoding='utf-8'))
assert len(result['stems'])==5
units=[s for line in result['lyrics'] for s in line['syllables']]
assert all(0<=s['seconds']<=s['end']<=len(data)/sr for s in units)
assert all(a['seconds']<=b['seconds'] for a,b in zip(units,units[1:]))
print(json.dumps(dict(stems=5,seconds=len(data)/sr,units=len(units),correlation=float(np.corrcoef(sum(stems).ravel(),data.ravel())[0,1]))))
''', encoding='utf-8')
    environment = dict(os.environ)
    environment.update(PATH=str(Path(os.environ['SystemRoot']) / 'System32'),
                       TORCH_HOME=str(output / 'empty-cache/torch'), HF_HOME=str(output / 'empty-cache/hf'),
                       XDG_CACHE_HOME=str(output / 'empty-cache/xdg'), PYTHONPATH='Z:/invalid-python-path',
                       HF_HUB_OFFLINE='1', PYTHONNOUSERSITE='1')
    subprocess.run([str(engine / 'runtime/python.exe'), '-I', '-B', '-X', 'utf8', str(runner)],
                   cwd=output, env=environment, check=True)
    print('Portable offline inference passed.', flush=True)


if __name__ == '__main__':
    main()
