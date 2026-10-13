# VisualEffect 合入 interaction

来源：`origin/VisualEffect`（`be9990c`）。在已有合并提交 `c3fa2ca` 上补齐原始特效接入。

- scene_01：现有 RightController 右手扳机调用保持不变。独立视觉副本播放原香炉 Timeline 的材质曲线和六个粒子系统；不绑定人物、剧情或场景灯光。结束、禁用和销毁时恢复原香炉渲染，不替换碰撞体或 Animator。
- scene_02：原 VisualEffect 场景只引用普通 bay_water，因此使用同一分支 scene_03 的完整海浪预制体，按港口海面边界对齐。
- scene_03：优先复用场景中已有 VisualEffect_Waves 的位置；没有时加载海浪资源。旧海面包含 SkinnedMeshRenderer，只关闭其渲染，保留动画和碰撞引用。
- 海浪保留原网格、三层可见粒子及隐藏的父粒子系统；初始化防重。原材质依赖 PandaShaderV2.2，Bloom 依赖 Post Processing 3.4.0。
- 不改写三个场景、人物模型、剧情动画、VR 输入或工程设置。工作区原有的未提交修改保留。

验证：Unity 2022.3.62f3c1 编译和资源导入；三个场景的 VR Pose、剧情 Timeline 与碰撞体引用；海浪防重及两种旧海面隐藏；Play Mode 中原香炉发光曲线、六个粒子、重复触发、自然结束、重放及中断恢复。

当前机器无法初始化 OpenVR（VRInitError_Init_PathRegistryNotFound），头显双眼显示、手柄实机交互和帧率仍需设备验收。

维护入口：`VisualEffectPortEditor.Build` 可重新生成独立香炉/海浪资源并检查场景；`VisualEffectPlayCheckEditor.Run` 执行播放生命周期检查。后者会在完成后退出 Unity，建议使用独立 batchmode 进程。日志报告保存在 `Library/VisualEffectPort`。
