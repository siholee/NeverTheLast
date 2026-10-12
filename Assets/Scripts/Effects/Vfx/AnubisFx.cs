using System;
using System.Collections;
using System.Collections.Generic;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    public static class AnubisFx
    {
        private static readonly Dictionary<Unit, bool> PendingSouls = new();
        private static readonly Color Gold = new(.92f, .72f, .24f, .9f);
        public static IEnumerator HeartScale(Unit caster, Unit target, bool empowered, Action onImpact)
        {
            string key = empowered ? "anubis.heart_scale.empowered" : "anubis.heart_scale";
            VfxComposer.PlayScreen(key, caster, caster);
            VfxComposer.PlaySlot(key, "cast", 0, caster, target, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            bool done = false;
            VfxRunner.Run(Motion(caster, target, () => done = true));
            // 옛 Charge의 들기 .100 + 예비 .075 + 전진 .130 = .305초 계약을 보존한다.
            yield return PreserveContactBeat();
            VfxComposer.PlayScreen(key, caster, target);
            VfxComposer.PlaySlot(key, "hit", 0, caster, target, VfxSpace.PortraitPoint(target), VfxSpace.SortingLayer(target));
            onImpact?.Invoke();
            while (!done) yield return null;
        }

        private static IEnumerator PreserveContactBeat()
        {
            // 합산 Wait 한 번은 프레임 반올림을 줄인다. 옛 Charge의 세 구간을 각각 기다려 판정 프레임도 보존한다.
            foreach (float duration in new[] { .100f, .075f, .130f })
                for (float t = 0f; t < duration; t += Time.deltaTime) yield return null;
        }

        private static IEnumerator Motion(Unit caster, Unit target, Action done)
        {
            yield return CardMotion.GroundRakeAdvance(caster, target, .075f, null);
            done();
        }

        public static void Book(Unit caster)
        {
            VfxComposer.PlayScreen("anubis.book", caster, caster);
            VfxComposer.PlaySlot("anubis.book", "cast", 0, caster, caster, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            float c = VfxSpace.CardSize;
            Vector3 p = VfxSpace.WorldPoint(caster) + Vector3.up * c * .20f;
            int layer = VfxSpace.SortingLayer(caster);
            // 두 파피루스 가장자리와 저울의 축·접시는 한 자기 강화 문양이다.
            Stroke(p + Vector3.left * c * .30f, new Vector2(.07f,.80f)*c, 0f);
            Stroke(p + Vector3.right * c * .30f, new Vector2(.07f,.80f)*c, 0f);
            Stroke(p, new Vector2(.045f,.55f)*c, 0f);
            Stroke(p + Vector3.up*c*.20f, new Vector2(.50f,.045f)*c, 0f);
            for(int side=-1;side<=1;side+=2)
                SpriteFx.Play(VfxArt.Crescent,p+new Vector3(side*.22f,.03f)*c,Gold,false,
                    new Vector2(.23f,.10f)*c,new Vector2(.23f,.10f)*c,.42f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+31);
            void Stroke(Vector3 point, Vector2 size, float angle) => SpriteFx.Play(VfxArt.Streak,point,Gold,false,size,size,.42f,angle,
                fadeIn:.03f,fadeOutFrom:.72f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+30);
        }

        public static void Soul(Unit caster, bool whole)
        {
            if (caster == null) return;
            if (PendingSouls.TryGetValue(caster, out bool previous))
            {
                PendingSouls[caster] = previous || whole;
                return;
            }
            PendingSouls[caster] = whole;
            VfxRunner.Run(FlushSoul(caster));
        }

        private static IEnumerator FlushSoul(Unit caster)
        {
            // 만천화우의 동시 사망은 획득 판정을 각각 보존하고, 같은 프레임의 불꽃·펄스만 하나로 합친다.
            yield return new WaitForSeconds(.12f);
            bool whole = PendingSouls.TryGetValue(caster, out bool value) && value;
            PendingSouls.Remove(caster);
            if (caster == null || !caster.isActive || !caster.IsOnField) yield break;
            VfxComposer.PlaySlot(whole ? "anubis.soul" : "anubis.soul.fraction", "cast", 0, caster, caster,
                VfxSpace.WorldPoint(caster) + Vector3.up * VfxSpace.CardSize * .40f, VfxSpace.SortingLayer(caster));
            CardMotion.ViewOf(caster)?.PulseResourceBadge(whole ? 1f : .7f);
            AudioManager.PlayCue("anubis.soul.resolve");
        }

    }
}
