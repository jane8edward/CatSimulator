# 卧室扩建（0.1.4）

参考视频为 `assets/video/catSimulator02.mp4`（约 29.5 秒）。视频里的主要布局元素是木色双人床、白色床品、床头柜台灯、衣柜和侧墙窗户；本次以这些元素为基础，加入符合上班族主人设定的小书桌与收纳柜。

原客厅保持 8.5×7.25 米；卧室为 5.6×6.1 米，位于 Unity 场景的正 X 一侧（Blender 源文件的负 X 一侧）。门口净宽约 1.6 米，中心在 Unity 的 `(4.25, 0, 1.1)` 附近。床靠内墙，衣柜靠外侧墙，书桌和收纳柜位于床尾两角，中央通道保持开放。

模型在 `Source/Blender/living-room-bedroom.blend`，游戏仍使用 `Game/Assets/Art/LivingRoom.fbx`。扩建前源文件 `spacious-living-room.blend` 未覆盖，替换前的游戏场景、模型与配置备份在 `Source/Backups/before-bedroom-20261010`。

`extend_bedroom.py` 从扩建前客厅源文件生成卧室；`FirstPlayableBuilder.ApplyBedroomExpansion` 仅替换房间对象并添加卧室光照，保留现有猫咪、动画控制器、镜头及关卡目标。运行 `tools/expand-bedroom.ps1` 可重新建模和构建；使用 `-SkipModeling` 可只重新导入、构建。重建房间会覆盖手动修改的房间对象，操作前请保存与备份。

`tools/verify-bedroom.ps1` 使用实际猫咪移动检查门口双向通行和卧室中央通道，检查地面支撑、实体碰撞和动画资源，保存实机截图及短时性能报告。`tools/verify.ps1` 继续验证客厅原有镜头、家具碰撞、两种任务顺序和重玩。

实机截图：

- `Source/Verification/Bedroom/bedroom-cat-view.png`：正常游戏镜头。
- `Source/Verification/Bedroom/bedroom-wide.png`：卧室全景。
- `Source/Verification/Bedroom/bedroom-doorway.png`：连接客厅的门口。
- `Source/Verification/Bedroom/home-layout.png`：两房间俯视布局；仅截图时临时隐藏卧室顶板，游戏中保留顶板。

性能数据只代表本机 1920×1080 下的 8 秒卧室镜头环绕测试。普通试玩继续限制为 60 FPS；手机端尚未验证。

2026-10-10 最终验证结果：

- Windows 构建成功，版本 0.1.4。
- 卧室双向门口通行、中央通道、地面支撑与猫咪资源检查通过；200 个实体部件无碰撞遗漏，运行时无错误。
- 卧室平均约 205 FPS，P95 约 5.31 ms，P99 约 5.62 ms。
- 客厅两种通关顺序、重玩、镜头及家具碰撞检查全部通过，平均约 228–231 FPS，运行时无错误。
- 床品外沿到衣柜把手的最窄净距约 0.89 米，另一侧到墙面约 1.54 米；精确几何测量见 `layout.json`。

本次修改不自动暂存、提交或上传 Git；需用户明确要求。
