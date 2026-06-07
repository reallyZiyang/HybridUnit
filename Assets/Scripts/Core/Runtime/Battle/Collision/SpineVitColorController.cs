using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpineVitColorController : MonoBehaviour
{
    private BakedSpineVitPlayer player;
    private Color originalColor = Color.white;
    private bool hasOriginalColor;

    public BakedSpineVitPlayer Player
    {
        get
        {
            EnsurePlayer();
            return player;
        }
    }

    public static SpineVitColorController GetOrAdd(BakedSpineVitPlayer targetPlayer)
    {
        if (targetPlayer == null)
        {
            return null;
        }

        if (!targetPlayer.TryGetComponent(out SpineVitColorController controller))
        {
            controller = targetPlayer.gameObject.AddComponent<SpineVitColorController>();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                controller.hideFlags = HideFlags.DontSaveInEditor;
            }
#endif
        }

        controller.Initialize(targetPlayer);
        return controller;
    }

    public void SetColor(Color color)
    {
        EnsurePlayer();
        if (player == null)
        {
            return;
        }

        CaptureOriginalColor();
        player.SetInstanceColor(color);
    }

    public void RestoreOriginalColor()
    {
        EnsurePlayer();
        if (player != null && hasOriginalColor)
        {
            player.SetInstanceColor(originalColor);
        }
    }

    private void Awake()
    {
        EnsurePlayer();
        CaptureOriginalColor();
    }

    private void Initialize(BakedSpineVitPlayer targetPlayer)
    {
        player = targetPlayer;
        CaptureOriginalColor();
    }

    private void EnsurePlayer()
    {
        if (player == null)
        {
            player = GetComponent<BakedSpineVitPlayer>();
        }
    }

    private void CaptureOriginalColor()
    {
        if (hasOriginalColor || player == null)
        {
            return;
        }

        originalColor = player.InstanceColor;
        hasOriginalColor = true;
    }
}
