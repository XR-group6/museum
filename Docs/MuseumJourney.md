# Museum journey

The runtime route is `scene_01 → scene_02 → scene_03 → scene_04_01 → scene_07 → scene_06_01 → scene_01`.

- Scene 3 waits for its existing story and narration completion, then loads `scene_04_01`.
- `scene_04_01` plays develop's original `TL_scene04_01` with its authored light fades, arrow animation and room props. The earlier 3-second scripted replacement and automatic viewpoint changes are no longer used. Pushing the right stick horizontally snaps the viewpoint 90 degrees; release the stick to its deadzone before the next snap. Scene seven loads 10 seconds after the fourth stage light first rises above 0.01 intensity, even if the original Timeline has since stopped or faded that light out.
- Room subtitles retain their authored positions and rotations while using the clearer styling: dynamic CJK font, size 34, 1100-pixel width at 0.002 world scale, dark background and the depth-independent UI material. They show only the selected Chinese or English version. The rig remains at (-0.003, 0.5, 2.584), yaw 180, named `[CameraRig]`.
- Scene 7 enables its existing movement, turning and permit inspection immediately on entry. A persistent world subtitle above the censer reads: “请用右手触碰并握住香炉，继续我们的旅程。” / “Touch the incense burner with your right controller and press the trigger to continue our journey.”
- Bring the right controller into contact with the censer mesh bounds (0.025 m controller contact allowance) and press `GrabPinch` (trigger) to enter `scene_06_01`. A distant ray, a press 0.05 m away from the mesh, or a held trigger carried across the transition does not activate it. Permit pointing, its open interface and any frame in which permit controls consume the trigger block the censer transition, independently of component update order.
- The final scene starts black, with the censer and the existing ground guide line visible. The line is kept active outside the hidden `Smoke_guide` hierarchy. Its headset subtitle reads: “请向前走到香炉前，用右手触碰握住香炉。” / “Move forward to the incense burner. Touch it with your right controller and press the trigger.” The left stick allows forward and backward movement; stick turning and strafing are disabled in this scene.
- Touch the final censer with the right controller and press the trigger. Stick locomotion stops and the original `TL_scene06_01` begins at time zero. Its authored boat motion, black-environment dissolve, light changes, smoke/line effects and two video activation tracks run to completion (about 24.93 seconds). The video screens have explicit renderer / `_MainTex` bindings and each player starts from zero when its activation track enables the object. The line is restored to its original hierarchy before playback. The previous manual dissolve and five-second ocean timeout are no longer used. After the original Timeline's final frame is evaluated, a full-headset white cover grows over 1.5 seconds. Scene 1 loads only after a fully white frame; language selection resets, and narration waits for a fresh choice.

`MuseumJourney` is attached to the **Museum journey** object in scenes `scene_04_01`, `scene_07`, and `scene_06_01`. The final-light delay, subtitle height, white fade, contact allowance, original room Timeline, stage lights, ground line and sea surfaces remain editable in the Inspector. The original assets are retained. Both scenes use their original Timelines. The final scene keeps autoplay off so the censer press owns its start.

Verification uses Unity 2022.3.62f3's C# compiler and `Tools/MuseumJourneyPlayVerify.cs` in an isolated project under `Temp/JourneyVerification`. The test constructs representative rigs and colliders with tracked input disabled; it exercises the runtime controller through two complete loops. Its test light curves reproduce the authored keyframe times; static checks confirm that the original Timeline asset and its scene bindings are unchanged. It does not validate the authored models' appearance, headset comfort, actual controller bindings or audio perception. Check those in the headset, including the four sector light directions, central censer height, approach floor and full white cover in both eyes.

Ocean Timeline regression: `Tools/MuseumOceanTimelineVerify.cs` constructs a representative boat track and the original video activation timings, imports the two real clips, and verifies two visits plus the final whiteout/language reset in an isolated project. Headset video projection and final visual appearance require hardware verification.


## 场景 7 探索完成确认

探索阶段触碰香炉并按右手扳机后，在香炉上方原引导字幕的位置显示固定的“是否已经探索完毕”（打开时朝向玩家，不跟随头显），提供“是 / 否”（支持英文）。右手射线命中按钮变绿，重新按下扳机确认；编辑器支持鼠标点击。

- 是：进入原下一幕 scene_06_01。
- 否：关闭面板并恢复探索提示与香炉交互；可以继续探索、查看入台证，再次触碰香炉重新打开确认，次数不限。

面板显示期间暂停入台证交互，关闭后恢复，避免同一个扳机操作同时触发两个界面。按住触碰时的扳机不会自动选中选项。


## scene_06_01 船上视频与返航

视频不再自动播放或循环。Timeline 先正常播放视频屏幕的弹出动画，在对应动画最后一个关键帧后暂停推进，视频上方显示“用右摇杆激光点击视频开始播放”（支持英文）。右手激光命中屏幕变绿，按下扳机播放对应视频，提示随之隐藏；每个视频由各自的点击开始，播放时长从原 Timeline 对应 ActivationTrack 片段读取（约 21.37 秒、15.05 秒），到时暂停；文件先结束时也视为完成。编辑器支持鼠标点击。

当前显示的视频全部播放完毕后继续原 Timeline；结束时检查所有视频均已完成，未观看的片段保留屏幕供点击观看。全部视频和原 Timeline 完成后，在船上视线前方偏下的固定位置显示“驶向博物馆”按钮。只有点击按钮后才开始原白光渐变，白光完成再返回 scene_01；不点击则留在船上。

未修改视频文件、原 Timeline 资源、动画曲线及白光时长。脚本编译通过，视频解码、屏幕布局、激光命中和转场需要在头显中验证。

视频等待点击时静音解码并暂停在首帧，载入前使用黑色底图；点击后恢复声音，从开头按原 Timeline 时长播放。等待用户确认的时间不计入播放时长。

第一段视频结束后，运行时跳过下一屏弹出前的空档，仅留约 0.2 秒再播放下一屏原有弹出动画；动画速度与关键帧不变。

第二个视频结束后立即停止 Timeline 并显示返航选项，不再等待剩余 Timeline。选项固定在 fishing_boat 模型顶部上方 0.35 米，打开时朝向玩家，不随头显移动。点击后仍按原白光转场返回博物馆。

返航选项位置调整为船体靠玩家一侧的侧甲上方，并向玩家左侧偏移 0.45 米；按钮保持原 600×110、缩放 0.0015 的大小，不跟随头显。

返航按钮最终改为固定悬浮在玩家与船近侧之间，沿两者连线靠船约 70% 位置，低于打开时眼高 0.2 米，朝向玩家；不再贴合船面，不跟随头显，保持原大小。

返航按钮距离进一步调整：以第二段视频结束时船的位置为准，放在玩家到船近侧连线的 95% 处，更靠近船、远离玩家。
