"""Local-only stem separation and multilingual lyric forced alignment.

Demucs htdemucs supplies real stems. TorchAudio MMS_FA supplies acoustic token
spans; kana/pinyin conversion supplies pronunciations, never timestamps.
All times in result.json use the input audio's timeline, including any silence.
"""
import argparse
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import unicodedata


def atomic_json(path, data):
    temp = path.with_suffix('.tmp')
    temp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    for attempt in range(8):
        try:
            os.replace(temp, path)
            return
        except PermissionError:
            if attempt == 7:
                raise
            time.sleep(.025 * (attempt + 1))


def load_separator(models):
    import torch
    from demucs.htdemucs import HTDemucs
    config = json.loads((models / 'htdemucs.json').read_text(encoding='utf-8'))
    model = HTDemucs(*config['args'], **config['kwargs'])
    model.load_state_dict(torch.load(models / 'htdemucs.pt', map_location='cpu', weights_only=True))
    return model.eval()


def load_aligner(models, bundle):
    import torch
    from torchaudio.pipelines._wav2vec2 import utils
    # Pinned TorchAudio 2.8 architecture + preprocessed bundled state dictionary.
    model = utils._get_model(bundle._model_type, bundle._params)
    model.load_state_dict(torch.load(models / 'mms_fa.pt', map_location='cpu', weights_only=True))
    return utils._extend_model(model, normalize_waveform=bundle._normalize_waveform,
                               apply_log_softmax=True, append_star=True).eval()


def parse_lrc(text, extra_offset=0):
    stamp = re.compile(r'\[(\d+):(\d{2})(?:[.:](\d{1,3}))?\]')
    offset_tag = re.search(r'\[offset:([+-]?\d+)\]', text, re.I)
    offset = extra_offset + (int(offset_tag[1]) / 1000 if offset_tag else 0)
    lines = []
    for line in text.splitlines():
        stamps = list(stamp.finditer(line))
        if not stamps:
            continue
        lyric = re.sub(r'<\d+:\d+(?:\.\d+)?>', '', stamp.sub('', line)).strip()
        if not lyric:
            continue
        for match in stamps:
            fraction = match[3] or ''
            t = int(match[1]) * 60 + int(match[2]) + (int(fraction) / 10 ** len(fraction) if fraction else 0)
            lines.append(dict(seconds=t + offset, text=lyric, syllables=[]))
    return sorted(lines, key=lambda line: line['seconds'])


