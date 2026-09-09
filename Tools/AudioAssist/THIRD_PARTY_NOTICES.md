# OpenSekai bundled audio analysis

This directory is part of the Windows application. Python, the CPU inference
runtime, the decoder, pronunciation dictionaries and model weights are included;
no interpreter installation, account, cloud upload or model download is required.

- CPython 3.12.10: PSF License. See runtime/LICENSE.txt.
- Microsoft Visual C++ 2015–2022 x64 runtime: Microsoft redistributable components,
  deployed app-locally from Visual Studio's licensed Redist directory.
  https://visualstudio.microsoft.com/license-terms/vs2022-cruntime/
- PyTorch/TorchAudio 2.8.0 CPU: BSD-style licenses in runtime/Lib/site-packages.
- Demucs 4.0.1 / HTDemucs weights: Meta Platforms, Inc., MIT License.
  https://github.com/facebookresearch/demucs
- MMS forced alignment weights: Meta Platforms, Inc., **CC BY-NC 4.0**.
  https://huggingface.co/facebook/mms-1b-all
  https://creativecommons.org/licenses/by-nc/4.0/
  The included weights retain the noncommercial restriction; they are not
  relicensed by OpenSekai. Their format is converted to a local state dictionary.
- FFmpeg: see decoder/license.txt and https://ffmpeg.org/legal.html.
- NumPy, SoundFile, pykakasi, pypinyin and transitive dependencies:
  license files are retained in the corresponding package/dist-info directories.

The per-file SHA-256 manifest and exact installed dependency versions are shipped
as manifest.json and dependencies.lock.json. Models execute only on this computer.
