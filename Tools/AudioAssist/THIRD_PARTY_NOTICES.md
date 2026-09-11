# Native offline audio analysis

Players execute C# and C++ only. ONNX graphs are converted model data, not Python
scripts. Windows x64 and Android ARM64 use the ONNX Runtime CPU execution provider.

- HTDemucs 4.0.1 weights and architecture: Meta Platforms, MIT.
  https://github.com/facebookresearch/demucs
- STFT-free forward used during conversion: sevagh/demucs.onnx, MIT,
  commit 81fa192e6fcc88e35e887f6e6ccce91227f4e6f5.
  https://github.com/sevagh/demucs.onnx
- MMS_FA weights: Meta Platforms, CC BY-NC 4.0. Converted to ONNX with dynamic
  int8 matrix weights; the noncommercial restriction remains unchanged.
  https://pytorch.org/audio/stable/generated/torchaudio.pipelines.MMS_FA.html
  https://creativecommons.org/licenses/by-nc/4.0/
- ONNX Runtime: Microsoft, MIT. Windows 1.22.1 / Android 1.22.0.
  LICENSE and third-party notices are included in licenses/.
  https://github.com/microsoft/onnxruntime
- Japanese pronunciation tables: derived from pykakasi 2.3.0 / KAKASI,
  GPL-3.0-or-later. COPYING and AUTHORS are included in licenses/.
  Corresponding table-generation source: Tools/AudioAssist/export_pronunciations.py
  Original source and dictionary data: https://github.com/miurahr/pykakasi/tree/v2.3.0
  These tables retain their original license; they are not relicensed as model weights.
- Chinese reading tables: pypinyin 0.55.0, MIT. License retained in licenses/.
  https://github.com/mozillazg/python-pinyin/tree/v0.55.0
- Windows MSVC runtime: Microsoft redistributable components, copied from the
  licensed Visual Studio Redist directory.
  https://visualstudio.microsoft.com/license-terms/vs2022-cruntime/

Per-file SHA-256 model hashes are shipped in manifest.json. Model inference is
local; song audio and lyrics are not uploaded. Developer conversion tools require
Python/PyTorch, but neither is included or executed by the application.
