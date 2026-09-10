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

## 2026-09-10 — 分析按钮进度与人声音节标签

自动分析按钮直接连接 worker 的数值进度，0–94% 为模型处理，剩余阶段读取分轨及波形，全部接入后才显示 100%。取消、失败、重试均保留明确状态。模型输出的 `end` 进入 Unity 持久化数据；旧 session 优先匹配本曲缓存 result.json 的音节时间及文字恢复结束时间，缺失时估算至下一音节（末尾 250 ms）并标明。增强 LRC 支持结尾空时间戳。

标签位于人声波形右侧，使用与谱面相同的秒／tick 转换；矩形高度不因点击热区或文字高度而拉长。低置信度及估算音节用黄色边线；过短标签保留范围，放大后显示文字。点击试听，拖动浏览，滚轮转发给谱面；右侧保留整句导航、试听和批量草稿，不再生成逐音节按钮。

验证：`Logs/audio-assist-syllables-tests.xml` 共 23 项通过，包括结束时间反序列化／持久化、静音间隙、旧缓存恢复、增强 LRC 尾时间戳，以及原曲／分轨实际混音输出、折叠恢复原曲、循环时钟、草稿与撤销重做等既有回归。`AudioAssistUiValidation.CaptureVocalReview` 在深浅两主题、五个分辨率、左右折叠组合中离线渲染测试音节与模拟进度状态；日志为 `Logs/audio-assist-vocal-layout.log`，图像为 `Logs/AudioAssistValidation/vocal-*.png`。已目视检查 1920×1080 深色及 844×390 浅色的标签与按钮进度。未重新运行整曲分轨模型或移动端真机验收，未启动用户曲包游玩。

Windows 构建成功：`Builds/Windows-AudioAssist-VocalLabels/OpenSekai.exe`；日志 `Logs/windows-audio-assist-vocal-labels-build.log` 确认 `Bundled offline audio analysis verified` 与 `OpenSekai Windows Player built`。输出使用独立目录。

## 2026-09-10 — 人声音节人工编辑

在人声标签上右键或按住约 550 ms 显示贴近标签的小菜单，仅包含“调整时间”“编辑内容”“删除”；普通点击选中并试听，之后可按 Delete 删除。时间模式高亮当前标签、压暗其他标签，通过上下边沿直接拖动起止位置，拖动中预览，松手保存，每次拖动对应一次撤销；短标签两端的 44 px 热区横向错开。文字模式将标签就地替换为输入框，Enter／✓ 保存、Esc／× 取消，点击外部保存；点击取消不会因输入框先失焦而误保存。

编辑和删除通过原制谱器撤销栈保存到 session.json；重新读取保留人工标记和结束时间。时间映射保存时仅扣除一次歌词偏移；既有草稿和正式音符保持独立。旧命令不会把被重新分析／重新导入替换的歌词重新插入。文本输入期间 Delete 交给输入框，不删除音节或正式音符；时间模式支持滚轮及撤销／重做，边沿拖动期间屏蔽快捷键。

验证入口：`AudioAssistUiValidation.CaptureSyllableEditorReview`。深浅两主题、五种分辨率与左右折叠组合渲染，并用 UGUI 指针事件验证右键、触控长按、拖动取消、释放不试听、菜单三选项、边沿预览／提交／撤销、第二指针不抢拖动、原位输入取消／提交／撤销、选中删除／恢复。验证器使用有效但无音频片段的双 bank，避免离线界面验证发声。长按识别器注入时间及前台状态。图像 `Logs/AudioAssistValidation/inline-{menu,time,text}-*.png`；布局及交互日志 `Logs/audio-assist-inline-edit-layout.log`。未做 Android/iOS 真机长按及软键盘验收；没有启动用户曲包游玩。

本版采音回归测试 32/32 通过（`Logs/audio-assist-inline-edit-tests.xml`），覆盖混音输出、循环时钟、时间映射、歌词保存与撤销。上述离线界面交互检查在全部分辨率及两主题通过，无脚本异常；已目视检查 PC 深色及 844×390 浅色的小菜单、时间高亮与就地文本框。Windows 输出 `Builds/Windows-AudioAssist-InlineEdit/OpenSekai.exe`，构建日志 `Logs/windows-audio-assist-inline-edit-build.log`。

