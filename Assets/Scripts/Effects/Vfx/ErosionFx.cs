using System;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 한 장의 실루엣을 노이즈의 단단한 경계로 드러내거나 지운다.
    /// 셰이더 경계는 흰 코어→원소색→짙은 림의 세 밴드라 부드러운 알파 페이드와 달리
    /// 그림이 깎여 나가는 방향을 프레임마다 또렷하게 남긴다.
    /// </summary>
    public sealed class ErosionFx : MonoBehaviour
    {
        private static Texture2D _noise;

        private Material _material;
        private SpriteRenderer _renderer;
        private float _delay;
        private float _reveal;
        private float _hold;
        private float _dissolve;
        private float _age;
        private bool _destroyRoot;
        private Action _onComplete;

        public static ErosionFx Attach(SpriteRenderer renderer, float delay, float reveal, float hold,
            float dissolve, Color core, Color band, Color rim, float radial = 0.35f,
            Vector2 uvScroll = default, bool destroyRoot = false, Action onComplete = null)
        {
            if (renderer == null) return null;
            Shader shader = Shader.Find("NeverTheLast/Vfx/HardEdgeErosion");
            if (shader == null || !shader.isSupported) return null;

            var fx = renderer.gameObject.AddComponent<ErosionFx>();
            fx._renderer = renderer;
            fx._material = new Material(shader)
            {
                name = "Vfx_HardEdgeErosion_Runtime",
                hideFlags = HideFlags.HideAndDontSave,
            };
            fx._material.SetTexture("_NoiseTex", Noise);
            fx._material.SetColor("_CoreColor", core);
            fx._material.SetColor("_BandColor", band);
            fx._material.SetColor("_RimColor", rim);
            fx._material.SetFloat("_Radial", Mathf.Clamp01(radial));
            fx._material.SetVector("_UvScroll", new Vector4(uvScroll.x, uvScroll.y, 0f, 0f));
            fx._material.SetFloat("_Dissolve", reveal > 0f ? 1f : 0f);
            renderer.material = fx._material;
            fx._delay = Mathf.Max(0f, delay);
            fx._reveal = Mathf.Max(0f, reveal);
            fx._hold = Mathf.Max(0f, hold);
            fx._dissolve = Mathf.Max(0.01f, dissolve);
            fx._destroyRoot = destroyRoot;
            fx._onComplete = onComplete;
            return fx;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age - _delay;
            if (t < 0f) return;

            float amount;
            if (_reveal > 0f && t < _reveal)
            {
                amount = 1f - Mathf.Clamp01(t / _reveal);
            }
            else
            {
                t -= _reveal;
                if (t < _hold) amount = 0f;
                else amount = Mathf.Clamp01((t - _hold) / _dissolve);
            }
            _material.SetFloat("_Dissolve", amount);

            if (amount < 1f || t < _hold) return;
            Action complete = _onComplete;
            _onComplete = null;
            complete?.Invoke();
            if (_renderer != null) _renderer.enabled = false;
            if (_destroyRoot) Destroy(transform.root.gameObject);
            else Destroy(this);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        private static Texture2D Noise
        {
            get
            {
                if (_noise != null) return _noise;
                const int size = 64;
                _noise = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Vfx_ErosionNoise",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float broad = Mathf.PerlinNoise(x * 0.087f + 17.3f, y * 0.087f + 9.1f);
                        float fine = Mathf.PerlinNoise(x * 0.231f + 41.7f, y * 0.231f + 3.8f);
                        float value = Mathf.Clamp01(broad * 0.72f + fine * 0.28f);
                        pixels[y * size + x] = new Color(value, value, value, 1f);
                    }
                }
                _noise.SetPixels(pixels);
                _noise.Apply(false, true);
                return _noise;
            }
        }
    }
}
