# MeshPlayer 方案详解

本文档说明当前项目里的 `MeshPlayer` 方案：它如何把多个小元素写进同一个动态 Mesh，为什么适合飘字、HUD、血条、名字、图标等 2D 战斗 UI/表现，以及如何基于 `FloatTextElement` 扩展新的 Element。

相关代码：

```text
Assets/Scripts/Core/Runtime/Rendering/MeshBatch/
  MeshPlayer.cs
  MeshElement.cs
  TickMeshElement.cs
  MeshQuadWriter.cs

Assets/Scripts/Core/Runtime/Rendering/FloatText/
  FloatTextElement.cs
  FloatTextFontAsset.cs
  FloatText.shader
```

## 1. MeshPlayer 要解决什么问题

战斗中会有大量小型 2D 表现：

```text
飘字
暴击图标
MISS
血条
名字
状态图标
HUD 标记
地面提示
```

如果每一个元素都用独立 `GameObject + MeshRenderer/SpriteRenderer`，同屏数量上来后会带来：

- Renderer 数量多。
- Transform/MonoBehaviour 更新多。
- 材质和贴图切换多。
- 后续迁移到 Manager 批量渲染困难。

`MeshPlayer` 的思路是：

```text
一个 MeshPlayer 持有一个动态 Mesh
多个 MeshElement 往这个 Mesh 写 quad
所有 quad 共用同一张贴图和同一个材质
最终由一个 MeshRenderer 渲染
```

当前 v1 仍保留 GameObject/Transform 体验，方便调试和摆放；但渲染数据已经走“合并成大 Mesh”的结构，后续迁移到 Manager 会比较自然。

## 2. 整体结构

```text
MeshPlayer
  持有 MeshFilter/MeshRenderer/动态 Mesh
  持有 material/texture
  收集多个 MeshElement
  LateUpdate 中 Rebuild 整个 Mesh

MeshElement
  挂在一个 GameObject 上
  自动注册到 MeshPlayer
  子类实现 WriteQuads

TickMeshElement
  MeshElement 的带时间版本
  提供 Runtime Update 和 Editor preview tick

MeshQuadWriter
  收集 vertices/uv/colors/indices
  把 element 的 local quad 转到 MeshPlayer 局部空间
  最后 ApplyTo(mesh)
```

核心关系：

```text
FloatTextElement
  -> TickMeshElement
    -> MeshElement
      -> MeshPlayer
        -> MeshQuadWriter
          -> Unity Mesh
```

## 3. MeshPlayer：一个渲染层

`MeshPlayer` 是聚合层。它负责维护动态 Mesh 和统一材质：

```csharp
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class MeshPlayer : BakedMeshPlayerBase
{
    [SerializeField] private Material material;
    [SerializeField] private Texture texture;
    [SerializeField] private bool rebuildInEditMode = true;
    [SerializeField] private Color color = Color.white;

    private readonly List<MeshElement> elements = new List<MeshElement>(128);
    private readonly MeshQuadWriter writer = new MeshQuadWriter();
    private Mesh mesh;
}
```

它的核心入口是 `Rebuild()`：

```csharp
public void Rebuild()
{
    EnsureComponents();
    writer.Begin(transform.worldToLocalMatrix);

    for (int i = elements.Count - 1; i >= 0; i--)
    {
        MeshElement element = elements[i];
        if (element == null)
        {
            elements.RemoveAt(i);
            continue;
        }

        if (element.CanWriteQuads)
        {
            element.WriteQuads(writer);
        }
    }

    mesh.indexFormat = writer.VertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
    writer.ApplyTo(mesh);
    SetRendererVisible(writer.VertexCount > 0 && material != null);
    SetSharedMesh(mesh);
    ApplyMeshPlayerPropertyBlock();
}
```

这段代码做了几件关键事情：

- `writer.Begin(transform.worldToLocalMatrix)`：告诉 writer 最终 MeshPlayer 的坐标空间是什么。
- 遍历所有 `MeshElement`：每个 element 自己决定写多少个 quad。
- `indexFormat`：顶点超过 65535 时切到 `UInt32` index。
- `writer.ApplyTo(mesh)`：把收集到的 vertices/uv/colors/indices 写进 Unity Mesh。
- `ApplyMeshPlayerPropertyBlock()`：用 MPB 设置 `_MainTex` 和 `_InstanceColor`，避免复制材质。

