using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>시전자에서 대상으로 향하는 전투 연출의 공통 방향 정보.</summary>
    public readonly struct VfxAim
    {
        public readonly Vector3 Direction;
        public readonly float AngleDegrees;
        public readonly bool FacingRight;

        public VfxAim(Vector3 direction)
        {
            direction.z = 0f;
            Direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.right;
            AngleDegrees = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
            FacingRight = Direction.x >= 0f;
        }

        /// <summary>원화가 기본적으로 바라보는 방향과 실제 대상 방향이 다를 때만 뒤집는다.</summary>
        public bool MirrorHorizontal(bool sourceFacesRight) => FacingRight != sourceFacesRight;

        /// <summary>기본 기울기를 대상 방향의 위아래 각도에 더하고 좌우 진영에서는 대칭으로 보존한다.</summary>
        public float StrokeRotation(float tiltDegrees)
            => AngleDegrees + (FacingRight ? -Mathf.Abs(tiltDegrees) : Mathf.Abs(tiltDegrees));
    }

    public readonly struct VfxTargetHit
    {
        public readonly Unit Target;
        public readonly float DelaySeconds;
        public readonly int Index;

        public VfxTargetHit(Unit target, float delaySeconds, int index)
        {
            Target = target;
            DelaySeconds = delaySeconds;
            Index = index;
        }
    }

    /// <summary>
    /// 전투 연출이 공통으로 쓰는 좌표 · 정렬 · 재질 · 절차 텍스처.
    ///
    /// 2026-09 개편으로 옛 이펙트(베기 · 투사체 · 원소 프리팹)를 전부 걷어내고 새로 짓는다.
    /// 기준은 하스스톤이다 — 카드가 직접 날아가 부딪히고, 투사체는 카드 정중앙에서 출발해
    /// 카드 정중앙에 꽂힌다. 그래서 "유닛의 화면 위치"는 언제나 카드의 중심 하나로 통일한다.
    /// </summary>
    public static class VfxSpace
    {
        /// <summary>카드보다 확실히 위에 그릴 연출의 정렬 순서. 행 깊이(×10)를 넉넉히 넘긴다.</summary>
        public const int OverlaySortingOrder = 1000;

        /// <summary>날아가는 카드가 쓰는 정렬 순서. 연출보다 아래, 다른 카드보다는 위다.</summary>
        public const int FlyingCardSortingOrder = 600;

        private const string CombatVfxSortingLayer = "CombatVfx";

        private static readonly Dictionary<string, Sprite> Sprites = new();
        private static Material _additive;
        private static Material _alpha;
        private static Material _compositeAlpha;
        private static Material _luminanceGlow;

        /// <summary>
        /// 유닛 카드의 <b>정중앙</b>. 투사체의 출발점과 착탄점이 모두 이것이다.
        /// 카드가 날아가는 도중이면 날아가는 자리를 돌려준다.
        /// </summary>
        public static Vector3 PortraitPoint(Unit unit)
        {
            var view = CardMotion.ViewOf(unit);
            Vector3 point = view != null ? view.PortraitFocusWorldPoint : WorldPoint(unit);
            point.z = 0f;
            return point;
        }

        public static Vector3 WorldPoint(Unit unit)
        {
            if (unit == null) return Vector3.zero;
            if (unit.currentCell != null)
            {
                Vector3 center = unit.currentCell.CardCenter;
                center.z = 0f;
                return center;
            }
            return unit.transform.position;
        }

        /// <summary>유닛을 기준으로 만드는 전투 연출의 전용 앞 정렬 레이어.</summary>
        public static int SortingLayer(Unit unit)
        {
            return ResolveSortingLayer(0);
        }

        /// <summary>
        /// 모든 전투 연출은 카드·이웃 카드·월드 HUD와 분리된 전용 레이어를 쓴다.
        /// 레이어가 없는 오래된 씬에서는 전달받은 레이어로 물러나되 새 프로젝트 설정에서는 항상 CombatVfx다.
        /// </summary>
        public static int ResolveSortingLayer(int fallback)
        {
            int id = UnityEngine.SortingLayer.NameToID(CombatVfxSortingLayer);
            return id != 0 ? id : fallback;
        }

        public static VfxAim AimFrom(Unit caster, Unit target)
            => AimBetween(WorldPoint(caster), WorldPoint(target));

        public static VfxAim AimBetween(Vector3 from, Vector3 to) => new(to - from);

        /// <summary>
        /// 한 번의 시전 뒤 대상별 피격만 아주 짧은 시간차로 열 순서를 만든다.
        /// 대상 사이를 잇는 좌표·길이·궤적은 만들지 않아 중간 칼날이나 관통선이 생길 여지를 없앤다.
        /// </summary>
        public static IReadOnlyList<VfxTargetHit> BuildTargetHitSequence(IReadOnlyList<Unit> targets,
            float totalSpreadSeconds = 0.05f)
        {
            var alive = new List<Unit>();
            if (targets != null)
            {
                foreach (Unit target in targets)
                    if (target != null && target.isActive) alive.Add(target);
            }
            var hits = new List<VfxTargetHit>(alive.Count);
            float spread = Mathf.Max(0f, totalSpreadSeconds);
            for (int i = 0; i < alive.Count; i++)
            {
                float delay = alive.Count <= 1 ? 0f : spread * i / (alive.Count - 1f);
                hits.Add(new VfxTargetHit(alive[i], delay, i));
            }
            return hits;
        }

        public static IEnumerator ResolveTargetHits(IReadOnlyList<VfxTargetHit> hits,
            Action<VfxTargetHit> onHit)
        {
            if (hits == null || hits.Count == 0) yield break;
            float age = 0f;
            for (int i = 0; i < hits.Count; i++)
            {
                while (age < hits[i].DelaySeconds)
                {
                    age += Time.deltaTime;
                    yield return null;
                }
                onHit?.Invoke(hits[i]);
            }
        }

        /// <summary>카드 한 변. 연출 크기는 모두 이 값에 비례해 잡는다.</summary>
        public static float CardSize => Cell.CardSize;

        // ── 재질 ─────────────────────────────────────────────────────

        /// <summary>
        /// 빛(불꽃 · 섬광 · 잔광)용 가산 재질.
        ///
        /// 빌드에서 셰이더가 빠지지 않도록 Resources의 머티리얼을 원본으로 복제한다.
        /// 그 셰이더를 못 쓰는 환경이면 파티클 가산 셰이더, 그마저 없으면 알파로 물러난다.
        /// </summary>
        public static Material Additive
        {
            get
            {
                if (_additive != null) return _additive;
                Material template = Resources.Load<Material>("Materials/CombatVfxAdditive");
                if (template != null && template.shader != null && template.shader.isSupported)
                {
                    _additive = new Material(template) { name = "Vfx_Additive" };
                    if (_additive.HasProperty("_MainTex")) _additive.SetTexture("_MainTex", Texture2D.whiteTexture);
                }
                else
                {
                    Shader shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
                    _additive = new Material(shader) { name = "Vfx_Additive" };
                }
                _additive.hideFlags = HideFlags.HideAndDontSave;
                return _additive;
            }
        }

        /// <summary>돌 조각 · 먼지 · 파편처럼 <b>빛이 아닌</b> 물체용 알파 재질.</summary>
        public static Material Alpha
        {
            get
            {
                if (_alpha != null) return _alpha;
                _alpha = new Material(Shader.Find("Sprites/Default")) { name = "Vfx_Alpha" };
                _alpha.hideFlags = HideFlags.HideAndDontSave;
                return _alpha;
            }
        }

        /// <summary>
        /// 밝은 전장의 휘도에 맞춰 암부를 진주·금빛 쪽으로 살짝 들어 올리는 플립북 몸체 재질.
        /// 시트의 알파는 그대로 보존하고 색만 보정하므로 불투명 스티커처럼 뜨는 면을 줄인다.
        /// </summary>
        public static Material CompositeAlpha
        {
            get
            {
                if (_compositeAlpha != null) return _compositeAlpha;
                Shader shader = Resources.Load<Shader>("Shaders/VfxCompositeAlpha")
                                ?? Shader.Find("NeverTheLast/VfxCompositeAlpha")
                                ?? Shader.Find("Sprites/Default");
                _compositeAlpha = new Material(shader) { name = "Vfx_CompositeAlpha" };
                _compositeAlpha.hideFlags = HideFlags.HideAndDontSave;
                return _compositeAlpha;
            }
        }

        /// <summary>원본 시트의 밝은 픽셀만 임계 추출해 가산 코어로 겹치는 재질.</summary>
        public static Material LuminanceGlow
        {
            get
            {
                if (_luminanceGlow != null) return _luminanceGlow;
                Shader shader = Resources.Load<Shader>("Shaders/VfxLuminanceGlow")
                                ?? Shader.Find("NeverTheLast/VfxLuminanceGlow")
                                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                                ?? Shader.Find("Sprites/Default");
                _luminanceGlow = new Material(shader) { name = "Vfx_LuminanceGlow" };
                _luminanceGlow.hideFlags = HideFlags.HideAndDontSave;
                return _luminanceGlow;
            }
        }

        // ── 렌더러 ───────────────────────────────────────────────────

        public static SpriteRenderer NewSprite(Transform parent, string name, Sprite sprite, Color color,
            int sortingLayer, int order, bool additive = false)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerID = ResolveSortingLayer(sortingLayer);
            renderer.sortingOrder = order;
            if (additive) renderer.sharedMaterial = Additive;
            return renderer;
        }

        // ── 절차 텍스처 ──────────────────────────────────────────────
        // 에셋 없이 코드로 그린다. 한 번 만들면 캐시한다.

        /// <summary>가운데가 밝고 가장자리로 부드럽게 사라지는 원. 섬광 · 잔광 · 먼지에 쓴다.</summary>
        public static Sprite SoftDot => Cached("soft_dot", 64, (x, y) =>
        {
            float d = Mathf.Clamp01(Radius(x, y));
            float a = 1f - d;
            return new Color(1f, 1f, 1f, a * a);
        });

        /// <summary>세제곱으로 급히 사라지는 넓은 빛무리. 배경 · 큰 섬광에 쓴다.</summary>
        public static Sprite Glow => Cached("glow", 256, (x, y) =>
        {
            float a = Mathf.Clamp01(1f - Radius(x, y));
            return new Color(1f, 1f, 1f, a * a * a);
        });

        /// <summary>가장자리가 딱 떨어지는 원. 타격 순간의 섬광 고리 안쪽에 쓴다.</summary>
        public static Sprite HardDot => Cached("hard_dot", 64, (x, y) =>
        {
            float d = Radius(x, y);
            return new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 18f));
        });

        /// <summary>두께가 있는 고리. 충격파에 쓴다.</summary>
        public static Sprite Ring => Cached("ring", 128, (x, y) =>
        {
            float d = Radius(x, y);
            float band = 1f - Mathf.Abs(d - 0.86f) / 0.12f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(band) * Mathf.Clamp01(band));
        });

        /// <summary>
        /// 문양 고리 — 가는 두 겹 고리 사이에 눈금과 마름모가 박혀 있다. 천천히 돌려 '유지 중'을 알린다.
        /// </summary>
        public static Sprite RuneRing => Cached("rune_ring", 256, (x, y) =>
        {
            float d = Radius(x, y);
            float outer = Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) / 0.022f);
            float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.014f);
            float angle = Mathf.Atan2(y, x);
            // 눈금 36개
            float tick = 0f;
            if (d > 0.80f && d < 0.91f)
            {
                float phase = Mathf.Repeat(angle / (Mathf.PI * 2f) * 36f, 1f);
                tick = Mathf.Clamp01(1f - Mathf.Abs(phase - 0.5f) / 0.06f) * 0.8f;
            }
            // 마름모 6개
            float gem = 0f;
            float gemPhase = Mathf.Repeat(angle / (Mathf.PI * 2f) * 6f, 1f) - 0.5f;
            float gemAngle = gemPhase / 6f * Mathf.PI * 2f;
            float gx = Mathf.Abs(d - 0.855f) / 0.07f + Mathf.Abs(gemAngle) * d / 0.07f;
            if (gx < 1f) gem = Mathf.Clamp01((1f - gx) * 4f);
            float a = Mathf.Clamp01(Mathf.Max(Mathf.Max(outer, inner), Mathf.Max(tick, gem)));
            return new Color(1f, 1f, 1f, a);
        });

        /// <summary>
        /// 모난 바위 조각. 오각형을 비틀어 깎고 윗면을 밝게, 아랫면을 어둡게 칠해 입체감을 준다.
        /// 원소 색은 렌더러 색으로 입힌다 — 텍스처는 명암만 가진다.
        /// </summary>
        public static Sprite RockShard => Cached("rock_shard", 64, (x, y) =>
        {
            // 불규칙한 다각형: 각 꼭짓점의 반지름을 다르게 준다.
            float[] radii = { 0.92f, 0.70f, 0.98f, 0.62f, 0.85f, 0.74f };
            float angle = Mathf.Atan2(y, x);
            if (angle < 0f) angle += Mathf.PI * 2f;
            float sector = angle / (Mathf.PI * 2f) * radii.Length;
            int i = Mathf.FloorToInt(sector) % radii.Length;
            float t = sector - Mathf.Floor(sector);
            float edge = Mathf.Lerp(radii[i], radii[(i + 1) % radii.Length], t);
            float d = Mathf.Sqrt(x * x + y * y);
            if (d > edge) return Color.clear;

            // 왼쪽 위에서 빛이 온다. 윗면은 밝고, 아래로 갈수록 어둡다. 가장자리는 테두리로 조인다.
            float light = Mathf.Clamp01(0.62f + 0.38f * (y * 0.7f - x * 0.3f));
            float rim = Mathf.Clamp01((edge - d) * 14f);
            float shade = Mathf.Lerp(0.28f, light, rim);
            // 결정면 — 가운데를 가르는 밝은 선 하나가 돌이 아니라 '결정'처럼 보이게 한다.
            float facet = Mathf.Abs(x * 0.8f + y * 0.6f);
            if (facet < 0.05f) shade = Mathf.Min(1f, shade + 0.25f);
            return new Color(shade, shade, shade, 1f);
        });

        /// <summary>작은 모래 · 자갈 알갱이. 파편 파티클용이다.</summary>
        public static Sprite Pebble => Cached("pebble", 32, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x * 1.3f + y * y);
            if (d > 0.9f) return Color.clear;
            float shade = Mathf.Clamp01(0.55f + 0.45f * y);
            return new Color(shade, shade, shade, Mathf.Clamp01((0.9f - d) * 8f));
        });

        /// <summary>네 갈래로 뻗는 반짝임. 버프 · 결정 · 고정피해 표식에 쓴다.</summary>
        public static Sprite Sparkle => Cached("sparkle", 64, (x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float cross = Mathf.Max(Mathf.Clamp01(1f - ax * 9f) * (1f - ay), Mathf.Clamp01(1f - ay * 9f) * (1f - ax));
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y) * 3f);
            float a = Mathf.Clamp01(cross + core * core);
            return new Color(1f, 1f, 1f, a);
        });

        /// <summary>가로로 길게 늘어진 잔상. 빠르게 나는 물체의 꼬리에 쓴다.</summary>
        public static Sprite Streak => Cached("streak", 64, (x, y) =>
        {
            float u = (x + 1f) * 0.5f;        // 0 = 꼬리, 1 = 머리
            float thickness = Mathf.Lerp(0.05f, 0.5f, u);
            float a = Mathf.Clamp01(1f - Mathf.Abs(y) / thickness) * u * u;
            return new Color(1f, 1f, 1f, a);
        });

        /// <summary>카드 한 장 크기의 둥근 사각형 면. 카드의 섬광 · 균열 판에 쓴다.</summary>
        public static Sprite RoundedRect => Cached("rounded_rect", 128, (x, y) =>
        {
            const float r = 0.16f;
            float qx = Mathf.Max(Mathf.Abs(x) - (1f - r), 0f);
            float qy = Mathf.Max(Mathf.Abs(y) - (1f - r), 0f);
            float d = Mathf.Sqrt(qx * qx + qy * qy) - r;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(-d * 60f));
        });

        private static float Radius(float x, float y) => Mathf.Sqrt(x * x + y * y);

        /// <summary>
        /// 텍스처를 한 번 그려 스프라이트로 캐시한다. 좌표는 -1~1로 정규화해 넘긴다.
        /// 스프라이트 한 변은 월드 1이다 — 크기는 트랜스폼 배율로 잡는다.
        /// </summary>
        private static Sprite Cached(string key, int size, System.Func<float, float, Color> paint)
        {
            if (Sprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Vfx_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = (px + 0.5f) / size * 2f - 1f;
                    float y = (py + 0.5f) / size * 2f - 1f;
                    pixels[py * size + px] = paint(x, y);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[key] = sprite;
            return sprite;
        }
    }
}
