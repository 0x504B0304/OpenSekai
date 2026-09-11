"""Real vocal timing parity and cancellable native session lifetime checks.

Input: PCM WAV containing a short sung 'yararara' phrase (development only).
"""
import argparse
import ctypes as ct
import gc
import os
from pathlib import Path
import threading
import time
import numpy as np
import soundfile as sf
from scipy.signal import resample_poly
import torch
import torchaudio

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / '.audio-assist-native'


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('wav', type=Path)
    parser.add_argument('--start', type=float, default=3.44)
    parser.add_argument('--end', type=float, default=6.61)
    args = parser.parse_args()
    audio, rate = sf.read(args.wav, dtype='float32', always_2d=True)
    wave = resample_poly(audio.mean(axis=1), 16000, rate)[int(args.start * 16000):int(args.end * 16000)].copy()
    torch.set_num_threads(4)
    from torchaudio.pipelines._wav2vec2 import utils
    bundle = torchaudio.pipelines.MMS_FA
    model = utils._get_model(bundle._model_type, bundle._params).eval()
    model.load_state_dict(torch.load(ROOT / '.audio-assist-bundle/models/mms_fa.pt', weights_only=True))
    tensor = torch.from_numpy(wave)[None]
    with torch.inference_mode():
        normalized = (tensor - tensor.mean(-1, keepdim=True)) / torch.sqrt(tensor.var(-1, keepdim=True, unbiased=False) + 1e-5)
        reference = model(normalized)[0].log_softmax(-1)
    del model, normalized
    gc.collect()
    dll_path = os.add_dll_directory(str(CACHE / 'deps/ort/runtimes/win-x64/native'))
    lib = ct.CDLL(str(CACHE / 'build-win/Release/opensekai_audio.dll'))
    ptr = np.ctypeslib.ndpointer(dtype=np.float32, flags='C_CONTIGUOUS')
    lib.osa_open.argtypes = [ct.c_char_p, ct.c_int]; lib.osa_open.restype = ct.c_void_p
    lib.osa_close.argtypes = lib.osa_cancel.argtypes = [ct.c_void_p]
    lib.osa_error.restype = ct.c_char_p
    lib.osa_emissions.argtypes = [ct.c_void_p, ptr, ct.c_int, ptr, ct.c_int]
    path = str(CACHE / 'models/mms_fa.int8.onnx').encode()
    handle = lib.osa_open(path, 4); assert handle, lib.osa_error()
    output = np.zeros((len(wave) // 320 + 1) * 28, np.float32)
    frames = lib.osa_emissions(handle, wave, len(wave), output, len(output))
    assert frames > 0, lib.osa_error()
    actual = torch.from_numpy(output[:frames * 28].reshape(1, frames, 28)).log_softmax(-1)
    tokens = bundle.get_dict()
    target = torch.tensor([[tokens[c] for c in '*yararara*']], dtype=torch.int32)

    def spans(logits):
        logits = torch.cat((logits, torch.zeros(*logits.shape[:2], 1)), -1)
        labels, scores = torchaudio.functional.forced_align(logits, target)
        return torchaudio.functional.merge_tokens(labels[0], scores[0].exp())

    expected, measured = spans(reference), spans(actual)
    assert len(expected) == len(measured) == 10
    delta = np.array([[a.start - b.start, a.end - b.end] for a, b in zip(expected[1:-1], measured[1:-1])]) * len(wave) / 16000 / frames
    print('Real vocal int8/PyTorch phoneme timing difference: max', float(abs(delta).max()), 'mean', float(abs(delta).mean()), flush=True)
    assert abs(delta).max() <= .12, 'Quantized alignment moved more than 120 ms'
    print('Native phoneme spans:', [(x.start, x.end) for x in measured], flush=True)
    # Cancel a running long inference, then open a fresh usable session.
    long_wave = np.tile(wave, 5).astype(np.float32)
    long_output = np.zeros((len(long_wave) // 320 + 1) * 28, np.float32)
    answer = []
    def infer():
        answer.append(lib.osa_emissions(handle, long_wave, len(long_wave), long_output, len(long_output)))
    worker = threading.Thread(target=infer); worker.start()
    time.sleep(.15); lib.osa_cancel(handle); worker.join(timeout=30)
    assert not worker.is_alive() and answer == [-1], 'Inference failed to cancel'
    lib.osa_close(handle)
    handle = lib.osa_open(path, 2); assert handle, lib.osa_error()
    assert lib.osa_emissions(handle, wave, len(wave), output, len(output)) == frames
    lib.osa_close(handle)
    print('REAL VOCAL PARITY AND NATIVE CANCELLATION PASSED', flush=True)


if __name__ == '__main__':
    main()
