# New_House 物理碰撞与门窗互动

> 0.3.3 已将本住宅替换为第二关：从「开始游戏 → 第二关 · 温馨大宅」进入。本机启动 `开始游戏.cmd`。当前布局、比例及验收入口见 [0.3.3 更新说明](second-level-033.md)；下文保留初次接入时的制作记录。


## 体验入口

打开桌面 `猫咪游戏/猫咪模拟器-场景选择版.app`，选择猫咪，进入「开始游戏 → New_House · 养猫之家」。

- 靠近门窗，在屏幕下方出现提示后按 **E** 开启或关闭。
- 可操作：主卧门、书房门、厨房移门、卫生间移门、阳台移门，以及主卧、书房、厨房的窗户。
- 客厅猫咪活动区地面的亚麻球可以用身体和前爪碰撞推动，会滚动、与家具碰撞并逐渐停下。
- **WASD** 移动，**Shift** 奔跑，**空格** 跳跃，**Esc / F4** 菜单，**V** 全屋查看。暂停与全屋查看会冻结物理和门窗运动。

## 实现范围

`NewHouseDoor` 复用现有猫咪互动入口。每个门扇由一个运动学刚体驱动，沿预设铰链或滑轨开合，使用首尾速度和加速度均为零的缓动；每帧从固定参考位置计算姿态。门框、墙体和轨道保持静态。

关闭遇到猫咪或动态物品会返回打开方向；打开遇阻会停下，之后可再次按 E 尝试。运动中忽略重复请求，避免叠加动画。玻璃与对应把手同步运动，重叠玻璃置于独立轨道。卧室门框与门扇的边界使用三角面几何裁切，保留原材质和 UV，清除旧拆分留下的锯齿碎片。

亚麻球使用球形碰撞体、连续碰撞检测、插值与独立接触材质，复用已有 `PushableProp` 的接触冲量。球体移动到猫窝前方的空地；带底座的抓挠球仍是固定家具。物理异常或离开地面支撑范围时，亚麻球会恢复到初始位置。

本次新增逻辑仅接入 New_House。共用 `CatController` 仅补充猫爪最近点查询的类型检查：静态非凸网格不执行不支持的查询。原有移动方式、`CatCamera`、`PushableProp`、猫咪选择和旧关卡保持原有行为。

## 工程与验证

Unity 工程：`桌面/猫咪游戏/CatSimulator-NewHouse-Selection/Game`。

- 场景：`Assets/Scenes/New_House_Preview.unity`
- 场景整理入口：`CatSimulator.Editor.NewHouseInteractionIntegration.PrepareAndBuild`
- 单独渲染运行检查：`--check-new-house-interactions --verification-output <绝对路径>`
- 三只猫操控与连续全屋通行检查：`--check-new-house-controls`
- 场景选择、猫咪选择、旧关卡检查：`--check-new-house-selection`
- 新互动检查结果：`Source/Verification/NewHouseInteractions/Runtime/validation-summary.json`
- 门窗运动采样：同目录 `door-motion.csv`

修改前的场景与本次首次检查结果保存在本机 `.tools/new-house-interaction-backup`。原始 FBX 和 Blender 源文件保留。

## 本机互动验证结果

Apple M4 渲染运行：三只猫均成功接触并推滚球，球体转动后稳定停下；五处门和三处窗完成反复开关、重复请求保护、轨迹与端点稳定性检查。五处门均完成猫咪及动态物品挡门保护、关闭阻挡、打开通行测试；三个窗户均通过实际互动入口开关。暂停、全屋查看、重新加载场景和返回菜单检查通过，运行错误为 0。门窗端点位置误差为 0，静止观察未出现漂移。

最终构建与复测汇总：`Source/Verification/NewHouseInteractions/release-checks.json`。构建错误为 0；互动、三猫操控与 8 区连续通行、场景及猫咪选择三组检查均通过。猫爪接触复杂门框时的非凸网格最近点查询警告已消除。
