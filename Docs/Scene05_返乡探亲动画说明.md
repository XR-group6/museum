# scene_05｜1988年返乡探亲

打开 `Assets/Project3D/Scenes/scene_05.unity` 后进入 Play 即可播放。旧的三秒自动跳场景已停用，结尾保持自由探索。

- 0–7 秒：父亲面对供桌蜡烛之间的香炉祈愿；“航海顺利”附近复用 Scene01 的同一声轻叮。
- 7–10 秒：香炉“那我……”；10–23 秒：父亲伸手抚炉并告别。
- 23–28 秒：收手、弯腰提起脚边行李。
- 28–37 秒：父亲经前门离开，门随后合上，屋内留下钟表声。
- 37–39 秒：退出香炉视角；39 秒后显示双手、开放移动和证件交互。
- 证件展开阅读至少三秒，再主动选择“放回桌面”，才播放最后一句香炉旁白。未读关闭、阅读中、组件禁用或场景退出都不会触发。每次进入场景最多播放一次。

VR：左摇杆移动，右摇杆每次转向 15 度；右手激光与扳机选择证件，展开、转动及放回。
桌面预览：WASD 移动，按住鼠标右键观察，左键选择证件及按钮。证件窗口打开时暂停移动。

`Scene05_Homecoming` 上的 `Scene05HomecomingStory` 保存角色、道具、视角、音频与时间参数。动作保存在 `Assets/Scene05HomecomingAnimation/Scene05_Homecoming.playable` 中，可拖动 Timeline 预览；探索与证件回调在运行时执行。

配音为本次生成的合成语音，可直接替换五个独立音频槽：Prayer / Expectation / Leave / Home / Final Voice。字幕采用用户提供的台词。轻叮复用 `Assets/Scene01Animation/航海顺利_轻叮.wav`。钟表及关门声为合成音效。

现有 Father_Elder 和 censer 被复用。香炉保留在原先有蜡烛的供桌上，父亲在供桌前抚炉告别；证件和行程单仍在圆桌上并排放置。证件为明确标注的叙事重构道具。房屋网格按原始模型恢复至正确尺寸，保留面海墙体；固定前门被移除，改用位于原门洞、可旋转的门扇。探索双手为简化几何模型，VR 下跟随控制器。

验证记录：`Logs/Scene05Homecoming-validation.txt`、`Logs/Scene05Homecoming-play-validation.txt`。
预览：`Previews/Scene05Homecoming/`。已进行 Unity 编辑器采样及桌面 Play 模式检查；未进行实体 VR 头显验收。

