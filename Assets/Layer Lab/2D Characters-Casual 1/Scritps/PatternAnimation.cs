using UnityEngine;
using UnityEngine.UI;

namespace LayerLab.Casual2DCharacter
{
    public class PatternAnimation : MonoBehaviour
    {
        [SerializeField] private float speedX = 0.1f;
        [SerializeField] private float speedY = 0.1f;
        private RawImage rawImage;
        private Rect imgUVRect;

        private void Awake()
        {
            rawImage = GetComponent<RawImage>();
            imgUVRect = rawImage.uvRect;
        }

        private void OnEnable()
        {
            rawImage.uvRect = Rect.zero;
        }

        private void Update()
        {
            imgUVRect.x += speedX * Time.deltaTime;
            imgUVRect.y -= speedY * Time.deltaTime;
            rawImage.uvRect = imgUVRect;
        }
    }
}