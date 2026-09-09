# 菜单主题验证记录

日期：2026-09-09。Unity 6000.6.0f1 / UGUI 2.6.0。交付：`Builds/Windows-MenuUI/OpenSekai.exe`，随目录附带离线分析引擎。

## 已完成检查

- `Logs/menu-ui-final-tests.xml`：71/71 通过。覆盖新控件静默状态恢复、指针选择、收起再展开后的选中项、深浅主题与未绑定音符隔离、菜单滚轮／拖动、旧版帧率配置兼容、原曲／分轨最终声音输出、慢放、循环、草稿与历史、波形和时间转换。
- `Logs/menu-ui-validation-final.log`：曲库／设置在深浅两种主题下、1920×1080、1366×768、1024×768、844×390、3840×2160 共 20 张离线 Unity 渲染检查通过；验证详情／设置面板边界、分类可访问及媒体字段保留。图片在 `Logs/MenuUI/`。
- `Logs/menu-ui-assist-validation.log`、`Logs/menu-ui-assist-light.log`：实际制谱器预制体五种尺寸、左右面板四种开合组合通过，重复折叠不漂移、左侧归还轨道宽度且右边界稳定，波形每条色带对应一个物理像素。最后一次浅色复查日志为 `Logs/menu-ui-assist-final.log`。
- HTML 经 `node --check`、浏览器点击及保存／刷新检查；深浅切换、多选混音、分类、详情切换和面板收起可操作。设置页在五种尺寸无横向溢出，主要按钮高度 44px。浏览器错误日志为空。
- Windows 构建成功：`Logs/menu-ui-windows-build.log`。构建钩子逐文件校验离线引擎的大小及 SHA-256，确认包含嵌入式 Python、Demucs、MMS_FA、FFmpeg 和许可证；原有分析代码与模型未因菜单重构替换。
- 原制谱器 prefab、音符皮肤／精灵、Live／暂停／结算资源无此次修改；新增样式只显式绑定菜单实例，不修改全局字体、材质或 uPalette。

## 检查范围与限制

Unity 截图使用独立空场景和合成曲目／波形；不导入用户曲包，不进入实际 Live。原编辑器资产在这种离线场景中有缺失字形及旧平铺精灵警告，部分背景／图标无法等同发行程序的完整资源加载结果。渲染记录用于布局和几何验证，不代表真机截图。

未进行触屏硬件测试，也未通过真实文件选择器完成新建／导入／导出流程的人工回归。业务回调与存储入口保留原实现；设置原有保存／取消语义保留，主题切换为独立即时偏好。新菜单分类支持中文、日文、英文；既有采音详细说明仍以中文为主。

HTML 是菜单交互参考：制谱器主体显示用户提供的原始截图，新增面板可操作，但不运行真实音频或修改谱面。可运行功能以 Unity 程序为准。

## 复现

```powershell
& 'D:/Unity/6000.6.0f1/Editor/Unity.exe' -batchmode -projectPath E:/Code/OpenSekai -runTests -testPlatform EditMode -testFilter 'MenuUiEditModeTests|MenuScrollEditModeTests|FramerateSettingEditModeTests|AudioAssist|HistoryShortcutEditModeTests|MusicScoreWheelEditModeTests' -testResults E:/Code/OpenSekai/Logs/menu-ui-final-tests.xml -logFile E:/Code/OpenSekai/Logs/menu-ui-final-tests.log
```

布局入口：`MenuUiValidation.Capture`、`AudioAssistUiValidation.Capture`、`AudioAssistUiValidation.CaptureLight`。同一工程不要同时运行多个 Unity 主进程。

UI Extensions 来源与补丁：`Packages/com.unity.uiextensions/OPENSEKAI-PATCH.md`。主题资源：`Assets/Resources/MenuUI/`。样式绑定与适配：`Assets/Scripts/Assembly-CSharp/Sekai/MenuUI/`。

## 2026-09-10：主题入口与圆角修复

主题入口仅保留在“设置 → 显示 → 菜单主题”，首页和采音标题不再提供切换。沿用独立即时保存的主题偏好，设置中明确标注立即生效。HTML 参考同步更新。

实际 MusicScoreMaker 场景的 CanvasScaler 使用 referencePixelsPerUnit=1，而之前离线验证使用 Unity 默认值 100，致使运行时的九宫格圆角缩小 100 倍。新增 MenuRoundedImage 只对菜单图片归一化像素单位，且在变更父画布后更新；不修改原场景画布、音符或材质。曲目卡片、分栏、配置表单、页签、输入框与操作按钮均使用该适配。

