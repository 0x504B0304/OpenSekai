# OpenSekai

OpenSekai 是一个用于学习和研究目的的 Project Sekai 音乐游戏玩法复刻项目，目前提供可用的**谱面编辑器**、**第三方歌曲包支持**，以及**从编辑器进入 live 测试游玩**的完整流程。

当前版本：**1.6.17**。发行变更见 [CHANGELOG.md](CHANGELOG.md)。

## 开发环境

- Unity：`6000.6.0f1`。工程资源已经按此版本序列化，建议使用完全相同的编辑器版本。
- 渲染管线：Universal Render Pipeline `17.6.0`。
- 开发平台：Windows 10/11 x64；建议安装 Visual Studio 2022 的“使用 Unity 的游戏开发”工作负载。
- Windows 构建模块：Windows Build Support，脚本后端为 Mono，目标架构为 x86-64。
- Android 构建模块：Android Build Support，以及 Unity Hub 随模块安装的 OpenJDK、Android SDK 和 Android NDK。脚本后端为 IL2CPP，目标架构为 ARM64，最低 Android API 为 26。
- 主要依赖：UGUI/TextMesh Pro `2.6.0/5.0.0`、Timeline `6.6.0`、UniTask、MessagePack、uPalette、UI Particle、SoftMaskForUGUI。

命令行构建入口：

- Windows：`Sekai.EditorTools.OpenSekaiAssetBundleBuildPipeline.BuildWindowsPlayer`
- Windows 双版本 ZIP：`Sekai.EditorTools.OpenSekaiWindowsReleaseBuild.Build`，构建前将环境变量 `OPENSEKAI_FFMPEG_ROOT` 设为解压后的 FFmpeg 发行目录（包含 `LICENSE` 和 `bin/ffmpeg.exe`）。输出 `Builds/Release/win_amd64.zip`（不含 FFmpeg）及 `Builds/Release/win_amd64_ffmpeg.zip`（包含 FFmpeg、所需 DLL 和许可说明），自动排除调试符号、Unity 调试备份目录和日志。
- Android：`Sekai.EditorTools.OpenSekaiAssetBundleBuildPipeline.BuildAndroidPlayer`

调用时使用 `<Unity 安装目录>/Editor/Unity.exe -batchmode -quit -projectPath <项目目录> -executeMethod <构建入口>`。构建流程会先生成对应平台的 AssetBundle，再生成 Player；默认输出分别为 `Builds/Windows` 和 `Builds/Android/OpenSekai.apk`。

## 社区功能

