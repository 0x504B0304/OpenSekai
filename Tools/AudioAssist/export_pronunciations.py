"""Export data dictionaries only. Players interpret these tables in C#."""
import json
import unicodedata
from pathlib import Path
from pykakasi.kanji import Kanwa
import pykakasi
from pypinyin.pinyin_dict import pinyin_dict
from pypinyin.phrases_dict import phrases_dict

root = Path(__file__).resolve().parents[2] / '.audio-assist-native/models'
root.mkdir(parents=True, exist_ok=True)
kakasi = pykakasi.kakasi()
kanji = {}
for group in Kanwa()._jisyo_table.values():
    for word, readings in group.items():
        if readings and word and '\t' not in word and '\n' not in word:
            kanji[word] = readings[0][0]
(root / 'ja-reading.tsv').write_text(''.join(k+'\t'+v+'\n' for k,v in sorted(kanji.items())), encoding='utf-8')
kana = {}
for a in range(0x3041, 0x3097):
    for tail in ['', 'ゃ', 'ゅ', 'ょ', 'ぁ', 'ぃ', 'ぅ', 'ぇ', 'ぉ']:
        s = chr(a) + tail
        kana[s] = ''.join(x['hepburn'] for x in kakasi.convert(s))
(root / 'ja-roman.tsv').write_text(''.join(k+'\t'+v+'\n' for k,v in sorted(kana.items())), encoding='utf-8')
def plain(s):
    return ''.join(c for c in unicodedata.normalize('NFD', s.replace('ü','u')) if 'a' <= c <= 'z')
zh = {chr(k):plain(v.split(',')[0]) for k,v in pinyin_dict.items()}
zh.update({k:' '.join(plain(x[0]) for x in v) for k,v in phrases_dict.items()})
(root / 'zh-reading.tsv').write_text(''.join(k+'\t'+v+'\n' for k,v in sorted(zh.items())), encoding='utf-8')
print('Exported Japanese and Chinese reading dictionaries', len(kanji), len(zh))