验证：`Logs/menu-rounded-tests-final.xml`，5/5 通过；覆盖原生／默认两种画布以及重新挂接画布后的圆角尺寸，另含选择、范围、主题状态测试。`Logs/menu-rounded-layout-final.log` 中五种分辨率、两种主题的 Unity 渲染通过，验证改为真实的 referencePixelsPerUnit=1，并确认主题入口不在首页。浏览器参考页验证主题仅在显示设置中切换，控制台错误为空。

Windows 输出：`Builds/Windows-MenuUI-Rounded/`；构建日志 `Logs/menu-rounded-windows-build.log`。不自动启动 Live。

## 2026-09-10：工具栏荧光折叠箭头

移除左上方“工具”文字按钮，新增独立 UGUI 荧光箭头，使用淡紫／青色边缘与浅色内芯。展开时贴实际工具列表右缘并朝左；折叠时位于屏幕左侧中央并朝右。点击热区至少 44 个物理像素，轨道扩展保留热区与小节编号间距；原有音符图片、材质与场景未修改。

按用户要求仅静态审查方向、定位、缩放、热区与销毁路径，没有运行测试用例、布局截图或游戏试玩。Windows 直接编译成功，离线引擎随包校验通过：`Builds/Windows-Editor-FoldArrow/OpenSekai.exe`；日志 `Logs/editor-fold-arrow-build.log`。实际屏幕显示和触控手感留待用户确认。

## 2026-09-10：右侧图标栏与高分辨率小地图

右侧四个独立图标替代测试游玩、保存、采音和小地图入口；保存／测试按钮复用原按钮回调和可交互状态，保留原播放按钮及时间显示。采音与小地图分别保存开合偏好，移除原缩放／移动按钮组；小地图关闭时停止刷新。紧凑布局通过运行时适配完成，原预制体、音符皮肤和游玩资源未修改。

轨道以上一版原生布局的宽度为上限，计算左右可用边界后居中或向内收窄；恢复基准尺寸后重算，避免重复折叠产生宽度漂移。小地图宽度按实际像素取 12 的倍数（144–768），高度兼顾实际像素与每拍 8 像素，最高 4096；长条先做连续轨道插值再转为像素坐标。保留 PC 滚轮及单指拖动；双指连续缩放限制在谱面开始且双指位于谱面内。

沿用静态审查后直接编译的验证方式，没有运行测试用例、截图验收或自动开始游戏。`Logs/editor-compact-dock-build.log` 记录 Windows 构建成功及离线引擎随包校验；输出 `Builds/Windows-Editor-CompactDock/OpenSekai.exe`。多分辨率实际布局、鼠标／触控手感和真实曲包流程尚未做运行时验证。

## 2026-09-10：图标悬停提示修复

共用 ActionHint 曾被 RuntimeLocalizationBootstrap 定期扫描绑定成 common.save，之后每 0.75 秒及重新启用时将其它图标的文字覆盖成“保存”。改为预先创建 LocalizedTextBinding，在进入图标时切换对应键，再启用提示；离开事件仅能隐藏自己拥有的提示。测试游玩／保存复用已有键，采音／小地图补齐中日英条目。

静态审查绑定生命周期和四个入口映射，未运行测试用例或游戏。`Logs/editor-tooltip-fix-build.log` 记录 Windows 编译、构建及离线引擎校验成功；输出 `Builds/Windows-Editor-TooltipFix/OpenSekai.exe`。

## 2026-09-10：多轨波形与可拖动分隔线

移除采音标题行重复的折叠按钮；六个混音按钮只用高亮表达启用状态，分别使用原曲青蓝、人声紫、伴奏绿、鼓组橙、贝斯黄、旋律粉，与波形和声部标题共用调色表。局部禁用这六个按钮的全局主题颜色绑定，保留其它菜单主题行为。

启用且载入的音轨自动加入波形，取消启用时移出；音量分组的“波形”入口仍可查看未参与混音的声部。移除两轨限制，每轨独立网格，避免六轨共用一个网格超过 UGUI 顶点限制。波形沿用精确时间换算与逐屏幕像素峰值绘制。

拖动音轨左侧分隔线调整该轨宽度；右侧其它轨宽度保持，左侧音轨与谱面随总宽度重排，达到谱面最小空间时限制扩展。按下分隔线使用青色高亮、6 个物理像素的线及更宽光晕，热区宽 44 个物理像素（外边缘受面板裁切）。释放后以独立 MenuUI.waveWidth.* 偏好保存；折叠或销毁时结束拖动，不改曲包或采音时间数据。

按既定偏好仅静态审查，无测试用例、布局截图或游戏试玩。`Logs/editor-multitrack-build.log` 记录 Windows 构建及离线引擎校验成功；输出 `Builds/Windows-Editor-MultiTrack/OpenSekai.exe`。六轨在窄屏上的实际布局及触控拖动体验未做运行时验收。

## 2026-09-10：曲目配置与设置选项卡对比

