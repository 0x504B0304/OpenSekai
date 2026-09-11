"""Development-only ONNX conversion; no Python code is shipped to players."""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('model', choices=['separator', 'aligner'])
    args = parser.parse_args()
    target = ROOT / '.audio-assist-native/models'
    target.mkdir(parents=True, exist_ok=True)
    source = ROOT / '.audio-assist-bundle/models'
    import torch
    # Reuse existing model caches, or fetch original weights during development.
    source = ROOT / '.audio-assist-bundle/models'
    source.mkdir(parents=True, exist_ok=True)
    if args.model == 'separator' and not (source / 'htdemucs.pt').exists():
        from demucs.pretrained import get_model
        original = get_model('htdemucs')
        original = original.models[0] if hasattr(original, 'models') else original
        positional, kwargs = original._init_args_kwargs
        kwargs = dict(kwargs); kwargs['segment'] = float(kwargs['segment'])
        (source / 'htdemucs.json').write_text(json.dumps(dict(args=positional, kwargs=kwargs)), encoding='utf-8')
        torch.save(original.state_dict(), source / 'htdemucs.pt')
        del original
    if args.model == 'aligner' and not (source / 'mms_fa.pt').exists():
        import torchaudio
        original = torchaudio.pipelines.MMS_FA.get_model()
        torch.save(original.model.state_dict(), source / 'mms_fa.pt')
        del original
    torch.set_num_threads(4)
    torch.manual_seed(0)
    if args.model == 'separator':
        import ast
        import types
        from demucs.htdemucs import HTDemucs
        from einops import rearrange
        config = json.loads((source / 'htdemucs.json').read_text())
        model = HTDemucs(*config['args'], **config['kwargs']).eval()
        model.load_state_dict(torch.load(source / 'htdemucs.pt', weights_only=True))
        wave = torch.randn(1, 2, 343980)
        spec = model._magnitude(model._spec(wave))
        # Use only the MIT fork's STFT-free forward; retain the installed original
        # architecture and our existing weights. Pin the source in prepare_native.ps1.
        tree = ast.parse((ROOT / '.utmp/demucs-onnx/demucs-for-onnx/demucs/htdemucs.py').read_text())
        cls = next(n for n in tree.body if isinstance(n, ast.ClassDef) and n.name == 'HTDemucs')
        method = next(n for n in cls.body if isinstance(n, ast.FunctionDef) and n.name == 'forward')
        scope = dict(torch=torch, rearrange=rearrange)
        exec(compile(ast.Module(body=[method], type_ignores=[]), '<demucs-onnx-forward>', 'exec'), scope)
        model.forward = types.MethodType(scope['forward'], model)
        torch.onnx.export(model, (wave, spec), str(target / 'htdemucs.onnx'),
                          input_names=['wave', 'spec'], output_names=['spec_out', 'wave_out'],
                          opset_version=17, dynamo=False)
        print('separator export complete', flush=True)
    else:
        import torchaudio
        from torchaudio.pipelines._wav2vec2 import utils
        b = torchaudio.pipelines.MMS_FA
        inner = utils._get_model(b._model_type, b._params)
        inner.load_state_dict(torch.load(source / 'mms_fa.pt', weights_only=True))
        class Aligner(torch.nn.Module):
            def __init__(self):
                super().__init__()
                self.model = inner
            def forward(self, wave):
                wave = (wave - wave.mean(dim=-1, keepdim=True)) / torch.sqrt(wave.var(dim=-1, keepdim=True, unbiased=False) + 1e-5)
                return self.model(wave)[0]
        model = Aligner().eval()
        torch.onnx.export(model, torch.randn(1, 16000 * 3), str(target / 'mms_fa.onnx'),
                          input_names=['wave'], output_names=['logits'],
                          dynamic_axes={'wave': {1: 'samples'}, 'logits': {1: 'frames'}},
                          opset_version=17, dynamo=False)
        del model, inner
        import gc
        gc.collect()
        from onnxruntime.quantization import quantize_dynamic, QuantType
        quantize_dynamic(str(target / 'mms_fa.onnx'), str(target / 'mms_fa.int8.onnx'),
                         weight_type=QuantType.QInt8, op_types_to_quantize=['MatMul', 'Gemm'],
                         extra_options={'MatMulConstBOnly': True})
        (target / 'tokens.json').write_text(json.dumps(b.get_labels()), encoding='utf-8')
        print('aligner export complete', flush=True)


if __name__ == '__main__':
    main()
