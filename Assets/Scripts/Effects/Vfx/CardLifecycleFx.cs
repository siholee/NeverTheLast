using System;
using BaseClasses;
using Entities;
using Entities.View;
using TMPro;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>칸이 비워지기 전에 원화 사본을 떠서 공통 퇴장 연출을 끝까지 보존한다.</summary>
    public static class CardLifecycleFx
    {
        public const float NormalDepartureDuration = 0.56f;
        public const float BossDepartureDuration = 0.80f;

        public static float DepartureDuration(Unit unit)
            => unit != null && unit.IsEnemy &&
               string.Equals(unit.UnitTier, "boss", StringComparison.OrdinalIgnoreCase)
                ? BossDepartureDuration : NormalDepartureDuration;

        public static void PlayDestroy(Unit unit)
        {
            Managers.AudioManager.PlayCue("common.death");
            Cell cell = unit != null ? unit.currentCell : null;
            if (cell == null || cell.yPos <= 0) return;
            UnitCardView view = cell.Card;
            if (view == null || view.Body == null || !view.Body.gameObject.activeInHierarchy) return;

            // 판정상 점유 해제는 즉시 유지하고, 표시 재배치만 퇴장 연출 뒤로 미룬다.
            Managers.GridManager.Instance?.HoldFieldLayout(DepartureDuration(unit));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("death_start");
#endif

            var go = new GameObject($"CardDeparture_{unit.UnitName}");
            go.AddComponent<CardShatter>().Begin(view, unit);
        }
    }

    /// <summary>
    /// 짧은 백색 피드백 뒤 카드 전체가 밝아지며 위로 떠서 사라지는 공통 퇴장.
    /// 사망은 이미 충분히 강한 사건이므로 파편·폭발·화면 흔들림을 더하지 않는다.
    /// </summary>
    public sealed class CardShatter : MonoBehaviour
    {
        private const float NormalFlashTime = 0.065f;
        private const float NormalFadeTime = 0.495f;
        private const float BossFlashTime = 0.075f;
        private const float BossFadeTime = 0.725f;
        private const int SortBase = VfxSpace.OverlaySortingOrder - 280;

        private GameObject _snapshot;
        private Transform _ghost;
        private Vector3 _startPosition;
        private Vector3 _startScale;
        private SpriteRenderer[] _sprites = Array.Empty<SpriteRenderer>();
        private Color[] _spriteColors = Array.Empty<Color>();
        private CanvasGroup[] _groups = Array.Empty<CanvasGroup>();
        private float[] _groupAlphas = Array.Empty<float>();
        private TMP_Text[] _worldLabels = Array.Empty<TMP_Text>();
        private Color[] _labelColors = Array.Empty<Color>();
        private SpriteRenderer _portrait;
        private SpriteRenderer _flash;
        private float _age;
        private float _cardSize;
        private int _layer;
        private bool _boss;
        private Vector3 _center;
        private Color _moteColor;
        private bool _motesEmitted;

        public void Begin(UnitCardView view, Unit unit)
        {
            _boss = unit != null && unit.IsEnemy &&
                    string.Equals(unit.UnitTier, "boss", StringComparison.OrdinalIgnoreCase);
            _cardSize = view.CardSize;
            _layer = view.LayerId;
            _center = view.Body.position;
            _center.z = 0f;
            _moteColor = unit != null && unit.IsEnemy
                ? new Color(0.92f, 0.78f, 1f, 1f)
                : new Color(0.76f, 0.89f, 1f, 1f);

            BuildGhost(view);
        }

        private void BuildGhost(UnitCardView view)
        {
            _snapshot = view.CreateDeathSnapshot();
            if (_snapshot == null)
            {
                Destroy(gameObject);
                return;
            }

            _ghost = _snapshot.transform;
            _ghost.SetParent(transform, true);
            _startPosition = _ghost.position;
            _startScale = _ghost.localScale;

            _sprites = _snapshot.GetComponentsInChildren<SpriteRenderer>(true);
            _spriteColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
            {
                _spriteColors[i] = _sprites[i].color;
                if (string.Equals(_sprites[i].gameObject.name, "Portrait", StringComparison.Ordinal))
                    _portrait = _sprites[i];
            }

            _groups = _snapshot.GetComponentsInChildren<CanvasGroup>(true);
            _groupAlphas = new float[_groups.Length];
            for (int i = 0; i < _groups.Length; i++) _groupAlphas[i] = _groups[i].alpha;

            TMP_Text[] labels = _snapshot.GetComponentsInChildren<TMP_Text>(true);
            int worldLabelCount = 0;
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] is not TextMeshProUGUI) worldLabelCount++;
            _worldLabels = new TMP_Text[worldLabelCount];
            _labelColors = new Color[worldLabelCount];
            for (int labelIndex = 0, target = 0; labelIndex < labels.Length; labelIndex++)
            {
                if (labels[labelIndex] is TextMeshProUGUI) continue;
                _worldLabels[target] = labels[labelIndex];
                _labelColors[target] = labels[labelIndex].color;
                target++;
            }

            SpriteRenderer sourceRenderer = view.PortraitRenderer;
            Sprite sprite = sourceRenderer != null ? sourceRenderer.sprite : null;
            if (sprite == null) return;

            _flash = VfxSpace.NewSprite(transform, "WhiteSilhouetteFlash", sprite, Color.clear,
                _layer, SortBase + 8, additive: true);
            _flash.transform.position = sourceRenderer.transform.position;
            _flash.transform.rotation = sourceRenderer.transform.rotation;
            _flash.transform.localScale = sourceRenderer.transform.lossyScale;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float flashTime = _boss ? BossFlashTime : NormalFlashTime;
            float fadeTime = _boss ? BossFadeTime : NormalFadeTime;
            float flash = _age < flashTime ? Mathf.Sin(Mathf.Clamp01(_age / flashTime) * Mathf.PI) : 0f;
            float fade = Mathf.Clamp01((_age - flashTime) / fadeTime);
            float eased = fade * fade * (3f - 2f * fade);
            float opacity = 1f - eased;

            if (_flash != null)
                _flash.color = new Color(1f, 1f, 1f, flash * (_boss ? 0.32f : 0.26f));

            if (_ghost != null)
            {
                float rise = (_boss ? 0.070f : 0.055f) * _cardSize;
                _ghost.position = _startPosition + Vector3.up * (rise * eased);
                _ghost.localScale = _startScale;
                ApplySnapshot(opacity, fade, flash);
            }

            if (!_motesEmitted && _age >= flashTime)
            {
                _motesEmitted = true;
                EmitLightMotes(_boss ? 20 : 12);
            }

            if (_age >= flashTime + fadeTime)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Core.VerifyEvents.Raise("death_complete");
