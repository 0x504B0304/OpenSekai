# 采音辅助验证

高精度波形：`Logs/audio-assist-hires-tests.xml` 的 16 项测试通过，覆盖同一旧 10 ms 桶内的独立瞬态、多级峰值查询与原始 PCM 一致、文件边界和小于一个 tick 的 BPM 时间换算。`Logs/audio-assist-hires-build.log` 记录五种分辨率（含 3840×2160）实际波形网格的横条高度，均约 1 个物理像素，并检查左右工具栏开合布局。

波形索引共享已加载的 PCM，缩小视图使用多级峰值索引，放大视图读取原始采样；只绘制可见区域，每列最多 4096 行。`Logs/audio-assist-waveform-benchmark.log` 的独立 .NET 基准：60 秒立体声索引构建约 9.2 ms，两条各 2160 行、显示 2 秒音频的峰值查询平均约 0.43 ms/帧。该数字仅为峰值查询开销，不包含 Unity 网格上传、其他界面和 GPU 渲染，不能视为游戏帧率保证。

Unity 6000.6.0f1 的 EditMode 测试过滤器：`AudioAssist|HistoryShortcutEditModeTests|MusicScoreWheelEditModeTests`。其中 `AudioAssistPlayModeTests` 在空场景中进入 PlayMode，使用静音音源检查实际 DSP 循环、混音切换和暂停，不导入用户曲包或启动 Live。

Python 测试：

```powershell
./.audio-assist-venv/Scripts/python.exe -B -m unittest discover -s Tests/AudioAssist -v
```

布局验证入口：`AudioAssistUiValidation.Capture`。从实际制谱器预制体生成 1920×1080、1366×768、1024×768、844×390 的展开/折叠截图，并检查面板边界和多次折叠后几何恢复。截图在 `Logs/AudioAssistValidation/`。该检查使用合成波形，不作为模型采音准确率证据，也不替代触屏真机验收。

本次真实分析验证使用独立裁剪的《ヤラララ》42–54 秒片段和相应 LRC。Demucs 实际生成 5 条等长分轨；四个基础声部重建与原曲相关系数约 0.999。MMS_FA 输出 62 个有顺序且位于音频范围内的发音单位。按当前 0.45 阈值，其中 56 个显示“待核对”；声学得分未经准确率校准，重复音节中存在可闻偏移。自动结果应作为可试听、可移动的采音候选，不能视为准确歌词或最终音符时间。

原始歌曲、LRC 和曲包没有被修改。测试素材和分析结果仅保留在忽略版本管理的 `Logs` 目录中。

工具栏折叠布局：`AudioAssistUiValidation.Capture` 覆盖左右面板的四种开合组合，检查左侧折叠后轨道加宽、右边界不移动，以及反复切换后几何准确恢复。1920×1080、1366×768、1024×768、844×390 均通过；1920×1080 采音面板展开时轨道宽度由约 436 增至 678。日志为 `Logs/audio-assist-tools-width-validation.log`，截图为 `Logs/AudioAssistValidation/ui-*-tools-*.png`。

## 编辑器无声回归

采音面板折叠混音隔离：`Logs/audio-assist-foldmix-tests.xml` 的 24 项采音及菜单测试通过。新增 `CollapsedPanelPlaysOriginalAndReopeningRestoresSavedMix` 用 AudioRenderer 捕获最终输出，验证初始折叠、播放中折叠、暂停后折叠再播放均恢复原曲；覆盖音乐静音、原曲未勾选、原曲音量为零、全部声部关闭，重新展开恢复原混音且不改写会话状态。切换只改变输出音量，不重启时钟，暂停时不会自动播放。测试使用合成信号，没有启动用户曲包或 Live。

对应 Windows 程序：`Builds/Windows-AudioAssist-FoldMix/OpenSekai.exe`；构建及离线引擎逐文件校验成功，记录于 `Logs/audio-assist-foldmix-build.log`。未自动启动程序或进行用户曲包试听。

`EditorWithoutUnityListenerProducesOriginalAndStemAudio` 从没有 Unity AudioListener 的空场景开始，匹配真实 CRI 制谱器场景。用 AudioRenderer 捕获最终混音输出，验证原曲 440 Hz 信号、切换后的分轨 880 Hz 信号，以及全部声部关闭后的静音；测试捕获期间不会向扬声器播放测试音。另检查已有监听器复用及关闭编辑器后的监听器停用。

修复前测试捕获原曲输出为 0 并失败（`Logs/audio-assist-silence-before.xml`）；补齐编辑器作用域的监听器后，全部 15 项采音测试通过（`Logs/audio-assist-audiofix-final-tests.xml`），并验证仅销毁采音组件、保留编辑器视图时也会清理监听器。旧测试手动添加监听器且使用静音片段，只能验证调度与音量状态，无法发现此次问题。

## Windows 内置引擎验证

`verify_portable.py --engine Builds/Windows-AudioAssist/AudioAssistEngine --audio <测试音频> --lrc <可选LRC> --output Logs/AudioAssistPortable` 使用发行目录中的嵌入式 Python，清空模型缓存位置、隔离 PATH 和用户 Python 搜索路径，并通过文件审计禁止访问发行引擎与测试目录之外的 Python 文件/权重。引擎自身禁止网络连接；测试同时检查 Microsoft C++ DLL 从程序目录加载。

2026-09-09：最终发行引擎在上述限制下完成《ヤラララ》12 秒片段，输出 5 条等长分轨、62 个范围内且有序的发音单位，四个基础声部重建与原曲相关系数 0.999048。仅验证时间边界与声部重建，不代表歌词对齐准确率。13 项 Unity 编辑器测试通过，包含替换原曲后的缓存失效和取消记录保留；发行运行环境中的 3 项 Python 测试通过。

Windows 主程序构建日志：`Logs/audio-assist-self-contained-build.log`。最终引擎校验/安装日志：`Logs/audio-assist-payload-final.log`。离线测试日志：`Logs/audio-assist-portable.log`，文件访问与 DLL 路径记录：`Logs/AudioAssistPortable/isolation.json`。引擎约 2.08 GB，保留全部模型及推理依赖，发行清单排除了不参与推理的静态编译库与头文件。