两处 UseTabs 共用连续标签栏；选中项使用浅底深字、加粗、上方圆角和青色底线，未选中项融入统一栏底。新增独立 TabBar/TabActive/TabActiveInk/TabInactiveInk 主题角色，深浅主题切换通过原有绑定即时更新。标签栏去除按钮间隙与阴影，保留既有按钮高度、互斥选择和业务回调；速度和主题等普通分段控件不改变。

静态审查包含两处接入、选中恢复、主题绑定、装饰不拦截点击和原触控高度；未运行测试用例、布局截图或自动试玩。Windows 构建成功，离线引擎随包校验通过，日志 `Logs/menu-tabs-build.log`；输出 `Builds/Windows-MenuUI-Tabs/OpenSekai.exe`。实际运行时的视觉与触控效果尚未验收。

## 2026-09-10：完整回归与 computer-use 视觉检查

本轮按用户新要求运行测试并打开 Windows 程序检查，取代上文各小版本的静态审查限制。

### 自动验证

- Unity EditMode：`Logs/ui-final-tests.xml`，187 项中 184 通过、0 失败、3 跳过。跳过的是原有 `LiveSpeedIntegralCache` 三档性能报告用例（Explicit），不是功能失败。
- Python 音频测试：`.audio-assist-venv/Scripts/python.exe -B -m unittest discover -s Tests/AudioAssist -v`，3/3 通过。
- `MenuUiValidation.CaptureReview` 覆盖 1920×1080、1366×768、1024×768、844×390、3840×2160，深浅主题的曲目配置三类、设置五类，以及六轨波形下左右面板四种折叠组合。菜单 80 张、制谱器 40 张离线渲染，检查可视区域、完整表单高度、折叠稳定性和波形每物理像素的采样密度。日志 `Logs/ui-final-layout-captures.log`；图像 `Logs/MenuUI/`、`Logs/AudioAssistValidation/`。
- 离线制谱器渲染用真实预制体和合成 PCM；为隔离跨临时场景的共享字体材质，统一采用独立 CJK 字体。实际字体、背景和图标以 Windows 实机截图复核。

### 实机发现与修复

使用 computer-use 的 Windows 窗口截图和逐步点击，检查曲库／基本信息／媒体／谱面参数、设置五个分类和主题切换、制谱器左右折叠、采音与小地图同时展开、编辑菜单、自动保存提示、试玩确认（取消）、文字／图片谱面工具、歌词及自动分析入口。

修复内容：页签整块内容底色连贯；隐藏设置页的选中形状和文字颜色；曲名被本地化占位文案替换；详情标题与摘要重叠；配置保存按钮间距；844×390 表单视口过短；短循环 A/B 热区重叠；波形分隔线缺少 CanvasRenderer；艺术谱面面板透出背后图标及关闭按钮被拉宽。

最初全套测试暴露的字体失败已定位为 TMP `TryAddCharacters` 在全部字形已缓存时返回 false，现直接验证每个字符是否存在，保留真实缺字失败。原透明面板测试同步更新为当前遮挡行为。修复后全套重新运行通过。

### 范围与限制

没有自动开始实际游玩，也没有导入／改写用户曲包。暂停、结算及原生音符资源通过 Git 范围审查确认未改动，不声称完成这些界面的动态验收。五种尺寸属于离线 Unity 渲染；Windows 实机检查是当前显示器窗口，未使用 Android/iOS 真机，触控手感和完整多指操作仍需设备验收。分轨模型未重新运行耗时分析，验证已有音频测试和构建的离线引擎校验。

Windows 输出：`Builds/Windows-MenuUI-Review/OpenSekai.exe`；构建日志：`Logs/menu-ui-review-build.log`。构建和歌曲媒体、模型权重不提交 Git；源码、嵌入的 UI Extensions 包及许可证、测试和工具脚本提交。

最终构建实机复查：曲名与标题摘要正常；设置显示分类在深浅主题下均保留连贯的页签／内容底色；英文主要设置文字在按钮和标签范围内，随后恢复中文和深色。原曲、人声、鼓组三轨的实际波形与谱面在小地图定位后同步移动；A/B 上下手柄独立显示。Windows Player 日志未出现 Exception、MissingComponent 或 NullReference。

补充限制：部分既有次要选项尚未完全翻译，切至英文仍显示中文，本轮未扩展翻译范围。computer-use 的拖动尝试未稳定产生预期的连续拖拽，不能据此宣称通过真实触控拖拽验收；已验证分隔线组件、显示、指针命中和多轨布局，持续拖动手感仍需人工确认。

新版图片谱面面板已实机确认：不再透出右侧图标，关闭按钮保持紧凑宽度，输入框及前景操作按钮之间有间距。最终 Player 日志另存为 `Logs/menu-ui-review-player.log`。
