# 弹窗字体与布局排查（2026-09-11）

已修复本轮确认的公共弹窗文字重叠。直接原因是旧 Prefab 的负行距沿用到了使用原生字体度量的新菜单字体上。截图中的两行文字来自同一个 MessageBody，并非两个弹窗同时打开。

修复集中在 `DialogTextLayout`，由 `DialogBase.Open` 应用到弹窗实例：清除负行距，按实际文字测量正文高度，超长通用消息允许滚动且按钮保持固定；量化标签按文字宽度排列；事件输入弹窗移除使正文高度归零的空布局组件。文案、字体或窗口尺寸变化后重新布局。本轮不修改游玩 HUD 共享字体、原始 Prefab 或已有谱面，不提交 Git。

## 修复前的原因与测量

- [LivePauseDialog.prefab](/E:/Code/OpenSekai/Assets/Resources/dialog/LivePauseDialog.prefab:2699)：正文 36，行距 -80，文本区域 1072 × 100；窗口约 1168 × 294。
- 1920 × 1080 下，HarmonyOS SC 的两行基线间隔仅约 13.39，实际字形纵向交叠约 20.44。到 4K 后重叠随界面等比例放大。
- 保留 36 号字，仅在诊断实例上把行距设为 0，正文所需高度由 55.59 恢复到 84.39，字形间隙变为约 8.36。当前 100 高度可以容纳中文样例。
- 日文所用菜单 Rodin 原生度量下，同样的两行内容在零行距时 preferredHeight 为 104.26，略超 100。因此不能只批量把行距改为 0，还应调整正文高度。
- [MenuTypographyBinding.cs](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/MenuUI/MenuTypographyBinding.cs:23) 会替换字体和材质、清除人工粗体，但保留原字号、行距、容器高度。这是换字体后暴露旧排版参数的连接点。

最新输出已由修复后的验证覆盖：[暂停](/E:/Code/OpenSekai/Logs/DialogAudit/LivePauseDialog-zh-Hans-1920-current.png)、[量化](/E:/Code/OpenSekai/Logs/DialogAudit/MusicScoreMakerCustomQuantizeDialog-zh-Hans-1920-current.png)。上方数字保留修复前的测量记录，不能用最新截图作为修复前图示。

## 全部同类 Prefab（修复前发现）

共检查 dialog 目录下 9 个弹窗 Prefab，以及额外的视频进度 Prefab。8 个含负行距，其中单音符速度旧 Prefab 无有效 DialogBase 组件，未纳入运行时渲染成功范围。

| 资源 | 字号/行距 | 检查结论 |
|---|---|---|
| [LivePauseDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/LivePauseDialog.prefab:2711) | 正文 36/-80；按钮 32/0 | 已复现两行重叠。正文高度 100，需要按字体和文本测量。按钮短文案可读。 |
| [MusicScoreMakerTestPlayPauseDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/MusicScoreMakerTestPlayPauseDialog.prefab:2711) | 正文 36/-80；按钮 32/0 | 同一缺陷，中文两行字形交叠约 21.41。覆盖普通测试及全连检查暂停。 |
| [Common1ButtonDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/Common1ButtonDialog.prefab:2490) | 正文 32/-80，启用自动字号；正文区域高 120 | 短单行可读，多行压力样例发生叠行；不是可靠的通用消息布局。 |
| [Common2ButtonDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/Common2ButtonDialog.prefab:2601) | 正文 32/-80，启用自动字号；正文区域高 120 | 重试确认短文案正常；多行、错误说明、文件路径存在相同风险。自动缩字仍可能叠行。 |
| [SubWindowDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/SubWindowDialog.prefab:878) | 正文 32/-88，内容自适应 | 自适应高度也会被负行距误导。两行样例测得字形交叠约 21.53。 |
| [MusicScoreMakerCustomQuantizeDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/MusicScoreMakerCustomQuantizeDialog.prefab:1547) | 说明 34/-87.2，高 80；标签/结果 28/-80 | 说明两行明确重叠。Setup(16) 后“网格”与“16分”也发生水平重叠，多次刷新布局仍可复现；结果区需同时整改。 |
| [AddMusicScoreEventDataDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/AddMusicScoreEventDataDialog.prefab:1892) | 正文配置 32/-80；BPM 样例最终缩至 22；输入/按钮 32 | BPM 单行样例可读，但正文 Rect 高度测为 0，依赖溢出绘制/缩字，排版基础不稳。拍号、速度、颜色共用该资源，需一起整改。 |
| [MusicScoreMakerTestPlayDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/MusicScoreMakerTestPlayDialog.prefab:1573) | 说明/选项/按钮主要 32，行距 0 | 基础实例未发现同类负行距重叠，说明/复选框区域高 50。LiveMode 初始化状态和长翻译仍需交互复查。 |
| [SingleNoteSpeedChangeSubWindowDialog](/E:/Code/OpenSekai/Assets/Resources/dialog/SingleNoteSpeedChangeSubWindowDialog.prefab:375) | 标题 32/-88；输入/按钮 24 | 根脚本 GUID 为占位值 a1b2c3…，无法获得 DialogBase；未找到实际调用。应单列为旧资源，不作为已验证正常窗口。 |
| [VideoProcessingProgressDialog](/E:/Code/OpenSekai/Assets/CustomMusicScoreManager/Resources/CustomMusicScoreManager/UI/VideoProcessingProgressDialog.prefab) | 传统 UI.Text：标题 24、百分比 32、步骤 18；行距倍数 1 | 短样例未见重叠；步骤区仅 480 × 60，长错误文本需要滚动/自适应策略。未发现当前实际调用，现用导出路径见下文。 |

