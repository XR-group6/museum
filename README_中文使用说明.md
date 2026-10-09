# 香炉记忆 XR 角色与动画原型 V2

这套文件用于小组作业中的人物原型。依据你提供的《The Incense Burner Remembers》双语故事流程，制作四个父女造型、骨骼和 17 种共享基础动作。造型参考截图中的低多边形面片和接近正常人体的比例，V2 按年代区分棉布短衫、偏襟童装、衬衫配针织背心、翻领内搭配开衫，并采用宽直裤和低帮布鞋；它是原创程序化原型，五官、衣褶及动作细腻程度仍低于截图中的商业素材。

先做一件事：把角色导入 Unity，打开演示场景并播放上香动作。成功后，再放进你们已有的候船屋或博物馆项目。


## V2 替换方法

如果你已经导入 V1，不需要重建整个 Unity 项目。退出 Play，先备份项目，再将本包 `Assets/IncenseCharacters` 复制到原来的同名目录，覆盖源 JSON 和脚本；保留旧 Generated 文件夹。然后重新运行 **Tools → Incense Characters → 1 Build Characters**，会生成新的 Generated 1 或后续编号。打开新演示场景，确认肩线、短发和服装后，把原场景中的旧人物替换为新版 Prefab，再设置原位置、旋转和剧情引用。只替换 GLB 不会更新由 JSON 生成的 Unity 角色。

每个角色仍有 52 骨骼和 17 动作，动作名称没有改变；肩关节位置改变了，因此不要把 V1 网格或动作单独混进 V2。

- `Previews/Revision_Comparison.png`：同一渲染方式下的新旧肩膀与发型对照。
- `Previews/Adult_Daughter_Views.png`：成年女儿正面、侧面、背面。
- `V2_修改与服装依据.md`：本次修改及服装参考的适用范围。

## 1 文件里有什么

| 文件或文件夹 | 作用 |
|---|---|
| Assets/IncenseCharacters | Unity 导入所需的全部内容，复制这一整个文件夹 |
| GLB | 四个带蒙皮、骨骼、动画的 .glb 模型，可导入 Blender 编辑 |
| Previews/Cast_Preview.png | 实际网格渲染的四角色预览 |
| Previews/Action_Preview.png | 12 个故事动作的关键姿态 |
| Previews/Animation_Preview.gif | 实际模型的上香、原地行走、招呼动作预览 |
| Tools/build_characters.py | 可编辑的模型和动作生成源代码 |
| Tools/export_fbx_in_blender.py | 可选的 Blender 转 FBX 脚本，需要你本地安装 Blender |
| asset_stats.json | 每个角色的面数、骨骼数、身高、动作数 |
| validation_report.json | 对模型与动画数据进行检查的结果 |

没有预先生成的 FBX、.blend 或 .unitypackage。GLB 是实际模型，不是概念图；Unity 路线用随附的编辑器脚本直接生成 Mesh、Prefab、Animator Controller 和 .anim，不要求安装 GLB 插件，也不用执行 Python。

## 2 四个角色怎么分配

年龄与身高是制作时的暂定值，原文没有规定精确数字。父亲在 40 年代末为成年、1988 年已老年；女儿从儿童成长为中年成人。故事中的几十年经过可用烟雾转场切换模型。

| 角色文件名 | 暂定身高 | 造型及使用场景 |
|---|---:|---|
| Father_Young | 1.76 m | 黑发、蓝灰小立领棉布短衫；场景 2、3、4，以及 5–6 的早期片段 |
| Father_Elder | 1.72 m | 灰发、米色衬衫与灰绿针织背心、轻微前倾；场景 5–6 后期、场景 7 的 1988 年告别 |
| Daughter_Child | 1.13 m | 较大头身比、短辫、小立领偏襟暗红棉衫；场景 2、3、4 |
| Daughter_Adult | 1.63 m | 侧分耳下短发、奶油色翻领内搭与灰蓝开衫；场景 5–6 的家庭生活 |

场景 0 和 10 的工作人员属于现实馆方人员，本包没有将其做成故事角色。场景 1、8、9 不额外加入父女。体验者也没有被替换成第三人称角色。V2 中成年女儿使用独立发型和分层服装结构，不再复用父亲的短发轮廓与上衣细节。

## 3 第一次导入 Unity

