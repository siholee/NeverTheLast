using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>로카팔라의 한 번 시전→동시 피격 문법. 판정은 코드가 넘긴 단일 콜백만 소유한다.</summary>
    public static class LokapalaFx
    {
        private static readonly Color Gold = new(0.94f, 0.65f, 0.20f, 0.94f);
        private static readonly Color GoldInk = new(0.30f, 0.17f, 0.055f, 0.98f);
        private static readonly Color GoldCore = new(1f, 0.94f, 0.64f, 0.90f);

        public static IEnumerator Normal(string key, Unit caster, Unit target, Action onImpact)
        {
            using (YamaVarunaFx.Observe(key, caster))
                yield return NormalCore(key, caster, target, onImpact);
        }

        public static IEnumerator Area(string key, Unit caster, IReadOnlyList<Unit> targets, Action onImpact)
        {
            using (YamaVarunaFx.Observe(key, caster))
                yield return AreaCore(key, caster, targets, onImpact);
        }

        private static IEnumerator NormalCore(string key, Unit caster, Unit target, Action onImpact)
        {
            yield return VfxComposer.PlaySingleTarget(key, caster, target, onImpact);
            if (key == "vayu.gust") yield return new WaitForSeconds(0.32f);
            AudioManager.PlayAction(caster, SfxActionSlot.Normal, SfxCuePhase.Resolve);
        }

        private static IEnumerator AreaCore(string key, Unit caster, IReadOnlyList<Unit> targets,
            Action onImpact)
        {
            List<Unit> alive = targets?.Where(unit => unit != null && unit.isActive).ToList() ?? new List<Unit>();
            if (caster == null || alive.Count == 0) yield break;
            VfxComposer.PlayScreen(key, caster, caster);
            VfxComposer.PlaySlot(key, "cast", 0, caster, alive[0],
                VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            yield return new WaitForSeconds(VfxComposer.Offset(key, "hit", fallback: 0.22f));
            VfxComposer.PlayScreen(key, caster, alive);

            bool applied = false;
            void ResolveOnce()
            {
                if (applied) return;
                applied = true;
                onImpact?.Invoke();
            }
            int pending = 0;
            // 시트가 동시에 열려도 판정은 원래 대상 배열의 순회 전체를 한 번 호출한다.
            yield return VfxSpace.ResolveTargetHits(VfxSpace.BuildTargetHitSequence(alive, 0f), hit =>
            {
                pending++;
                VfxRunner.Run(TargetHit(key, caster, hit.Target, ResolveOnce, () => pending--));
            });
            while (pending > 0) yield return null;
            if (VfxComposer.Slot(key, "hit", 1) != null) yield return new WaitForSeconds(0.18f);
            if (key.StartsWith("vayu.")) yield return new WaitForSeconds(0.14f);
            AudioManager.PlayCue(key + ".resolve");
        }

        private static IEnumerator TargetHit(string key, Unit caster, Unit target,
            Action onImpact, Action onFinished)
        {
            yield return VfxComposer.PlayHitSlot(key, 0, caster, target, () =>
            {
                // 바즈라의 충격은 낙뢰 끝점에서 발생한다. 별도 대상/카드 사이 궤적은 만들지 않는다.
                if (VfxComposer.Slot(key, "hit", 1) != null)
                    VfxComposer.PlaySlot(key, "hit", 1, caster, target,
                        VfxSpace.WorldPoint(target), VfxSpace.SortingLayer(target));
                onImpact();
            });
            onFinished();
        }

        public static IEnumerator Gather(Unit caster, Action openGate)
        {
            const string key = "indra.gathering";
            VfxComposer.PlayScreen(key, caster, caster);
            VfxComposer.PlaySlot(key, "cast", 0, caster, caster,
                VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            yield return new WaitForSeconds(0.16f);
            openGate?.Invoke();
            AudioManager.PlayAction(caster, SfxActionSlot.Ultimate, SfxCuePhase.Hit);
            foreach (Unit participant in Combat.SpecialAction.Participants(caster))
                VfxComposer.PlayAffectedSlot(key, 0, caster, participant);
            yield return new WaitForSeconds(0.24f);
            AudioManager.PlayAction(caster, SfxActionSlot.Ultimate, SfxCuePhase.Resolve);
        }

        /// <summary>96_vfx의 간단한 실루엣 부품. 기존 원화·절차 도형만 쓰며 전투 난수는 소비하지 않는다.</summary>
        public static bool PlayMotif(VfxPartData part, Unit caster, Unit target, Vector3 point,
            int layer, Vector2 size, float angle)
        {
            if (YamaVarunaFx.PlayMotif(part, caster, target, point, layer, size, angle)) return true;
            if (VedicTrioFx.PlayMotif(part, caster, target, point, layer, size, angle)) return true;
            float card = VfxSpace.CardSize;
            int order = VfxSpace.OverlaySortingOrder + 34;
            switch (part.renderer)
            {
                case "lokapalaGather":
                    SpriteFx.Play(VfxArt.RuneRadiant, point, GoldInk, false, card * 1.14f, card * 1.14f,
                        0.40f, angle, 0f, 0.08f, 0.65f, sortingLayer: layer, order: order);
                    for (int i = 0; i < 8; i++)
                    {
                        float turn = angle + i * 45f;
                        Vector3 direction = Quaternion.Euler(0f, 0f, turn) * Vector3.right;
                        Mark(VfxArt.Diamond, point + direction * card * 0.52f,
                            new Vector2(0.18f, 0.10f) * card, turn, 0.40f, layer, order + 2);
                    }
                    return true;
                case "lokapalaVajra":
                    GoldenCast(caster, target, point, layer, angle);
                    // 金剛杵는 카드 위에 머무는 양끝 삼지 실루엣 하나다. 낙뢰처럼 시전자 몸에서 터뜨리지 않는다.
                    Vector3 top = point + Vector3.up * card * 0.52f;
                    Mark(VfxArt.Streak, top, new Vector2(0.88f, 0.075f) * card, angle, 0.34f, layer, order);
                    Vector3 axis = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
                    for (int side = -1; side <= 1; side += 2)
                        for (int tine = -1; tine <= 1; tine++)
                            Mark(VfxArt.Diamond, top + axis * card * side * 0.32f +
                                Quaternion.Euler(0f, 0f, angle) * Vector3.up * card * tine * 0.09f,
                                new Vector2(0.24f, 0.075f) * card, angle + side * tine * 18f,
                                0.34f, layer, order + 2);
                    return true;
                case "lokapalaCoinCast":
                    GoldenCast(caster, target, point, layer, angle);
                    Coins(point + VfxSpace.AimFrom(caster, target).Direction * card * 0.22f,
                        layer, 1, 0.24f, angle, false);
                    return true;
                case "lokapalaCoinOrbit":
                    GoldenCast(caster, target, point, layer, angle);
                    float power = Codes.Special.KuberaGoldRush.GoldMultiplier();
                    // 크기·궤도는 고정하고 금화 개수와 명암만 금력에 따라 짙어진다.
                    Coins(point + Vector3.up * card * 0.28f, layer,
                        5 + Mathf.RoundToInt((power - 1f) * 3f), 0.38f, angle, true);
                    return true;
                case "lokapalaTreasury":
                    Vector3 door = point + Vector3.up * card * 0.40f;
                    Mark(VfxArt.Streak, door + Vector3.left * card * 0.32f,
                        new Vector2(0.85f, 0.075f) * card, 90f, 0.42f, layer, order);
                    Mark(VfxArt.Streak, door + Vector3.right * card * 0.32f,
                        new Vector2(0.85f, 0.075f) * card, 90f, 0.42f, layer, order);
                    Mark(VfxArt.Streak, door + Vector3.up * card * 0.41f,
                        new Vector2(0.70f, 0.075f) * card, 0f, 0.42f, layer, order);
                    SpriteFx.Play(VfxArt.RuneOrnate, door, Gold, false,
                        new Vector2(0.64f, 0.88f) * card, new Vector2(0.64f, 0.88f) * card,
                        0.42f, angle, 0f, 0.08f, 0.70f, sortingLayer: layer, order: order + 1);
                    return true;
                case "lokapalaHud":
                    CardMotion.ViewOf(target)?.PulseSupportHud(false, false, Gold);
                    return true;
                default:
                    return false;
            }
        }

        public static void CoinImpact(Unit target, float angle, bool rich)
            => Coins(VfxSpace.PortraitPoint(target), VfxSpace.SortingLayer(target), rich ? 7 : 3,
                0.28f, angle, true);

        public static void ResourceGained(Unit caster, SfxActionSlot slot = SfxActionSlot.Passive)
        {
            CardMotion.ViewOf(caster)?.PulseSupportHud(false, false, Gold);
            AudioManager.PlayAction(caster, slot, SfxCuePhase.Resolve);
        }

        private static void GoldenCast(Unit caster, Unit target, Vector3 point, int layer, float angle)
        {
            // 실루엣의 발광은 공용 법사 불꽃에서 시작한다. 부품 수를 늘리지 않는 한 번의 시전 합성이다.
            VfxPartData source = VfxDefinitionCatalog.Part("cast.class.caster");
            if (source == null) return;
            VfxComposer.PlayCasterTwin(source, new VfxSlotData(), caster, target, point, layer,
                new Vector2(source.scaleX, source.scaleY), Gold, GoldCore, angle, false, recolor: true);
        }

        private static void Coins(Vector3 point, int layer, int count, float life, float angle, bool orbit)
        {
            float card = VfxSpace.CardSize;
            Transform pivot = orbit ? new GameObject("LokapalaCoinOrbit").transform : null;
            if (pivot != null) { pivot.position = point; VfxRunner.Run(RotateCoins(pivot, life)); }
            for (int i = 0; i < count; i++)
            {
                float turn = angle + i * 360f / count;
                Vector3 direction = Quaternion.Euler(0f, 0f, turn) * Vector3.right;
                Vector3 center = point + (orbit ? direction * card * 0.28f : Vector3.zero);
                Vector2 size = new Vector2(i % 2 == 0 ? 0.22f : 0.14f, 0.22f) * card;
                Mark(VfxSpace.Ring, center, size, turn, life, layer, VfxSpace.OverlaySortingOrder + 36, pivot);
                SpriteFx.Play(VfxArt.Streak, center, GoldCore, false,
                    new Vector2(0.06f, 0.12f) * card, new Vector2(0.06f, 0.12f) * card,
                    life, turn, 0f, 0.06f, 0.65f, sortingLayer: layer, order: VfxSpace.OverlaySortingOrder + 39)?.transform.SetParent(pivot, true);
            }
        }

        private static IEnumerator RotateCoins(Transform pivot, float life)
        {
            for (float age = 0f; age < life && pivot != null; age += Time.deltaTime)
            {
                pivot.rotation = Quaternion.Euler(0f, 0f, age * 240f);
                yield return null;
            }
            if (pivot != null) UnityEngine.Object.Destroy(pivot.gameObject);
        }

        private static void Mark(Sprite sprite, Vector3 point, Vector2 size, float angle,
            float life, int layer, int order, Transform parent = null)
        {
            SpriteFx.Play(sprite, point, GoldInk, false, size * 1.18f, size * 1.18f, life,
                angle, 0f, 0.06f, 0.70f, sortingLayer: layer, order: order)?.transform.SetParent(parent, true);
            SpriteFx.Play(sprite, point, Gold, false, size, size, life,
                angle, 0f, 0.06f, 0.70f, sortingLayer: layer, order: order + 1)?.transform.SetParent(parent, true);
            SpriteFx.Play(sprite, point, GoldCore, true, size * 0.68f, size * 0.68f, life,
                angle, 0f, 0.06f, 0.60f, sortingLayer: layer, order: order + 2)?.transform.SetParent(parent, true);
        }
    }
}
