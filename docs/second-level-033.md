# 0.3.3：新版住宅替换第二关

以下为已上传版本的历史记录。后续本地开发现已加入 [第二关五项任务与家具机器人](house-tasks.md)，尚未发布。

## 关卡与布局

- 开始菜单只有第一关「客厅与卧室」和第二关「温馨大宅」。第二关自由探索，不设任务。
- `ContentPreviewMenu.LoadLevel(2)` 进入 `New_House_Preview`，关卡 ID 为 `home-02`；场景文件名保留，避免破坏材质、网格及遮挡资源引用。
- 成品只包含 `StartMenu`、`LivingRoom`、`New_House_Preview` 三个场景。旧 Home90 及模型预览不再随成品发布，历史源资源仍可追溯。
- 大沙发整体向阳台侧移动约 1.03 米、向背墙移动约 0.31 米。与两面墙各留 0.10 米，抱枕、软接触网格及固定支撑同步移动，重烘焙遮挡剔除数据。
- 保留上一轮住宅扩大 20%、猫咪比例 0.675、34 个刚体小物件、19 处软表面及家具修正。

## 启动与构建

Unity 6000.3.26f1。克隆后执行 `git lfs pull`，打开 `Game/Assets/Scenes/StartMenu.unity` 运行。Windows 构建入口为 `CatSimulator.Editor.FirstPlayableBuilder.Build`，输出 `Builds/Windows/CatSimulator.exe`；本机可双击根目录 `开始游戏.cmd`。Windows 默认 Direct3D 11，以避免本机 Direct3D 12 退出时的驱动崩溃。

`FirstPlayableBuilder.Build` 使用已保存的关卡，不会因旧住宅资源缺失而重新生成旧场景或覆盖三只猫的目录。

启动预热改为低分辨率离屏渲染当前住宅的实际视角，完成后开放操控。复测定位到 `Shader.WarmupAllShaders` 在本机偶发 Unity 原生崩溃，因此不再调用该全局接口。游戏画面的分辨率和画质设置保持原样。

## 实机检查入口

- `--check-new-house-upgrade --verification-output <绝对目录>`：两关菜单、第二关加载新版住宅、旧住宅不在成品、沙发墙距、跳上跳下沙发、八区域连续通行、三只猫、门窗、摆件与软表面、1920×1080 渲染性能。
- `--check-new-house-selection --verification-output <绝对目录>`：三只猫的展示、选择、两关进入和返回。
- `--verify-game-flow --verification-output <绝对目录>`：从开始菜单选择三只猫，完成第一关两项任务，再进入第二关、移动跳跃及重开。
- `--verify-bedroom-physics --verification-output <绝对目录>`：第一关卧室物理回归。
- `--verify-cat-polish --verification-output <绝对目录>`：有限质量接触、能量检查及三花猫尾巴姿态；实际摆件取自新版第二关。

本轮修改前的完整本机备份保存在 `Source/Backups/before-level-two-20261016-174729`，包含修改中的源文件和 0.3.2 Windows 成品；备份与构建均不入库。

## 本次验收结果

两关菜单、第二关替换、沙发墙距及跳跃、八区域连续通行、三只猫选择与任务流程、卧室物理及有限质量接触检查均通过。住宅 54 项检查的功能项均通过，性能项有未达标项，因此保留原报告 `passed=false` 与退出码 1。

1920×1080 下，住宅最终测试视角中位帧率 79.5 FPS、5% 低帧率 72.9 FPS（RTX 3050 Laptop GPU，测试上限 90 FPS）。主卧床及睡枕视角分别约 43.8、53.6 FPS，低于 60 FPS，尚需后续优化，不能视为全场景性能验收通过。测试数据与两张截图见 `Source/Verification/SecondLevel033`。