`MeshPlayer` 本身不懂飘字、血条或图标。它只认识：

```text
element.WriteQuads(writer)
```

## 4. MeshElement：可写入 MeshPlayer 的元素

`MeshElement` 是所有元素的基类：

```csharp
[ExecuteAlways]
public abstract class MeshElement : MonoBehaviour
{
    [SerializeField] private MeshPlayer meshPlayer;
    [SerializeField] private bool visible = true;

    public virtual bool CanWriteQuads => visible && isActiveAndEnabled;

    public abstract void WriteQuads(MeshQuadWriter writer);
}
```

它负责注册和注销：

```csharp
protected virtual void OnEnable()
{
    RegisterToMeshPlayer();
}

protected virtual void OnDisable()
{
    if (meshPlayer != null)
    {
        meshPlayer.Unregister(this);
    }
}
```

注册逻辑：

```csharp
protected void RegisterToMeshPlayer()
{
    if (meshPlayer == null)
    {
        meshPlayer = GetComponentInParent<MeshPlayer>();
    }

    if (meshPlayer == null)
    {
        meshPlayer = FindObjectOfType<MeshPlayer>();
    }

    if (meshPlayer != null)
    {
        meshPlayer.Register(this);
    }
}
```

这个设计方便 v1 使用：

- Element 放在 MeshPlayer 子节点下，会自动找父级。
- 没有父级时，会找场景里的第一个 MeshPlayer。
- 也可以手动调用 `SetMeshPlayer()` 指定。

后续正式 Manager 版可以去掉 `FindObjectOfType`，改成显式注册。

## 5. TickMeshElement：有生命周期的 Element

飘字这类元素有播放时间、上飘、淡出，所以继承 `TickMeshElement`：

```csharp
[ExecuteAlways]
public abstract class TickMeshElement : MeshElement
{
    [SerializeField] private bool simulateInEditMode = true;

    protected virtual bool IsRuntimeTickActive => true;
    protected virtual bool IsEditorTickActive => IsRuntimeTickActive;

    protected abstract void Tick(float deltaTime);
}
```

Runtime 里：

```csharp
protected void Update()
{
    if (Application.isPlaying && IsRuntimeTickActive)
    {
        Tick(Time.deltaTime);
    }
}
```

Editor 里通过 `EditorApplication.update` 模拟 tick，并在 tick 后重建 MeshPlayer：

```csharp
protected virtual void OnAfterEditorTick()
{
    if (MeshPlayer != null)
    {
        MeshPlayer.Rebuild();
    }

    SceneView.RepaintAll();
}
```

所以 `FloatTextElement` 只需要关心“时间怎么变化”，不用自己管 Mesh 何时提交。

## 6. MeshQuadWriter：真正写 quad 的地方

`MeshQuadWriter` 内部维护四组列表：

```csharp
private readonly List<Vector3> vertices = new List<Vector3>(256);
private readonly List<Vector2> uvs = new List<Vector2>(256);
private readonly List<Color32> colors = new List<Color32>(256);
private readonly List<int> indices = new List<int>(384);
```

每帧开始时清空：

```csharp
public void Begin(Matrix4x4 layerWorldToLocal)
{
    worldToLayer = layerWorldToLocal;
    vertices.Clear();
    uvs.Clear();
    colors.Clear();
    indices.Clear();
}
```

写入一个 quad：

```csharp
public void AddQuad(Matrix4x4 localToWorld, float xMin, float yMin, float xMax, float yMax, Vector4 uvRect, Color32 color)
{
    Matrix4x4 localToLayer = worldToLayer * localToWorld;
    int vertexStart = vertices.Count;
    vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMin, yMin, 0f)));
    vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMax, yMin, 0f)));
    vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMax, yMax, 0f)));
    vertices.Add(localToLayer.MultiplyPoint3x4(new Vector3(xMin, yMax, 0f)));

    float uMin = uvRect.x;
    float uMax = uvRect.x + uvRect.z;
    float vMin = uvRect.y;
    float vMax = uvRect.y + uvRect.w;
    uvs.Add(new Vector2(uMin, vMin));
    uvs.Add(new Vector2(uMax, vMin));
    uvs.Add(new Vector2(uMax, vMax));
    uvs.Add(new Vector2(uMin, vMax));

    colors.Add(color);
    colors.Add(color);
    colors.Add(color);
    colors.Add(color);

    indices.Add(vertexStart);
    indices.Add(vertexStart + 2);
    indices.Add(vertexStart + 1);
    indices.Add(vertexStart);
    indices.Add(vertexStart + 3);
    indices.Add(vertexStart + 2);
}
```

