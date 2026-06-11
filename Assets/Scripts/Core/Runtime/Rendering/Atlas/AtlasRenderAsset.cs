using UnityEngine;
using UnityEngine.U2D;

[CreateAssetMenu(fileName = "AtlasRenderAsset", menuName = "Hybrid/Rendering/Atlas Render Asset")]
public sealed class AtlasRenderAsset : BattleRenderAssetBase
{
    public SpriteAtlas spriteAtlas;
    public string spriteName;
    public Sprite sprite;
    public Texture2D atlas;
    public Material material;
    public Vector4 uvRect = new Vector4(0f, 0f, 1f, 1f);
    public Vector2 size = Vector2.one;
    public Color color = Color.white;
}
