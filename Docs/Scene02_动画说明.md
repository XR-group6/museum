# Scene02｜第一天上班

已接入 Assets/Project3D/Scenes/scene_04_01.unity 中的1960年代轮盘，使用已导入的 Mother_Elder_02、Father_Elder_02、Daughter_Adult_02。

选中 TimeLine 打开 Timeline，找到「Scene02 · 第一天上班 / 回头告别 / 父亲点头」。本段时间为约37.233–55.233秒，共18秒。原有轮转在第一段转场完成处插入18秒停留，后续关键帧及激活片段顺延。

## 本段相对时序

- 0–2秒：成年女儿在左后门口回身看父母。
- 2–6.3秒：女儿台词「爸、妈，我上班去了。」；双腿保持站立。
- 6.4–8.8秒：父亲面向女儿，点一次头，台词「路上小心。」
- 9–13秒：香炉画外台词「我还在等。」
- 10.8–16秒：女儿转回门口并走出；父母留在门边相送。
- 母亲站在圆桌右侧，先看女儿，父亲开口时看向父亲，双手自然垂放。

上方显示用户提供的「约1960年｜白天」历史字幕。

## 香炉第一视角

Scene02_Burner 位于圆桌上。Scene01CenserView 在进入本段时平滑移动到该香炉视点并转向父女；本段内视点位置固定，保留头显自由转头及实际头部位移；结束后衔接原轮盘中心视点。自身香炉不渲染，避免从内部看见炉壁。

Scene02_FirstWorkDay 下包含人物、年代字幕和轻微补光，随场景父节点，并由 Activation Track 控制本段显示。动作是绝对时间采样，初始姿态已序列化；来回拖动时间轴不会累积转头角度或把离场位置保存为新的起点。

## 配音与检查

Scene02Story 在 TimeLine 上预留 daughterVoice、fatherVoice、burnerVoice 槽位。目前未找到本段对应配音，已接入台词字幕，待正式录音导入后可绑定。

第一视角预览：Previews/Scene02/FirstPerson_Goodbye.png。
检查数据：Logs/Scene02-final-validation.txt（对话期间双腿稳定、视点误差、离场位移、回拖复位、父亲单次点头）。
生成前场景备份：Logs/scene_04_01_before_scene02.unity。
