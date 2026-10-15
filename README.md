# 猫咪模拟器

首关开发原型：在相连的暖色客厅与卧室中扮演青年猫，自由探索、爬猫爬架、钻纸箱、打翻水杯和抓坏窗帘。两项破坏任务都完成即可通关，顺序自由；猫爬架与纸箱增加探索评分。

## 工程与启动

- Unity 工程：`Game`，编辑器版本 **6000.3.26f1**，URP **17.3.0**。
- Windows 程序：已生成 `Builds/Windows/CatSimulator.exe`。请保留同目录的 Data 文件夹和 DLL。
- 双击根目录的 `开始游戏.cmd` 即可试玩，也可使用 `tools/play.ps1`。
- 游戏内演示：`Source/Preview/first-playable.mp4`，1920×1080、30 FPS；展示自动验证流程中的猫爬架、纸箱、水杯、窗帘和通关界面，互动间使用测试用位置跳转。
- 构建脚本：`tools/build.ps1`；也可以在 Unity 中执行 **Cat Simulator → Create First Playable**，打开 `Assets/Scenes/LivingRoom.unity` 后按 Play。
- Blender 源文件：当前客厅与卧室为 `Source/Blender/living-room-bedroom.blend`，扩建前客厅为 `Source/Blender/spacious-living-room.blend`，新猫为 `Source/Blender/BlendSwap18519/Cat_Game.blend`；FBX 位于 `Game/Assets/Art`。原有文件与场景备份位于 `Source/Backups`。

普通构建脚本直接构建保存好的 `LivingRoom.unity`。**Create First Playable** 会重新生成场景和原型配置，使用该菜单前请保存手动修改。此次更换猫与调整客厅前均已备份相关文件至 `Source/Backups`。

## 从 Git 仓库开始开发

仓库为 `https://github.com/jane8edward/CatSimulator`。公开后任何人均可克隆；需要直接推送分支的协作者仍需由仓库所有者在 GitHub 的 **Settings → Collaborators** 中邀请，并接受邀请。

安装 Git（包含 Git LFS）后，在希望存放工程的位置执行：

```powershell
git lfs install
git clone https://github.com/jane8edward/CatSimulator.git
cd CatSimulator
git lfs pull
```

在 Unity Hub 中添加克隆目录下的 **Game** 文件夹，使用 **6000.3.26f1** 打开。等待依赖与资源导入完成后，打开 `Assets/Scenes/LivingRoom.unity` 并按 Play。克隆时需要下载 LFS 文件；如果 FBX、贴图或 Blender 文件只是几行文字，请重新执行 `git lfs pull`。

仓库包含 Unity 工程、Blender 源文件、参考素材、演示视频和验证报告；本地缓存、备份、录制帧序列及 `Builds` 不入库。新克隆需先运行 `tools/build.ps1` 构建，再使用 `开始游戏.cmd`；通过 `-EditorPath` 指定本机 Unity 编辑器的完整路径，例如：

```powershell
.\tools\build.ps1 -EditorPath "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe"
```

请按实际安装位置调整路径，并在批量构建前保存、关闭占用同一工程的 Unity 编辑器。

协作时各自使用功能分支，提交 Unity 资源时同时提交对应 `.meta` 文件；同一场景或同一 Blender 文件尽量由一人修改，再通过拉取请求合并。

## 操作

| 操作 | 按键 |
| --- | --- |
| 移动 | WASD |
| 转动镜头、调整距离 | 鼠标、滚轮 |
| 跳跃 | 空格 |
| 奔跑 | 左 Shift |
| 附近物体互动 | E；可互动物体显示黄色轮廓 |
| 暂停、改名、重玩、退出 | Esc |

重新开始会恢复水杯、窗帘和任务状态，猫名保留。通关后可以继续探索。

## 卧室物理物件（0.1.5）

书桌扩大到 **2.20×0.88 米**，新增桌垫、鼠标、机箱、纸巾、键盘按键、书本和笔筒。卧室共有 **14 组独立刚体物件**，包括床头台灯、柜上绿植和手机，具有质量、重力、摩擦与碰撞，可以推动、倾倒或落下；较重机箱更难推动。可先跳上椅子，再跳到书桌。

猫踩到枕头时会产生局部下陷，离开后回弹，支撑碰撞同步变形。床头手机通过充电线连接墙面插座，手机移动时线也会跟随。重新进入关卡恢复这些物件。实现范围、重建方式和验证记录见 `docs/bedroom-physics.md`。

可先跳上床尾凳，再跳到床与枕头上。0.1.5 的物理验证通过；本机 RTX 3050 Laptop GPU 在 1080p、4×MSAA、猫踩枕头的 8 秒渲染测试中平均约 **192 FPS**，普通试玩限制为 60 FPS。手机端尚未验证。

## 内容与目标