以下脚本以 Unity 2022.3 LTS 和 Unity 6 常用 API 编写，采用 Built-in/URP 可识别的 Lit 材质。当前制作环境没有 Unity 编辑器，尚未做 Unity 编译或真机验证，请按以下步骤先完成本机导入检查。已有项目建议先复制一份备份。

1. 解压 ZIP。你会看到 Incense_Characters 文件夹。
2. 打开你们正在使用的 Unity 项目。
3. 找到窗口下方的 Project 面板。这是项目文件区；如果没有，使用 Window → General → Project 打开。
4. 在 Project 里点 Assets。将压缩包内 Assets 文件夹中的 **IncenseCharacters** 整个文件夹拖进去。最终路径应为 `Assets/IncenseCharacters`，避免变成 `Assets/Assets/IncenseCharacters`。
5. 等待右下角编译进度结束。导入脚本不需要挂在人物上。
6. 在 Unity 顶部菜单选择 **Tools → Incense Characters → 1 Build Characters**。
7. 生成完成后，Assets/IncenseCharacters 下会出现 Generated 文件夹。每个角色都有自己的 Prefab、Mesh、材质、GenericAvatar、Character.controller 和 Animations 文件夹。
8. 再选择 **Tools → Incense Characters → 2 Open Demo Scene**。如提示保存你当前的场景，请先保存。
9. 点击 Unity 顶部三角形 **Play**。切换到 Game 窗口后，左侧会出现角色名和动作按钮。
10. 选择 Father_Young，再点击 Pray。你应该看到父亲将双手移到胸前并低头。点击 Idle 回到站姿。

重复 Build 会创建新的 Generated 1、Generated 2 等文件夹，避免覆盖你修改过的旧角色。确认新一版无误后，可以在 Unity 中自行清理不需要的旧生成文件夹。

**演示场景是桌面检查场景。** 放入正式 XR 项目时只拖人物 Prefab，不要把演示相机、AudioListener 或桌面按钮面板一起复制进去。保留团队原有的 XR Origin 和头显相机。

## 4 把人物放进你们的房间

1. 打开你们的目标场景。
2. 在 Project 中找到 Generated/Father_Young/Father_Young.prefab。
3. 将它拖到左侧 Hierarchy。Hierarchy 是当前场景中的物体列表，窗口入口为 Window → General → Hierarchy。
4. 选中角色。在右侧 Inspector 的 Transform 中设置 Position。先用 X=0、Y=0、Z=0，再移动到桌前。
5. Scale 保持 1、1、1。模型使用米制；脚底约在 Y=0。若地板是 Y=2，角色的 Y 也应约为 2。
6. 按 W 开启移动工具，按 E 开启旋转工具。在 Scene 窗口选中人物后按 F 可聚焦。
7. 模型正面朝本地 +Z，使用 Rotation 的 Y 调整朝向。它应面对香炉，而不一定面对场景主相机。
8. 点击 Play，选中 Hierarchy 中的人物。在 Inspector 的 Incense Character Player 组件下点击动作按钮。

初次摆位建议父亲与桌沿保持约 0.4–0.6 m，之后按实际香炉大小和桌高调整。具体接触动作没有自动 IK，不能通过任意移动桌子让双手自动吸附到香炉。

## 5 故事中的动作顺序

| 场景 | 角色 | 建议动画顺序 | 你们需要配合的场景内容 |
|---|---|---|---|
| 2 候船屋 | 年轻父亲 | Idle → Pray → WrapBurner → CarryBurner | 三炷香、布、香炉、行李袋；包裹时以布遮住香炉视点 |
| 2 催促出发 | 幼年女儿 | Idle → CallFather → Idle | 门口位置、女儿台词、远处船声 |
| 3 船上 | 幼年女儿、年轻父亲 | Worried；父亲 Talk → Reassure → Idle | 面向女儿，袋口为观察方向；不要额外加入开袋任务 |
| 4 新家 | 年轻父亲 | PlaceBurner → Unpack | 开场可从 PlaceBurner 中段开始，以符合“刚放下香炉”的画面 |
| 4 桌边 | 幼年女儿 | PeekTable → Talk | 女儿站在桌后，调整桌高使她先被桌沿遮住，再探头 |
| 5–6 家庭岁月 | 成年女儿 | DinnerCall → Idle 或 Talk | 配合“爸，饭好了”的声音和餐桌 |
| 5–6 等待 | 年老父亲 | SitDown → SitWindow | 加椅子，让背影朝香炉、正面朝窗；坐下结束后再切换坐姿 |
| 7 1988 年 | 年老父亲 | Walk → Idle → Pray → Talk → TouchFarewell → PickUpBag → Walk | 查看文件的停顿另由剧情系统控制；行李跟手走，香炉留在桌上 |

