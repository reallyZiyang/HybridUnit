using UnityEngine;
using UnityEngine.UI;

public class SimpleUIMask : MonoBehaviour
{
    [SerializeField] private Texture2D maskSprite;
    [SerializeField] private Vector2 maskSize = new Vector2(300, 300);
    
    private void Start()
    {
        SetupMaskEffect();
    }
    
    private void SetupMaskEffect()
    {
        // 获取当前RawImage
        RawImage sceneImage = GetComponent<RawImage>();
        
        // 创建遮罩对象作为子物体
        GameObject maskObject = new GameObject("Mask");
        maskObject.transform.SetParent(transform);
        maskObject.transform.localPosition = Vector3.zero;
        maskObject.transform.localScale = Vector3.one;
        
        // 添加Image组件
        Image maskImage = maskObject.AddComponent<Image>();
        maskImage.rectTransform.sizeDelta = maskSize;
        
        if (maskSprite != null)
        {
            Sprite sprite = Sprite.Create(maskSprite, 
                new Rect(0, 0, maskSprite.width, maskSprite.height), 
                new Vector2(0.5f, 0.5f));
            maskImage.sprite = sprite;
            maskImage.type = Image.Type.Simple;
        }
        
        // 设置遮罩材质（写入模板）
        Material maskMat = new Material(Shader.Find("UI/Default"));
        SetupStencilForMask(maskMat);
        maskImage.material = maskMat;
        
        // 设置场景显示材质（读取模板）
        Material sceneMat = new Material(Shader.Find("UI/Default"));
        SetupStencilForScene(sceneMat);
        sceneImage.material = sceneMat;
    }
    
    private void SetupStencilForMask(Material material)
    {
        // 设置模板参数：在透明区域写入模板值1
        material.SetInt("_Stencil", 1);
        material.SetInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Always);
        material.SetInt("_StencilOp", (int)UnityEngine.Rendering.StencilOp.Replace);
        material.SetInt("_StencilWriteMask", 255);
        material.SetInt("_StencilReadMask", 255);
        
        // 启用Alpha测试
        material.EnableKeyword("UNITY_UI_ALPHACLIP");
        material.SetFloat("_ClipRect", 1);
    }
    
    private void SetupStencilForScene(Material material)
    {
        // 设置模板参数：只在模板值等于1的区域显示
        material.SetInt("_Stencil", 1);
        material.SetInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Equal);
        material.SetInt("_StencilOp", (int)UnityEngine.Rendering.StencilOp.Keep);
        material.SetInt("_StencilWriteMask", 0);
        material.SetInt("_StencilReadMask", 255);
        
        // 确保正常渲染
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        material.SetInt("_ColorMask", 15); // 写入所有颜色通道
    }
}