using System.Collections.Generic;
using Entities.Status;
using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>아이콘이 그리는 대상. 무엇에 관한 상태인지를 나타낸다.</summary>
    public enum StatusGlyph
    {
        Buff,       // 분류만 아는 이로운 상태
        Debuff,     // 분류만 아는 해로운 상태
        Attack,
        Defense,
        Speed,
        Crit,
        Shield,
        Heal,
        Poison,
        Burn,
        Bleed,
        Stun,
        Freeze,
        Airborne,
        Silence,
        Root,
        Taunt,
        Thorns,
        Mark,
    }

    /// <summary>오른쪽 아래 구석에 붙는 방향 표식. 올라갔는지 내려갔는지를 알린다.</summary>
    public enum StatusArrow
    {
        None,
        Up,
        Down,
    }

    /// <summary>
    /// 버프 · 디버프 · 모디파이어 아이콘을 <b>코드로 그려서</b> 만든다.
    /// <see cref="UIShapes"/>와 같은 방식이라 별도 이미지 파일이 필요 없다.
    ///
    /// 아이콘을 한 장씩 그려 모으면 선 굵기도 여백도 제각각이 되어 결국 따로 논다.
    /// 여기서는 <b>같은 격자 · 같은 선 굵기 · 같은 여백</b>으로 전부 찍어 내고,
    /// 뜻이 겹치는 것은 그림을 나누는 대신 <b>같은 그림 + 방향 표식</b>으로 구분한다.
    /// 공격 상승과 공격 하락이 같은 검 위에 ▲ · ▼만 달라지는 식이다.
    ///
    /// 색은 넣지 않는다. 흰색으로 그려 두고 쓰는 쪽에서 분류 색(이로움 초록 · 해로움 적색)을
    /// 곱한다 — 그래야 같은 아이콘을 아군 버프와 적 디버프 양쪽에 쓸 수 있다.
    /// </summary>
    public static class StatusIcons
    {
        /// <summary>아이콘 한 변(px). 카드 위에서는 10~14px로 줄어드니 단순해야 한다.</summary>
        private const int Size = 40;

        /// <summary>선 굵기(px). 모든 그림이 이 값 하나를 쓴다.</summary>
        private const float Stroke = 3.4f;

        /// <summary>방향 표식이 붙을 때 본 그림을 이만큼 줄이고 왼쪽 위로 물린다.</summary>
        private const float SubjectScale = 0.84f;

        private static readonly Dictionary<int, Sprite> Cache = new();

        /// <summary>
        /// 상태 이름 → 아이콘. 이름이 같으면 효과 구성도 같으므로 한 번만 판정한다.
        /// 상태 아이콘은 매 프레임 다시 그려지는 자리에서 불리기 때문에,
        /// 문자열 검사와 효과 훅 탐침을 그때마다 돌리면 안 된다.
        /// </summary>
        private static readonly Dictionary<string, (StatusGlyph Glyph, StatusArrow Arrow)> Resolved = new();

        // ── 바깥에서 쓰는 입구 ───────────────────────────────────────

        /// <summary>그림 + 방향으로 아이콘 하나를 얻는다. 같은 조합은 한 번만 그린다.</summary>
        public static Sprite Get(StatusGlyph glyph, StatusArrow arrow = StatusArrow.None)
        {
            int key = (int)glyph * 4 + (int)arrow;
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            var painter = new Painter();
            if (arrow != StatusArrow.None) painter.PushSubjectTransform();
            Draw(painter, glyph);
            if (arrow != StatusArrow.None)
            {
                painter.PopSubjectTransform();
                DrawArrow(painter, arrow);
            }

            Sprite sprite = painter.ToSprite($"icon_{glyph}_{arrow}");
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>상태 하나에 맞는 아이콘.</summary>
        public static Sprite For(UnitStatus status)
        {
            (StatusGlyph glyph, StatusArrow arrow) = Resolve(status);
            return Get(glyph, arrow);
        }

        /// <summary>
        /// 상태를 보고 어떤 그림을 쓸지 고른다.
        ///
        /// 상태마다 아이콘을 손으로 지정하게 하면 새 상태를 만들 때마다 빠뜨린다.
        /// 그래서 <b>효과 클래스 이름과 상태 이름</b>에서 유추한다 — 둘 다 이미 그 상태가
        /// 무엇을 하는지 말하고 있기 때문이다. 유추가 빗나가면 마지막에 분류만 보고
        /// 이로움/해로움 화살표로 떨어진다.
        /// </summary>
        public static (StatusGlyph Glyph, StatusArrow Arrow) Resolve(UnitStatus status)
        {
            if (status == null) return (StatusGlyph.Buff, StatusArrow.None);

            string cacheKey = $"{status.StatusId}|{status.StatusName}";
            if (Resolved.TryGetValue(cacheKey, out (StatusGlyph, StatusArrow) hit)) return hit;

            (StatusGlyph, StatusArrow) result = ResolveUncached(status);
            Resolved[cacheKey] = result;
            return result;
        }

        private static (StatusGlyph Glyph, StatusArrow Arrow) ResolveUncached(UnitStatus status)
        {
            bool good = status.IsBeneficial
                        || status.Category == BaseClasses.BaseEnums.StatusCategory.Positive;
            StatusArrow arrow = good ? StatusArrow.Up : StatusArrow.Down;

            string name = status.StatusName ?? "";
            string effects = EffectTypeNames(status);

            // ── 이름으로 바로 잡히는 것들 ──
            if (Has(name, "맹독", "중독", "독")) return (StatusGlyph.Poison, StatusArrow.None);
            if (Has(name, "화상", "발화", "연소")) return (StatusGlyph.Burn, StatusArrow.None);
            if (Has(name, "출혈", "열상")) return (StatusGlyph.Bleed, StatusArrow.None);
            if (Has(name, "빙결", "동결")) return (StatusGlyph.Freeze, StatusArrow.None);
            if (Has(name, "에어본", "공중")) return (StatusGlyph.Airborne, StatusArrow.None);
            if (Has(name, "기절", "스턴", "혼절")) return (StatusGlyph.Stun, StatusArrow.None);
            if (Has(name, "속박", "구속", "결박")) return (StatusGlyph.Root, StatusArrow.None);
            if (Has(name, "침묵", "행동불가")) return (StatusGlyph.Silence, StatusArrow.None);
            if (Has(name, "도발")) return (StatusGlyph.Taunt, StatusArrow.None);
            if (Has(name, "가시")) return (StatusGlyph.Thorns, StatusArrow.None);
            if (Has(name, "치유")) return (StatusGlyph.Heal, arrow);
            if (Has(name, "방어막", "보호막")) return (StatusGlyph.Shield, StatusArrow.None);

            // ── 효과 클래스 이름으로 잡히는 것들 ──
            if (Has(effects, "DamageOverTime")) return (StatusGlyph.Poison, StatusArrow.None);
            if (Has(effects, "HealingReduction")) return (StatusGlyph.Heal, StatusArrow.Down);
            if (Has(effects, "Taunt")) return (StatusGlyph.Taunt, StatusArrow.None);
            if (Has(effects, "Thorn")) return (StatusGlyph.Thorns, StatusArrow.None);
            if (Has(effects, "DisableNormalAttack", "ActionLock")) return (StatusGlyph.Silence, StatusArrow.None);
            if (Has(effects, "Shield")) return (StatusGlyph.Shield, StatusArrow.None);
            if (Has(effects, "CritChance", "CritMultiplier")) return (StatusGlyph.Crit, arrow);
            if (Has(effects, "CodeAcceleration", "ActionSpeed", "Speed")) return (StatusGlyph.Speed, arrow);
            if (Has(effects, "ReceivingDamage", "Armor", "Defense", "Bastion", "IronWall", "Bulwark"))
            {
                return (StatusGlyph.Defense, arrow);
            }

            if (Has(effects, "PrimaryStatBonus", "Attack", "Damage")) return (StatusGlyph.Attack, arrow);
            if (Has(effects, "Marker")) return (StatusGlyph.Mark, StatusArrow.None);

            // 이름으로도 클래스로도 못 잡았으면 효과가 실제로 무엇을 건드리는지 물어본다.
            if (Probe(status, arrow, out (StatusGlyph, StatusArrow) probed)) return probed;

            return (good ? StatusGlyph.Buff : StatusGlyph.Debuff, StatusArrow.None);
        }

        /// <summary>
        /// 효과의 스탯 질의 훅을 실제로 불러 보고, 값이 바뀌는 축으로 그림을 고른다.
        ///
        /// 이름 규칙은 새 상태가 생길 때마다 빠뜨리게 되지만 이 탐침은 빠뜨릴 수가 없다.
        /// <b>Unit 하나만 받는 훅</b>만 부른다 — DamageContext를 받는 훅은 문맥 없이 부르면
        /// 구현 쪽에서 터질 수 있다. 훅은 전부 스탯 계산에서 매 프레임 불리는 순수 조회다.
        /// </summary>
        private static bool Probe(UnitStatus status, StatusArrow arrow,
            out (StatusGlyph Glyph, StatusArrow Arrow) result)
        {
            result = default;

            Entities.Unit owner = status.Owner;
            if (owner == null || status.Effects == null) return false;


            foreach (UnitStatus.EffectInstance instance in status.Effects)
            {
                Effects.Base.BaseEffect effect = instance?.EffectObject;
                if (effect == null) continue;

                if (effect.IsDamageOverTime) return Take(out result, StatusGlyph.Poison, StatusArrow.None);
                if (effect.BlocksNormalAttack(owner)) return Take(out result, StatusGlyph.Silence, StatusArrow.None);
                if (effect.TargetPriorityAdditiveModifier(owner) > 0) return Take(out result, StatusGlyph.Taunt, StatusArrow.None);
                if (!Mathf.Approximately(effect.ShieldBonusAdditiveModifier(owner), 0f))
                {
                    return Take(out result, StatusGlyph.Shield, StatusArrow.None);
                }

                float healing = effect.HealingReceivedMultiplierModifier(owner);
                if (!Mathf.Approximately(healing, 1f))
                {
                    return Take(out result, StatusGlyph.Heal, healing > 1f ? StatusArrow.Up : StatusArrow.Down);
                }

                if (!Mathf.Approximately(effect.CritChanceAdditiveModifier(owner), 0f)
                    || !Mathf.Approximately(effect.CritMultiplierAdditiveModifier(owner), 0f))
                {
                    return Take(out result, StatusGlyph.Crit, arrow);
                }

                if (!Mathf.Approximately(effect.CodeAccelerationAdditiveModifier(owner), 0f)
                    || !Mathf.Approximately(effect.ManaRecoveryMultiplierModifier(owner), 1f))
                {
                    return Take(out result, StatusGlyph.Speed, arrow);
                }

                float taken = effect.ReceivingDamageModifier(owner);
                if (!Mathf.Approximately(taken, 1f))
                {
                    // 받는 피해가 줄면 방어가 오른 것이다. 화살표 방향이 뒤집힌다.
                    return Take(out result, StatusGlyph.Defense, taken < 1f ? StatusArrow.Up : StatusArrow.Down);
                }

                if (effect.DurabilityAdditiveModifier(owner) != 0
                    || !Mathf.Approximately(effect.MaxHpMultiplierModifier(owner), 1f))
                {
                    return Take(out result, StatusGlyph.Defense, arrow);
                }

                foreach (BaseClasses.BaseEnums.PrimaryStat stat in PrimaryStats)
                {
                    if (effect.PrimaryStatAdditiveModifier(owner, stat) == 0
                        && Mathf.Approximately(effect.PrimaryStatMultiplierModifier(owner, stat), 1f))
                    {
                        continue;
                    }

                    return Take(out result, GlyphForStat(stat), arrow);
                }
            }

            return false;


        }

        /// <summary>탐침에서 맞는 축을 찾았을 때 결과를 담아 true를 돌려주는 자잘한 도우미.</summary>
        private static bool Take(out (StatusGlyph Glyph, StatusArrow Arrow) result,
            StatusGlyph glyph, StatusArrow arrow)
        {
            result = (glyph, arrow);
            return true;
        }

        private static readonly BaseClasses.BaseEnums.PrimaryStat[] PrimaryStats =
        {
            BaseClasses.BaseEnums.PrimaryStat.STR,
            BaseClasses.BaseEnums.PrimaryStat.DEX,
            BaseClasses.BaseEnums.PrimaryStat.CON,
            BaseClasses.BaseEnums.PrimaryStat.INT,
            BaseClasses.BaseEnums.PrimaryStat.LUK,
        };

        /// <summary>주스탯이 오르내릴 때 그 스탯이 하는 일에 맞는 그림.</summary>
        private static StatusGlyph GlyphForStat(BaseClasses.BaseEnums.PrimaryStat stat) => stat switch
        {
            BaseClasses.BaseEnums.PrimaryStat.STR => StatusGlyph.Attack,
            BaseClasses.BaseEnums.PrimaryStat.DEX => StatusGlyph.Speed,
            BaseClasses.BaseEnums.PrimaryStat.CON => StatusGlyph.Defense,
            BaseClasses.BaseEnums.PrimaryStat.LUK => StatusGlyph.Crit,
            _ => StatusGlyph.Buff,
        };

        /// <summary>이 상태가 이로운지. 아이콘 색을 고를 때 쓴다.</summary>
        public static Color Tint(UnitStatus status)
        {
            if (status == null) return UITheme.TextMuted;

            // 행동 불가는 이로움/해로움보다 먼저 갈린다. 기절한 유닛을 찾는 눈이
            // 다른 디버프 사이에서 적색 하나를 더 골라내야 하면 8배속에서 놓친다.
            if (IsControl(Resolve(status).Glyph)) return UITheme.Control;

            if (status.IsBeneficial
                || status.Category == BaseClasses.BaseEnums.StatusCategory.Positive)
            {
                return UITheme.Positive;
            }

            return status.Category == BaseClasses.BaseEnums.StatusCategory.Negative
                ? UITheme.Danger
                : UITheme.TextSecondary;
        }

        /// <summary>행동을 묶는 상태인가. 색 분류가 여기서 갈린다.</summary>
        public static bool IsControl(StatusGlyph glyph) => glyph is StatusGlyph.Stun
            or StatusGlyph.Freeze
            or StatusGlyph.Airborne
            or StatusGlyph.Silence
            or StatusGlyph.Root
            or StatusGlyph.Taunt;

        private static string EffectTypeNames(UnitStatus status)
        {
            if (status.Effects == null || status.Effects.Count == 0) return "";

            var builder = new System.Text.StringBuilder();
            foreach (UnitStatus.EffectInstance instance in status.Effects)
            {
                if (instance?.EffectObject == null) continue;
                builder.Append(instance.EffectObject.GetType().Name).Append(' ');
            }

            return builder.ToString();
        }

        private static bool Has(string haystack, params string[] needles)
        {
            if (string.IsNullOrEmpty(haystack)) return false;

            foreach (string needle in needles)
            {
                if (haystack.Contains(needle)) return true;
            }

            return false;
        }

        // ── 그림 ─────────────────────────────────────────────────────

        private static void Draw(Painter p, StatusGlyph glyph)
        {
            switch (glyph)
            {
                case StatusGlyph.Buff:
                    p.Triangle(0.50f, 0.88f, 0.14f, 0.24f, 0.86f, 0.24f);
                    break;

                case StatusGlyph.Debuff:
                    p.Triangle(0.50f, 0.12f, 0.14f, 0.76f, 0.86f, 0.76f);
                    break;

                case StatusGlyph.Attack:
                    // 칼날 + 코등이 + 자루 끝.
                    p.Line(0.30f, 0.22f, 0.82f, 0.80f);
                    p.Line(0.22f, 0.46f, 0.44f, 0.24f);
                    p.Disc(0.24f, 0.20f, 0.07f);
                    break;

                case StatusGlyph.Defense:
                case StatusGlyph.Shield:
                    // 위는 평평하고 아래는 한 점으로 모이는 방패. 육각형처럼 보이지 않도록
                    // 윗변을 길게 두고 아래 삼각을 깊게 판다.
                    p.Poly(true, 0.16f, 0.90f, 0.84f, 0.90f, 0.84f, 0.52f,
                        0.50f, 0.10f, 0.16f, 0.52f);
                    break;

                case StatusGlyph.Speed:
                    p.Poly(false, 0.22f, 0.20f, 0.50f, 0.50f, 0.22f, 0.80f);
                    p.Poly(false, 0.52f, 0.20f, 0.80f, 0.50f, 0.52f, 0.80f);
                    break;

                case StatusGlyph.Crit:
                    // 네 갈래 반짝임. 가운데가 두툼해 작게 줄여도 뭉치지 않는다.
                    p.Triangle(0.50f, 0.96f, 0.41f, 0.50f, 0.59f, 0.50f);
                    p.Triangle(0.50f, 0.04f, 0.41f, 0.50f, 0.59f, 0.50f);
                    p.Triangle(0.96f, 0.50f, 0.50f, 0.41f, 0.50f, 0.59f);
                    p.Triangle(0.04f, 0.50f, 0.50f, 0.41f, 0.50f, 0.59f);
                    break;

                case StatusGlyph.Heal:
                    p.Line(0.50f, 0.16f, 0.50f, 0.84f);
                    p.Line(0.16f, 0.50f, 0.84f, 0.50f);
                    break;

                case StatusGlyph.Poison:
                    Droplet(p);
                    p.Disc(0.50f, 0.34f, 0.10f);
                    break;

                case StatusGlyph.Bleed:
                    Droplet(p);
                    break;

                case StatusGlyph.Burn:
                    // 좌우대칭으로 그리면 마름모로 읽힌다. 왼쪽 허리를 안으로 꺾어
                    // 불꽃 특유의 S자를 만든다.
                    p.Poly(true, 0.50f, 0.97f, 0.30f, 0.64f, 0.43f, 0.47f,
                        0.26f, 0.24f, 0.50f, 0.05f, 0.76f, 0.27f, 0.73f, 0.60f);
                    break;

                case StatusGlyph.Stun:
                    Spiral(p);
                    break;

                case StatusGlyph.Freeze:
                    // 눈 결정. 세 축이 가운데에서 만난다.
                    p.Line(0.50f, 0.08f, 0.50f, 0.92f);
                    p.Line(0.14f, 0.29f, 0.86f, 0.71f);
                    p.Line(0.14f, 0.71f, 0.86f, 0.29f);
                    break;

                case StatusGlyph.Airborne:
                    p.Poly(false, 0.20f, 0.52f, 0.50f, 0.86f, 0.80f, 0.52f);
                    p.Line(0.24f, 0.20f, 0.76f, 0.20f);
                    break;

                case StatusGlyph.Silence:
                    p.Ring(0.50f, 0.50f, 0.34f);
                    p.Line(0.26f, 0.74f, 0.74f, 0.26f);
                    break;

                case StatusGlyph.Root:
                    // 가로로 나란히 두면 무한대(∞), 세로로 세우면 숫자 8로 읽힌다.
                    // 비스듬히 겹쳐 놓아야 고리 두 개가 맞물린 사슬로 보인다.
                    p.Ring(0.38f, 0.64f, 0.21f);
                    p.Ring(0.62f, 0.36f, 0.21f);
                    break;

                case StatusGlyph.Taunt:
                    p.Ring(0.50f, 0.50f, 0.28f);
                    p.Line(0.50f, 0.82f, 0.50f, 0.96f);
                    p.Line(0.50f, 0.04f, 0.50f, 0.18f);
                    p.Line(0.82f, 0.50f, 0.96f, 0.50f);
                    p.Line(0.04f, 0.50f, 0.18f, 0.50f);
                    break;

                case StatusGlyph.Thorns:
                    p.Ring(0.50f, 0.50f, 0.20f);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f;
                        float dx = Mathf.Cos(angle);
                        float dy = Mathf.Sin(angle);
                        p.Line(0.50f + dx * 0.26f, 0.50f + dy * 0.26f,
                            0.50f + dx * 0.46f, 0.50f + dy * 0.46f);
                    }

                    break;

                case StatusGlyph.Mark:
                    // 마름모로 그렸더니 불꽃과 실루엣이 겹쳤다. 아래로 뾰족한 핀으로 바꾼다.
                    p.Ring(0.50f, 0.64f, 0.22f);
                    p.Triangle(0.50f, 0.06f, 0.33f, 0.52f, 0.67f, 0.52f);
                    break;
            }
        }

        private static void Droplet(Painter p)
        {
            p.Poly(false, 0.50f, 0.94f, 0.24f, 0.52f);
            p.Poly(false, 0.50f, 0.94f, 0.76f, 0.52f);
            p.Arc(0.50f, 0.40f, 0.28f, 200f, 340f);
        }

        private static void Spiral(Painter p)
        {
            // 두 바퀴 반. 가운데에서 바깥으로 감긴다.
            const int steps = 26;
            var points = new List<float>();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = t * Mathf.PI * 4.4f;
                float radius = 0.06f + t * 0.34f;
                points.Add(0.50f + Mathf.Cos(angle) * radius);
                points.Add(0.50f + Mathf.Sin(angle) * radius);
            }

            p.Poly(false, points.ToArray());
        }

        private static void DrawArrow(Painter p, StatusArrow arrow)
        {
            // 오른쪽 아래 구석. 본 그림과 겹치지 않도록 뒤에 어두운 자리를 파지 않고
            // 그림 쪽을 줄여서 자리를 만든다(PushSubjectTransform).
            if (arrow == StatusArrow.Up)
            {
                p.Triangle(0.82f, 0.36f, 0.62f, 0.02f, 1.00f, 0.02f);
            }
            else
            {
                p.Triangle(0.82f, 0.02f, 0.62f, 0.36f, 1.00f, 0.36f);
            }
        }

        // ── 래스터라이저 ─────────────────────────────────────────────

        /// <summary>
        /// 0~1 좌표로 받은 선·원·삼각형을 알파 버퍼에 찍는다.
        /// 경계는 거리 기반으로 1px 감쇠시켜 계단이 보이지 않게 한다.
        /// </summary>
        private sealed class Painter
        {
            private readonly float[] _alpha = new float[Size * Size];
            private float _scale = 1f;
            private Vector2 _offset = Vector2.zero;

            /// <summary>방향 표식이 붙을 동안 본 그림을 줄이고 왼쪽 위로 물린다.</summary>
            public void PushSubjectTransform()
            {
                _scale = SubjectScale;
                _offset = new Vector2(-0.05f, 0.06f);
            }

            public void PopSubjectTransform()
            {
                _scale = 1f;
                _offset = Vector2.zero;
            }

            public void Line(float x0, float y0, float x1, float y1, float thickness = Stroke)
            {
                Vector2 a = ToPixel(x0, y0);
                Vector2 b = ToPixel(x1, y1);
                float half = thickness * 0.5f * ScaleForThickness();

                Each((px, py) =>
                {
                    float distance = SegmentDistance(px, py, a, b);
                    return Mathf.Clamp01(half + 0.5f - distance);
                });
            }

            /// <summary>이어진 선. <paramref name="close"/>면 마지막 점과 첫 점을 잇는다.</summary>
            public void Poly(bool close, params float[] xy)
            {
                for (int i = 0; i + 3 < xy.Length; i += 2)
                {
                    Line(xy[i], xy[i + 1], xy[i + 2], xy[i + 3]);
                }

                if (close && xy.Length >= 4)
                {
                    Line(xy[^2], xy[^1], xy[0], xy[1]);
                }
            }

            public void Ring(float cx, float cy, float radius)
            {
                Vector2 center = ToPixel(cx, cy);
                float r = radius * Size * _scale;
                float half = Stroke * 0.5f * ScaleForThickness();

                Each((px, py) =>
                {
                    float distance = Mathf.Abs(Vector2.Distance(new Vector2(px, py), center) - r);
                    return Mathf.Clamp01(half + 0.5f - distance);
                });
            }

            /// <summary>부채꼴 호. 각도는 도(°)이며 x축 기준 반시계 방향이다.</summary>
            public void Arc(float cx, float cy, float radius, float fromDegrees, float toDegrees)
            {
                const int steps = 14;
                var points = new List<float>();
                for (int i = 0; i <= steps; i++)
                {
                    float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps) * Mathf.Deg2Rad;
                    points.Add(cx + Mathf.Cos(angle) * radius);
                    points.Add(cy + Mathf.Sin(angle) * radius);
                }

                Poly(false, points.ToArray());
            }

            public void Disc(float cx, float cy, float radius)
            {
                Vector2 center = ToPixel(cx, cy);
                float r = radius * Size * _scale;

                Each((px, py) =>
                    Mathf.Clamp01(r - Vector2.Distance(new Vector2(px, py), center) + 0.5f));
            }

            public void Triangle(float ax, float ay, float bx, float by, float cx, float cy)
            {
                Vector2 a = ToPixel(ax, ay);
                Vector2 b = ToPixel(bx, by);
                Vector2 c = ToPixel(cx, cy);

                Each((px, py) =>
                {
                    var p = new Vector2(px, py);
                    float d = Mathf.Min(SegmentDistance(px, py, a, b),
                        Mathf.Min(SegmentDistance(px, py, b, c), SegmentDistance(px, py, c, a)));

                    bool inside = Sign(p, a, b) >= 0f && Sign(p, b, c) >= 0f && Sign(p, c, a) >= 0f
                                  || Sign(p, a, b) <= 0f && Sign(p, b, c) <= 0f && Sign(p, c, a) <= 0f;

                    return inside ? Mathf.Clamp01(d + 0.5f) : Mathf.Clamp01(0.5f - d);
                });
            }

            public Sprite ToSprite(string name)
            {
                var pixels = new Color[Size * Size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(1f, 1f, 1f, _alpha[i]);
                }

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = name,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                texture.SetPixels(pixels);
                texture.Apply();

                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = name;
                sprite.hideFlags = HideFlags.HideAndDontSave;
                return sprite;
            }

            /// <summary>모든 픽셀을 훑어 덮어쓴다. 겹치는 획은 진한 쪽이 남는다.</summary>
            private void Each(System.Func<float, float, float> coverage)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float value = coverage(x + 0.5f, y + 0.5f);
                        if (value <= 0f) continue;

                        int index = y * Size + x;
                        if (value > _alpha[index]) _alpha[index] = value;
                    }
                }
            }

            private Vector2 ToPixel(float x, float y)
            {
                float sx = (x - 0.5f) * _scale + 0.5f + _offset.x;
                float sy = (y - 0.5f) * _scale + 0.5f + _offset.y;
                return new Vector2(sx * Size, sy * Size);
            }

            /// <summary>본 그림을 줄일 때 선까지 같이 얇아지면 방향 표식만 도드라진다.</summary>
            private float ScaleForThickness() => Mathf.Lerp(1f, _scale, 0.4f);

            private static float SegmentDistance(float px, float py, Vector2 a, Vector2 b)
            {
                Vector2 ab = b - a;
                float lengthSquared = ab.sqrMagnitude;
                var p = new Vector2(px, py);
                if (lengthSquared < 0.0001f) return Vector2.Distance(p, a);

                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                return Vector2.Distance(p, a + ab * t);
            }

            private static float Sign(Vector2 p, Vector2 a, Vector2 b)
            {
                return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            }
        }
    }
}
