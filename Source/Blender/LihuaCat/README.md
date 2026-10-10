# 狸花猫独立模型

根据 `assets/photo/lihuacat` 的三张图片制作同一只灰棕色狸花猫。图片分别显示正面和左右侧面，模型采用便于后续绑定的四脚站立姿态。

## 文件

- `LihuaCat.blend`：可编辑的主源文件，包含连续身体网格、尾巴、耳朵、眼睛、鼻子、胡须、原生短毛曲线、基础变形骨架和摄影棚。三张参考图已打包进文件。
- `threequarter.png`、`front.png`、`left.png`、`right.png`、`face.png`：Blender Cycles 对真实模型渲染的预览，1600×1100。
- `GameExport/LihuaCat.fbx`：独立 FBX，包含网格、27 根骨骼、UV 和嵌入颜色贴图。
- `GameExport/Textures`：身体 2048×2048、尾巴 1024×1024，以及耳朵、眼睛和眼睑的 512×512 颜色贴图。
- `GameExport/LihuaCat_Game.blend`：导出时使用的 Blender 工作副本，降低了胡须细分，毛发曲线被隐藏。外部贴图使用相对路径，分享时保留整个目录。
- `model-report.json`、`GameExport/export-report.json`、`GameExport/reimport-report.json`：生成和检查结果。

## 编辑

使用 Blender 5.2.2 LTS 打开 `LihuaCat.blend`，当前场景为 `LihuaCat_ReferenceWorkshop`。

`LihuaCat_Model` 集合中是角色，`Studio_RenderOnly` 中是灯光、地面和相机，`Reference_Images` 中是参考图。参考集合默认在视口隐藏，可以在 Outliner 打开。身体和尾巴保留细分修改器；毛发为原生 Curves，可继续梳理。

单位为米，Z 向上，模型局部 +Y 朝前。骨架以站立姿态保存，脚掌位于摄影棚地面附近。

## 重建

以下脚本位于此目录的上一级，必须由 Blender 的 Python 执行：

1. `build_lihua_cat.py`：生成新源文件；在后台进程中使用空场景，不改写原来的客厅工程。
2. `refine_lihua_cat.py`：应用渲染检查后的耳朵、眼睑、尾巴和脚趾调整。
3. `render_lihua_cat.py -- all`：输出五个视角；也支持 `front`、`left`、`right`、`threequarter`、`face`。
4. `export_lihua_cat.py`：烘焙颜色并导出 FBX，不覆盖主源文件。
5. `verify_lihua_export.py`：在空场景重新导入 FBX，检查尺寸、UV、蒙皮、骨架和贴图。

重建会覆盖本目录中的生成文件。手动雕刻和改动后，请另存一个版本，再运行重建脚本。

## 本轮验证和边界

检查连续身体网格是否闭合，以及身体顶点是否都有骨骼权重；检查 FBX 是否能在空场景重新导入并找到全部颜色贴图。模型包未覆盖游戏里的 `ReferenceCat.fbx`，也没有改动 `LivingRoom.unity`。

当前是参考重建的第一轮资产，脸部形状、眼睑、耳内结构、纹理锐度和毛发仍需继续艺术细化，不能据此认定已经达到参考视频的最终画质标准。

原生 Curves 毛发用于 Blender 预览，FBX 不包含它们。游戏接入还需要制作毛发卡片或其他实时毛发方案、重新适配动画、改进权重并验证关节变形。现有骨架为基础骨架，毛发尚未完成随身体变形的绑定；没有完成行走动画、Unity 角色替换或手机性能验证。