客厅由 Blender 脚本自制。当前主角使用 JonasDichelle 的 [Rigged and animated Cat](https://blendswap.com/blend/18519)，保留原作的网格、毛色、骨骼和 Walk / run 动画，转换实时毛发并补充游戏动作。署名见 `THIRD_PARTY_NOTICES.md` 与游戏暂停菜单。参考照片、视频与下载的原始猫文件位于 `assets`，原文件不修改。项目使用 Unity 和 URP 官方依赖；Windows 原型运行时调用本机系统字体，未复制或分发字体文件。

首关与后续关卡共享住宅空间的设计方向不变。`LevelDefinition` 保存本关活动范围、出生点、必要目标和固定光照配置。完整住宅、多猫协作、主人行为和手机操作属于后续内容。

当前是第一轮玩法与资产原型。最终画质目标仍为参考视频；毛发、猫的解剖细节、脚掌接触与动作重心、液体表现、布料破坏和室内间接光需要继续迭代，不能把原型构建通过视为视觉验收通过。手机端尚未构建或验证。

## 卧室扩建记录（0.1.4）

参考 `assets/video/catSimulator02.mp4` 的床、床头柜台灯、衣柜与侧窗布局，在原客厅的右侧扩建 **5.6×6.1 米卧室**，通过 **1.6 米宽门口** 连通。卧室有双人床、两侧床头柜、衣柜、书桌、收纳柜、床尾凳、地毯和猫窝，床品包含建模褶皱，窗边补光与床头灯提供温暖的室内光照。

猫咪可直接走进卧室，客厅原有任务、互动和猫咪动画保留。家具与墙体带碰撞，活动范围扩展至两个房间。开发只在本地进行；只有用户明确要求提交或上传 Git 时才执行对应操作。

扩建脚本为 `tools/expand-bedroom.ps1`，实机检查为 `tools/verify-bedroom.ps1`；布局、实机截图与报告在 `Source/Verification/Bedroom`，说明见 `docs/bedroom-expansion.md`。`Source/Preview` 中现有两个演示视频仍展示 0.1.3，尚不包含卧室。

0.1.4 在本机 RTX 3050 Laptop GPU 的 1920×1080 短时取消限帧测试中，卧室平均约 **205 FPS**，客厅两种任务顺序约 **228–231 FPS**。两房间通行、200 个实体部件的碰撞覆盖及原有客厅任务、镜头与重玩检查通过。普通试玩仍限制为 60 FPS。

## 猫咪与客厅更新（0.1.3）

已接入 BlendSwap 猫和原作走跑动画，修正控制骨骼误参与蒙皮的问题，保留最多八个有效骨骼权重，减少肩背变形偏差。当前猫比初次导入再小 **10%**，身体碰撞与动作速度同步调整。

客厅地面从 **6.8×5.8 米** 增至 **8.5×7.25 米**，家具保持原尺寸并拉开位置，互动点同步移动。毛发最长约 **5.6 毫米**，去掉尾部异常长线条，面部胡须变细。原作毛发经过实时转换，游戏内表现仍需进一步打磨。

模型转换与验证方法见 `docs/blendswap-cat-integration.md`。动画展示为 `Source/Preview/cat-animation-showcase.mp4`；游戏内互动视频为 `Source/Preview/first-playable.mp4`。手机端尚未验证。

0.1.3 在本机 RTX 3050 Laptop GPU 的 1920×1080、8 秒取消限帧环绕测试中，最终两种任务顺序平均约 **189–232 FPS**，P95 约 **4.77–8.23 ms**。移动、跳跃、家具碰撞、镜头和两种通关顺序通过；77 个实体网格均有碰撞，茶几与沙发间距约 **1.195 米**。普通试玩仍设为 60 FPS，完整住宅与长期稳定性尚未验证。

## 保留的视角与碰撞修复（0.1.1）

移动方向由玩家控制的镜头方向决定，自动避障转向不再反过来改变 WASD 的方向；避障角度保持片刻并平滑恢复，镜头距离也平滑恢复。

重新摆放桌子和沙发，实际模型间距约 **0.715 米**。客厅实体网格按部件添加碰撞，包括沙发靠背、扶手、软垫、桌腿、电视、电视柜、花盆及猫爬架；纸箱内部、桌下等空间保持开放。地板木板由整体地面碰撞覆盖，水体和高亮描边不作为实体阻挡。猫另有头部与臀部的身体碰撞检查，减少单个竖向控制胶囊没有覆盖的穿模。

2026-10-10 在本机 RTX 3050 Laptop GPU 上完成两种任务顺序的实际渲染验证。桌底、墙边、沙发旁、纸箱旁的持续前进、77 个实体部件的碰撞覆盖、10 项家具阻挡、任务旁的镜头距离与重玩均通过，运行时未记录错误。实际截图为 **1920×1080**。

| 顺序 | 平均 FPS | P95 帧时间 | P99 帧时间 |
| --- | ---: | ---: | ---: |
| 先水杯后窗帘 | 305.44 | 3.76 ms | 3.96 ms |
| 先窗帘后水杯 | 305.78 | 3.76 ms | 4.04 ms |

以上是当前原型在取消帧率限制后的 **8 秒镜头环绕测试**，录制发生在性能测试之后；普通试玩设为 60 FPS。这不是最终画质、完整住宅、手机性能或长时间稳定性的验收结果。

通过 `tools/verify.ps1` 可复测；添加 `-RecordPreview` 还会保存游戏内画面序列。完整结果与方法见 `docs/first-playable-validation.md`、`docs/camera-collision-fix.md`，原始报告、日志及截图位于 `Source/Verification`。

## 新增美术资源（2026-10-14）

90㎡养猫住宅、84 类独立原生资产、物体参考图、猫四视图、阿比尼西亚猫蒙皮修复与循环行走动画已作为独立资源接入。打开方式与 Mac 构建/复测见 [资源接入说明](docs/model-resources-20261014.md)。现有 `LivingRoom.unity` 的玩法和玩家保持原样，另有 `Home90_Preview.unity` 与 `Assets/Cat/Scenes/Walk_Preview.unity` 供检查。

## New_House 与新增猫资源（2026-10-15）

最新 Tripo 全屋源工程、78 份原始模型、79 个校准资产、新猫模型及改进三花四视图已单独整理到仓库。New_House 保留实例、阵列、基础灯光和四视角 Cycles 渲染；目录、排除规则与验证方式见 [本次资源说明](docs/new-house-resources-20261015.md)。该高密度美术源工程独立于旧住宅和现有 Unity 可玩关卡。