def pronunciation_units(text, language):
    """Japanese mora / Chinese character / English word, with display labels."""
    text = unicodedata.normalize('NFKC', text)
    result = []
    if language == 'ja':
        import pykakasi
        converter = pykakasi.kakasi()
        reading = ''.join(part['hira'] for part in converter.convert(text))
        units = re.findall(r'[ぁ-んァ-ン][ゃゅょャュョぁぃぅぇぉァィゥェォ]?|ー|[A-Za-z]+', reading)
        previous = 'a'
        for unit in units:
            roman = ''.join(part['hepburn'] for part in converter.convert(unit)).lower()
            if unit == 'ー':
                roman = previous
            roman = re.sub('[^a-z]', '', roman)
            if not roman:
                continue
            vowels = re.findall('[aeiou]', roman)
            if vowels:
                previous = vowels[-1]
            result.append((unit, roman))
    elif language == 'zh':
        from pypinyin import lazy_pinyin
        for unit in re.findall(r'[\u3400-\u9fff]|[A-Za-z]+', text):
            roman = re.sub('[^a-z]', '', ''.join(lazy_pinyin(unit)).lower().replace('ü', 'u'))
            if roman:
                result.append((unit, roman))
    else:
        result = [(word, re.sub('[^a-z]', '', word.lower())) for word in re.findall(r"[A-Za-z]+(?:'[A-Za-z]+)?", text)]
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--audio', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--lrc')
    parser.add_argument('--language', choices=['ja', 'zh', 'en'], default='ja')
    parser.add_argument('--offset', type=float, default=0)
    parser.add_argument('--ffmpeg', default='ffmpeg')
    parser.add_argument('--vocals', help='Use existing same-length vocals; skips separation for re-alignment.')
    parser.add_argument('--models', type=Path, required=True, help='Bundled offline model directory; no download fallback.')
    args = parser.parse_args()
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    progress = lambda value, message: atomic_json(output / 'progress.json', dict(progress=value, message=message))
    progress(0, '载入内置分析引擎')
    # Inference is deliberately offline, including accidental calls in dependencies.
    import socket
    def no_network(*unused, **kwargs):
        raise RuntimeError('内置分析引擎禁止网络访问；请检查程序附带的模型文件')
    socket.socket.connect = no_network
    socket.create_connection = no_network
    import numpy as np
    import soundfile as sf
    import torch
    import torchaudio
    torch.set_num_threads(min(8, os.cpu_count() or 4))
    torch.manual_seed(0)
    device = 'cuda' if torch.cuda.is_available() else 'cpu'
    decoded = output / 'input.wav'
    subprocess.run([args.ffmpeg, '-v', 'error', '-y', '-i', str(Path(args.audio).resolve()), '-ar', '44100', '-ac', '2', '-c:a', 'pcm_f32le', str(decoded)], check=True, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    audio, rate = sf.read(decoded, dtype='float32', always_2d=True)
    if len(audio) == 0 or len(audio) / rate > 1200:
        raise ValueError('音频须在 0–20 分钟范围内')
    result = dict(stems=[], lyrics=[], warnings=[], models=['htdemucs', 'torchaudio.pipelines.MMS_FA'])
    if args.vocals:
        vocals, vocal_rate = sf.read(args.vocals, dtype='float32', always_2d=True)
        if vocal_rate != rate or len(vocals) != len(audio):
            raise ValueError('已有分轨必须与原曲采样率和长度一致')
    else:
        progress(.03, '载入内置 Demucs 分轨模型')
        from demucs.apply import apply_model
        model = load_separator(args.models).to(device)
        mix = torch.from_numpy(audio.T.copy()).to(device)
        reference = mix.mean(0)
        mean, std = reference.mean(), reference.std().clamp_min(1e-6)
        completed = 0
        segments = math.ceil(len(audio) / max(1, int(int(rate * model.segment) * .75)))
        def segment_finished(module, inputs, output_tensor):
            nonlocal completed
            completed += 1
            progress(.05 + .48 * min(1, completed / segments), '正在分离人声、鼓组、贝斯和旋律')
        # Official Demucs 4.0.1 has no apply_model callback parameter. A forward
        # hook reports completed inference segments without patching its package.
        hook = model.register_forward_hook(segment_finished)
        try:
            with torch.inference_mode():
                separated = apply_model(model, ((mix - mean) / std)[None], device=device, shifts=0, split=True, overlap=.25)[0]
                separated = separated * std + mean
        finally:
            hook.remove()
        for i, key in enumerate(model.sources):
            samples = separated[i].cpu().numpy().T
            path = output / (key + '.wav')
            sf.write(path, samples, rate, subtype='PCM_16')
            result['stems'].append(dict(key=key, path=str(path), offset=0))
        vocals = separated[model.sources.index('vocals')].cpu().numpy().T.copy()
        path = output / 'instrumental.wav'
        sf.write(path, audio - vocals, rate, subtype='PCM_16')
        result['stems'].append(dict(key='instrumental', path=str(path), offset=0))
        del separated, model, mix
        if device == 'cuda':
            torch.cuda.empty_cache()
    if args.lrc:
        lines = parse_lrc(Path(args.lrc).read_text(encoding='utf-8-sig'), args.offset)
        lines = [line for line in lines if -.9 <= line['seconds'] < len(audio) / rate]
        if not lines:
            raise ValueError('LRC 没有落在音频范围内的歌词；请检查整体偏移')
        progress(.55, '载入内置多语言逐音节对齐模型')
        bundle = torchaudio.pipelines.MMS_FA
        model = load_aligner(args.models, bundle).to(device)
        tokenizer, aligner = bundle.get_tokenizer(), bundle.get_aligner()
        wave = torchaudio.functional.resample(torch.from_numpy(vocals.mean(1).copy()), rate, bundle.sample_rate)
        # Align consecutive lines together so repeated syllables retain a single
        # monotonic acoustic path even when line-level LRC timestamps are coarse.
        groups = []
        for i, line in enumerate(lines):
            if not groups or line['seconds'] - groups[-1][0][1]['seconds'] > 14 or line['seconds'] - groups[-1][-1][1]['seconds'] > 5:
                groups.append([])
            groups[-1].append((i, line))
        for group_index, group in enumerate(groups):
            progress(.58 + .4 * group_index / len(groups), f'对齐乐句 {group_index + 1}/{len(groups)}')
            items = [(index, label, roman) for index, line in group for label, roman in pronunciation_units(line['text'], args.language)]
            if not items:
                continue
            start = max(0, group[0][1]['seconds'] - .9)
            last_index = group[-1][0]
            end = min(len(wave) / bundle.sample_rate, lines[last_index + 1]['seconds'] + .35 if last_index + 1 < len(lines) else group[-1][1]['seconds'] + 5)
            segment = wave[int(start * bundle.sample_rate):int(end * bundle.sample_rate)]
            try:
                with torch.inference_mode():
                    emission, _ = model(segment[None].to(device))
                    spans = aligner(emission[0], tokenizer(['*'] + [item[2] for item in items] + ['*']))[1:-1]
                ratio = len(segment) / bundle.sample_rate / emission.shape[1]
                for (line_index, label, roman), aligned in zip(items, spans):
                    duration = sum(span.end - span.start for span in aligned)
                    confidence = sum(span.score * (span.end - span.start) for span in aligned) / max(1, duration)
                    lines[line_index]['syllables'].append(dict(seconds=round(start + aligned[0].start * ratio, 5), end=round(start + aligned[-1].end * ratio, 5), label=label, pronunciation=roman, confidence=round(float(confidence), 4)))
            except (RuntimeError, ValueError) as error:
                result['warnings'].append(f'乐句组 {group_index + 1} 对齐失败: {error}')
        if not any(line['syllables'] for line in lines):
            raise RuntimeError('未能对齐歌词。请检查语言、歌词内容、时间偏移和人声质量。')
        result['lyrics'] = lines
        # Portable enhanced LRC lets mobile editors import actual syllable timings.
        def stamp(t):
            ms = max(0, round(t * 1000))
            return f'{ms // 60000:02}:{ms // 1000 % 60:02}.{ms % 1000:03}'
        enhanced = []
        for line in lines:
            enhanced.append('[' + stamp(line['seconds']) + ']' + (''.join('<' + stamp(p['seconds']) + '>' + p['label'] for p in line['syllables']) or line['text']))
        (output / 'aligned.lrc').write_text('\n'.join(enhanced), encoding='utf-8')
    atomic_json(output / 'result.json', result)
    progress(1, '分析完成' if not result['warnings'] else '分析完成，部分乐句需要手动核对')
    print(str(output / 'result.json'), flush=True)


if __name__ == '__main__':
    main()