这里的坐标转换很关键：

```text
element local quad
  -> element localToWorld
  -> MeshPlayer worldToLocal
  -> MeshPlayer local vertex
```

也就是：

```csharp
Matrix4x4 localToLayer = worldToLayer * localToWorld;
```

这样每个 Element 仍然可以使用自己的 Transform：

- 位置。
- 旋转。
- 缩放。
- 父子层级。

最终所有顶点都会合到 MeshPlayer 的一个 Mesh 里。

三角形顺序：

```csharp
0, 2, 1,
0, 3, 2
```

这会让 quad 的正面朝向 2D 摄像机常用方向。当前 shader `Cull Off`，即使顺序反了也能显示，但统一绕序能避免以后改剔除模式时出问题。

最后提交：

```csharp
public void ApplyTo(Mesh mesh)
{
    mesh.Clear();
    if (vertices.Count == 0)
    {
        return;
    }

    mesh.SetVertices(vertices);
    mesh.SetUVs(0, uvs);
    mesh.SetColors(colors);
    mesh.SetTriangles(indices, 0);
    mesh.RecalculateBounds();
}
```

## 7. FloatText 例子：一条字符串如何变成多个 quad

`FloatTextElement` 是当前最完整的 MeshElement 示例。

它持有字体资源：

```csharp
[SerializeField] private FloatTextFontAsset fontAsset;
```

`FloatTextFontAsset` 保存：

```csharp
public sealed class FloatTextFontAsset : ScriptableObject
{
    public Texture2D atlas;
    public Material material;
    public float pixelsPerUnit = 100f;
    public float defaultLineHeight = 150f;
    public FloatTextGlyph[] glyphs = Array.Empty<FloatTextGlyph>();
}
```

每个 glyph 保存：

```csharp
public sealed class FloatTextGlyph
{
    public string key;
    public FloatTextStyleId style;
    public Vector4 uvRect;
    public Vector2 pixelSize;
    public Vector2 offset;
    public float advance;
    public float scale = 1f;
}
```

含义：

```text
key       字符或 token，例如 "1"、"+"、"MISS"、"crit_icon"
style     Damage/Heal/Icon/Token
uvRect    在 atlas 里的 UV
pixelSize 原始像素尺寸
offset    排版偏移
advance   光标前进距离
scale     单 glyph 缩放
```

### 7.1 配置 MeshPlayer 材质

`FloatTextElement` 启用或校验时，会把字体材质和贴图设置给 MeshPlayer：

```csharp
private void ConfigureMeshPlayer()
{
    if (MeshPlayer != null && fontAsset != null)
    {
        MeshPlayer.SetMaterial(fontAsset.material, fontAsset.atlas);
    }
}
```

这意味着同一个 MeshPlayer 下的 FloatTextElement 必须共用同一套字体 atlas/material。

如果后续要同时显示多套字体或不同 shader，需要拆成多个 MeshPlayer。

### 7.2 Play 接口

FloatText 对外提供语义化播放接口：

```csharp
public void PlayDamage(int value)
{
    Play(Mathf.Abs(value).ToString(), FloatTextStyleId.Damage);
}

public void PlayHeal(int value)
{
    Play("+" + Mathf.Abs(value), FloatTextStyleId.Heal);
}

public void PlayCritical(int value)
{
    Play("crit_icon" + Mathf.Abs(value), FloatTextStyleId.Damage);
}

public void PlayMiss()
{
    Play("MISS", FloatTextStyleId.Token);
}
```

最终都进入：

```csharp
public void Play(string text, FloatTextStyleId style)
{
    EnsureFontAsset();
    ConfigureMeshPlayer();
    BuildQuads(text, style);
    baseLocalPosition = transform.localPosition;
    elapsed = 0f;
    currentAlpha = 1f;
    playing = quads.Count > 0;
    editorPreviewVisible = false;
    ApplyMotion(0f);
}
```

关键点：

