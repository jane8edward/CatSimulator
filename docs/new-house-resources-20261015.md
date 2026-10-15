# New_House 与新增猫资源提交（2026-10-15）

## 收录内容

|内容|仓库位置|
|---|---|
|新的 90㎡ Tripo 住宅工程|`Source/Blender/New_House/04_场景工程/New_House_v001.blend`|
|78 份原始模型 FBX 和配套贴图|`Source/Blender/New_House/02_解压原文件/NH002` 至 `NH079`|
|79 个校准后原生模型|`Source/Blender/New_House/03_整理资产/NH*/NH*_Ready.blend`|
|模型映射、尺寸、修正记录|`Source/Blender/New_House/00_管理`|
|建模、搭建、灯光、检查和渲染脚本|`Source/Blender/New_House/06_脚本`|
|平面图、物体参考和住宅效果方案|`Source/Blender/New_House/01_参考资料`|
|正式 Cycles 渲染与模型对照页|`Source/Blender/New_House/05_渲染与检查`|
|新提供的猫 FBX 与完整贴图|`assets/Cats/CuteCat/Original`|
|改进后的三花猫四视图|`assets/Cats/Calico/Reference/Improved20261015`|
|复制清单、校验与本次构建/试玩结果|`Source/Verification/NewHouse`|

本次是独立美术源工程提交，旧 `CatHome90`、Unity `LivingRoom.unity` 及既有猫玩家保持原样。New_House 原生场景保留 124 个共享实例、16 个阵列；12,847,735 个独立网格面，尚未针对游戏运行时制作 LOD 或碰撞。本次不将该高密度全屋源场景直接导入 Unity 的 Assets。

桌面原件保持原样。ZIP、NH001 插件源码包、`*_Asset.blend` 高精度导入检查副本、Blender 备份、草稿渲染和 Unity 缓存不上传。原始高精度 FBX 与纹理已保留；需要重新修正时可从 FBX 重建导入检查副本。提交清单中的 SHA-256 对应桌面来源；文本路径和辅助脚本为适应克隆位置进行过调整，二进制模型与参考图保持来源字节不变。

## 打开与复测

先执行 `git lfs pull`，再用 Blender 5.2.2 LTS 打开主场景。模型、贴图、预览图均使用仓库现有 LFS 规则。

新文件位于 Unity 的 `Game/Assets` 之外，无需生成 Unity `.meta`；现有 `.meta` 与 GUID 全部保留。未来将模型导入 Unity 时，应提交导入产生的 `.meta`。

```sh
blender --background --python tools/verify-new-house.py
```

该检查重新读取仓库主场景与全部 79 个 Ready 资产，验证原始 FBX/贴图长度、实例与阵列、坐标、贴图打包与资源完整性；另行导入新增猫原始 FBX 并渲染。不会覆盖桌面原件。

Unity 复测使用 6000.3.26f1，以当前默认可玩场景构建到 `Builds/NewHouseSubmission/CatSimulator.app`；Builds 与日志不提交。新模型的验证范围是 Blender 资产与原始 FBX，Unity 构建及试玩用于检查现有关卡回归。

本次实际结果见 `Source/Verification/NewHouse/validation-summary.json`；四张住宅 Cycles 图是本次建模交付的正式渲染，已经随原生工程保留。

## 本次验证结果

- Blender 5.2.2 LTS：重新读取主工程和 79 个 Ready 资产；78 份原始模型文件完整，124 个实例、16 个阵列，缺失贴图 0。新提供的猫原始 FBX 导入并完成渲染。
- 712 个二进制来源文件 SHA-256 与桌面原件一致。新增范围 Git LFS 对象校验通过。全仓库扫描还发现历史美术/媒体的本地 LFS 缓存未全量下载；本次未改动这些历史文件。
- Unity 6000.3.26f1：Mac 通用开发构建 Succeeded，构建错误 0；191 个现有 Unity GUID 无重复，无缺失 .meta。
- Apple M4 实际运行：猫动画、位移、暂停、重置、旧住宅 1352 个网格加载通过，运行错误 0。
- 原关卡：移动、跳跃、爬架支撑、286 个实体网格碰撞覆盖、两项任务、镜头和重玩通过，顺序为先水杯后窗帘。
- 合入远程 main `b63affc`，无文件冲突；带入的菜单与原生猫贴图修复均保留。

构建调用现有 `CatSimulator.Editor.ModelResourceIntegration.BuildMac`，输出另存于 `Builds/NewHouseSubmission`，旧构建与历史验证记录已恢复保留。复测可调用仓库已有 `tools/build-mac.sh`（默认输出 `Builds/Mac`），然后对其生成的程序运行：

```sh
"Builds/Mac/CatSimulator.app/Contents/MacOS/Cat Simulator" --verify-model-resources --verification-output "$PWD/Source/Verification/NewHouse/Runtime"
"Builds/Mac/CatSimulator.app/Contents/MacOS/Cat Simulator" --verify-playable --verification-output "$PWD/Source/Verification/NewHouse/Playable"
```