本包没有修改你的叙事：父亲不会听见香炉的心声；1988 年没有把香炉再次装进行李。每个角色都带完整的共享动作库，方便复用，但按表格选择即可，不必把每个动作都用在每个角色上。

## 6 动作名称与时长

| 动作 | 秒数 | 循环 | 用途 |
|---|---:|---|---|
| Idle | 4 | 是 | 呼吸、轻微转头 |
| Walk | 1.1，老父亲 1.4 | 是 | 原地走路 |
| Talk | 4 | 是 | 点头和轻微手势，没有口型 |
| Pray | 5 | 否 | 举香位置和低头祈愿 |
| CarryBurner | 3 | 是 | 双手托抱 |
| WrapBurner | 6 | 否 | 包裹动作占位，不含布料模拟 |
| PlaceBurner | 4 | 否 | 放置后收手 |
| Unpack | 5 | 是 | 整理物品 |
| Reassure | 4 | 否 | 转头与伸手安慰 |
| SitDown | 3 | 否 | 坐下，降低髋部 |
| SitWindow | 5 | 是 | 窗边坐姿 |
| TouchFarewell | 5 | 否 | 伸右手触摸、停顿、收手 |
| PickUpBag | 4 | 否 | 弯腰提取行李 |
| CallFather | 4 | 否 | 幼年女儿抬头催促 |
| Worried | 4 | 是 | 看父亲与海面 |
| PeekTable | 4 | 否 | 桌边起身探头 |
| DinnerCall | 4 | 否 | 成年女儿招呼父亲 |

非循环动作播完后保留末帧，便于接下一个动作；不会自动回到 Idle。点击 Idle 可以复位。SitDown 后应接 SitWindow，直接接 Idle 会站起来。步行动画不含根位移，因此只点 Walk 时人物不会向前走。

## 7 香、香炉和行李怎么跟随手

角色内有两个挂点：`Grip_R`（右手）和 `Grip_L`（左手）。这些挂点随手骨骼一起移动。

最简单的手工试法：进入 Play，播放 CarryBurner，暂停播放；在 Hierarchy 展开人物，找到 Grip_R，将一个香炉模型副本拖到它下面，再用移动工具把香炉摆在双手之间。这里的摆位仅用于试验，退出 Play 后 Unity 通常不保留运行中改动；记下所需的局部位置和旋转，再在非运行状态设置副本。

正式剧情中，场景香炉与角色携带的香炉副本可以交替显示。把香炉放在桌面后，关闭手持副本、打开桌面香炉，避免香炉继续跟着手移动。不要同时显示两只香炉。

- Pray：1.5–4 秒是举香与祈愿的主要阶段。香道具需放在两手附近；本包没有自动把三炷香插入炉口。
- WrapBurner：约 1.3–4.1 秒配合布覆盖，约 5.1 秒接近托抱姿势。原型阶段用遮挡布和平滑淡出，不必先做复杂布料模拟。
- PlaceBurner：约 2–2.8 秒接近放置姿态，可切换手持/桌面副本。最终时机要按实际画面微调。
- PickUpBag：约 1.8–2.6 秒在低处，可将行李挂到 Grip_R。
- TouchFarewell：约 1.6–3.6 秒为触摸停顿。1988 年这一幕香炉始终留在桌面，不能挂到手上。

脚本中的 `AttachProp` 保持道具当前世界位置，只更换父节点，不会自动对齐位置。手指有骨骼和简单卷曲姿态，但没有针对你的香炉尺寸逐指精修。

## 8 让人物边走边移动

Walk 是原地循环，这是为了让你们可以按房间大小决定走多远。动画控制身体，位置控制路线，两者要同时安排。

有 Timeline 的项目，可以在 Animation Track 上放 Walk 动作，并用另一个父物体控制角色从桌边移动到门口。父物体负责路径，人物本体保留原有骨架。先从约 0.6 m/s 开始试，视脚步是否滑动调节速度。没有现成 Timeline 时，可让负责程序的组员调用 `PlayAction("Walk")` 并移动人物最外层物体；到达后调用 `PlayAction("Idle")`。

脚本调用例子：

```csharp
// npc 为场景角色上的 IncenseCharacterPlayer 组件
npc.PlayAction("Pray");
// 5 秒后由你们的剧情系统触发下一个动作
npc.PlayAction("Talk");
```

