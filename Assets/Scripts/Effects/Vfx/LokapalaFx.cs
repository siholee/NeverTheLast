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
            // 바즈라는 한 표적을 겨누는 궤적 대신 전 적의 몸통에 같은 충전 신호를 먼저 준다.
            // 시각 예고만 추가하며 피격 순서와 단일 판정 콜백은 아래 공통 경로가 소유한다.
            if (key == "indra.vajra") PortraitAccentFx.VajraField(alive);
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
                    // 전신 원을 이름/체력 앞에 펼치지 않고, 손 옆의 국소 팔방 인장으로 수렴한다.
                    Vector3 gatherSeal=VfxSpace.CastPoint(caster,target)+Vector3.right*(caster?.IsEnemy==true?-1f:1f)*card*.20f;
                    gatherSeal.y=VfxSpace.PortraitHudTop(caster)+.30f*card;
                    SpriteFx.Play(VfxArt.RuneRadiant, gatherSeal, GoldInk, false, card * .44f, card * .34f,
                        0.40f, angle, 0f, 0.08f, 0.65f, sortingLayer: layer, order: order);
                    for (int i = 0; i < 8; i++)
                    {
                        float turn = angle + i * 45f;
                        Vector3 direction = Quaternion.Euler(0f, 0f, turn) * Vector3.right;
                        Vector2 gem=new Vector2(.07f,.045f)*card;
                        var rim=SpriteFx.Play(VfxArt.Diamond,gatherSeal+direction*card*.20f,
                            new Color(.16f,.24f,.36f,.88f),false,gem*1.16f,gem*.95f,.40f,turn,
                            fadeIn:.06f,fadeOutFrom:.70f,sortingLayer:layer,order:order+2,preserveInk:true);
                        var face=SpriteFx.Play(VfxArt.Diamond,gatherSeal+direction*card*.20f,Gold,false,
                            gem,gem*.82f,.40f,turn,fadeIn:.06f,fadeOutFrom:.70f,sortingLayer:layer,order:order+3);
                        VfxRunner.Run(ContractGatherMark(rim,face,gatherSeal,direction,card));
                    }
                    return true;
                case "lokapalaVajra":
                    // 한 적을 겨누는 공용 시전 파편은 제외한다. 범위 예고는 AreaCore가 전 대상에 준다.
                    // 金剛杵는 카드 위에 머무는 양끝 삼지 실루엣 하나다. 낙뢰처럼 시전자 몸에서 터뜨리지 않는다.
                    VajraHand(caster, point, layer, order);
                    return true;
                case "lokapalaCoinCast":
                    Coins(point + VfxSpace.AimFrom(caster, target).Direction * card * 0.22f,
                        layer, 1, 0.24f, angle, false, filled: true, embossed: true);
                    return true;
                case "lokapalaCoinOrbit":
                    GoldenCast(caster, target, point, layer, angle);
                    float power = Codes.Special.KuberaGoldRush.GoldMultiplier();
                    // 크기·궤도는 고정하고 금화 개수와 명암만 금력에 따라 짙어진다.
                    Coins(TreasuryHandPoint(caster, point), layer,
                        5 + Mathf.RoundToInt((power - 1f) * 3f), 0.38f, angle, true,
                        radiusInCards: .09f, coinScale: .65f, filled: true);
                    return true;
                case "lokapalaTreasury":
                    // 얼굴의 가로선을 없애고 손 옆에 닫힌 금고와 잠금쇠를 남긴다.
                    Vector3 door = TreasuryHandPoint(caster, point);
                    TreasuryChest(door, layer, order);
                    return true;
                case "lokapalaHud":
                    CardMotion.ViewOf(target)?.PulseActionGateHud(GoldCore);
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

        private static void VajraHand(Unit caster, Vector3 fallback, int layer, int order)
        {
            float c=VfxSpace.CardSize;
            Vector3 center=caster==null?fallback:VfxSpace.WorldPoint(caster)+Vector3.right*(caster.IsEnemy?-.44f:.44f)*c;
            if(caster!=null)center.y=VfxSpace.PortraitHudTop(caster)+.20f*c;
            // 굵은 마름모 양끝은 적 한 명을 향한 화살표처럼 보였다. 손 크기의 채워진 세 갈래 무기로 읽힌다.
            void Stroke(Vector2 a,Vector2 b)
            {
                Vector2 delta=b-a;float rotation=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
                Vector3 p=center+(Vector3)((a+b)*.5f*c);
                Vector2 size=new(delta.magnitude*c,.016f*c);
                SpriteFx.Play(VfxSpace.GlyphLine,p,new Color(.18f,.26f,.38f,.90f),false,
                    size*new Vector2(1f,1.75f),size*new Vector2(1f,1.75f),.34f,rotation,
                    fadeIn:0f,fadeOutFrom:.65f,sortingLayer:layer,order:order);
                SpriteFx.Play(VfxSpace.GlyphLine,p,new Color(.99f,.87f,.58f,.94f),false,
                    size,size,.34f,rotation,fadeIn:0f,fadeOutFrom:.65f,sortingLayer:layer,order:order+1);
            }
            Stroke(new(-.17f,0f),new(.17f,0f));
            for(int side=-1;side<=1;side+=2)
                for(int tine=-1;tine<=1;tine+=2)
                    Stroke(new(side*.11f,0f),new(side*.16f,tine*.055f));
        }

        private static Vector3 TreasuryHandPoint(Unit caster, Vector3 fallback)
        {
            var view = CardMotion.ViewOf(caster);
            if (view?.PortraitRenderer == null) return fallback;
            float side = caster.IsEnemy ? -1f : 1f;
            Vector3 center = VfxSpace.WorldPoint(caster);
            // 무기 끝까지 포함하는 원화 bounds를 쓰면 이웃 적 HUD까지 넘어간다. 실제 카드 칸을 기준으로 삼는다.
            return new Vector3(center.x + side * VfxSpace.CardSize * .24f,
                StatusMotifFx.Anchor(caster).y + VfxSpace.CardSize * .17f, center.z);
        }

        private static void TreasuryChest(Vector3 point, int layer, int order)
        {
            float c=VfxSpace.CardSize;
            Vector2 size=new(.46f*c,.32f*c);
            SpriteFx.Play(VfxArt.TreasuryCoffer,point,new Color(1f,1f,1f,.95f),false,
                size*.94f,size,.42f,fadeIn:.06f,fadeOutFrom:.70f,sortingLayer:layer,order:order+1);
            // 한 번만 반짝여 실제 금속 시전물과 평면 상태 타일을 구분한다. 전면 섬광은 쓰지 않는다.
            SpriteFx.Play(VfxArt.Glint,point+new Vector3(.11f,.075f)*c,new Color(1f,.93f,.72f,.72f),false,
                .035f*c,.070f*c,.23f,fadeIn:.14f,fadeOutFrom:.50f,sortingLayer:layer,order:order+3);
        }

        public static void TreasuryGlints(Unit target)
        {
            if (target == null) return;
            float c = VfxSpace.CardSize;
            Vector3 center = StatusMotifFx.Anchor(target) + new Vector3(.16f, .04f) * c;
            int layer = VfxSpace.SortingLayer(target);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = center + new Vector3((i - 1) * .12f, i == 1 ? .07f : -.03f) * c;
                SpriteFx.Play(VfxSpace.HardDot, p, new Color(.20f,.28f,.38f,.84f), false,
                    Vector2.one * .14f * c, Vector2.one * .17f * c, .28f,
                    fadeIn:0f, fadeOutFrom:.56f, sortingLayer:layer, order:VfxSpace.OverlaySortingOrder+146);
                SpriteFx.Play(VfxSpace.HardDot, p, new Color(.99f,.85f,.47f,.95f), false,
                    Vector2.one * .105f * c, Vector2.one * .13f * c, .28f,
                    fadeIn:0f, fadeOutFrom:.56f, sortingLayer:layer, order:VfxSpace.OverlaySortingOrder+147);
                SpriteFx.Play(VfxSpace.Ring, p, new Color(.18f, .25f, .36f, .76f), false,
                    Vector2.one * .15f * c, Vector2.one * .18f * c, .28f,
                    fadeIn:0f, fadeOutFrom:.56f, sortingLayer:layer, order:VfxSpace.OverlaySortingOrder+148);
                SpriteFx.Play(VfxSpace.Ring, p, new Color(.97f, .84f, .52f, .94f), false,
                    Vector2.one * .12f * c, Vector2.one * .15f * c, .28f,
                    fadeIn:0f, fadeOutFrom:.56f, sortingLayer:layer, order:VfxSpace.OverlaySortingOrder+149);
                SpriteFx.Play(VfxArt.Streak, p, GoldCore, false,
                    new Vector2(.014f, .06f) * c, new Vector2(.014f, .06f) * c, .28f,
                    fadeIn:0f, fadeOutFrom:.56f, sortingLayer:layer, order:VfxSpace.OverlaySortingOrder+150);
                SpriteFx.Play(VfxSpace.HardDot,p+new Vector3(-.02f,.025f)*c,
                    new Color(1f,.96f,.78f,.94f),false,Vector2.one*.018f*c,Vector2.one*.022f*c,.28f,
                    fadeIn:0f,fadeOutFrom:.56f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+151);
            }
        }

        private static void Coins(Vector3 point, int layer, int count, float life, float angle, bool orbit,
            float radiusInCards = .28f, float coinScale = 1f, bool filled = false, bool embossed = false)
        {
            float card = VfxSpace.CardSize;
            Transform pivot = orbit ? new GameObject("LokapalaCoinOrbit").transform : null;
            if (pivot != null) { pivot.position = point; VfxRunner.Run(RotateCoins(pivot, life)); }
            for (int i = 0; i < count; i++)
            {
                float turn = angle + i * 360f / count;
                Vector3 direction = Quaternion.Euler(0f, 0f, turn) * Vector3.right;
                Vector3 center = point + (orbit ? direction * card * radiusInCards : Vector3.zero);
                Vector2 size = new Vector2(i % 2 == 0 ? 0.22f : 0.14f, 0.22f) * card * coinScale;
                if(filled)
                {
                    SpriteFx.Play(VfxSpace.HardDot,center,new Color(.18f,.25f,.37f,.82f),false,
                        size*1.08f,size*1.08f,life,turn,0f,.06f,.65f,
                        sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+34)?.transform.SetParent(pivot,true);
                    SpriteFx.Play(VfxSpace.HardDot,center,new Color(.98f,.84f,.52f,.94f),false,
                        size*.80f,size*.80f,life,turn,0f,.06f,.65f,
                        sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+35)?.transform.SetParent(pivot,true);
                }
                if(embossed)
                {
                    // 단일 금화는 공용 마법 사각 파편/후광 없이 테두리와 금력 각인으로 구분한다.
                    SpriteFx.Play(VfxSpace.PressureRing,center,new Color(.18f,.25f,.37f,.94f),false,
                        size,size,life,turn,fadeIn:0f,fadeOutFrom:.65f,
                        sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+36);
                    for(int stroke=0;stroke<3;stroke++)
                    {
                        Vector2 glyph=stroke==0?new(.018f,.10f):new(.070f,.016f);
                        Vector3 at=center+Vector3.up*(stroke==1?.024f:stroke==2?-.024f:0f)*card;
                        SpriteFx.Play(VfxSpace.GlyphLine,at,new Color(.24f,.29f,.36f,.94f),false,
                            glyph*card,glyph*card,life,fadeIn:0f,fadeOutFrom:.65f,
                            sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+40);
                    }
                    continue;
                }
                Mark(VfxSpace.Ring, center, size, turn, life, layer, VfxSpace.OverlaySortingOrder + 36, pivot);
                SpriteFx.Play(VfxArt.Streak, center, GoldCore, false,
                    new Vector2(0.06f, 0.12f) * card * coinScale, new Vector2(0.06f, 0.12f) * card * coinScale,
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

        private static IEnumerator ContractGatherMark(SpriteFx rim,SpriteFx face,Vector3 center,Vector3 direction,float card)
        {
            // 시전자 주위에서만 팔방 표식이 수렴한다. 비행 투사체나 아군 사이 궤적을 생성하지 않는다.
            float age=0f;
            while(age<.40f&&(rim!=null||face!=null))
            {
                Vector3 at=center+direction*card*Mathf.Lerp(.20f,.15f,Mathf.Clamp01(age/.32f));
                if(rim!=null)rim.transform.position=at;
                if(face!=null)face.transform.position=at;
                age+=Time.deltaTime;
                yield return null;
            }
        }
    }
}
