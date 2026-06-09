# Baked Rendering Architecture

本文档记录当前项目中 2D 战斗表现的烘培渲染结构、运行时分层、Editor 工具目录，以及后续向 Manager/Draw 方案迁移的规划。

目标平台包含移动端和微信小游戏，因此设计优先级是：

- 减少同屏大量对象的 CPU 播放成本。
- 减少 Renderer、材质实例和状态切换。
- 资源先可验证，再逐步迁移到批量提交。
- 保留 GameObject 验证路径，避免一开始就把调试复杂度压到 Manager 层。

## 1. 当前目录结构

Runtime:

```text
Assets/Scripts/Core/Runtime/Rendering/
  Common/
    BakedMeshPlayerBase.cs
    BakedTickPlayer.cs
    BakedMaterialUtility.cs

  MeshBatch/
    MeshPlayer.cs
    MeshElement.cs
    TickMeshElement.cs
    MeshQuadWriter.cs

  BakedSequence/
    BakedSequencePlayer.cs
    BakedEffectAtlas.shader

  SpineVit/
    BakedSpineVitAsset.cs
    BakedSpineVitPlayer.cs
    SpineVit.shader

  FloatText/
    FloatTextFontAsset.cs
    FloatTextElement.cs
    FloatText.shader
```

Editor:

```text
Assets/Scripts/Core/Editor/Rendering/
  BakedSequence/
    SequenceAtlasBakerWindow.cs
    ParticleAtlasBaker.cs
    ParticleAtlasBakeSettings.cs
    ParticleAtlasBakeResult.cs
    ParticleAtlasMetadata.cs
    ParticleAtlasBakeUtility.cs
    ParticleAtlasLayoutUtility.cs
    ParticleAtlasTextureUtility.cs
    ParticleAtlasPathUtility.cs
    ParticleAtlasBakeMaterialUtility.cs

  SpineVit/
    SpineVitBakerWindow.cs
    SpineVitBaker.cs
    SpineVitBakeSettings.cs
    SpineVitBakeResult.cs
    SpineVitMeshSampler.cs

  FloatText/
    后续放 FloatText atlas/glyph/font asset 编辑工具
```

资源示例：

```text
Assets/BakedSequences/FloatText/
  float_text_atlas_v1_source.png
  float_text_atlas_v1.png
  FloatText.mat
  FloatTextFontAsset.asset
```

## 2. 分层原则

当前不是把所有方案强行做成一个 Player，而是拆成两条复用链路。

独立 MeshRenderer Player：

```text
BakedMeshPlayerBase
  -> BakedTickPlayer
       -> BakedSequencePlayer
       -> BakedSpineVitPlayer
```

大 Mesh 元素链路：

```text
MeshPlayer
  <- MeshElement
       -> TickMeshElement
            -> FloatTextElement
```

这样拆的原因是：

- 序列帧特效和 Spine VIT 都是一个 GameObject 一个 MeshRenderer。
- 飘字、血条、名字、HUD 图标更适合写入同一个大 Mesh。
- `MeshPlayer` 是聚合层，用 `LateUpdate/Rebuild` 统一生成 Mesh；它不应该继承播放帧的 tick 逻辑。
- `FloatTextElement` 当前保留 GameObject 编辑体验，但渲染数据已经走 `MeshPlayer`，后续迁移 Manager 更自然。

## 3. Common 层

### BakedMeshPlayerBase

负责所有 MeshRenderer Player 共同的组件和渲染状态：

- 缓存 `MeshFilter`、`MeshRenderer`。
- 统一设置 `sharedMesh`、`sharedMaterial`。
- 统一创建和提交 `MaterialPropertyBlock`。
- 统一控制 renderer visible。
- 统一设置 sorting layer/order。

约束：

- 子类不直接缓存自己的 `MeshRenderer`。
- 子类不复制材质实例。
- 每实例差异优先通过 `MaterialPropertyBlock` 传入。

### BakedTickPlayer

用于有时间推进的独立 MeshRenderer Player。

它统一管理：

- Runtime `Update`。
- Editor `EditorApplication.update`。
- 编辑器 deltaTime 限制。
- Scene/Game view repaint。
- `simulateInEditMode`。

子类只关心：

```csharp
protected override bool IsRuntimeTickActive => playing;
protected override bool IsEditorTickActive => playing;
protected override void OnPlayerEnable();
protected override void Tick(float deltaTime);
protected override void OnEditorPreviewTick();
```

这样可以避免子类漏写 `base.OnEnable()` 或忘记注销 editor update。

### BakedMaterialUtility

当前只做一件事：

```text
SetTextureIfNeeded(material, propertyId, texture)
```

目的是避免多个 Player 重复写：

- 判断 material 是否为空。
- 判断 texture 是否为空。
- 判断 shader property 是否存在。
- 判断当前贴图是否已经一致。

后续如果材质关键字、shader pass、默认纹理设置变多，也放在这里统一处理。

## 4. MeshBatch 层

### MeshPlayer

`MeshPlayer` 是当前大 Mesh 聚合层。

