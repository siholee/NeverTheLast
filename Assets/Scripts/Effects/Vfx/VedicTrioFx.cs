using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Entities;
using Effects.Vfx.Presets;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>베다 3인의 표적 국소 연출. 전투 판정과 자원 변경은 전달받은 코드 콜백만 소유한다.</summary>
    public static class VedicTrioFx
    {
        private sealed class WeaknessPresentation { public int Frame = -1; public int Elements; }
        private static readonly ConditionalWeakTable<Unit, WeaknessPresentation> WeaknessFrames = new();
        public static IEnumerator Single(string key, Unit caster, Unit target, Action onImpact)
        {
            if(key is "surya.normal" or "surya.finale" or "surya.follow_up" or "surya.focused_follow_up" or "marie.normal")
            {
                var slot=VfxComposer.Slot(key,"hit");var part=VfxDefinitionCatalog.Part(slot?.partId);
                int peak=slot!=null&&slot.peakFrame>=0?slot.peakFrame:part?.peakFrame??3;
                float arrival=VfxComposer.Offset(key,"hit")+(slot!=null&&slot.impactDelay>=0?slot.impactDelay:
                    Mathf.Max(0,peak-(part?.startFrame??0))/Mathf.Max(1f,part?.fps??30f));
                // 비행은 장식뿐이다. 기존 슬롯의 정점과 콜백·타격음이 판정을 그대로 소유한다.
                VfxRunner.Run(PurchasedProjectileFx.Fly(caster,target,arrival,null,
                    weight:key is "surya.finale" or "surya.focused_follow_up"?1.05f:.80f,
                    paintContact:false,minimumFlightFrames:false));
            }
            yield return VfxComposer.PlaySingleTarget(key, caster, target, () =>
            {
                // 종언탄은 일반/특수 어느 경로에서도 정점 타격음을 이 입구가 한 번 소유한다.
                if(key=="surya.finale") AudioManager.PlayCue("surya.finale.hit");
                if (key == "surya.ultimate" && target != null)
                    VfxComposer.PlayAffectedPart(target, "hit.surya.wheel", caster);
                onImpact?.Invoke();
            });
            AudioManager.PlayCue(key + ".resolve");
        }

        /// <summary>이 입구가 수리야 추공 Cast/Hit/Resolve를 소유한다. 호출 코드에서 같은 큐를 추가하지 않는다.</summary>
        public static IEnumerator NapoleonAdditional(Unit caster, Unit target, bool empowered, Action impact)
        {
            string key = empowered ? "napoleon.focused_follow_up" : "napoleon.follow_up";
            Managers.AudioManager.PlayCue(key + ".cast");
            yield return Single(key, caster, target, impact);
            Managers.AudioManager.PlayCue(key + ".hit");
        }

        public static IEnumerator SuryaAdditional(Unit caster, Unit target, bool empowered, Action onImpact)
        {
            string key = empowered ? "surya.focused_follow_up" : "surya.follow_up";
            AudioManager.PlayCue(key + ".cast");
            yield return Single(key, caster, target, () =>
            {
                AudioManager.PlayCue(key + ".hit");
                onImpact?.Invoke();
            });
        }

        /// <summary>중심 강조는 실제 강화 티켓의 선택 대상만 따른다. 추가 피해나 불 부착은 생성하지 않는다.</summary>
        public static IEnumerator AgniAdditional(Unit caster, IReadOnlyList<Unit> targets, Unit centralTarget,
            bool empowered, Action onImpact)
        {
            string key = empowered ? "agni.focused_follow_up" : "agni.follow_up";
            AudioManager.PlayCue(key + ".cast");
            yield return LokapalaFx.Area(key, caster, targets, () =>
            {
                if (empowered && centralTarget != null && targets != null && targets.Contains(centralTarget))
                {
                    Color tint = ElementPalette.ColorFor(caster.Element);
                    HandpaintFx.PlayAt(VfxSpace.BodyContactPoint(centralTarget), VfxSpace.SortingLayer(centralTarget),
                        new HandpaintSheet("Explosion3_Yellow",3,3,30f,new Vector2(.5f,.5f)),
                        new Vector2(.62f,.72f), tint, tint, peakAlpha:.40f, glowAlpha:0f,
                        desaturate:1f, calmLight:true, orderOffset:160)?.BindRecipient(centralTarget);
                }
                AudioManager.PlayCue(key + ".hit");
                onImpact?.Invoke();
            });
        }

        /// <summary>BaseUltimate의 Cast를 재사용한다. 수혜 광은 실제 추가된 상태의 affected 경로가 표시한다.</summary>
        public static IEnumerator AgniUltimate(Unit caster, IReadOnlyList<Unit> beneficiaries, Action onBuff)
        {
            const string key = "agni.ultimate";
            if (caster == null) { onBuff?.Invoke(); yield break; }
            VfxComposer.PlayScreen(key, caster, caster);
            VfxComposer.PlaySlot(key, "cast", 0, caster, caster, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            yield return new WaitForSeconds(0.18f);
            AudioManager.PlayCue(key + ".hit");
            onBuff?.Invoke();
            yield return new WaitForSeconds(0.22f);
            AudioManager.PlayCue(key + ".resolve");
        }

        /// <summary>무피해 준비 행동 한 번. 보류 티켓의 실행·원소 이동은 게임 코드만 수행한다.</summary>
        public static IEnumerator AgniSpecialSetup(Unit caster, Action onSetup)
        {
            const string key = "agni.special_setup";
            if (caster == null) { onSetup?.Invoke(); yield break; }
            VfxComposer.PlayScreen(key, caster, caster);
            VfxComposer.PlaySlot(key, "cast", 0, caster, caster, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster));
            yield return new WaitForSeconds(0.16f);
            AudioManager.PlayCue(key + ".hit");
            onSetup?.Invoke();
            yield return new WaitForSeconds(0.14f);
            AudioManager.PlayCue(key + ".resolve");
        }

        /// <summary>실제 재장전 완료 뒤 호출한다. 잔탄은 게임 코드가 넘긴 값만 표시한다.</summary>
        public static void Reload(Unit caster, int actualAmmo)
        {
            if (caster == null) return;
            Vector3 point = VfxSpace.WorldPoint(caster) + Vector3.down * VfxSpace.CardSize * 0.26f;
            int layer = VfxSpace.SortingLayer(caster);
            for (int i = 0; i < Mathf.Clamp(actualAmmo, 0, 2); i++)
                Mark(VfxArt.Diamond, point + Vector3.right * VfxSpace.CardSize * (0.32f + i * 0.10f),
                    new Vector2(0.06f, 0.13f), new Color(0.98f, 0.79f, 0.47f, 0.72f), 0f, layer);
            CardMotion.ViewOf(caster)?.PulseSupportHud(false, false, new Color(0.94f, 0.76f, 0.44f));
            AudioManager.PlayCue("surya.reload");
        }

        public static bool PlayMotif(VfxPartData part, Unit caster, Unit target, Vector3 point,
            int layer, Vector2 size, float angle)
        {
            if(part.renderer=="trioSuryaBuff")
            {
                float card=VfxSpace.CardSize;
                Vector3 center=StatusMotifFx.Anchor(target);
                center.x+=.025f*card;
                center.y=VfxSpace.PortraitHudTop(target)+.19f*card;
                // 적의 ㄱ자 조준 표식과 달리 채워진 태양과 여덟 광선으로 자기 강화를 읽힌다.
                // 다중 반투명 레이어가 감쇠 중 서로 비쳐 갈색 원호를 만들지 않게 한 문양으로 합성한다.
                Vector2 sun=Vector2.one*.305f*card;
                SpriteFx.Play(VfxSpace.SuryaSunBuff,center,Color.white,false,sun,sun*.90f,
                    .32f*Mathf.Max(1f,Time.timeScale),fadeIn:.16f,fadeOutFrom:.70f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+38,preserveInk:true);
                CardMotion.ViewOf(target)?.PulseSupportHud(false,false,new Color(.98f,.77f,.34f));
                return true;
            }
            if (part.renderer == "trioSunTarget")
            {
                // 원화의 금색 장식과 혼동되는 큰 원 대신 작은 조준 모서리를 흉부 옆에 표시한다.
                float card=VfxSpace.CardSize;
                Vector3 center=StatusMotifFx.Anchor(target);
                center.x+=.15f*card;
                center.y=VfxSpace.PortraitHudTop(target)+.26f*card;
                for(int x=-1;x<=1;x+=2)
                    for(int y=-1;y<=1;y+=2)
                    {
                        Vector2 corner=new(x*.10f,y*.10f);
                        TargetStroke(center,corner,corner+new Vector2(-x*.045f,0f),layer);
                        TargetStroke(center,corner,corner+new Vector2(0f,-y*.045f),layer);
                    }
                Mark(VfxArt.Diamond,center,new Vector2(.026f,.034f),new Color(1f,.77f,.30f,.90f),0f,layer);
                return true;
            }
            if (part.renderer == "trioSuryaWheel")
            {
                bool wheel = part.renderer == "trioSuryaWheel";
                float card = VfxSpace.CardSize;
                Color rim = new(0.88f, 0.65f, 0.40f, 0.64f);
                float radius = wheel ? 0.37f : 0.24f;
                Vector2 ring = Vector2.one * radius * 2f;
                Mark(VfxSpace.Ring, point, ring, rim, 0f, layer);
                int spokes = wheel ? 7 : 4;
                for (int i = 0; i < spokes; i++)
                {
                    float turn = i * 360f / spokes;
                    Vector3 ray = Quaternion.Euler(0f, 0f, turn) * Vector3.right;
                    Mark(VfxArt.Streak, point + ray * radius * card,
                        new Vector2(wheel ? 0.20f : 0.11f, 0.025f), new Color(0.98f, 0.84f, 0.57f, 0.66f), turn, layer);
                }
                return true;
            }
            if (part.renderer == "trioWeaknessAll")
            {
                string[] all = { "pyro", "hydro", "dendro", "electro", "cryo", "geo", "anemo" };
                foreach (string element in all)
                {
                    var piece = VfxDefinitionCatalog.Part("affected.trio.weakness." + element);
                    if (piece != null) PlayMotif(piece, caster, target, point, layer, size, angle);
                }
                return true;
            }
            if (part.renderer == "trioWeakness")
            {
                // 실제 성공한 원소별 상태가 자기 색 한 조각을 남긴다. 일곱 조각이 같은 정점에서 고리 하나를 이룬다.
                string[] elements = { "pyro", "hydro", "dendro", "electro", "cryo", "geo", "anemo" };
                int element = Array.FindIndex(elements, value => part.id.EndsWith("." + value, StringComparison.OrdinalIgnoreCase));
                element = Mathf.Max(0, element);
                if (target != null)
                {
                    var presentation = WeaknessFrames.GetValue(target, _ => new WeaknessPresentation());
                    if (presentation.Frame != Time.frameCount) { presentation.Frame = Time.frameCount; presentation.Elements = 0; }
                    int bit = 1 << element;
                    if ((presentation.Elements & bit) != 0) return true;
                    presentation.Elements |= bit;
                }
                Vector3 center = StatusMotifFx.Anchor(target);
                Color tint = ColorUtility.TryParseHtmlString("#" + part.bodyColor.TrimStart('#'), out Color parsed) ? parsed : new Color(0.65f, 0.86f, 0.78f);
                float card=VfxSpace.CardSize;
                // 흩어진 체크 표시 대신 HUD의 원소 아이콘을 일정한 일곱 칸에 둔다. 얼굴/HUD 사이 여백은 공유한다.
                center.y=VfxSpace.PortraitHudTop(target)+.14f*card;
                Vector3 glyph=center+Vector3.right*(element-3)*.12f*card;
                BaseClasses.BaseEnums.UnitElement[] kinds={BaseClasses.BaseEnums.UnitElement.Pyro,
                    BaseClasses.BaseEnums.UnitElement.Hydro,BaseClasses.BaseEnums.UnitElement.Dendro,
                    BaseClasses.BaseEnums.UnitElement.Electro,BaseClasses.BaseEnums.UnitElement.Cryo,
                    BaseClasses.BaseEnums.UnitElement.Geo,BaseClasses.BaseEnums.UnitElement.Anemo};
                Sprite icon=Managers.UI.Theme.StatusIcons.Element(kinds[element]);
                float life=.36f*Mathf.Max(1f,Time.timeScale);
                Vector2 iconSize=Vector2.one*.095f*card;
                // 아이콘 실루엣의 1px 테두리만으로는 밝은 털에서 대비가 사라진다. 작은 공통 받침을 쓴다.
                SpriteFx.Play(VfxSpace.HardDot,glyph,new Color(.12f,.22f,.36f,.68f),false,
                    Vector2.one*.11f*card,Vector2.one*.11f*card,life,fadeIn:.08f,fadeOutFrom:.64f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+42,preserveInk:true);
                tint=element==2 ? new Color(.46f,.78f,.55f,.98f)
                    : element==5 ? new Color(.90f,.68f,.29f,.98f) : Color.Lerp(tint,Color.white,.12f);
                tint.a=.98f;
                SpriteFx.Play(icon,glyph,tint,false,iconSize,iconSize,life,fadeIn:.08f,fadeOutFrom:.64f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+43,preserveInk:true);
                WeaknessStroke(glyph+Vector3.down*.065f*card,new Vector2(-.025f,.012f),Vector2.zero,tint,layer);
                WeaknessStroke(glyph+Vector3.down*.065f*card,Vector2.zero,new Vector2(.025f,.012f),tint,layer);
                return true;
            }
            if (part.renderer == "trioAgniBuff")
            {
                // 실제 상태 부여/갱신이 수혜 그림을 한 번 소유한다. 자신에게는 연결선을 중복하지 않는다.
                PurchasedSupportThreadsFx.Play(caster,target,PurchasedSupportThreadsFx.Kind.Order);
                PurchasedSupportThreadsFx.Play(caster,target,PurchasedSupportThreadsFx.Kind.Receive);
                return true;
            }
            if (part.renderer == "trioAgniCast")
            {
                PurchasedArcherFx.RosterPreparation(caster,target,attack:part.id=="cast.agni.seal");
                return true;
            }
            if (part.renderer != "trioVayuCast" && part.renderer != "trioSuryaCast") return false;
            bool wind = part.renderer == "trioVayuCast";
            bool gun = part.renderer == "trioSuryaCast";
            Color body = wind ? new Color(0.53f, 0.82f, 0.73f) : gun ? new Color(0.94f, 0.73f, 0.54f) : new Color(0.91f, 0.62f, 0.52f);
            Color core = wind ? new Color(0.86f, 0.98f, 0.92f) : new Color(1f, 0.91f, 0.73f);
            var sheet = new HandpaintSheet(part.sheet, part.columns, part.rows, part.fps, new Vector2(0.5f, 0.5f));
            if(gun)
            {
                // 짧은 발사광의 실제 밝은 프레임을 유지하고 얇은 군청 면으로 하늘과 분리한다.
                point.x+=.09f*VfxSpace.CardSize*(caster!=null&&caster.IsEnemy?-1f:1f);
                HandpaintFx.PlayAt(point,layer,sheet,size*1.10f,new Color(.12f,.23f,.38f),Color.clear,angle,
                    heldFrame:part.peakFrame,peakHoldSeconds:part.peakHoldSeconds,
                    peakAlpha:.72f,glowAlpha:0f,desaturate:1f,steadyBody:true,calmLight:true);
                HandpaintFx.PlayAt(point,layer,sheet,size,new Color(.96f,.65f,.24f),new Color(1f,.91f,.70f),angle,
                    orderOffset:119,heldFrame:part.peakFrame,peakHoldSeconds:part.peakHoldSeconds,
                    peakAlpha:.92f,glowAlpha:.14f,desaturate:1f,steadyBody:true,calmLight:true);
                return true;
            }
            HandpaintFx.PlayAt(point, layer, sheet, size, body, core, angle, heldFrame: part.peakFrame,
                peakAlpha: 0.8f, glowAlpha: 0.16f, desaturate: 1f, steadyBody: true, calmLight: true);
            if (!gun) Mark(VfxSpace.Ring, point, new Vector2(0.48f, 0.21f),
                wind ? new Color(0.50f, 0.75f, 0.69f, 0.54f) : new Color(0.89f, 0.65f, 0.46f, 0.58f), angle, layer);
            return true;
        }

        private static void Mark(Sprite art, Vector3 point, Vector2 size, Color color, float angle, int layer, bool preserveInk=false)
        {
            size *= VfxSpace.CardSize;
            // 빠른 전투에서도 국소 문양은 약 0.36초 동안 한 번 부드럽게 보인다.
            float life = 0.36f * Mathf.Max(1f, Time.timeScale);
            SpriteFx.Play(art, point, color, false, size, size, life, angle,
                fadeIn: 0.24f, fadeOutFrom: 0.50f, sortingLayer: layer, order: VfxSpace.OverlaySortingOrder + 38,preserveInk:preserveInk);
        }

        private static void WeaknessStroke(Vector3 center,Vector2 from,Vector2 to,Color color,int layer)
        {
            float card=VfxSpace.CardSize;
            Vector2 delta=to-from;
            Vector3 point=center+(Vector3)((from+to)*.5f*card);
            float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
            float life=.36f*Mathf.Max(1f,Time.timeScale);
            Vector2 size=new(delta.magnitude*card,.020f*card);
            SpriteFx.Play(VfxSpace.GlyphLine,point,new Color(.13f,.22f,.27f,.48f),false,
                size*new Vector2(1.04f,1.40f),size*new Vector2(1.08f,1.40f),life,angle,
                fadeIn:.08f,fadeOutFrom:.64f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+42);
            color.a=.90f;
            SpriteFx.Play(VfxSpace.GlyphLine,point,color,false,size*.94f,size*1.08f,life,angle,
                fadeIn:.08f,fadeOutFrom:.64f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+43);
        }

        private static void TargetStroke(Vector3 center,Vector2 from,Vector2 to,int layer)
        {
            float card=VfxSpace.CardSize;
            Vector2 delta=to-from,size=new(delta.magnitude*card,.015f*card);
            Vector3 at=center+(Vector3)((from+to)*.5f*card);
            float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
            float life=.36f*Mathf.Max(1f,Time.timeScale);
            SpriteFx.Play(VfxSpace.GlyphLine,at,new Color(.12f,.22f,.36f,.90f),false,
                size*new Vector2(1.10f,1.9f),size*new Vector2(1.10f,1.9f),life,angle,
                fadeIn:.08f,fadeOutFrom:.64f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+42,preserveInk:true);
            SpriteFx.Play(VfxSpace.GlyphLine,at,new Color(1f,.94f,.78f,.98f),false,size,size,life,angle,
                fadeIn:.08f,fadeOutFrom:.64f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+43);
        }
    }
}
