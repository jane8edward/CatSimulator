# BlendSwap 猫与客厅调整（0.1.3）

使用用户下载的 `assets/BlendSwap18519/cat.blend`，来源为 [Rigged and animated Cat](https://blendswap.com/blend/18519)，作者 JonasDichelle。页面注明 CC-BY，未注明具体版本。署名随 Windows 程序分发，暂停菜单也可查看。

原始文件 SHA256：`FDA4B80F1A06E67A1CC04396B858085FAA5CD9742F28D9BDE9837AAEAB2727FA`。转换时禁用自动执行脚本，不覆盖下载文件。

## 资产与动作

- 原作 Walk：28 帧，24 FPS，循环时长 1.125 秒。
- 原作 run：15 帧，24 FPS，循环时长约 0.583 秒。
- 补充 Idle、Jump、Land、Paw、Scratch，与现有移动和互动系统衔接。
- 约 10.24 万三角形，包括身体、眼睛、5999 个短毛发片与面部胡须。
- 保留原作毛色贴图；眼睛的旧版 Generated 坐标材质烘焙成实际 UV 贴图。
- Blender 源文件：`Source/Blender/BlendSwap18519/Cat_Game.blend`。
- Unity 模型、材质、控制器和预制体：`Game/Assets/Art/BlendSwap18519`。

原作使用 Blender 粒子毛发与旧版骨骼约束。Unity 使用蒙皮毛发片，原作粒子动力学没有直接移植。游戏效果和原作 Cycles 渲染存在差异，当前版本仍属于资产与玩法迭代。

## 背部错位修复

导出时，控制动画但不参与 Blender 网格变形的骨骼曾被保留在有效蒙皮权重里。四权重裁剪也丢掉了部分肩背的有效影响。现在只保留 `use_deform` 骨骼，按有效权重重新归一化；身体、眼睛、毛发、胡须都经过相同检查。

导入使用最多八个骨骼权重，运行时使用原资产保存的权重数量。[Unity 自定义蒙皮导入说明](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ModelImporterSkinWeights.Custom.html)支持这一设置。

`verify_blendswap_deformation.py` 对照原模型全部 28 个走路帧和 15 个奔跑帧的实际表面。原模型在变形后细分，实时网格在静态细分后蒙皮，因此仍有细小表面差异；不能用骨骼位置吻合代替网格变形检查。原始结果保存在 `Source/Blender/BlendSwap18519/deformation-comparison.json`。游戏录制额外检查背部视角的八个步态阶段。

## 房间、比例与毛发

客厅宽、深增加 25%，由 6.8×5.8 米变为 8.5×7.25 米。家具保持原尺寸，沙发、茶几、猫爬架、纸箱、绿植、电视柜和窗帘的位置分别向外移动，相关互动点同步调整。活动边界和反射探针覆盖新空间。

猫比初次导入缩小 10%。身体胶囊和前后身体碰撞检查同步缩小，移动与动画速度重新匹配，拨杯站位按新猫的伸爪范围调整。

身上的细线来自毛发片与旧文件的长毛束。当前毛发最长约 5.6 毫米，胡须减细，尾部异常长线条移除，取消会放大远处零散毛丝的 mip 覆盖保留。面部胡须仍保留。

## 验证与复现

2026-10-10，本机 RTX 3050 Laptop GPU、Windows 0.1.3 实际渲染验证：

| 顺序 | 平均 FPS | P95 帧时间 | P99 帧时间 |
| --- | ---: | ---: | ---: |
| 水杯后窗帘 | 232.42 | 4.77 ms | 4.99 ms |
| 窗帘后水杯 | 189.03 | 8.23 ms | 10.89 ms |

性能测试分辨率 1920×1080。两种顺序均通过移动、跳跃落地、爬架支撑、杯子碰撞、必要目标、可选互动、重玩、镜头避障和家具阻挡检查，未记录运行时错误。77 个实体网格全部有碰撞，客厅共保留 82 个碰撞组件。茶几和沙发之间实际间距约 1.195 米。结果见 `Source/Verification/cup-first.json`、`curtain-first.json` 与对应 `*-room-regression.json`。

动画资产验证实际画面为 2560×1440，导出展示视频缩放为 1920×1080。七个动作均成功导入，游戏内走跑脚掌位移有效；背部八个阶段均已截图检查，未发现控制骨骼进入蒙皮。最终日志显示缺失骨骼引用为零。结果见 `Source/Verification/BlendSwap18519/cat-asset.json`、`cat-asset.log` 与 `cat-back-walk-*.png`。

1. `tools/install-blendswap-cat.ps1` 从原猫转换并安装到保存好的场景，随后构建。
2. `Source/Blender/expand_living_room.py` 从原 `first-level.blend` 生成扩大后的客厅；新源文件为 `spacious-living-room.blend`，不会覆盖原文件。
3. `tools/verify.ps1 -RecordPreview` 在实际渲染的 Windows 程序内复测移动、镜头、家具碰撞、两种必要目标顺序和重玩，并录制互动。
4. 程序参数 `--verify-cat --verification-output <目录>` 输出模型动画与背部步态截图。

源文件与场景备份见 `Source/Backups/before-blendswap-cat-*`、`Source/Backups/before-room-spacing-*`。普通构建只构建保存的场景，使用重新生成场景的菜单前应保存个人修改。

游戏内视频是实际程序画面，动画展示部分是原地播放，互动视频在互动之间使用验证用位置跳转。性能测试是取消帧率限制后的短时间镜头环绕测试；普通试玩设为 60 FPS，手机端、完整住宅与长期稳定性尚未验证。

录制时使用固定 30 FPS 的模拟帧步长，确保截图编码为 30 FPS 视频后，动作播放速度与游戏相同。此设置只在演示录制期间启用，不参与前面的性能测量。