## 创建链与业务入口

公共创建链为 [ScreenManager.InstantiateDialog](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/ScreenManager.cs:3036) → Resources 的 Dialog/dialog 路径 → Initialize → Open；字体在 [DialogBase.Open](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/DialogBase.cs:111) 应用。弹窗宽度配置在 [DialogSizeFitter](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/DialogSizeFitter.cs:31)，主要调整宽度及底部留白，没有统一根据正文重算可用高度。

| 调用来源 | 具体业务路径 | 最终资源 |
|---|---|---|
| [LiveOutUIController](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/LiveOutUIController.cs:75) | ShowPauseDialog；ShowMusicScoreMakerTestPlayPauseDialog；ShowMusicScoreMakerFullComboCheckPauseDialog | 两种暂停 Prefab |
| 同上 | ShowConfirmRetryDialog；ShowConfirmRetireDialog；ShowMusicScoreMakerTestPlayFinishDialog | Common2ButtonDialog |
| 同上 | ShowMusicScoreMakerFullComboSuccessDialog；ShowMusicScoreMakerFullComboFailedDialog | DialogUtility → SubWindowDialog |
| [管理器](/E:/Code/OpenSekai/Assets/CustomMusicScoreManager/Runtime/UI/ScreenLayerCustomMusicScoreManager.cs:1507) | 备份确认/进度/完成/失败/分享；恢复范围/全部恢复确认/进度/完成/失败 | Common1ButtonDialog、Common2ButtonDialog |
| 同上 | 删除谱面；导出失败/导出后分享；计算时长确认；生成视频确认；相册权限说明；ShowSuccessDialog（设置保存、导入/复制结果及各类错误） | Common1ButtonDialog、Common2ButtonDialog |
| [MusicScoreMakerPresenter](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Presenters/MusicScoreMakerPresenter.cs:1340) | 自动保存关闭警告；BPM 变更/删除导致音符超范围确认；清空音符与速度事件；教程及教程类型选择；工具选项提示 | Common2ButtonDialog |
| 同上 | 无音符无法测试；测试前警告；OnTestPlay | Common1ButtonDialog、SubWindowDialog、MusicScoreMakerTestPlayDialog |
| 同上 | ShowAddMusicScoreEventDataDialog（BPM、速度、拍号） | AddMusicScoreEventDataDialog |
| [SelectedObjectEditUIView](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Views/SelectedObjectEditUIView.cs:517) | 单音符速度、颜色输入、装饰确认、颜色校验错误 | AddMusicScoreEventDataDialog、Common2ButtonDialog、Common1ButtonDialog |
| [QuantizeSettingsView](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Views/QuantizeSettingsView.cs:218) | OpenCustomQuantizeDialog → Setup(currentDivision) | MusicScoreMakerCustomQuantizeDialog |
| [MusicScoreMakerMusicScoreIdSearchDialog](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/MusicScoreMakerMusicScoreIdSearchDialog.cs:23) | ID 搜索及未找到提示 | 搜索 Prefab 缺失，有代码 fallback；未找到 Show 的业务调用。未找到提示使用 SubWindowDialog。 |

