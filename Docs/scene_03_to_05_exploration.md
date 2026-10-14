# 场景 3 过场与场景 5 自由探索

2026-10-14，Unity 2022.3.62f3 / SteamVR / OpenVR。

场景 3 剧情导演播放到 30 秒、上下盖布只留下窄缝后，自动进入场景 4。
场景 4 → 5 → 6 → 7 各停留 3 秒，再回到场景 5 开始自由探索。
停留计时使用真实时间，从新场景第一帧之后开始，加载耗时另计。
第一次经过场景 5 时移动、转向均关闭；最后返回时开启并停止自动跳转。
直接在 Unity 中运行场景 5，也可以测试自由探索。

场景位置：`Assets/Project3D/Scenes/scene_04.unity` 和 `scene_05.unity`，均已启用在 Build Settings 中。
复用场景 5 原有 `[CameraRig]`、Camera、左右 SteamVR Pose，没有增加重复头显或手柄。

选择场景 5 的 `[CameraRig]`，在 Inspector 中调整：

- `MuseumLeftStickMove / Speed`：移动米/秒，默认 1.2。
- `MuseumRightStickTurn / Degrees Per Second`：转向度/秒，默认 60。
- 两个组件的 `Deadzone`：默认 0.2。
- `Gravity`：默认 9.81；`Fall Reset Distance`：低于出生点 5 米时复位。
- CharacterController：Radius 0.2、Skin Width 0.02、Min Move Distance 0。
  实际楼梯踏步高度包含约 0.17～0.223 米，因此 Step Offset 使用 0.243。
  胶囊高度按头显高度限制在 0.6～2.2 米，水平中心跟随头显偏移。

左手读取 platformer/Move 的 LeftHand；右手读取同一 Action 的 RightHand X 轴。
未初始化 SteamVR 输入、未绑定或对应手柄追踪无效时，不产生摇杆移动或转向。
右手连续转向先更新，围绕头显世界位置转动整个 Rig；旋转后恢复 CharacterController 状态。
原 default 输入、扳机和抓取绑定保留；Touch、Index、Vive、Cosmos、WMR 已有左右二维输入绑定。
Vive 的二维输入来自触摸板；不具备左右二维输入的设备不能用此方式探索。

场景 5 增加 111 个环境网格碰撞体和 4 个外边界碰撞体，保留已有触发器。
海面不添加可行走碰撞；重力、楼梯踏步和低处复位用于处理高度变化。
未改动模型、材质、贴图或 XR 参考项目。

验证结果保存在 `Docs/MuseumExplorationValidation.txt` 和 `Docs/MuseumTourPlayValidation.txt`。
Unity 编译、场景无缺失脚本、方向/死区/斜向限速、30/60 Hz 平滑转向、半幅转速、松手停止、
头显偏移支点、转向后朝向、真实 CharacterController 位移/墙体阻挡/重力落地均通过。
无头显 Play Mode 实际验证了完整场景加载、3 秒停留、探索启用条件及最终场景不再跳转。
该测试在临时副本中禁用了硬件追踪，场景 3 从 0 秒正常自动播放，没有手动跳到结束时间。
结束判定容许 Hold 模式的最后一帧略小于 30 秒，并明确采样完整盖布帧后再加载场景 4；
不代表真实头显或手柄测试通过。实际操作、楼梯通行、视觉效果与原右手交互仍需真机验收。

重配配置与基础验证菜单：`Museum / Configure tour and scene five exploration`。
`Tools/MuseumTourPlayVerify.cs` 为集成测试源，只应放进临时验证副本，
其中会关闭副本的硬件追踪组件，不应作为正式项目运行时脚本导入。