- `Play` 时构建一次 quad 数据。
- 播放期间不重新解析字符串。
- 每帧只更新 alpha、时间、运动状态。

### 7.3 解析字符串

`ResolveGlyphs` 把字符串拆成 glyph 列表：

```csharp
private List<ResolvedGlyph> ResolveGlyphs(string text, FloatTextStyleId style)
{
    List<ResolvedGlyph> result = new List<ResolvedGlyph>(text.Length);
    int index = 0;
    while (index < text.Length)
    {
        if (text.IndexOf("crit_icon", index, System.StringComparison.Ordinal) == index &&
            fontAsset.TryGetGlyph("crit_icon", FloatTextStyleId.Icon, out FloatTextGlyph iconGlyph))
        {
            result.Add(new ResolvedGlyph(iconGlyph));
            index += "crit_icon".Length;
            continue;
        }

        if (text.IndexOf("MISS", index, System.StringComparison.Ordinal) == index &&
            fontAsset.TryGetGlyph("MISS", FloatTextStyleId.Token, out FloatTextGlyph missGlyph))
        {
            result.Add(new ResolvedGlyph(missGlyph));
            index += "MISS".Length;
            continue;
        }

        string key = text[index].ToString();
        FloatTextStyleId glyphStyle = style;
        if (key == "+")
        {
            glyphStyle = FloatTextStyleId.Heal;
        }

        if (fontAsset.TryGetGlyph(key, glyphStyle, out FloatTextGlyph glyph))
        {
            result.Add(new ResolvedGlyph(glyph));
        }
        else
        {
            WarnMissingGlyph(key, glyphStyle);
        }

        index++;
    }

    return result;
}
```

这里支持两种 token：

```text
crit_icon  一个 icon quad
MISS       一个整词 quad
```

数字仍然是一个字符一个 quad。比如：

```text
"1200" -> 4 个 glyph -> 4 个 quad
"MISS" -> 1 个 token glyph -> 1 个 quad
"crit_icon9999" -> 1 个 icon + 4 个数字 -> 5 个 quad
```

### 7.4 排版生成 quad

`BuildQuads` 根据 glyph 尺寸和 advance 计算每个 quad 的本地坐标：

```csharp
private void BuildQuads(string text, FloatTextStyleId style)
{
    quads.Clear();
    if (fontAsset == null || string.IsNullOrEmpty(text))
    {
        return;
    }

    List<ResolvedGlyph> resolvedGlyphs = ResolveGlyphs(text, style);
    if (resolvedGlyphs.Count == 0)
    {
        return;
    }

    float pixelsPerUnit = Mathf.Max(0.0001f, fontAsset.pixelsPerUnit);
    float totalAdvance = 0f;
    for (int i = 0; i < resolvedGlyphs.Count; i++)
    {
        FloatTextGlyph glyph = resolvedGlyphs[i].Glyph;
        totalAdvance += GetAdvance(glyph) * Mathf.Max(0.0001f, glyph.scale);
    }

    float cursor = -totalAdvance * 0.5f;
    for (int i = 0; i < resolvedGlyphs.Count; i++)
    {
        FloatTextGlyph glyph = resolvedGlyphs[i].Glyph;
        float glyphScale = Mathf.Max(0.0001f, glyph.scale);
        float width = glyph.pixelSize.x * glyphScale;
        float height = glyph.pixelSize.y * glyphScale;
        float xMin = (cursor + glyph.offset.x * glyphScale) / pixelsPerUnit;
        float yMin = (glyph.offset.y * glyphScale) / pixelsPerUnit;
        float xMax = xMin + width / pixelsPerUnit;
        float yMax = yMin + height / pixelsPerUnit;

        quads.Add(new FloatTextQuad(xMin, yMin, xMax, yMax, glyph.uvRect));
        cursor += GetAdvance(glyph) * glyphScale;
    }
}
```

这段逻辑的关键点：

- 先算 `totalAdvance`，再让 `cursor = -totalAdvance * 0.5f`，实现整条文本居中。
- `pixelSize / pixelsPerUnit` 把像素尺寸转换成 Unity 世界单位。
- `offset` 用于微调单个 glyph 的摆放。
- `advance` 控制下一个 glyph 的起点。
- `uvRect` 决定这个 quad 去 atlas 的哪个区域采样。

### 7.5 写入 MeshPlayer

