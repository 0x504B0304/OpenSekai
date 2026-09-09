# 内置采音分析

Windows 完整版本附带 `AudioAssistEngine`：独立 CPython 运行环境、CPU 推理库、Demucs 分轨权重、MMS_FA 对齐权重及音频解码器。无需安装 Python、选择解释器、下载模型或联网；请保留整个程序目录，不能只复制 OpenSekai.exe。

进入制谱器读取原曲后，自动分离人声、鼓组、贝斯和其他旋律，并生成伴奏。成功结果写入 `persistentDataPath/AudioAssist/<MusicId>/`，再次打开复用缓存。源文件按 SHA-256 识别，替换原曲会使旧分轨失效。取消或失败后不自动反复重试，可在“采音 → 声部与乐句 → 内置自动分析”中重新运行。模型计算期间可继续原曲回听，完成后暂停并载入分轨；所有处理都保留原始输入。

导入 LRC、选择语言，再点击“分轨并对齐歌词”可生成逐发音单位时间：日语按假名拍、中文按字音、英语按词。时间来自 MMS_FA 声学强制对齐，pykakasi/pypinyin 只提供发音。得分不是准确概率，歌唱、和声、连读和汉字读音仍可能造成错位，应通过试听和微调核对。增强 LRC 可以直接导入及转移到移动端。

音频时间包含文件自身的前置静音；LRC 若对应未加空白的原曲，应设置相应歌词偏移。谱面通过曲包 fillerSec 转换，不固定假设某个等待长度。普通 LRC 仅有逐句时间。Android/iOS 尚未内置这些推理模型，可手动导入分轨及增强 LRC。

## 开发与打包

开发者在项目根目录执行 `Tools/AudioAssist/setup.ps1 -Python <Python3.12路径>`，或用已有开发环境运行 `Tools/AudioAssist/package_runtime.py`。此步骤会下载依赖、准备权重，生成不入 Git 的 `.audio-assist-bundle`；不是用户安装流程。

Windows 构建钩子校验全部文件 SHA-256，复制到 Player 同级 `AudioAssistEngine`，缺文件、损坏或脚本过期会阻止构建。普通 ZIP 和带曲包导入 FFmpeg 的 ZIP 均保留分析引擎。独立嵌入式 Python 的 `_pth` 和 `-I` 模式隔离系统及用户安装的包；模型加载不走下载缓存。

模型及依赖许可随引擎分发，见 `THIRD_PARTY_NOTICES.md`。Demucs 为 MIT；MMS_FA 模型为 CC-BY-NC-4.0，保留其非商业许可限制。开发更新 worker.py 后需重新打包。
