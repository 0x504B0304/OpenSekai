# OpenSekai

OpenSekai 是一个用于学习和研究目的的 Project Sekai 音乐游戏玩法复刻项目，目前提供可用的**谱面编辑器**、**第三方歌曲包支持**，以及**从编辑器进入 live 测试游玩**的完整流程。

## 开发环境

- Unity：`6000.6.0f1`。工程资源已经按此版本序列化，建议使用完全相同的编辑器版本。
- 渲染管线：Universal Render Pipeline `17.6.0`。
- 开发平台：Windows 10/11 x64；建议安装 Visual Studio 2022 的“使用 Unity 的游戏开发”工作负载。
- Windows 构建模块：Windows Build Support，脚本后端为 Mono，目标架构为 x86-64。
- Android 构建模块：Android Build Support，以及 Unity Hub 随模块安装的 OpenJDK、Android SDK 和 Android NDK。脚本后端为 IL2CPP，目标架构为 ARM64，最低 Android API 为 26。
- 主要依赖：UGUI/TextMesh Pro `2.6.0/5.0.0`、Timeline `6.6.0`、UniTask、MessagePack、uPalette、UI Particle、SoftMaskForUGUI。

命令行构建入口：

- Windows：`Sekai.EditorTools.OpenSekaiAssetBundleBuildPipeline.BuildWindowsPlayer`
- Android：`Sekai.EditorTools.OpenSekaiAssetBundleBuildPipeline.BuildAndroidPlayer`

调用时使用 `<Unity 安装目录>/Editor/Unity.exe -batchmode -quit -projectPath <项目目录> -executeMethod <构建入口>`。构建流程会先生成对应平台的 AssetBundle，再生成 Player；默认输出分别为 `Builds/Windows` 和 `Builds/Android/OpenSekai.apk`。

## 目录说明

- `Assets/Scripts/Assembly-CSharp/Sekai`：主要游戏逻辑。
- `Assets/Resources`：运行时通过 `Resources` 加载的 Prefab、文本、字体、特效和界面数据。
- `Assets/Sekai/assetbundle/resources`：原样放置的 AssetBundle 源资源，资源本身应保留 AssetBundle 名称，之后通过构建流程打包。
- `Assets/StreamingAssets`：打包后的 AssetBundle 与本地运行数据输出位置，主要用于构建后的运行环境。
- `Assets/Editor/OpenSekaiAssetBundleBuildPipeline.cs`：本地 AssetBundle 构建流程。

## 资源说明

本仓库中的代码以 MIT 协议发布。第三方库、字体、音频、贴图、prefab、shader 参考资源以及任何可能来源于原作或其他权利方的资源，不自动包含在 MIT 授权范围内，请分别遵循其原始许可和权利归属。

本项目与任何原作游戏、发行方或权利方没有官方关联。