Windows 编译完成，日志确认 `OpenSekai Windows Player built` 和 `Bundled offline audio analysis verified`，离线分析引擎随包交付。

### 2026-09-10 — 删除菜单文字延迟消失

运行时本地化扫描每 0.75 秒给“删除”绑定翻译，同时默认改成 Ellipsis；44 px 按钮扣除内边距后，整行字被裁掉。紧凑音节菜单预先绑定翻译并启用 `PreserveLayout`，保留原有文字布局；其他本地化控件的默认行为不变。

在原有菜单验证中加入连续三次运行时本地化刷新，并检查 TMP 的可见字符。修复前稳定失败（`Logs/audio-assist-delete-caption-before.log`）；修复后两主题 × 五分辨率 × 折叠组合全部通过，原有边沿编辑、原位文字、删除及撤销交互检查也通过（`Logs/audio-assist-delete-caption-layout.log`）。已目视检查 3840×2160 深色菜单的“删除”文字。Windows 输出 `Builds/Windows-AudioAssist-DeleteCaption/OpenSekai.exe`，构建日志 `Logs/windows-audio-assist-delete-caption-build.log`。

### 2026-09-10 — 右键直接切换音节菜单

菜单的透明外部点击层处理右键，命中另一可见音节时就地切换选中并重建菜单位置；保持单个编辑器实例，不触发试听，无需先关闭菜单。菜单内部按钮及波形外区域不会被当成音节。

`AudioAssistUiValidation.CaptureSyllableEditorReview` 使用 UGUI 指针事件直接投递给菜单背景，验证 A→B→A 连续右键后的选中状态、单个菜单及不发声；继续运行原有长按、边沿编辑、文字编辑、删除、撤销和延迟本地化检查。深浅主题、五分辨率及折叠组合全部通过，日志 `Logs/audio-assist-menu-switch-layout.log`。Windows 输出 `Builds/Windows-AudioAssist-MenuSwitch/OpenSekai.exe`，构建日志 `Logs/windows-audio-assist-menu-switch-build.log`。

### 2026-09-10 — 音节菜单圆角比例

三个菜单按钮的圆角半径设为高度的 16%（44 px 高时约 7 px），采用九宫格像素比例随尺寸更新。仅菜单按钮启用此比例，其他控件保留现有样式。静态检查及既有深浅主题／五分辨率界面验证通过（`Logs/audio-assist-menu-corners-layout.log`），已目视检查 4K 深色菜单。Windows 输出 `Builds/Windows-AudioAssist-MenuCorners/OpenSekai.exe`，构建日志 `Logs/windows-audio-assist-menu-corners-build.log`。

### 2026-09-10 — Esc 退出后再次进入制谱器

Esc 原来落到通用 `BackUIScreen`，退出按钮则发布 `BackKeyPressedEvent`，执行谱面保存及 `BackUIScreenAndDestroyExitedScreen`。现在仅当前页面为制谱器时，把硬件返回键路由到相同事件；加载／切换期间不返回，弹窗和音节编辑菜单优先处理，音节输入框先处理 Escape 时同一帧不继续退出页面。

`EditorBackNavigationTests` 检查路由、加载状态、过渡状态、弹窗优先级、同帧菜单关闭不冒泡，以及其他页面的普通返回。运行态使用临时曲包交替执行四轮 Esc／退出按钮，保留真实保存、Presenter 清理、缓存删除与 Unity 延迟销毁，仅用同步回调代替首页动画；验证每轮编辑保存、旧页面销毁以及下一轮创建新实例。测试未改写用户曲包，也未启动试玩。与采音和预览池回归合计 37/37 通过（`Logs/editor-escape-reentry-tests.xml`）。Windows 输出 `Builds/Windows-EditorReentry/OpenSekai.exe`，构建日志 `Logs/windows-editor-reentry-build.log`。