管理器的 WithMenuTheme 仅调整颜色/按钮外观，不修复行距或高度，不能规避以上问题。仓库中其他大量 DialogType 枚举和反编译 Dialog 类没有对应资源或有效打开路径，不能据类名声称已完成交互验证。

## 非公共弹窗的浮层

- [ArtToolsRuntimePanel](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/Views/ArtToolsRuntimePanel.cs:182)：手调覆盖确认是工具面板内联区域，字号 18、高 36，操作行高 38。没有旧负行距；短中文提示适合，窄宽度下英语换成两行时应按 preferredHeight 增高。此项为静态风险，未在本轮操作真实生成/覆盖谱面。
- [AudioAssistSyllableDialog](/E:/Code/OpenSekai/Assets/Scripts/Assembly-CSharp/Sekai/MusicScoreMaker/Ingame/AudioAssist/AudioAssistSyllableDialog.cs:203)：音节菜单/编辑框是运行时控件，字体 20/22，最小输入和操作高度 44，位置会限制在屏幕内。没有该负行距问题；此次静态核查，之前的音频 UI 截图验证不可替代本轮完整交互验收。
- [VideoExportOverlay](/E:/Code/OpenSekai/Assets/CustomMusicScoreManager/Runtime/UI/VideoExportOverlay.cs:37)：实际导出进度/结果浮层；标题 32，高 70，状态 23，高 185，按钮 23，高 60。普通短文案区域分离；status 使用 Overflow，长路径/异常可能越过状态区进入进度条和按钮。应限制高度并提供滚动或展开详情。
- 设置页是独立 SettingsDialog 面板，已在上一轮处理配色和语言选择内边距，不走这里的公共消息 Prefab。
- Windows/Android 系统文件选择器由系统/插件绘制，不使用这些 Unity 字体和布局参数。

## 已实施的统一修复

1. 在弹窗实例范围清除旧负行距，采用原生字体正常行高，游玩 HUD 的共享字体资产不变。
2. 通用消息、量化说明及事件正文设为 32，暂停提示保留 36；按钮保留原字号。通用消息关闭自动缩字。
3. 通用正文按当前字体和最终文案的 preferredHeight 计算，最小可见高度 120，上下各留 24，左右各留 64。超长正文按 Canvas 高度限制可见区域，使用滚轮或触摸拖动查看，按钮保持固定。
4. 量化标签和数值使用实际文字宽度加内边距；BPM/速度/拍号/颜色正文停用错误的空布局组件，恢复至少 100 高度。
5. 文案、字体、采样字号和 Canvas 尺寸变化触发重新布局；本地化绑定保留修复后的正文换行规则。SubWindow 继续使用已有自适应高度，但去除负行距。

## 验证范围和限制

- 执行 [DialogTypographyAudit.cs](/E:/Code/OpenSekai/Assets/Editor/DialogTypographyAudit.cs) 的 Run：9 个可实例化弹窗，3 个语言设置，1920 × 1080、3840 × 2066、844 × 390；另有暂停零行距对照及通用消息多行压力样例。
- 修复后回归通过，共 399 条文字测量，其中 57 条包含多行，最小字形行间隙约 4.17，无负间隙及窗口越界。测量输出在 [text-metrics.tsv](/E:/Code/OpenSekai/Logs/DialogAudit/text-metrics.tsv)，截图在 Logs/DialogAudit。Canvas 按 1920 × 1080 等比例适配；不是 Windows 系统 DPI 或 Android 实机交互测试。
- 当前旧 Wording 文案有未映射到三语表的项：例如暂停在英语/日语设置下仍可能显示旧中文文案。因此本报告的三语是三种语言设置/字体路径的验证，不意味着所有文案都已翻译正确。
- 初始窗口在测试尺寸中未测得越出屏幕；这不代表正文无叠行，也不覆盖所有长错误信息和动态状态。压力样例是专门构造的八行文本，未当作正常业务文案。
- 回归脚本检查实际字形行间隙、正文容器高度、量化标签不相交及窗口边界；额外使用 40 段消息检查打开后更新、滚动到底和正文与按钮不重叠。未操作用户正在运行的游戏。
- Windows Player 已重新构建成功，输出 `Builds/Windows-HarmonyOS/OpenSekai.exe`，日志 `Logs/DialogFix-windows.log`。本轮未重新构建或安装 Android APK。
