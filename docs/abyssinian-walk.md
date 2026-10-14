# 阿比尼西亚猫行走动画

## 打开与播放

使用 Unity 6000.3.26f1 打开仓库的 `Game` 文件夹，在 Project 中双击 `Assets/Cat/Scenes/Walk_Preview.unity`，点击编辑器顶部 Play。

预览提供暂停、向前行走／原地切换、重置和速度滑块。默认步速为 0.2333 米／秒，摄影机跟随猫咪。预览场景每前进 7 米回到起点，便于持续观察；这一设置只用于预览。按正常速度行走时，每周期前进 0.28 米。

## 可复用文件

| 文件 | 用途 |
| --- | --- |
| `Assets/Cat/Animations/Abyssinian_Walk.anim` | 1.2 秒的循环行走片段，标称 30 FPS，曲线以更密的采样烘焙 |
| `Assets/Cat/Animations/Abyssinian_Walk.controller` | 默认播放 Walk 的 Animator Controller |
| `Assets/Cat/Prefabs/Abyssinian_Walking_Cat.prefab` | 已绑定材质、动画和预览移动脚本的猫咪 |
| `Assets/Cat/Source/Abyssinian_Cat_WalkRig.fbx` | 修复蒙皮权重后的模型，保留 38 根原有骨骼 |
| `Source/Blender/Abyssinian/Abyssinian_Cat_WalkRig.blend` | 权重修复工作文件 |
| `Source/Preview/Abyssinian/阿比尼西亚猫_行走预览.mp4` | Unity 场景渲染的 4.8 秒预览，960×720、30 FPS、4 个行走周期 |

原文件 `tripo_convert_45f28305-4a37-4a3f-a576-12a91ddb791f.fbx` 和贴图保存在 `Assets/Cat/Source/`；桌面原始“阿比尼西亚猫-拓扑-绑定”文件夹保留原样。

## 动作与绑定

- 落脚顺序：左后 → 左前 → 右后 → 右前，间隔 0.3 秒；支撑期约占每条腿周期的 65%。
- 四肢使用双段 IK 求解后烘焙；摆腿弧线低，最高抬爪约 3 厘米。骨段长度保持不变。
- 身体小幅起伏、侧摆，头部保持稳定，尾巴带有轻微延迟摆动。
- 原 FBX 的权重覆盖不完整，部分后肢、头部和尾巴无法正常变形。动画副本通过 Blender 骨骼热权重重新蒙皮、归一化，并限制每顶点最多 4 个影响骨骼；无未绑定顶点。
- 使用 Generic 四足骨架。片段为原地循环，实际向前位移由 `CatWalkPlayback` 按步幅匹配；`Apply Root Motion` 关闭。
- 接入自己的角色控制器时，可以移除预览脚本，保留 Animator 和动画片段。移动速度应与动画速度同比调整。

现有骨架没有独立肩胛骨，肩部滑动用前肢根部的小幅移动近似。该动画用于游戏模型预览，没有肌肉动力学、转向、坡面脚掌修正或完整面部动画。模型外形和材质沿用提供的模型。

## 检查记录

`Assets/Cat/Reports/Walk_Checks.json` 保存了 241 个姿态的检查结果：循环首尾位置与旋转连续、骨段长度稳定、四个爪子的蒙皮网格均有运动。支撑阶段爪骨的最大速度误差约为 0.0003 米／秒；网格最低点距地面约 -0.000305 米（约 0.3 毫米）。这些数值描述平地预览，不能代替复杂游戏场景中的地面适配。

`Assets/Cat/Reports/Skinning_Repair.json` 是 Blender 权重修复记录；`Rig_Mapping.json` 是 Unity 实际导入的骨骼映射。Unity 导入后为 16298 个渲染顶点，原 Blender 网格为 8378 个顶点；UV 和法线边界在导入时会拆分渲染顶点。

项目的 `Source/Verification/Abyssinian/Runtime_Verification_historical.json` 记录实时播放、暂停和控制台检查。动画与素材生成菜单位于 Unity 的 `Cat` 菜单下；重新生成会覆盖本项目的动画资产和预览场景。

中文预览字体使用 Noto Sans SC，授权见 `FONT_LICENSE.txt`。

仓库版本与已有 URP 关卡共存，材质已转换为 URP/Lit。历史验证记录来自桌面原工程；本次仓库集成的构建与运行结果位于 `Source/Verification/ModelResources`。重新生成预览会修改构建场景列表；仅在预览工程中使用 `Cat/Build Walking Animation`，共享关卡日常构建使用仓库构建工具。