当前分支合并了 [OpenSekai Community](https://github.com/kamcdev/OpenSekai_Community) 的功能，包括谱面自动保存、预览与音频波形、备份恢复和 Android 分享、自定义 Autoplay 与结算动画、判定线透明度与引导线颜色、谱面时长计算、负流速与单键变速、装饰音符、PC 键鼠操作，以及 Windows/Android 谱面视频生成。

Windows 点击“生成视频”时，会先依次检查主程序目录、`ffmpeg/` 子目录和环境变量 `PATH` 中的 `ffmpeg.exe`，并确认它能启动；不可用时弹窗提示，不会进入录像。无 FFmpeg 版本可自行放入 FFmpeg 及所需 DLL，或将其 `bin` 目录加入 PATH 后重启程序。带 FFmpeg 版本可直接使用，无需配置 PATH。

Windows 录像捕获帧末的完整画面（含谱面、UI 和 MV），声音取自 CRI 主输出。编码窗口在切换场景后仍保留，显示进度；保存完成后显示文件路径，可打开文件位置。录制失败时保留原始帧和音频，便于排查。

Android 使用相同的完整画面和 CRI 声音录制，再由工作线程调用 MediaCodec 编码 H.264/AAC、MediaMuxer 合成 MP4，无需 FFmpeg。开始前检查设备编码能力，生成时显示画面、音频、合并和保存进度。成功后保存到 `Movies/OpenSekai_Rec` 相册，并可直接分享视频；保存失败可重试，原视频会保留。Android 10 及以上写入本程序生成的视频不要求读取相册权限；Android 8/9 需要存储写入权限。请保持应用在前台，切到后台会中断录制或编码；编码中断后可重试生成，录制阶段中断则需重新录制。

## 菜单外观与主题

首页曲库、曲目配置、设置和新增采音侧栏采用 PJSK 实机参考中的灰紫色容器、圆角按钮和青绿选中状态。主题选项统一放在“设置 → 显示 → 菜单主题”，立即生效并记忆选择。制谱器原有音符、长条、滑键、轨道和工具按钮，以及 Live、暂停和结算资源不参与菜单换肤。

曲目配置按基本信息、媒体资源和谱面参数分组；设置按声音、游玩、显示、制谱、语言与数据分类。新菜单布局偏好独立使用 `MenuUI.*` 保存，不迁移曲包或采音草稿。左右侧栏可独立折叠并归还轨道宽度。

UI Extensions 3.0.0 固定上游提交 `d47f838ba918bbb9b26f242bd944940a711f525f`，通过 UPM 嵌入源码交付，以保留 Unity 6000.6 / UGUI 2.6 必需的兼容补丁。详见 `Packages/com.unity.uiextensions/OPENSEKAI-PATCH.md`；未导入示例场景，保留 BSD 许可证。

设计参考：`designs/opensekai-ui-redesign/index.html`。本机预览：`http://127.0.0.1:4311/opensekai-ui-redesign/`。验证范围见 `Tests/MenuUI/README.md`。

## 制谱采音辅助

制谱器右侧“采音”展开波形和辅助面板，左侧“工具”可独立折叠。声部按钮亮起即参与混音，可同时启用多个声部或全部关闭；波形最多同时显示两条。波形与谱面使用同一时间映射，支持起音提示、拖动草稿、A/B 循环和 0.75× / 0.5× 保音高回听。

Windows 完整版本自带 Demucs 分轨、MMS_FA 对齐模型和运行环境，无需安装 Python 或联网下载模型。读取原曲后自动分轨，后续复用缓存；取消后可手动重试。导入 LRC 后可逐句试听并进行对齐（日语假名拍、中文按字、英语按词），增强 LRC 可直接读入音节时间。自动结果保留声学得分，低分音节标为“待核对”。使用与开发打包说明见 [内置分析说明](Assets/StreamingAssets/AudioAssist/README.md)。

将起音或歌词加入草稿，选定原有音符工具后点击轨道即可按草稿时间落键；长条可依次指定头、中间点及尾，并沿用原来的撤销/重做。候选 BPM 可预览拍线后应用并撤销。采音状态另存于 `persistentDataPath/AudioAssist/<MusicId>/session.json`，正式谱面仍由游戏保存按钮保存。音频与歌词偏移均按曲包实际时间处理，不固定加入某个等待时长。移动端可导入分轨与增强 LRC，本机模型进程暂限 Windows。

## 高 DPI 字体

Windows Player 使用 PerMonitorV2 DPI 感知。Unity 6 下每 250 毫秒检查窗口 DPI 和渲染尺寸，变化稳定 500 毫秒后，从内置 OTF 重新生成动态 SDF 字形并刷新文字；无需重启。字体采样随系统缩放和渲染尺寸在 90–270 点之间调整，采用 2048×2048 多图集。此功能提升文字清晰度，界面布局仍以 1920×1080 为基准缩放。

## 目录说明

- `Assets/Scripts/Assembly-CSharp/Sekai`：主要游戏逻辑。
- `Assets/Resources`：运行时通过 `Resources` 加载的 Prefab、文本、字体、特效和界面数据。
- `Assets/Sekai/assetbundle/resources`：原样放置的 AssetBundle 源资源，资源本身应保留 AssetBundle 名称，之后通过构建流程打包。
- `Assets/StreamingAssets`：打包后的 AssetBundle 与本地运行数据输出位置，主要用于构建后的运行环境。
- `Assets/Editor/OpenSekaiAssetBundleBuildPipeline.cs`：本地 AssetBundle 构建流程。

## 资源说明

本仓库中的代码以 MIT 协议发布。第三方库、字体、音频、贴图、prefab、shader 参考资源以及任何可能来源于原作或其他权利方的资源，不自动包含在 MIT 授权范围内，请分别遵循其原始许可和权利归属。

本项目与任何原作游戏、发行方或权利方没有官方关联。