`FloatTextElement.WriteQuads` 是和 MeshPlayer 对接的唯一出口：

```csharp
public override void WriteQuads(MeshQuadWriter writer)
{
    Color instanceColor = color;
    instanceColor.a *= Mathf.Clamp01(currentAlpha);
    Color32 vertexColor = instanceColor;
    Matrix4x4 localToWorld = transform.localToWorldMatrix;
    for (int i = 0; i < quads.Count; i++)
    {
        FloatTextQuad quad = quads[i];
        writer.AddQuad(localToWorld, quad.XMin, quad.YMin, quad.XMax, quad.YMax, quad.UvRect, vertexColor);
    }
}
```

这里每个 FloatTextQuad 写成一个 mesh quad。

alpha 是通过顶点色传入：

```text
vertexColor.a = color.a * currentAlpha
```

`FloatText.shader` 中会采样 atlas 并乘顶点色：

```hlsl
output.color = input.color * _Tint * UNITY_ACCESS_INSTANCED_PROP(Props, _InstanceColor);
...
half4 atlasColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
return half4(atlasColor.rgb * input.color.rgb, atlasColor.a * input.color.a);
```

所以 FloatText 的淡出不需要改材质，也不需要每个字符一个 Renderer。

## 8. 当前方案的性能特点

优点：

- 多个 Element 合并到一个 MeshRenderer。
- 共享一个 material/texture。
- 每个 Element 可以保留自己的 Transform。
- 适合 GameObject v1 验证，后续能迁移到 Manager。
- 每个 FloatText 只在 `Play` 时解析字符串和构建 quad 列表。

当前限制：

- `MeshPlayer` 每帧会 `Rebuild` 整个 Mesh。
- 内部仍用 `List<T>` 收集顶点，容量不足时可能扩容。
- `FloatTextElement.ResolveGlyphs` 当前每次 `Play` 会创建临时 `List<ResolvedGlyph>`。
- 所有写入同一个 MeshPlayer 的元素必须共用材质和贴图。
- 如果 Element 数量极大，GameObject/MonoBehaviour 本身仍会成为成本。

因此当前版本适合：

```text
验证流程
中等数量飘字/HUD
为后续 Manager 化铺路
```

正式超高密度版本应该继续演进：

```text
Element 数据结构化
Manager 统一 tick
预分配数组 buffer
按 material/texture/sorting 分批
必要时 DrawMesh 或 Graphics.DrawMeshInstanced
```

## 9. 如何扩展新的 Element

新增一个 Element 的基本步骤：

```text
1. 选择继承 MeshElement 还是 TickMeshElement
2. 准备自己的数据和贴图 UV
3. 实现 CanWriteQuads
4. 实现 WriteQuads
5. 确认和所属 MeshPlayer 使用同一 material/texture
```

如果元素没有动画，例如静态图标，继承 `MeshElement`。

如果元素有生命周期、淡出、移动、闪烁，继承 `TickMeshElement`。

## 10. 示例：扩展一个 HudIconElement

假设要做一个状态图标，每个图标一个 quad：

```csharp
public sealed class HudIconElement : MeshElement
{
    [SerializeField] private Vector2 size = Vector2.one;
    [SerializeField] private Vector4 uvRect = new Vector4(0f, 0f, 1f, 1f);
    [SerializeField] private Color color = Color.white;

    public override void WriteQuads(MeshQuadWriter writer)
    {
        Vector2 half = size * 0.5f;
        writer.AddQuad(
            transform.localToWorldMatrix,
            -half.x,
            -half.y,
            half.x,
            half.y,
            uvRect,
            (Color32)color);
    }
}
```

这个 Element 只负责写自己的 quad。材质和贴图仍由 MeshPlayer 管。

## 11. 示例：扩展一个 HealthBarElement

血条可以由两个或三个 quad 组成：

```text
背景 quad
填充 quad
边框 quad 可选
```

核心写法：