#endif
                Destroy(gameObject);
            }
        }

        private void ApplySnapshot(float opacity, float fade, float flash)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                SpriteRenderer renderer = _sprites[i];
                if (renderer == null) continue;
                Color color = _spriteColors[i];
                if (ReferenceEquals(renderer, _portrait))
                {
                    float pale = Mathf.Clamp01(fade * 0.62f + flash * 0.22f);
                    color.r = Mathf.Lerp(color.r, 1f, pale);
                    color.g = Mathf.Lerp(color.g, 1f, pale);
                    color.b = Mathf.Lerp(color.b, 1f, pale);
                }
                color.a = _spriteColors[i].a * opacity;
                renderer.color = color;
            }

            for (int i = 0; i < _groups.Length; i++)
                if (_groups[i] != null) _groups[i].alpha = _groupAlphas[i] * opacity;

            for (int i = 0; i < _worldLabels.Length; i++)
            {
                if (_worldLabels[i] == null) continue;
                Color color = _labelColors[i];
                color.a *= opacity;
                _worldLabels[i].color = color;
            }
        }

        private void EmitLightMotes(int count)
        {
            Color light = Color.Lerp(_moteColor, Color.white, 0.62f);
            VfxParticles.Burst(_center + Vector3.up * (_cardSize * 0.02f), new ParticleSpec
            {
                Sprite = VfxSpace.SoftDot,
                Additive = true,
                StartColor = new Color(light.r, light.g, light.b, 0.72f),
                EndColor = new Color(_moteColor.r, _moteColor.g, _moteColor.b, 0f),
                Lifetime = _boss ? new Vector2(0.34f, 0.58f) : new Vector2(0.24f, 0.42f),
                Speed = new Vector2(0.20f * _cardSize, 0.62f * _cardSize),
                Angle = 90f,
                Spread = 42f,
                StartSize = new Vector2(0.010f * _cardSize, 0.024f * _cardSize),
                EndSizeScale = 0.42f,
                Gravity = -0.05f * _cardSize,
                Drag = 1.7f,
                SpawnRadius = 0.28f * _cardSize,
                FadeIn = 0.10f,
            }, count, _layer, SortBase + 7);
        }
    }
}