职责：

- 持有一个动态 Mesh。
- 收集多个 `MeshElement`。
- 每帧或编辑器预览时调用 `WriteQuads`。
- 由 `MeshQuadWriter` 写入 vertices/uv/colors/indices。
- 设置统一材质、贴图、颜色和 sorting。

当前一个 `MeshPlayer` 对应一组：

```text
material + texture + sorting layer + sorting order
```

如果材质、贴图或排序不同，应拆成不同 `MeshPlayer`。

### MeshElement

`MeshElement` 是可写入 `MeshPlayer` 的最小逻辑元素。

职责：

- 自动注册到父级或场景中的 `MeshPlayer`。
- 提供 `CanWriteQuads`。
- 子类实现 `WriteQuads(MeshQuadWriter writer)`。

它本身不关心动画，也不关心具体业务。

### TickMeshElement

`TickMeshElement` 用于会播放、会淡出、会移动，但仍写入 `MeshPlayer` 的元素。

职责：

- Runtime tick。
- Editor tick。
- 编辑器 tick 后触发所属 `MeshPlayer.Rebuild()`。

当前 `FloatTextElement` 已经继承它。

### MeshQuadWriter

统一写 quad：

```text
element local quad
  -> element localToWorld
  -> MeshPlayer worldToLocal
  -> MeshPlayer local vertices
```

这样每个元素仍然可以用自己的 Transform 做摆放，最终合并到一个 MeshPlayer 的本地坐标中。

## 5. BakedSequence 方案

用途：

- 技能特效。
- 状态特效。
- 烟、光、爆点等粒子表现。

Editor 烘培流程：

```text
Prefab
  -> 临时场景
  -> 指定 Bake Layer
  -> Camera + RenderTexture 逐帧采样
  -> ReadPixels
  -> 计算可见 rect
  -> tight packing 到 atlas
  -> 输出 png + json
```

运行时资源：

```text
atlas texture
metadata json
material
shared quad mesh
```

运行时播放：

- `BakedSequencePlayer` 使用 shared quad mesh。
- 当前帧的 uv、uv clamp、offset、size 通过 MPB 传入。
- `BakedEffectAtlas.shader` 用 atlas 采样并乘 `_InstanceColor`。
- 播放期间不重建 mesh，只更新 MPB 中的帧数据。

关键 metadata：

```text
uvX/uvY/uvWidth/uvHeight
quadOffsetX/quadOffsetY
quadWidth/quadHeight
```

其中 `quadWidth/quadHeight` 来自 tight rect。它让原本 256x256 的整帧 quad 只绘制真正有内容的区域，减少透明空白造成的 overdraw。

如果后续关闭 tight rect，`quadWidth/quadHeight` 可以退化为 1，但会增加 atlas 空间和 overdraw。

## 6. SpineVit 方案

用途：

- 同屏大量相同或相近 Spine 角色。
- 需要保留 Spine atlas 贴图，但避免每实例 CPU 骨骼和 mesh 计算。

VIT 表示 Vertex Information Texture。

当前使用两张贴图保存顶点动画：

```text
positionTexture:
  x = vertexIndex
  y = frameIndex
  rgba = position.x, position.y, position.z, visible

colorTexture:
  x = vertexIndex
  y = frameIndex
  rgba = vertexColor
```

UV 和 triangles 不进贴图，而是固化在静态 mesh 中。

Editor 烘培流程：

```text
SkeletonDataAsset
  -> 解析动画列表
  -> SpineVitMeshSampler 用 Spine MeshGenerator 采样
  -> 第一帧确定 topology
  -> 后续每帧校验 vertex count / triangles / uv
  -> 写 positionTexture/colorTexture
  -> 创建静态 mesh
  -> 创建 BakedSpineVitAsset
```

运行时资源：

```text
BakedSpineVitAsset
  mesh
  material
  sourceTexture
  positionTexture
  colorTexture
  frameRate
  vertexCount
  totalFrameCount
  bounds
  clips
```

运行时播放：

- `BakedSpineVitPlayer` 使用 asset 中的静态 mesh 和 shared material。
- 当前帧号通过 MPB 的 `_FrameIndex` 传入。
- `SpineVit.shader` 通过 `SV_VertexID` 读取 position/color texture。
- CPU 不再逐帧计算 Spine skeleton 和 mesh。

当前限制：

- v1 只支持一个 atlas asset、一个 material/page。
- 不支持 clipping attachment。
- 不支持播放过程中 topology 改变。
- draw order、attachment、UV 改变可能导致 topology 校验失败。

## 7. FloatText 方案

用途：

- 伤害数字。
- 治疗数字。
- 暴击图标 + 数字。
- MISS token。

当前是 v1 验证版：

```text
FloatTextElement
  -> TickMeshElement
  -> MeshElement
  -> MeshPlayer
```

数据来源：

```text
FloatTextFontAsset
  atlas
  material
  pixelsPerUnit
  defaultLineHeight
  glyphs[]

FloatTextGlyph
  key
  style
  uvRect
  pixelSize
  advance
  offset
  scale
```

