using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class WeeklyMilestoneFillFx : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private int textureWidth = 96;
        [SerializeField] private int textureHeight = 8;
        [SerializeField] private Gradient horizontalGradient;
        [SerializeField] private float topHighlightStrength = 0.2f;
        [SerializeField] private float bottomShadowStrength = 0.15f;
        [SerializeField] private float sheenStrength = 0.15f;
        [SerializeField] private float sheenPosition = 0.7f;
        [SerializeField] private float sheenWidth = 0.12f;

        private Texture2D generatedTexture;
        private Sprite generatedSprite;

        private void Awake()
        {
            EnsureReferences();
            EnsureGradient();
            BuildFillTexture();
        }

        private void OnDestroy()
        {
            if (generatedSprite != null)
            {
                Destroy(generatedSprite);
                generatedSprite = null;
            }

            if (generatedTexture != null)
            {
                Destroy(generatedTexture);
                generatedTexture = null;
            }
        }

        private void EnsureReferences()
        {
            if (fillImage != null) return;

            var slider = GetComponent<Slider>();
            if (slider == null)
            {
                slider = GetComponentInParent<Slider>();
            }

            if (slider != null && slider.fillRect != null)
            {
                fillImage = slider.fillRect.GetComponent<Image>();
            }

            if (fillImage == null)
            {
                var tf = transform.Find("Fill Area/Fill");
                if (tf != null)
                {
                    fillImage = tf.GetComponent<Image>();
                }
            }
        }

        private void EnsureGradient()
        {
            if (horizontalGradient != null && horizontalGradient.colorKeys.Length > 0) return;

            horizontalGradient = new Gradient();
            horizontalGradient.colorKeys = new[]
            {
                new GradientColorKey(new Color(0.18f, 0.76f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.3f, 1f, 0.65f, 1f), 1f)
            };
            horizontalGradient.alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            };
        }

        private void BuildFillTexture()
        {
            if (fillImage == null) return;

            int width = Mathf.Max(16, textureWidth);
            int height = Mathf.Max(4, textureHeight);

            generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            generatedTexture.wrapMode = TextureWrapMode.Clamp;
            generatedTexture.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < width; x++)
            {
                float u = width > 1 ? x / (float)(width - 1) : 0f;
                Color baseColor = horizontalGradient.Evaluate(u);
                float sheen = Mathf.Exp(-Mathf.Pow((u - sheenPosition) / Mathf.Max(0.0001f, sheenWidth), 2f)) * sheenStrength;

                for (int y = 0; y < height; y++)
                {
                    float v = height > 1 ? y / (float)(height - 1) : 0f;
                    float top = Mathf.SmoothStep(0.7f, 1f, v) * topHighlightStrength;
                    float bottom = Mathf.SmoothStep(0.3f, 0f, v) * bottomShadowStrength;

                    Color shaded = baseColor;
                    shaded.r = Mathf.Clamp01(shaded.r + top - bottom);
                    shaded.g = Mathf.Clamp01(shaded.g + top - bottom);
                    shaded.b = Mathf.Clamp01(shaded.b + top - bottom);
                    shaded.a = 1f;

                    Color final = shaded + new Color(sheen, sheen, sheen, 0f);
                    final.a = 1f;

                    generatedTexture.SetPixel(x, y, final);
                }
            }

            generatedTexture.Apply(false, true);

            generatedSprite = Sprite.Create(generatedTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            fillImage.sprite = generatedSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillCenter = true;
        }
    }
}
