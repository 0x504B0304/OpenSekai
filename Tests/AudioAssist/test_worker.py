"""Run with the configured analysis Python: python -B -m unittest discover -s Tests/AudioAssist."""
import importlib.util
from pathlib import Path
import unittest

source = Path(__file__).resolve().parents[2] / 'Assets/StreamingAssets/AudioAssist/worker.py'
spec = importlib.util.spec_from_file_location('audio_assist_worker', source)
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class WorkerTests(unittest.TestCase):
    def test_lrc_offsets_and_duplicate_lines(self):
        lines = worker.parse_lrc('[ar:name]\n[offset:250]\n[00:10.10][00:20.10]ヤラララ\n[00:30.01]<00:30.01>ラ', 9)
        for expected, line in zip([19.35, 29.35, 39.26], lines):
            self.assertAlmostEqual(expected, line['seconds'], places=8)
        self.assertEqual('ラ', lines[-1]['text'])
        self.assertTrue(all(not line['syllables'] for line in lines))

    def test_repeated_japanese_mora_remain_separate(self):
        units = worker.pronunciation_units('ヤラララ', 'ja')
        self.assertEqual(['ya', 'ra', 'ra', 'ra'], [roman for _, roman in units])
        self.assertEqual(4, len(units))
        self.assertEqual('kyo', worker.pronunciation_units('きょ', 'ja')[0][1])

    def test_chinese_units_and_english_words(self):
        self.assertEqual([('你', 'ni'), ('好', 'hao')], worker.pronunciation_units('你好！', 'zh'))
        self.assertEqual([('Hello', 'hello'), ("don't", 'dont')], worker.pronunciation_units("Hello, don't!", 'en'))


if __name__ == '__main__':
    unittest.main()