排版规则：

- 每个数字字符一个 quad。
- `MISS` 是一个整词 token，一个 quad。
- `crit_icon` 是一个图标 token，一个 quad。
- 绿色 `+` 作为 glyph，运行时使用较小 scale。
- 整条文本水平居中。

播放规则：

- `PlayDamage(int value)` 使用红橙数字。
- `PlayHeal(int value)` 使用绿色 `+` 和绿色数字。
- `PlayCritical(int value)` 使用 `crit_icon + 数字`。
- `PlayMiss()` 使用 `MISS` token。

v1 重点：

- 验证 atlas、glyph rect、UV、排版、动效和接口。
- 使用 GameObject 组织，便于场景中复制和调试。
- 渲染已经走 `MeshPlayer`，多个 FloatTextElement 可以合成一个大 Mesh。

后续 v2：

- 引入 FloatTextManager。
- Manager 直接维护元素池和大 Mesh 数据。
- 减少 GameObject 数量。
- 同一 atlas/material/sorting 的飘字集中 Draw。
- 动效参数可以迁到数据结构或 shader。

## 8. Manager/Draw 后续规划

当前三类表现的优化层级不同：

```text
BakedSequencePlayer:
  GameObject + shared mesh + shared material + MPB

BakedSpineVitPlayer:
  GameObject + baked static mesh + shared material + MPB

FloatTextElement:
  GameObject element + MeshPlayer dynamic mesh
```

后续 Manager 方向分两类。

### Instanced Manager

适合：

- BakedSequence。
- SpineVit。

核心思路：

```text
manager 收集同资源实例
  -> 按 mesh/material/texture 分组
  -> 生成 per-instance 数据
  -> Graphics.DrawMeshInstanced 或 DrawMeshInstancedIndirect
```

需要处理：

- 每实例 frame。
- 每实例 color。
- 每实例 transform。
- sorting 分组。
- 透明排序策略。

### Dynamic Mesh Manager

适合：

- FloatText。
- HUD 图标。
- 血条。
- 名字。
- 小型 2D quad 类 UI/HUD。

核心思路：

```text
manager 持有一个或多个 MeshPlayer-like batch
  -> 每个元素写多个 quad
  -> 同 atlas/material/sorting 合并
  -> 每帧按需 rebuild
```

当前 `MeshPlayer/MeshElement/MeshQuadWriter` 就是这条线的 GameObject 过渡版本。

## 9. 透明和排序

这些方案大多是透明对象，排序和 overdraw 是长期风险。

基本原则：

- 同材质、同 atlas、同 sorting 的对象尽量合批。
- 不同 sorting layer/order 需要拆 batch。
- 粒子序列帧使用 tight rect 减少透明空白。
- 飘字 atlas glyph rect 要尽量贴边，但保留足够 padding 防止采样串色。
- Spine VIT bounds 要覆盖所有帧，避免动画大动作被裁剪。

透明排序如果严格依赖每个对象前后关系，合批会更难。战斗表现应尽量按层级分组，而不是要求每个特效都和角色逐像素正确排序。

## 10. 当前验证清单

BakedSequence：

- 烘培 atlas/json 是否正确。
- tight rect 是否导致位置漂移。
- additive 特效是否需要 Alpha From RGB。
- 播放时是否共用 shared mesh/material。
- Frame Debugger 中是否出现预期批次。

SpineVit：

- `BakedSpineVitAsset` 是否包含 mesh/material/sourceTexture/VIT textures/clips。
- 动画 topology 是否稳定。
- 当前帧 `_FrameIndex` 是否正确。
- bounds 是否覆盖所有动作。
- 多实例是否共用材质和资源。

FloatText：

- glyph uvRect 是否正确。
- `MISS` 和 `crit_icon` 是否按 token 匹配。
- 治疗 `+` 尺寸是否合适。
- 多个 FloatTextElement 是否写入同一个 MeshPlayer。
- 播放期间是否只更新状态，不重复创建材质。

## 11. 目录维护约定

- Runtime 和 Editor 都按功能域分目录。
- shader 放在对应 Runtime 功能目录内。
- Editor baker 放在 `Assets/Scripts/Core/Editor/Rendering/<Feature>`。
- 新方案优先判断属于独立 MeshRenderer Player 还是 MeshBatch 元素。
- 移动 Unity 脚本、shader、asset 时必须同时移动 `.meta`。
- 不把第三方 Spine、YooAsset、插件代码混入 Core Rendering 目录。

后续新增建议：

```text
Assets/Scripts/Core/Editor/Rendering/FloatText/
  FloatTextFontAssetEditor.cs
  FloatTextAtlasBakerWindow.cs
  FloatTextGlyphScanner.cs

Assets/Scripts/Core/Runtime/Rendering/Managers/
  BakedSequenceManager.cs
  BakedSpineVitManager.cs
  FloatTextManager.cs
```

Manager 目录建议等第一个 Manager 真正落地时再创建，避免空抽象过早固定。
