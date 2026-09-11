"""Numerical parity checks against PyTorch, run only during development."""
import ctypes as ct
import json
import os
from pathlib import Path
import numpy as np
import torch
import torchaudio
from demucs.htdemucs import HTDemucs
ROOT = Path(__file__).resolve().parents[2]
native = ROOT / '.audio-assist-native'
dll_dir = os.add_dll_directory(str(native / 'deps/ort/runtimes/win-x64/native'))
lib = ct.CDLL(str(native / 'build-win/Release/opensekai_audio.dll'))
ptr = np.ctypeslib.ndpointer(dtype=np.float32, flags='C_CONTIGUOUS')
lib.osa_spectrum.argtypes = [ptr, ptr]
lib.osa_reconstruct.argtypes = [ptr, ptr, ptr]
lib.osa_open.argtypes = [ct.c_char_p, ct.c_int]; lib.osa_open.restype = ct.c_void_p
lib.osa_error.restype = ct.c_char_p
lib.osa_close.argtypes = [ct.c_void_p]
lib.osa_separate.argtypes = [ct.c_void_p, ptr, ptr]
lib.osa_emissions.argtypes = [ct.c_void_p, ptr, ct.c_int, ptr, ct.c_int]
torch.set_num_threads(4); torch.manual_seed(7)
config = json.loads((ROOT / '.audio-assist-bundle/models/htdemucs.json').read_text())
model = HTDemucs(*config['args'], **config['kwargs']).eval()
model.load_state_dict(torch.load(ROOT / '.audio-assist-bundle/models/htdemucs.pt', weights_only=True))
wave = torch.randn(1,2,343980) * .03
spec = np.empty((1,4,2048,336),np.float32)
lib.osa_spectrum(wave.numpy(), spec)
expected = model._magnitude(model._spec(wave)).numpy()
np.testing.assert_allclose(spec, expected, atol=2e-5, rtol=1e-3)
print('STFT matches PyTorch; max error', abs(spec-expected).max(), flush=True)
mask = torch.randn(1,4,4,2048,336) * .02
temporal = torch.randn(1,4,2,343980) * .02
out = np.empty((1,4,2,343980),np.float32)
lib.osa_reconstruct(mask.numpy(), temporal.numpy(), out)
expected = (model._ispec(model._mask(None, mask),343980) + temporal).numpy()
np.testing.assert_allclose(out, expected, atol=2e-5, rtol=1e-3)
print('iSTFT matches PyTorch; max error',abs(out-expected).max(),flush=True)
with torch.inference_mode(): expected = model(wave).numpy()
del model, mask, temporal
handle=lib.osa_open(str(native / 'models/htdemucs.onnx').encode(),4)
assert handle,lib.osa_error()
assert lib.osa_separate(handle,wave.numpy(),out)==0,lib.osa_error()
np.testing.assert_allclose(out,expected,atol=2e-4,rtol=.02)
print('Full separator matches PyTorch; max error', abs(out-expected).max(),flush=True)
lib.osa_close(handle)
handle=lib.osa_open(str(native / 'models/mms_fa.int8.onnx').encode(),4)
assert handle,lib.osa_error()
for n in [16000,80000,192000]:
    wave=np.random.default_rng(1).normal(0,.01,n).astype(np.float32)
    logits=np.zeros((n//320+1)*28,np.float32)
    frames=lib.osa_emissions(handle,wave,n,logits,len(logits))
    assert frames>0,lib.osa_error()
    assert np.isfinite(logits[:frames*28]).all()
    print('MMS dynamic inference',n,frames,flush=True)
lib.osa_close(handle)
print('NATIVE NUMERICAL VALIDATION PASSED',flush=True)
