# 猫模型与 90㎡住宅资源接入

## 目录

| 内容 | 仓库位置 |
| --- | --- |
| 住宅主场景、84 类可编辑资产、管理表与参考图 | `Source/Blender/CatHome90` |
| 原设计平面图、全屋、客厅及阳台方案 | `assets/Reference/CatHome90Design` |
| 三花猫与阿比尼西亚猫四视图 | `assets/Cats/*/Reference` |
| 阿比尼西亚猫原始高精度模型与贴图 | `assets/Cats/Abyssinian/Original` |
| 修复蒙皮后的原生工作文件 | `Source/Blender/Abyssinian/Abyssinian_Cat_WalkRig.blend` |
| Unity 猫模型、38 根骨骼、行走片段、控制器及预制体 | `Game/Assets/Cat` |
| 猫咪行走视频和原工程的历史检查 | `Source/Preview/Abyssinian`、`Source/Verification/Abyssinian` |
| 住宅引擎预览 FBX、基础 URP 材质及贴图 | `Game/Assets/Art/CatHome90` |
| 本次导出、导入、构建和运行记录 | `Source/Verification/CatHome90`、`Source/Verification/ModelResources` |

桌面原文件保留。压缩归档、Blender 自动备份、Unity 缓存和 Builds 不提交。
原拓扑模型与贴图保留在 `Game/Assets/Cat/Source`，不重复复制第二套压缩包。
Unity 现有 `.meta` 的 GUID 沿用桌面工程；新增资源的 `.meta` 随资源一起提交。
大模型、源文件、贴图、预览图、视频和 OFL 字体使用仓库 Git LFS。

## 打开

1. 使用 Unity 6000.3.26f1 打开仓库的 `Game`，先完成 `git lfs pull`。
2. 原关卡仍是 `Assets/Scenes/LivingRoom.unity`。本次不替换现有玩家、场景及任务。
3. 行走预览：`Assets/Cat/Scenes/Walk_Preview.unity`，点 Play。可暂停、重置、切换前进与原地。
4. 全屋预览：`Assets/Scenes/Home90_Preview.unity`。这是供美术检查的独立场景。
5. 在 Blender 5.2 打开 `Source/Blender/CatHome90/04_场景工程/Blender/Layout/H90_Residence_v001.blend`。

住宅原生源文件保留 22 个阵列与 51 个集合实例；FBX 将它们展开为 1352 个网格对象，使用米制。
Unity 基础材质近似原生程序化材质；木纹、布料与玻璃的最终纹理烘焙、LOD、全屋玩法与碰撞验收待后续制作。
猫的步幅为 0.28m、周期 1.2s，匹配 0.2333m/s。现骨架以肩根移动近似肩胛滑动，尚无肌肉动力学。

## 复测

先保存并关闭占用同一工程的 Unity 编辑器。

```sh
# 原生资产检查、保留实例与阵列的源文件，导出单独预览副本
blender --background --python tools/export-home90.py

# 新增资源准备（保持原可玩关卡与默认构建列表）
"$UNITY_EDITOR" -batchmode -quit -nographics -projectPath "$PWD/Game" \
  -executeMethod CatSimulator.Editor.ModelResourceIntegration.Prepare -logFile /tmp/cat-import.log

# Mac 开发构建：原关卡 + 两个资源预览
UNITY_EDITOR="/path/to/Unity" bash tools/build-mac.sh

# 渲染运行检查：实际播放行走、验证位移/暂停/重置、加载住宅
Builds/Mac/CatSimulator.app/Contents/MacOS/Cat\ Simulator \
  --verify-model-resources --verification-output "$PWD/Source/Verification/ModelResources/Runtime"

# 仓库已有任务、移动、碰撞及重玩回归检查
Builds/Mac/CatSimulator.app/Contents/MacOS/Cat\ Simulator \
  --verify-playable --verification-output "$PWD/Source/Verification/ModelResources/Playable"
```

本次复测结果在 `Source/Verification/ModelResources`，旧 Windows 性能数据不代表本次 Mac 测试。

### 2026-10-14 本次结果

- Blender 5.2.2：84 个独立资产库可读取，89 个住宅原生 `.blend` 副本与桌面源文件一致；无缺失贴图。
- Unity 6000.3.26f1：1352 个住宅网格对象导入，新增资源 `.meta` 齐全，无重复 GUID；38 骨骼 Generic 动画的 241 姿态检查通过。
- Mac 通用开发构建（ARM64 + x86_64）：Succeeded，0 个构建错误。
- Apple M4 实际渲染运行：猫动画、位移、暂停、重置、住宅加载通过，运行错误 0。
- 现有关卡回归：移动、跳跃、爬架支撑、碰撞、两项任务、镜头与重玩通过；测试顺序为先水杯后窗帘，未改动原场景或玩家脚本。
- 第一次包初始化时 NUnit 依赖尚未完整导入，完成导入并重开后编译通过，仓库依赖版本保持原样。
- 初次预览发现高墙遮挡和色彩空间差异，已修正并重新构建、运行、检查截图。

完整结果与截图见 `Source/Verification/ModelResources/validation-summary.json`、`Runtime`、`Playable`。

### Windows 复核与原生猫贴图修复

PR #1 复核发现原生 `Abyssinian_Cat_WalkRig.blend` 的四张贴图引用了仓库外的旧目录。现改为仓库内 `Game/Assets/Cat/Source` 的相对路径，并将 basecolor、metallic、normal、roughness 四张贴图打包进 `.blend`。修复前后网格、对象变换、骨骼和蒙皮权重的 SHA-256 指纹一致，原 Unity FBX 和动画未变动。

Windows / Unity 6000.3.26f1 / RTX 3050 Laptop 复核：原生 84 类资产读取、38 骨骼的 241 姿态检查、1352 网格住宅加载，以及新猫实际动画、位移、暂停和重置均通过。Windows 构建成功；现有关卡的两种任务顺序、卧室通行与镜头、动态物品和软枕头四项回归通过，1080p 平均约 185–214 FPS。

截图复核通过临时离屏渲染验证器使用 Direct3D 11 完成。PR 自带的 `ScreenCapture` 检查在 Windows 隐藏窗口下无法截图，其他检查项正常，未将这次退出码 2 记为通过；现有关卡四项回归使用默认 Direct3D 12 并以退出码 0 完成。临时验证脚本和本机生成的 Unity 设置不随修复提交。
