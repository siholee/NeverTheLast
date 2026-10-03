using System;
using System.Collections;
using BaseClasses;
using Entities;
using Entities.View;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>
    /// 모든 유닛이 공유하는 카드 등장 연출. 발밑 Hovl 원소 Flash → 단일 엣지 스윕 → Hovl Hit·착지 먼지의
    /// 세 박자를 카드 움직임과 분리해, <see cref="UnitCardView"/>는 위치와 알파만 책임지게 한다.
    /// </summary>
    public static class EntryFx
    {
        private static int _transientClearFrame = -1;

        /// <summary>카드가 보이기 직전, 바닥 인장과 수렴 파편을 먼저 펼친다.</summary>
        public static void Begin(Unit unit, Vector3 center, float cardSize, int layer, int baseOrder)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise("entry");
#endif
            Managers.AudioManager.PlayCue("common.entry");
            // 같은 프레임에 여러 카드가 등장하므로 첫 카드만 이전 스테이지의 Hovl 잔상을 회수한다.
            if (_transientClearFrame != Time.frameCount)
            {
                _transientClearFrame = Time.frameCount;
                HovlFx.ClearTransient();
            }
            float tier = TierScale(unit);
            Color accent = Accent(unit);
            Vector3 feet = center + Vector3.down * cardSize * 0.46f;

            // Hovl Flash가 등장 연출의 주인공이다. 발밑 알파 파동과 단일 엣지 스윕만 보조해
            // 밝은 바닥에서 형태를 남기되 얼굴과 하단 HUD는 덮지 않는다.
            HovlProjectileSet cue = HovlFx.ForElement(EntryElement(unit), 0.76f * tier);
            cue.Tint = Color.Lerp(accent, Color.white, 0.08f);
            cue.Alpha = 0.82f;
            cue.UnderlayColor = WithAlpha(SideAccent(unit), 0.12f);
            HovlFx.PlayFlash(cue, feet, Vector3.up, cardSize, layer, baseOrder + 3, 1f, 0.94f);
            SpriteFx.Play(VfxArt.Shockwave, feet, WithAlpha(SideAccent(unit), 0.18f), false,
                new Vector2(0.18f, 0.045f) * cardSize,
                new Vector2(1.34f, 0.24f) * cardSize * tier,
                0.42f, 0f, 0f, 0.01f, 0.48f, SpriteFx.Ease.OutCubic, layer, baseOrder - 4);
            VfxRunner.Run(SweepSilhouette(center, cardSize, SideAccent(unit), tier, layer, baseOrder));
        }

        /// <summary>카드가 바닥에 찍히는 프레임. 파동·먼지·테두리 스윕을 같은 프레임에 낸다.</summary>
        public static void Land(Unit unit, Vector3 center, float cardSize, int layer, int baseOrder)
        {
            float tier = TierScale(unit);
            int rank = EntryRank(unit);
            Color accent = Accent(unit);
            Vector3 feet = center + Vector3.down * cardSize * 0.46f;

            HovlProjectileSet impact = HovlFx.ForElement(EntryElement(unit), 0.70f * tier);
            impact.Tint = Color.Lerp(accent, Color.white, 0.06f);
            impact.Alpha = 0.84f;
            impact.UnderlayColor = WithAlpha(SideAccent(unit), 0.12f);
            HovlFx.PlayHit(impact, feet, Vector3.up, cardSize, layer, baseOrder + 4, 1f, 0.92f);
            SpriteFx.Play(VfxSpace.Ring, feet, WithAlpha(SideAccent(unit), 0.20f), false,
                new Vector2(0.18f, 0.08f) * cardSize,
                new Vector2(1.18f, 0.34f) * cardSize * tier,
                0.34f, 0f, 0f, 0.006f, 0.42f, SpriteFx.Ease.OutCubic, layer, baseOrder + 1);
            SpriteFx.Play(VfxArt.ImpactStar, feet, WithAlpha(SideAccent(unit), 0.24f), false,
                new Vector2(0.12f, 0.035f) * cardSize,
                new Vector2(0.86f, 0.12f) * cardSize * tier,
                0.24f, 90f, 0f, 0.004f, 0.32f, SpriteFx.Ease.OutCubic, layer, baseOrder + 5);

            int dustCount = 2 + rank;
            for (int i = 0; i < dustCount; i++)
            {
                float centered = i - (dustCount - 1f) * 0.5f;
                Vector3 offset = new(centered * 0.12f * cardSize, UnityEngine.Random.Range(-0.025f, 0.06f) * cardSize, 0f);
                SpriteFx.Play(VfxArt.DustPuff(UnityEngine.Random.Range(0, 4)), feet + offset,
                    new Color(0.31f, 0.39f, 0.45f, 0.16f + rank * 0.02f), false,
                    new Vector2(0.16f, 0.07f) * cardSize * (1f + rank * 0.08f),
                    new Vector2(0.44f, 0.18f) * cardSize * tier,
                    0.26f + rank * 0.04f, UnityEngine.Random.Range(-8f, 8f), 0f, 0.015f, 0.34f,
                    SpriteFx.Ease.OutCubic, layer, baseOrder - 1);
            }

            CameraShake.Shake(0.015f + rank * 0.008f);
        }

        private static IEnumerator GatherShards(Vector3 center, float cardSize, Color accent, float tier,
            int layer, int baseOrder)
        {
            int count = 8 + TierRank(tier) * 4;
            var root = new GameObject("EntryGatherShards").transform;
            UnityEngine.Object.Destroy(root.gameObject, 2f);
            var shards = new SpriteRenderer[count];
            var starts = new Vector3[count];
            var sizes = new float[count];
            int rank = TierRank(tier);
            for (int i = 0; i < count; i++)
            {
                // 등간격 각도에 안팎 반경을 교차시켜 보스 16개도 한 덩어리로 포개지지 않는다.
                float angle = (i * 360f / count + 11.25f + (i % 2 == 0 ? -3f : 3f)) * Mathf.Deg2Rad;
                float ring = (i % 2 == 0 ? 0.9f : 1.08f) * (1f + (i % 3 - 1) * 0.035f);
                starts[i] = center + new Vector3(Mathf.Cos(angle) * 0.86f, Mathf.Sin(angle) * 0.70f, 0f)
                    * cardSize * tier * ring;
                sizes[i] = (0.046f + (i % 4) * 0.009f) * cardSize * (1f + rank * 0.08f);
                shards[i] = VfxSpace.NewSprite(root, "GatherShard", VfxArt.Diamond,
                    WithAlpha(Color.Lerp(accent, Color.white, 0.48f), 0f), layer, baseOrder + 7, true);
                shards[i].transform.position = starts[i];
                shards[i].transform.localScale = new Vector3(sizes[i] * 0.72f, sizes[i] * 1.22f, 1f);
            }

            float hold = 0.10f + rank * 0.045f;
            float converge = 0.27f + rank * 0.055f;
            float duration = hold + converge;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float visible = Mathf.Clamp01(t / 0.075f);
                float k = Mathf.Clamp01((t - hold) / converge);
                float move = k * k * k;
                for (int i = 0; i < count; i++)
                {
                    float targetAngle = (i * 137.5f) * Mathf.Deg2Rad;
                    Vector3 target = center + new Vector3(Mathf.Cos(targetAngle) * 0.15f, Mathf.Sin(targetAngle) * 0.25f, 0f) * cardSize;
                    shards[i].transform.position = Vector3.LerpUnclamped(starts[i], target, move);
                    shards[i].transform.Rotate(0f, 0f, (i % 2 == 0 ? 260f : -260f) * Time.deltaTime);
                    float fade = k < 0.72f ? 1f : 1f - (k - 0.72f) / 0.28f;
                    shards[i].color = WithAlpha(Color.Lerp(accent, Color.white, 0.48f), visible * fade * 0.88f);
                }
                yield return null;
            }
            UnityEngine.Object.Destroy(root.gameObject);
        }

        /// <summary>카드 면을 덮지 않는 네 변의 진영색 외곽광.</summary>
        private static void OutlinePulse(Vector3 center, float cardSize, Color color, float alpha, float duration,
            int layer, int order)
        {
            Color darkCore = new(color.r * 0.12f, color.g * 0.34f, color.b * 0.31f, alpha * 0.92f);
            Color brightRim = WithAlpha(Color.Lerp(color, Color.white, 0.22f), alpha);
            float half = cardSize * 0.515f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 vertical = center + Vector3.right * side * half;
                Vector3 horizontal = center + Vector3.up * side * half;
                SpriteFx.Play(VfxArt.Streak, vertical, darkCore, false,
                    new Vector2(0.76f, 0.038f) * cardSize, new Vector2(0.98f, 0.052f) * cardSize,
                    duration, 90f, 0f, 0.01f, 0.42f, SpriteFx.Ease.OutCubic, layer, order);
                SpriteFx.Play(VfxArt.Streak, vertical + Vector3.right * side * 0.012f * cardSize, brightRim, true,
                    new Vector2(0.72f, 0.014f) * cardSize, new Vector2(0.94f, 0.022f) * cardSize,
                    duration, 90f, 0f, 0.01f, 0.36f, SpriteFx.Ease.OutCubic, layer, order + 1);
                SpriteFx.Play(VfxArt.Streak, horizontal, darkCore, false,
                    new Vector2(0.76f, 0.038f) * cardSize, new Vector2(0.98f, 0.052f) * cardSize,
                    duration, 0f, 0f, 0.01f, 0.42f, SpriteFx.Ease.OutCubic, layer, order);
                SpriteFx.Play(VfxArt.Streak, horizontal + Vector3.up * side * 0.012f * cardSize, brightRim, true,
                    new Vector2(0.72f, 0.014f) * cardSize, new Vector2(0.94f, 0.022f) * cardSize,
                    duration, 0f, 0f, 0.01f, 0.36f, SpriteFx.Ease.OutCubic, layer, order + 1);
            }
        }

        /// <summary>짧은 전경 헤드가 카드 네 변을 한 바퀴 돌아 진영색 실루엣을 읽힌다.</summary>
        private static IEnumerator SweepSilhouette(Vector3 center, float cardSize, Color color, float tier,
            int layer, int baseOrder)
        {
            const int headCount = 1;
            var root = new GameObject("EntryEdgeSweep").transform;
            UnityEngine.Object.Destroy(root.gameObject, 1.5f);
            var heads = new SpriteRenderer[headCount];
            for (int i = 0; i < headCount; i++)
            {
                heads[i] = VfxSpace.NewSprite(root, "EdgeHead", VfxArt.Streak,
                    WithAlpha(color, 0f), layer, baseOrder + 14 - i, false);
                heads[i].transform.localScale = new Vector3(0.22f * cardSize, 0.032f * cardSize, 1f);
            }

            float duration = 0.52f + (tier - 1f) * 0.15f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (root == null) yield break;
                float k = Mathf.Clamp01(t / duration);
                for (int i = 0; i < headCount; i++)
                {
                    float phase = k;
                    heads[i].transform.position = OutlinePoint(center, cardSize, phase, out float angle);
                    heads[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    float fade = Mathf.Sin(Mathf.Clamp01(k * 1.18f) * Mathf.PI);
                    heads[i].color = WithAlpha(color, fade * 0.72f);
                }
                yield return null;
            }
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }

        private static Vector3 OutlinePoint(Vector3 center, float cardSize, float phase, out float angle)
        {
            float half = cardSize * 0.515f;
            float p = Mathf.Clamp01(phase) * 4f;
            int side = Mathf.Min(3, Mathf.FloorToInt(p));
            float k = p - side;
            switch (side)
            {
                case 0:
                    angle = 0f;
                    return center + new Vector3(Mathf.Lerp(-half, half, k), -half, 0f);
                case 1:
                    angle = 90f;
                    return center + new Vector3(half, Mathf.Lerp(-half, half, k), 0f);
                case 2:
                    angle = 180f;
                    return center + new Vector3(Mathf.Lerp(half, -half, k), half, 0f);
                default:
                    angle = -90f;
                    return center + new Vector3(-half, Mathf.Lerp(half, -half, k), 0f);
            }
        }

        private static float TierScale(Unit unit)
        {
            int rank = EntryRank(unit);
            return rank == 2 ? 1.42f : rank == 1 ? 1.2f : 1f;
        }

        private static int EntryRank(Unit unit)
        {
            if (unit == null) return 0;
            if (string.Equals(unit.UnitTier, "boss", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(unit.UnitTier, "elite", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0;
        }

        private static int TierRank(float tier) => tier > 1.3f ? 2 : tier > 1.05f ? 1 : 0;

        private static Color SideAccent(Unit unit)
            => unit != null && unit.IsEnemy
                ? new Color(0.52f, 0.10f, 0.20f, 1f)
                : new Color(0.18f, 0.43f, 0.58f, 1f);

        private static Color Accent(Unit unit)
        {
            Color side = unit != null && unit.IsEnemy
                ? new Color(1f, 0.30f, 0.42f, 1f)
                : new Color(0.38f, 0.86f, 1f, 1f);
            if (unit != null && Enum.TryParse(unit.Element, true, out BaseEnums.UnitElement element)
                && element != BaseEnums.UnitElement.None)
            {
                return Color.Lerp(side, ElementPalette.ColorFor(element), 0.55f);
            }
            return side;
        }

        private static BaseEnums.UnitElement EntryElement(Unit unit)
        {
            if (unit != null && Enum.TryParse(unit.Element, true, out BaseEnums.UnitElement element)) return element;
            return BaseEnums.UnitElement.None;
        }

        private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