```csharp
public sealed class HealthBarElement : MeshElement
{
    [SerializeField] private Vector2 size = new Vector2(1.2f, 0.16f);
    [SerializeField, Range(0f, 1f)] private float normalizedHp = 1f;
    [SerializeField] private Vector4 backgroundUv;
    [SerializeField] private Vector4 fillUv;
    [SerializeField] private Color color = Color.white;

    public override void WriteQuads(MeshQuadWriter writer)
    {
        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Vector2 half = size * 0.5f;

        writer.AddQuad(localToWorld, -half.x, -half.y, half.x, half.y, backgroundUv, (Color32)Color.white);

        float fillWidth = size.x * Mathf.Clamp01(normalizedHp);
        float fillXMax = -half.x + fillWidth;
        writer.AddQuad(localToWorld, -half.x, -half.y, fillXMax, half.y, fillUv, (Color32)color);
    }
}
```

注意如果 fill quad 裁剪宽度，UV 也应该按比例裁剪，否则贴图会被横向压缩。第一版如果 fill 是纯色图，可以接受压缩；正式版要同步修改 `fillUv.z`。

## 12. 示例：扩展一个 TimedIconElement

带生命周期的图标可以继承 `TickMeshElement`：

```csharp
public sealed class TimedIconElement : TickMeshElement
{
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private Vector2 size = Vector2.one;
    [SerializeField] private Vector4 uvRect = new Vector4(0f, 0f, 1f, 1f);
    [SerializeField] private Color color = Color.white;

    private float elapsed;
    private float alpha = 1f;
    private bool playing;

    public override bool CanWriteQuads => base.CanWriteQuads && playing;
    protected override bool IsRuntimeTickActive => playing;

    public void Play()
    {
        elapsed = 0f;
        alpha = 1f;
        playing = true;
    }

    protected override void Tick(float deltaTime)
    {
        elapsed += deltaTime;
        float t = lifetime > 0f ? Mathf.Clamp01(elapsed / lifetime) : 1f;
        alpha = 1f - t;
        if (elapsed >= lifetime)
        {
            playing = false;
        }
    }

    public override void WriteQuads(MeshQuadWriter writer)
    {
        Color c = color;
        c.a *= alpha;
        Vector2 half = size * 0.5f;
        writer.AddQuad(transform.localToWorldMatrix, -half.x, -half.y, half.x, half.y, uvRect, (Color32)c);
    }
}
```

## 13. 扩展 Element 的设计原则

扩展新 Element 时遵守这些规则：

- Element 只负责生成 quad，不直接操作 Mesh。
- Element 不复制材质，不创建材质实例。
- Element 不直接改 MeshPlayer 的 mesh。
- 同一个 MeshPlayer 下的 Element 必须共用 atlas/material/sorting。
- 高频 Element 尽量避免在 `WriteQuads` 里分配临时对象。
- 字符串解析、布局计算这类工作尽量放在状态变化时做，不要每帧做。
- 真正超大量对象后，Element 逻辑应迁移到 Manager 的数组数据。

## 14. 从 MeshPlayer 迁移到 Manager 的方向

当前结构已经把渲染输出统一成：

```text
AddQuad(localToWorld, xMin, yMin, xMax, yMax, uvRect, color)
```

这就是未来 Manager 化的核心接口。

迁移方向：

```text
GameObject Element
  -> ElementData struct
  -> Manager tick
  -> Manager 写 MeshQuadWriter 或数组 buffer
  -> 按 material/texture/sorting 分组提交
```

例如 FloatText 可以迁移为：

```text
FloatTextElement GameObject
  -> FloatTextInstance 数据
    text/quads/position/time/alpha/state
  -> FloatTextManager 批量更新
  -> FloatTextManager 统一写大 Mesh
```

这样最终可以减少：

- GameObject 数量。
- MonoBehaviour Update。
- Transform 访问。
- 每帧 Rebuild 的分散调用。

但 v1 保留 GameObject 是合理的，因为它让资源、UV、排版、动效和接口更容易验证。

## 15. 小结

`MeshPlayer` 当前方案的本质是：

```text
用 GameObject 保留编辑和调试体验
用 MeshElement 抽象“可写 quad 的元素”
用 MeshQuadWriter 把多个元素合成一个动态 Mesh
用 shared material/texture/MPB 避免材质实例化
```

`FloatTextElement` 是这个方案的典型例子：

```text
字符串 -> glyph -> quad 列表 -> WriteQuads -> MeshPlayer -> 一个 MeshRenderer 渲染
```

后续血条、名字、状态图标、技能指示图形都可以沿用同一条链路。最终如果同屏数量继续上升，再把 Element 从 GameObject 迁移到 Manager 数据结构即可。