这里不是整套剧情自动播放系统。字幕、配音、道具显隐、文件弹窗、转场与 XR 输入仍由你们原有项目负责。

## 9 想继续在 Blender 里改模型

1. 在 Blender 选择 File → Import → glTF 2.0，导入 GLB 文件夹中的一个角色。
2. 模型包含实际网格、材质、蒙皮和骨架。选中骨架，进入 Pose Mode 可以调整骨骼。
3. glTF 的多个动作在不同 Blender 版本里可能显示在 Action Editor 或 NLA 中；若当前 Action 下拉菜单没有全部动作，请检查 NLA Tracks。
4. 优先修改肩部、脸部和手指，再做更复杂的服饰。保留骨骼名字有助于动作复用。
5. 如果修改了网格，请直接从 Blender 导出新 GLB 或 FBX。Unity 里的 JSON 不会自动同步 Blender 修改。
6. 可选 FBX 导出脚本在 Tools 中。先在脚本里将 PACKAGE_DIR 改成你电脑上的解压路径，再在 Blender 的 Scripting 页面运行。此脚本会清空当前 Blender 场景，请先保存你的 Blender 工作，最好在新建空白文件中运行。转换脚本未在本环境执行，导出后需核对所有动作。

FBX 导入 Unity 时可先使用 Rig → Generic、启用 Import Animation，并检查动作片段。本包的直接生成路线已经建立通用骨架与动画，不需要这一步。它不是已验证的 Humanoid/Mixamo 重定向骨架，不能直接声称兼容所有外部动作。

官方参考：
- https://docs.unity3d.com/cn/current/Manual/GenericAnimations.html
- https://docs.unity3d.com/Manual/AnimationsImport.html

## 10 常见问题

| 现象 | 先检查什么 |
|---|---|
| 顶部没有 Tools 菜单 | 文件夹是否在 Assets/IncenseCharacters；Window → General → Console 是否有红色编译错误。项目中其他脚本报错也会阻止本工具出现 |
| 模型粉红色 | 材质 Shader 与项目渲染管线是否一致；URP 选 Universal Render Pipeline/Lit，Built-in 选 Standard，重新生成时工具会按当前管线选择 |
| 角色站着不动 | 是否点了 Play；是否选中了 Prefab 实例而非 Project 中的资源；Animator 中是否有 Character.controller |
| Walk 不向前移动 | 这是原地步行，需要另外移动角色根物体 |
| 人物穿过桌子或手没碰到香炉 | 没有自动接触 IK；调整站位、桌高，或在 Blender 中细修对应动作 |
| 肩膀或手腕折痕明显 | 本包为简化蒙皮，需要后续权重与拓扑精修；避免极端抬臂动作 |
| 按钮不出现在头显里 | 演示按钮只用于桌面 Game 窗口；XR 交互仍使用项目现有输入 |
| 小女孩还看得见身体，没被桌子挡住 | 调整女孩相对桌子的深度及桌高，镜头保持在香炉位置 |
| 退出 Play 后摆位丢失 | 运行状态下的修改通常不保存；退出后设置正式 Transform |

## 11 已验证和仍待完成的内容

已完成数据级检查：四个 GLB 的结构、三角形索引、蒙皮权重归一化、52 个骨骼的层级、动作时间序列、旋转四元数、骨骼蒙皮计算、实际网格关键姿态与动画预览。

尚未完成：Unity 编辑器编译与导入实测、XR 设备测试、与实际香炉/桌椅的接触校正、口型、眨眼、表情、布料模拟、脚底锁定、Humanoid 重定向、LOD 与材质合批。每个人物有 14 个材质槽（部分未使用）；面数低不等于已经完成移动 XR 性能优化。

预览由交付网格与动画数据直接渲染，不是 AI 概念图，也不是 Unity 引擎截图。V2 四角色各约 2,600–3,000 个三角面，精确数字见 asset_stats.json。衣身与袖子共用相同的边界坐标和蒙皮权重，低模平面法线导致这些位置在导出时有重复顶点，但动画时不会因边界权重不同而分开。其余衣服、人体细节仍有分块，尚非制作级完整角色。

本包所有角色网格与动画均为本次新建，不包含参考截图对应商业素材包的文件。截图仅用于风格参考；服装是故事原型的美术选择，不构成对特定历史年代服饰的学术复原。
