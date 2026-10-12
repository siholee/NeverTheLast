using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Effects.Vfx.Presets;
using Entities;
using Entities.Status;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Effects.Vfx
{
    /// <summary>
    /// 메이플식 prepare/cast/hit/affected/screen 정의와 손그림 부품을 읽는다.
    /// 게임 판정은 소유하지 않고, 지정된 hit 정점에서 전달받은 콜백만 한 번 연다.
    /// </summary>
    public static class VfxDefinitionCatalog
    {
        private const string ResourcePath = "Data/96_vfx";
        private static VfxDataList _data;
        private static Dictionary<string, VfxPartData> _parts;
        private static Dictionary<string, VfxSkillData> _skills;
        private static Dictionary<string, string> _statusBindings;
        private static Dictionary<string, string> _classCastBindings;

        public static IReadOnlyList<VfxPartData> Parts => Data.parts;
        public static IReadOnlyList<VfxSkillData> Skills => Data.skills;

        public static VfxSkillData Skill(string key)
        {
            Ensure();
            return !string.IsNullOrWhiteSpace(key) && _skills.TryGetValue(key, out VfxSkillData value)
                ? value
                : null;
        }

        public static VfxPartData Part(string id)
        {
            Ensure();
            return !string.IsNullOrWhiteSpace(id) && _parts.TryGetValue(id, out VfxPartData value)
                ? value
                : null;
        }

        public static string StatusPart(string key)
        {
            Ensure();
            return !string.IsNullOrWhiteSpace(key) && _statusBindings.TryGetValue(key, out string value)
                ? value
                : null;
        }

        public static string ClassCastPart(string vfxClass)
        {
            Ensure();
            return !string.IsNullOrWhiteSpace(vfxClass) &&
                   _classCastBindings.TryGetValue(vfxClass.Trim().ToLowerInvariant(), out string value)
                ? value
                : null;
        }

        private static VfxDataList Data
        {
            get
            {
                Ensure();
                return _data;
            }
        }

        private static void Ensure()
        {
            if (_data != null) return;
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"{ResourcePath}.yaml not found.");
                _data = new VfxDataList();
            }
            else
            {
                _data = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                    .Deserialize<VfxDataList>(asset.text) ?? new VfxDataList();
            }

            _data.parts ??= new List<VfxPartData>();
            _data.skills ??= new List<VfxSkillData>();
            _data.statusBindings ??= new List<VfxStatusBindingData>();
            _data.classCastBindings ??= new List<VfxClassCastBindingData>();
            _parts = _data.parts.Where(item => item != null && !string.IsNullOrWhiteSpace(item.id))
                .GroupBy(item => item.id).ToDictionary(group => group.Key, group => group.Last());
            _skills = _data.skills.Where(item => item != null && !string.IsNullOrWhiteSpace(item.key))
                .GroupBy(item => item.key).ToDictionary(group => group.Key, group => group.Last());
            _statusBindings = _data.statusBindings
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.statusKey) &&
                               !string.IsNullOrWhiteSpace(item.partId))
                .GroupBy(item => item.statusKey).ToDictionary(group => group.Key, group => group.Last().partId);
            _classCastBindings = _data.classCastBindings
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.vfxClass) &&
                               !string.IsNullOrWhiteSpace(item.partId))
                .GroupBy(item => item.vfxClass.Trim().ToLowerInvariant())
                .ToDictionary(group => group.Key, group => group.Last().partId);
        }
    }

    /// <summary>하네스가 단계·방향·정렬·피격 정점을 같은 실제 시간축에서 기록하는 경량 사건.</summary>
    public sealed class VfxSlotPlaybackEvent
    {
        public string SkillKey;
        public string SlotName;
        public string PartId;
        public string Phase;
        public int SlotIndex;
        public Unit Caster;
        public Unit Target;
        public Vector3 WorldPoint;
        public float Angle;
        public bool Mirrored;
        public int SortingLayerId;
        public int SortingOrder;
        public float Realtime;
    }

    /// <summary>96_vfx의 슬롯을 HandpaintFx 합성 레시피로 재생하는 공용 조합기.</summary>
    public static class VfxComposer
    {
        public static event Action<VfxSlotPlaybackEvent> SlotPlayed;

        public static VfxSlotData Slot(string skillKey, string slot, int index = 0)
            => VfxDefinitionCatalog.Skill(skillKey)?.slots?
                .Where(item => item != null && string.Equals(item.slot, slot, StringComparison.OrdinalIgnoreCase))
                .Skip(Mathf.Max(0, index)).FirstOrDefault();

        public static float Offset(string skillKey, string slot, int index = 0, float fallback = 0f)
            => Slot(skillKey, slot, index)?.timeOffset ?? fallback;

        public static FlipbookFx PlaySlot(string skillKey, string slotName, int index,
            Unit caster, Unit target, Vector3 point, int layer, float extraScale = 1f,
            float routeAngle = float.NaN, bool routeFacingRight = true)
        {
            VfxSlotData slot = Slot(skillKey, slotName, index);
            string partId = slot?.partId;
            if (string.Equals(partId, "cast.class.default", StringComparison.OrdinalIgnoreCase))
                partId = VfxDefinitionCatalog.ClassCastPart(caster?.VfxClass);
            VfxPartData part = VfxDefinitionCatalog.Part(partId);
            if (slot == null || part == null) return null;

            if (slotName == "hit" && slot.portraitAnchor) point = VfxSpace.PortraitPoint(target);
            if (slotName == "cast" && caster != null) point = VfxSpace.PortraitPoint(caster);
            ResolveDirection(slot, caster, target, routeAngle, routeFacingRight,
                out float angle, out bool flipX);
            Color body = ParseColor(slot.bodyColor, ParseColor(part.bodyColor, Color.white));
            Color core = ParseColor(slot.coreColor, ParseColor(part.coreColor, Color.white));
            ResolveCasterPalette(slot, caster, ref body, ref core);
            if(part.id.StartsWith("hit.crystal"))body=BattleVfxStyle.Platinum(body);
            LiftLokapalaOnDarkBoard(part, ref body);
            float x = slot.scaleX > 0f ? slot.scaleX : part.scaleX;
            float y = slot.scaleY > 0f ? slot.scaleY : part.scaleY;
            Vector2 size = new(Mathf.Max(0.01f, x), Mathf.Max(0.01f, y));
            size *= Mathf.Max(0.01f, slot.scale * extraScale);
            bool swordDance = slotName == "cast" && skillKey == "jean.tension_rising";
            if (slotName == "cast" && !swordDance)
            {
                float limit=part.id=="cast.loki.shear"?1.32f:part.id=="cast.sword.void_flourish"?1.14f:.90f;
                size *= Mathf.Min(1f,limit/Mathf.Max(size.x,size.y));
                if(part.id.StartsWith("cast.sword",StringComparison.Ordinal))
                    size.y=Mathf.Min(size.y,part.id=="cast.sword.void_flourish"?.56f:.44f);
                if(part.id.StartsWith("cast.class.archer",StringComparison.Ordinal)&&caster!=null)
                {
                    var element=ElementPalette.Parse(caster.Element);
                    body=ElementPalette.PastelColorFor(element); body.a=.88f;
                    if(element==BaseClasses.BaseEnums.UnitElement.Geo)body=BattleVfxStyle.Platinum(body);
                    core=Color.Lerp(body,Color.white,.72f);
                    size=new Vector2(Mathf.Min(size.x,.74f),Mathf.Min(size.y,.42f));
                    var view=CardMotion.ViewOf(caster);
                    if(view?.PortraitRenderer!=null)
                    {
                        Bounds bounds=view.PortraitRenderer.bounds;
                        float side=VfxSpace.AimFrom(caster,target).Direction.x>=0f?1f:-1f;
                        point=new Vector3(bounds.center.x+side*(bounds.extents.x+.10f*VfxSpace.CardSize),
                            Mathf.Lerp(bounds.min.y,bounds.max.y,.45f),bounds.center.z);
                    }
                }
            }
            HandpaintSheet sheet = Sheet(part);
            if (!(slotName == "hit" && slot.portraitAnchor))
                point += new Vector3(slot.offsetX, slot.offsetY) * VfxSpace.CardSize;
            if (Mathf.Abs(slot.forwardOffset) > 0.0001f && caster != null && target != null && caster != target)
            {
                VfxAim aim = VfxSpace.AimFrom(caster, target);
                point += aim.Direction * (slot.forwardOffset * VfxSpace.CardSize);
            }
            if(swordDance)
            {
                Vector3 chest=StatusMotifFx.Anchor(caster);
                float orbit=index*72f*Mathf.Deg2Rad;
                point=chest+new Vector3(Mathf.Sin(orbit)*.52f,Mathf.Cos(orbit)*.20f,0f)*VfxSpace.CardSize;
                size=new Vector2(.72f,.34f);
            }
            else if(slotName=="cast")
            {
                point=VfxSpace.CastPoint(caster,target);
                size=VfxSpace.FitCastSize(caster,size,angle);
            }
            if(slotName=="cast"&&part.id=="cast.dragon.breath")
            {
                point=PortraitAccentFx.DragonMouth(caster);
                size=new Vector2(.44f,.18f);
                angle=VfxSpace.AimFrom(caster,target).AngleDegrees;
            }
            if(slotName=="cast"&&index==0&&skillKey is "himiko.blaze_focus" or "uchi.clone" or "jean.tension_rising")
                PortraitAccentFx.SelfGuard(caster,target,false,body,instant:skillKey=="uchi.clone");
            if (slotName == "hit" && part.renderer == "clawTriple")
            {
                // 이 경로는 표시만 소유한다. 피해는 호출한 전투 코드의 기존 콜백이 소유한다.
                VfxRunner.Run(PlayHitSlot(skillKey,index,caster,target,null,extraScale));
                return null;
            }
            NotifySlot(skillKey, slotName, part.id, "start", index, caster, target, point,
                angle, flipX, layer, slot.orderOffset != 0 ? slot.orderOffset : 30);
            if(part.id=="cast.sword.void_flourish")PortraitAccentFx.CastRim(caster,target,body);
            if(slotName=="prepare"&&part.id=="prepare.beast.claw")PortraitAccentFx.BeastPrepare(caster);
            if(slotName=="prepare"&&part.id=="prepare.heavy.impact")PortraitAccentFx.HeavyPrepare(caster);
            if(slotName=="cast"&&part.id=="cast.dragon.breath")PortraitAccentFx.DragonBreathPrepare(caster,target);
            if(slotName=="cast"&&index==0&&skillKey=="varuna.ultimate")YamaVarunaFx.LawInvocation(caster,target);

            if (part.renderer == "statusMotif")
            {
                StatusMotifFx.Play(target, part.id);
                NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point, angle, flipX, layer, .10f);
                return null;
            }
            if(part.id=="affected.buff.moon")
            {
                PortraitAccentFx.MoonBlessing(target);
                NotifyCustomPeak(skillKey,slotName,part,index,caster,target,point,angle,flipX,layer,.13f);
                return null;
            }
            if(part.id=="affected.debuff.blaze")
            {
                PortraitAccentFx.BlazeMark(target);
                HandpaintFx.PlayAt(StatusMotifFx.Anchor(target),layer,Sheet(part),new Vector2(.40f,.36f),
                    new Color(1f,.68f,.36f,.14f),Color.clear,orderOffset:130,peakAlpha:.04f,
                    glowAlpha:0f,desaturate:1f,calmLight:true)?.ClipPortraitHud(target);
                NotifyCustomPeak(skillKey,slotName,part,index,caster,target,point,angle,flipX,layer,.10f);
                return null;
            }

            if (LokapalaFx.PlayMotif(part, caster, target, point, layer, size, angle))
            {
                NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point,
                    angle, flipX, layer, part.id=="cast.surya.muzzle"?part.peakFrame/Mathf.Max(1f,part.fps):0.10f);
                return null;
            }
            if(part.id=="cast.uchi.wind")PortraitAccentFx.WindFlow(caster);
            if(part.id=="affected.yama.detonation")YamaVarunaFx.DetonationCue(target);
            if(part.id=="affected.yama.verdict")YamaVarunaFx.VerdictCue(target);
            if(part.id.StartsWith("affected.yama.",StringComparison.Ordinal))
            {
                body.a*=.36f;
                size*=.72f;
            }

            if(part.id=="cast.anubis.soul")
            {
                angle=0f;
                size=new Vector2(.90f,1.05f)*Mathf.Max(.01f,slot.scale*extraScale);
            }

            if (string.Equals(part.renderer, "casterTwin", StringComparison.OrdinalIgnoreCase))
            {
                PlayCasterTwin(part, slot, caster, target, point, layer, size, body, core, angle, flipX,
                    slot.recolor);
                NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point,
                    angle, flipX, layer, 0.10f);
                return null;
            }
            if (string.Equals(part.renderer, "beneficialAura", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part.renderer, "supportBuff", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportBuff(target, point, layer, size, body, core, slot.supportInk);
                NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point,
                    angle, flipX, layer, 0.13f);
                return null;
            }
            if (string.Equals(part.renderer, "supportCleanse", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportCleanse(target, point, layer, size, body, core);
                NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point,
                    angle, flipX, layer, 0.13f);
                return null;
            }
            if (string.Equals(part.renderer, "supportHeal", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportHeal(target, point, layer, size, body, core,
                    slot.supportScale, slot.supportInk);
                NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point,
                    angle, flipX, layer, 0.13f);
                return null;
            }
            if (string.Equals(part.renderer, "shieldMembrane", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part.renderer, "supportShield", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportShield(target, point, layer, size, body, core);
                if(part.id=="affected.buff.crystal_shield")PortraitAccentFx.CrystalWard(target);
                NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point,
                    angle, flipX, layer, 0.14f);
                return null;
            }
            // 전진형 클래스는 카드 움직임 자체가 시전이므로 데이터 슬롯만 기록하고 별도 광원을 만들지 않는다.
            if (string.Equals(part.renderer, "cardAdvance", StringComparison.OrdinalIgnoreCase))
            {
                if(part.id is "cast.anubis.book" or "cast.uchi.guard")
                {
                    PortraitAccentFx.SelfGuard(caster,target,part.id=="cast.anubis.book",guard:part.id=="cast.uchi.guard");
                    NotifyCustomPeak(skillKey,slotName,part,index,caster,target,point,angle,flipX,layer,.10f);
                }
                return null;
            }
            if (string.IsNullOrWhiteSpace(part.sheet)) return null;
            int peakFrame = slot.peakFrame >= 0 ? slot.peakFrame :
                part.peakFrame >= 0 ? part.peakFrame : Mathf.Max(0, part.columns * part.rows / 2);
            if (part.edgeUnderlay)
            {
                // 총구광은 한 부품의 진한 외곽과 밝은 몸체를 겹쳐 양쪽 배경에서 같은 실루엣을 유지한다.
                Color edge = part.id.StartsWith("cast.class.archer",StringComparison.Ordinal)
                    ? Color.Lerp(body,new Color(.19f,.27f,.38f,.46f),.54f)
                    : ParseColor(slot.edgeColor, ParseColor(part.edgeColor, Color.clear));
                if (edge.a > 0.01f)
                {
                    Vector2 edgeSize = size * 1.10f;
                    Vector3 edgeCenter = VisualCenterForAnchor(point, edgeSize, part, angle, flipX);
                    if(slotName=="cast"&&!swordDance)edgeCenter=BelowFace(caster,edgeCenter,edgeSize,angle);
                    Color edgeCore = new(edge.r, edge.g, edge.b, 0.24f);
                    HandpaintFx.PlayAt(edgeCenter, layer, sheet, edgeSize, edge, edgeCore, angle,
                        (slot.orderOffset != 0 ? slot.orderOffset : 30) - 1,
                        0.76f, 0.22f, false, flipX, peakFrame, part.peakHoldSeconds,desaturate:1f,calmLight:true);
                }
            }
            Vector3 visualCenter = part.id=="cast.anubis.soul"?point:VisualCenterForAnchor(point,size,part,angle,flipX);
            if(slotName=="cast"&&!swordDance&&part.id!="cast.anubis.soul"&&part.id!="cast.dragon.breath")visualCenter=BelowFace(caster,visualCenter,size,angle);
            if(slotName=="cast"&&part.id=="cast.dragon.breath")visualCenter=point;
            if(slotName=="cast"&&part.id=="cast.uchi.wind"&&caster!=null)
            {
                // 구매 시트도 바람 세 획과 같은 손 쪽에 둔다. 반대쪽 머리 장식과 겹치지 않는다.
                visualCenter=VfxSpace.WorldPoint(caster)+Vector3.right*(caster.IsEnemy?.52f:-.52f)*VfxSpace.CardSize;
                visualCenter.y=VfxSpace.PortraitHudTop(caster)+.13f*VfxSpace.CardSize;
            }
            if(slotName=="cast"&&part.id.StartsWith("cast.magic.",StringComparison.Ordinal))
            {
                // 불투명 구체를 시전 표식으로 쓰면 평평한 얼룩이 된다. 구매 시트는 작은 기운으로 남기고 얇은 문양으로 의미를 준다.
                body=Color.Lerp(body,Color.white,.22f);
                body.a=Mathf.Min(body.a,part.sheet.StartsWith("Ball1",StringComparison.Ordinal)?.30f:.56f);
                PlayMagicCastAccent(caster,part,visualCenter,layer,core);
            }
            if (slot.steadyBody) DrawInk(point, layer, part, size, angle, flipX, peakFrame);
            FlipbookFx fx = HandpaintFx.PlayAt(visualCenter, layer, sheet, size, body, core, angle,
                slot.orderOffset != 0 ? slot.orderOffset : 30,
                Mathf.Clamp01(part.peakAlpha), Mathf.Clamp01(part.glowAlpha), false, flipX,
                peakFrame, part.peakHoldSeconds, steadyBody: slot.steadyBody, desaturate: WindOnDark(part), startFrame: part.startFrame, tailHoldFrame: part.tailHoldFrame, tailHoldSeconds: part.tailHoldSeconds, calmLight: part.calmLight, paintContrast:PaintContrast(part));
            // 일반 플립북도 정점 사건을 남겨야 시전 가시성을 실제 캡처 프레임으로 잴 수 있다.
            // 시작 사건만 있으면 짧은 총구광이 사라진 뒤의 이론상 시트 길이로 잘못 통과할 수 있다.
            NotifyCustomPeak(skillKey, slotName, part, index, caster, target, point,
                angle, flipX, layer, Mathf.Max(0, peakFrame - part.startFrame) / Mathf.Max(1f, part.fps));
            return fx;
        }

        public static IEnumerator PlayHitSlot(string skillKey, int index, Unit caster, Unit target,
            Action onPeak, float extraScale = 1f)
        {
            VfxSlotData slot = Slot(skillKey, "hit", index);
            VfxPartData part = VfxDefinitionCatalog.Part(slot?.partId);
            if (slot == null || part == null)
            {
                onPeak?.Invoke();
                yield break;
            }

            ResolveDirection(slot, caster, target, float.NaN, true, out float eventAngle, out bool eventFlipX);
            // 공격 충돌은 원화 중심에 붙인다. 바닥/진형은 PlayPointHitSlot의 별도 계약이다.
            bool portraitAnchor = true;
            Vector3 Anchor()
            {
                Vector3 point = VfxSpace.PortraitPoint(target) + new Vector3(slot.offsetX, slot.offsetY) * VfxSpace.CardSize;
                var view = CardMotion.ViewOf(target);
                return view != null ? view.ClampPortraitHitPoint(point) : point;
            }
            Vector3 eventPoint = Anchor();
            int eventLayer = VfxSpace.SortingLayer(target);
            int eventOrder = slot.orderOffset != 0 ? slot.orderOffset : 30;
            NotifySlot(skillKey, "hit", part.id, "start", index, caster, target, eventPoint,
                eventAngle, eventFlipX, eventLayer, eventOrder);
            Action heavyCrestAtPeak = null;
            Action visualPeakNotification = () =>
            {
                heavyCrestAtPeak?.Invoke();
                if(skillKey is "himiko.ember" or "pygmalion.ember" or "agni.follow_up" or "agni.focused_follow_up")
                    PortraitAccentFx.Fire(caster,target,skillKey);
                if(skillKey=="chandra.sachi"&&index==2)PortraitAccentFx.Moon(caster,target);
                if(part.id=="hit.fire")PortraitAccentFx.FlameStrikeCore(target);
                if(part.id=="hit.indra.vajra")PortraitAccentFx.VajraContact(caster,target);
                if(part.id=="hit.varuna.judgment")YamaVarunaFx.BalanceCue(target);
                if(part.id=="hit.explosion.small")PortraitAccentFx.SmallExplosion(target);
                if(part.id=="hit.enemy.archer_pierce")PortraitAccentFx.EnemyPierce(caster,target);
                if(part.id=="hit.enemy.crusher_heavy")PortraitAccentFx.EnemyCrusher(target);
                if(part.id=="hit.smoke.fire_mark")PortraitAccentFx.FlameStrikeCore(target);
                if(part.id=="hit.smoke.horizontal.afterglow")PortraitAccentFx.SmokeSweep(target,true);
                if(part.id.StartsWith("hit.crystal",StringComparison.Ordinal))PortraitAccentFx.CrystalFragments(target,part.id=="hit.crystal.debris");
                if(part.id is "hit.fire.burst" or "hit.fire.sculptor_burst")
                    PortraitAccentFx.FireContact(target,part.id=="hit.fire.sculptor_burst");
                if(part.id=="hit.lightning.portrait")PortraitAccentFx.LightningContact(target);
                if(part.id=="hit.kubera.treasury")LokapalaFx.TreasuryGlints(target);
                if(part.id=="hit.uchi.overflow")PortraitAccentFx.OverflowRipple(target);
                if(part.id=="hit.vayu.gale")PortraitAccentFx.WindStrikeFlow(target);
                if(part.id is "hit.jean.impact" or "hit.impact.small" or "hit.jean.punch_pressure")
                    PortraitAccentFx.PressureHit(target,part.id=="hit.jean.punch_pressure");
                if(part.id=="hit.jean.smoke")PortraitAccentFx.SmokeSweep(target,true);
                if(part.id is "hit.slash.aqua" or "hit.slash.aqua_finisher")
                    PortraitAccentFx.AquaSlash(target,ParseColor(part.coreColor,Color.white),part.id=="hit.slash.aqua_finisher");
                if(part.id is "hit.explosion.large" or "hit.moonlight" or "hit.moonlight.changya" or "hit.impact.large")
                    PortraitAccentFx.HeavyContact(target,ParseColor(part.coreColor,Color.white),
                        part.id is "hit.moonlight" or "hit.moonlight.changya",part.id=="hit.explosion.large");
                // 전용 피격 문양은 논리 키가 아니라 부품이 소유한다. 단독 검수와 실제 일제사격도 같은 정점을 쓴다.
                if(part.id is "hit.marie.normal" or "hit.tsukuyomi.normal" or "hit.napoleon.normal")
                    PortraitAccentFx.ElementContact(target,part.id[4..]);
                NotifySlot(skillKey, "hit", part.id, "peak", index, caster, target, Anchor(),
                    eventAngle, eventFlipX, eventLayer, eventOrder);
            };
            Action peakCallback = () =>
            {
                visualPeakNotification();
                if (string.Equals(part.renderer, "lokapalaCoinImpact", StringComparison.OrdinalIgnoreCase))
                    LokapalaFx.CoinImpact(target, eventAngle, slot.coinBurst);
                onPeak?.Invoke();
            };

            if (string.Equals(part.renderer, "clawTriple", StringComparison.OrdinalIgnoreCase))
            {
                yield return PlayClawHit(slot, part, caster, target, peakCallback, extraScale);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(part.sheet))
            {
                onPeak?.Invoke();
                yield break;
            }

            float angle = eventAngle;
            bool flipX = eventFlipX;
            Color body = ParseColor(slot.bodyColor, ParseColor(part.bodyColor, Color.white));
            Color core = ParseColor(slot.coreColor, ParseColor(part.coreColor, Color.white));
            ResolveCasterPalette(slot, caster, ref body, ref core);
            LiftLokapalaOnDarkBoard(part, ref body);
            if(part.id.StartsWith("hit.crystal", StringComparison.Ordinal)) body=BattleVfxStyle.Platinum(body);
            float x = slot.scaleX > 0f ? slot.scaleX : part.scaleX;
            float y = slot.scaleY > 0f ? slot.scaleY : part.scaleY;
            Vector2 size = new(Mathf.Max(0.01f, x), Mathf.Max(0.01f, y));
            size *= Mathf.Max(0.01f, slot.scale * extraScale);
            VfxPartData accentPart = VfxDefinitionCatalog.Part(part.accentPartId);
            HandpaintSheet? accent = accentPart != null && !string.IsNullOrWhiteSpace(accentPart.sheet)
                ? Sheet(accentPart)
                : null;
            int peak = slot.peakFrame >= 0 ? slot.peakFrame :
                part.peakFrame >= 0 ? part.peakFrame : Mathf.Max(0, part.columns * part.rows / 2);
            Action visualPeakCallback = peakCallback;
            if (slot.impactDelay >= 0f)
            {
                // 긴 잔상을 위해 플립북 FPS를 낮춰도 피해 판정 계약은 원래 시각에 정확히 한 번 유지한다.
                VfxRunner.Run(InvokeAfterDelay(slot.impactDelay, () =>
                {
                    NotifySlot(skillKey, "hit", part.id, "impact", index, caster, target, eventPoint,
                        eventAngle, eventFlipX, eventLayer, eventOrder);
                    onPeak?.Invoke();
                }));
                // peak는 원화의 실제 정점 프레임, impact는 보존한 전투 판정 시각을 각각 기록한다.
                visualPeakCallback = visualPeakNotification;
            }
            if(part.id=="hit.smoke.horizontal")
            {
                PlayFormationSmokeRow(part,new[]{target},Anchor(),size,body,
                    ParseColor(slot.edgeColor,Color.clear),core,angle,flipX,peak,eventLayer,visualPeakCallback,skillKey=="shi.final_bell.9");
                yield return new WaitForSeconds(Mathf.Max(.20f,part.columns*part.rows/Mathf.Max(1f,part.fps)));
                yield break;
            }
            // 방향성 시트는 그림 중심이 아니라 실제 충돌 끝점을 대상 초상화 중심에 맞춘다.
            // 좌우 반전과 회전을 먼저 반영한 뒤 그만큼 원화 중심을 반대로 민다.
            Vector2 rotatedAnchor = RotatedAnchorOffset(size, part, angle, flipX);
            Vector3 fittedCenter=FitTargetVisual(target,Anchor()-(Vector3)(rotatedAnchor*VfxSpace.CardSize),ref size,angle);
            Vector2 visualOffset = (Vector2)(fittedCenter - VfxSpace.PortraitPoint(target)) / VfxSpace.CardSize;
            if(part.id=="hit.lightning.formation")
                visualOffset=(Vector2)(FitPortraitVisual(new[]{target},Anchor(),ref size)-VfxSpace.PortraitPoint(target))/VfxSpace.CardSize;
            if (string.Equals(slot.direction, "horizontalFacing", StringComparison.OrdinalIgnoreCase))
            {
                // Smoke3 수평 피격은 같은 원화를 한 부품의 외곽·몸체로 합성한다.
                // 밝은 판에서는 진한 외곽이, 어두운 판에서는 자홍 몸체와 휘도 코어가 실루엣을 보존한다.
                Vector3 impactPoint = fittedCenter;
                Color edge = ParseColor(slot.edgeColor, ParseColor(part.edgeColor, Color.clear));
                if (edge.a > 0.01f)
                {
                    Vector2 edgeSize = new(size.x * 1.05f, size.y * 1.12f);
                    Vector3 edgeCenter = VisualCenterForAnchor(impactPoint, edgeSize, part, angle, flipX);
                    Color edgeCore = new(edge.r, edge.g, edge.b, Mathf.Min(edge.a, 0.28f));
                    VfxRunner.Run(HandpaintFx.PlayPoint(edgeCenter, eventLayer, Sheet(part), edgeSize,
                        edge, edgeCore, null, angle, flipX, peak, 0.72f, 0.24f, true, part.peakHoldSeconds));
                }

                Vector3 visualCenter = fittedCenter;
                VfxRunner.Run(HandpaintFx.PlayPoint(visualCenter, eventLayer, Sheet(part), size,
                    body, core, visualPeakCallback, angle, flipX, peak,
                    Mathf.Clamp01(part.peakAlpha), Mathf.Clamp01(part.glowAlpha), true, part.peakHoldSeconds));
                // 같은 Smoke 원화의 좁은 휘도 코어를 몸체 안에 넣는다. 별도 부품 사건은 남기지 않는다.
                Vector2 coreSize = new(size.x * 0.92f, size.y * 0.52f);
                Vector3 coreCenter = fittedCenter;
                Color coreBody = new(core.r, core.g, core.b, 0.34f);
                yield return HandpaintFx.PlayPoint(coreCenter, eventLayer, Sheet(part), coreSize,
                    coreBody, core, null, angle, flipX, peak, part.id=="hit.enemy.archer_pierce"?.03f:.36f, 0.20f, true, part.peakHoldSeconds);
                yield break;
            }
            if (portraitAnchor)
                DrawInk(Anchor(), eventLayer, part, size, angle, flipX, peak, target, portraitAnchor,
                    part.peakHoldSeconds);
            if(part.id is "hit.explosion.large" or "hit.moonlight" or "hit.impact.large" or "hit.moonlight.changya" or
                "hit.fire.burst" or "hit.fire.sculptor_burst")
            {
                // 넓은 불빛을 진하게 만들지 않고, 몸통 안의 구매 원화 실루엣만 또렷하게 남긴다.
                bool fire=part.id is "hit.fire.burst" or "hit.fire.sculptor_burst";
                Vector2 crest=size*(fire ? .58f : .56f);
                Vector3 crestPoint=StatusMotifFx.Anchor(target);
                Color crestBody=Color.Lerp(body,core,fire ? .08f : .45f);
                if(part.id=="hit.explosion.large")
                {
                    // 폭발의 짙은 초기 셀을 중복 재생하지 않는다. 실제 정점부터 국소 잔광만 남긴다.
                    heavyCrestAtPeak=()=>HandpaintFx.PlayAt(crestPoint,eventLayer,Sheet(part),crest,crestBody,core,angle,
                        orderOffset:35,peakAlpha:.30f,glowAlpha:.12f,flipX:flipX,heldFrame:peak,
                        peakHoldSeconds:VfxTiming.AtLeastFrames(.08f,4),desaturate:.85f,steadyBody:true,
                        startFrame:peak,calmLight:true,paintContrast:PaintContrast(part))?.ClipPortraitHud(target);
                }
                else HandpaintFx.PlayAt(crestPoint,eventLayer,Sheet(part),crest,crestBody,core,angle,
                    orderOffset:35,peakAlpha:fire ? .36f : .45f,glowAlpha:.12f,flipX:flipX,heldFrame:peak,
                    peakHoldSeconds:Mathf.Max(part.peakHoldSeconds,VfxTiming.AtLeastFrames(fire ? .04f : .08f,fire?3:4)),desaturate:fire ? .65f : .45f,steadyBody:true,
                    calmLight:true,paintContrast:PaintContrast(part))?.ClipPortraitHud(target);
            }
            yield return HandpaintFx.PlayHit(target, Sheet(part), size, body, core, visualPeakCallback,
                angle, flipX, peak, accent, visualOffset,
                part.peakHoldSeconds,
                part.useHitAlpha ? part.peakAlpha : 0.86f,
                part.useHitAlpha ? part.glowAlpha : 0.60f, portraitAnchor, steadyBody: slot.steadyBody, desaturate: WindOnDark(part), tailHoldFrame: part.tailHoldFrame, tailHoldSeconds: part.tailHoldSeconds, calmLight: part.calmLight, paintContrast:PaintContrast(part));
        }

        /// <summary>
        /// 만종처럼 개별 카드가 아니라 진영 중앙에 뜨는 피격 슬롯을 재생한다.
        /// 방향 기준 대상은 로그와 좌우 반전에만 쓰고, 실제 좌표는 고정해 카드 사망에도 흔들리지 않는다.
        /// </summary>
        public static IEnumerator PlayPointHitSlot(string skillKey, int index, Unit caster,
            Unit directionTarget, Vector3 point, Action onPeak, float extraScale = 1f)
        {
            VfxSlotData slot = Slot(skillKey, "hit", index);
            VfxPartData part = VfxDefinitionCatalog.Part(slot?.partId);
            if (slot == null || part == null || string.IsNullOrWhiteSpace(part.sheet))
            {
                onPeak?.Invoke();
                yield break;
            }

            ResolveDirection(slot, caster, directionTarget, float.NaN, true,
                out float angle, out bool flipX);
            Vector3 eventPoint = point + new Vector3(slot.offsetX, slot.offsetY) * VfxSpace.CardSize;
            int layer = VfxSpace.SortingLayer(directionTarget != null ? directionTarget : caster);
            int order = slot.orderOffset != 0 ? slot.orderOffset : 30;
            NotifySlot(skillKey, "hit", part.id, "start", index, caster, directionTarget,
                eventPoint, angle, flipX, layer, order);

            Color body = ParseColor(slot.bodyColor, ParseColor(part.bodyColor, Color.white));
            Color core = ParseColor(slot.coreColor, ParseColor(part.coreColor, Color.white));
            float x = slot.scaleX > 0f ? slot.scaleX : part.scaleX;
            float y = slot.scaleY > 0f ? slot.scaleY : part.scaleY;
            Vector2 size = new(Mathf.Max(0.01f, x), Mathf.Max(0.01f, y));
            size *= Mathf.Max(0.01f, slot.scale * extraScale);
            int peak = slot.peakFrame >= 0 ? slot.peakFrame :
                part.peakFrame >= 0 ? part.peakFrame : Mathf.Max(0, part.columns * part.rows / 2);
            Vector3 visualCenter = VisualCenterForAnchor(eventPoint, size, part, angle, flipX);
            if(part.id=="hit.lightning.formation"&&directionTarget!=null)
                visualCenter=FitPortraitVisual(new[]{directionTarget},VfxSpace.PortraitPoint(directionTarget),ref size);
            yield return HandpaintFx.PlayPoint(visualCenter, layer, Sheet(part), size, body, core, () =>
            {
                NotifySlot(skillKey, "hit", part.id, "peak", index, caster, directionTarget,
                    eventPoint, angle, flipX, layer, order);
                onPeak?.Invoke();
            }, angle, flipX, peak, Mathf.Clamp01(part.peakAlpha), Mathf.Clamp01(part.glowAlpha));
        }

        /// <summary>
        /// 한 장의 직선 원화를 세 줄로 벌려 시간차 와이프한다. 몸체는 알파, 좁은 칼심만 가산해
        /// 밝은 판에서도 검정 밑칠·원색 몸체·백색 하이라이트의 손그림 명암을 보존한다.
        /// </summary>
        private static IEnumerator PlayClawHit(VfxSlotData slot, VfxPartData part,
            Unit caster, Unit target, Action onPeak, float extraScale)
        {
            if (target == null)
            {
                onPeak?.Invoke();
                yield break;
            }

            VfxAim clawAim = VfxSpace.AimFrom(caster, target);
            float tilt = Mathf.Abs(slot.angleOffset);
            // 할퀴기는 오른쪽 공격에서 좌하→우상으로 뻗고, 반대 진영은 이를 거울상으로 쓴다.
            // 공용 검격의 붓 기울기 규칙과 반대인 이유는 직선 발톱 원화 자체가 진행 방향을 갖기 때문이다.
            float angle = part.id=="hit.loki.fenrir"
                ? (clawAim.FacingRight ? 12f : 168f)
                : clawAim.AngleDegrees + (clawAim.FacingRight ? tilt : -tilt);
            Color body = ParseColor(slot.bodyColor, ParseColor(part.bodyColor,
                new Color(0.018f, 0.025f, 0.036f, 0.94f)));
            Color edge = ParseColor(slot.edgeColor, ParseColor(part.edgeColor,
                new Color(0.10f, 0.72f, 0.92f, 0.92f)));
            Color core = ParseColor(slot.coreColor, ParseColor(part.coreColor, Color.white));
            ResolveClawPalette(slot, caster, ref body, ref edge, ref core);

            float scale = Mathf.Max(0.01f, slot.scale * extraScale);
            float xScale = slot.scaleX > 0f ? slot.scaleX : 1f;
            float yScale = slot.scaleY > 0f ? slot.scaleY : 1f;
            // 사람형 아군은 얼굴과 정보판 사이가 좁다. 끝까지 그려진 세 줄도 몸통 띠 안에 남긴다.
            float bodyFit = target.IsEnemy ? 1f : .68f;
            bool narrowHumanClaw = !target.IsEnemy && part.id != "hit.claw.triple.heavy";
            var style = new ClawRakeStyle
            {
                Sprite = VfxArt.HandpaintTexture(part.sheet, VfxArt.HandpaintClawStroke),
                LineCount = 3,
                SourceAngleDegrees = part.sourceAngle,
                LengthInCards = Mathf.Max(0.40f, part.lengthInCards * scale * xScale * bodyFit),
                ThicknessInCards = Mathf.Max(0.04f, part.thicknessInCards * scale * yScale * bodyFit),
                AngleDegrees = angle,
                DrawDuration = part.drawDuration,
                CenterCrossFraction = part.centerCrossFraction,
                LineDelay = part.lineDelay,
                LineSpacingInCards = part.lineSpacingInCards * scale * bodyFit,
                LingerDuration = part.lingerDuration,
                FadeDuration = part.fadeDuration,
                RevealSoftness = part.revealSoftness,
                TipWidth = part.tipWidth,
                CardKickStrength = part.cardKickStrength * Mathf.Sqrt(scale),
                PortraitWidthFraction = part.id=="hit.loki.fenrir" ? .86f : 1.10f,
                PortraitHeightFraction = part.id=="hit.loki.fenrir" ? .20f : .42f,
                // 세 줄이 적의 머리 전체를 덮지 않도록 실제 HUD 위 몸통 띠로 내려 붙인다.
                OffsetInCards = new Vector2(slot.offsetX, slot.offsetY)+(Vector2)(
                    StatusMotifFx.Anchor(target)+Vector3.right*(narrowHumanClaw ? .16f : .10f)*VfxSpace.CardSize
                    -VfxSpace.PortraitPoint(target)-Vector3.up*(target.IsEnemy ? (part.id=="hit.loki.fenrir" ? .005f : .10f) : narrowHumanClaw ? .18f : .15f)*VfxSpace.CardSize)/VfxSpace.CardSize,
                BodyColor = body,
                EdgeColor = edge,
                CoreColor = core,
            };

            Vector3 fallback = VfxSpace.PortraitPoint(target);
            int layer = VfxSpace.SortingLayer(target);
            yield return ClawRakeFx.Play(target, fallback, layer, style, () =>
            {
                PlayClawSupport(part.accentPartId, target, edge, core, scale * 0.54f, 26);
                PlayClawSupport(part.secondaryPartId, target, edge, core, scale * 0.46f, 22);
                onPeak?.Invoke();
            });
        }

        /// <summary>
        /// Smoke3 한 장을 진영 중앙에서 좌우로 펼치고, 중앙에서 가까운 대상부터 연기가 닿는 순서로
        /// 기존 피해 콜백을 한 번씩 연다. 대상 사이를 잇는 칼날이나 투사체는 만들지 않는다.
        /// </summary>
        public static IEnumerator PlayHorizontalSweepHit(string skillKey, int index, Unit caster,
            Unit directionTarget, Vector3 center, IReadOnlyList<Unit> targets, Action<Unit> onImpact,
            float extraScale = 1f)
        {
            VfxSlotData slot = Slot(skillKey, "hit", index);
            VfxPartData part = VfxDefinitionCatalog.Part(slot?.partId);
            List<Unit> alive = targets?.Where(unit => unit != null && unit.isActive).ToList()
                               ?? new List<Unit>();
            if (slot == null || part == null || string.IsNullOrWhiteSpace(part.sheet) || alive.Count == 0)
            {
                foreach (Unit unit in alive) onImpact?.Invoke(unit);
                yield break;
            }

            ResolveDirection(slot, caster, directionTarget, float.NaN, true,
                out float angle, out bool flipX);
            Vector3 point = alive.Aggregate(Vector3.zero,(sum,u)=>sum+VfxSpace.PortraitPoint(u))/alive.Count;
            int layer = VfxSpace.SortingLayer(directionTarget != null ? directionTarget : caster);
            int order = slot.orderOffset != 0 ? slot.orderOffset : 30;
            NotifySlot(skillKey, "hit", part.id, "start", index, caster, directionTarget,
                point, angle, flipX, layer, order);

            Color body = ParseColor(slot.bodyColor, ParseColor(part.bodyColor, Color.white));
            Color edge = ParseColor(slot.edgeColor, Color.clear);
            Color core = ParseColor(slot.coreColor, ParseColor(part.coreColor, Color.white));
            float x = slot.scaleX > 0f ? slot.scaleX : part.scaleX;
            float y = slot.scaleY > 0f ? slot.scaleY : part.scaleY;
            Vector2 size = new(Mathf.Max(0.01f, x), Mathf.Max(0.01f, y));
            size *= Mathf.Max(0.01f, slot.scale * extraScale);
            int peak = slot.peakFrame >= 0 ? slot.peakFrame :
                part.peakFrame >= 0 ? part.peakFrame : Mathf.Max(0, part.columns * part.rows / 2);
            bool peakNotified = false;
            Action peakCallback = () =>
            {
                if(peakNotified)return;
                peakNotified = true;
                NotifySlot(skillKey, "hit", part.id, "peak", index, caster, directionTarget,
                    point, angle, flipX, layer, order);
            };
            // 빈 진영 중앙 대신 실제 원화 행마다 같은 팽창을 동시 합성한다.
            // 시각 사본만 분리하며 정점 사건·대상별 판정의 소유권과 시각은 하나로 보존한다.
            var rows=new List<List<Unit>>();
            foreach(Unit unit in alive.OrderByDescending(u=>VfxSpace.PortraitPoint(u).y))
            {
                var row=rows.LastOrDefault();
                if(row==null||Mathf.Abs(VfxSpace.PortraitPoint(row[0]).y-VfxSpace.PortraitPoint(unit).y)>VfxSpace.CardSize*.45f)
                {row=new List<Unit>();rows.Add(row);}
                row.Add(unit);
            }
            foreach(var row in rows)
            {
                Vector3 rowPoint=row.Aggregate(Vector3.zero,(sum,u)=>sum+VfxSpace.PortraitPoint(u))/row.Count;
                PlayFormationSmokeRow(part,row,rowPoint,size,body,edge,core,angle,flipX,peak,layer,peakCallback,skillKey=="shi.final_bell.9");
            }

            float farthest = alive.Max(unit => Mathf.Abs(VfxSpace.WorldPoint(unit).x - point.x));
            List<Unit> ordered = alive.OrderBy(unit => Mathf.Abs(VfxSpace.WorldPoint(unit).x - point.x)).ToList();
            float elapsed = 0f;
            foreach (Unit target in ordered)
            {
                float distance = Mathf.Abs(VfxSpace.WorldPoint(target).x - point.x);
                float due = 0.055f + (farthest > 0.001f ? distance / farthest : 0f) * 0.075f;
                if (due > elapsed) yield return new WaitForSeconds(due - elapsed);
                elapsed = due;
                NotifySlot(skillKey, "hit", part.id, "impact", index, caster, target,
                    VfxSpace.WorldPoint(target), angle, flipX, VfxSpace.SortingLayer(target), order);
                if(skillKey is "shi.final_bell.6" or "shi.final_bell.9")
                    PortraitAccentFx.Contact(target,core);
                onImpact?.Invoke(target);
            }
            float total = Mathf.Max(0.20f, Mathf.Max(1, part.columns * part.rows) / Mathf.Max(1f, part.fps));
            if (elapsed < total) yield return new WaitForSeconds(total - elapsed);
            if (!peakNotified)
                NotifySlot(skillKey, "hit", part.id, "peak", index, caster, directionTarget,
                    point, angle, flipX, layer, order);
        }

        private static Vector3 FitPortraitVisual(IReadOnlyList<Unit> receivers,Vector3 center,ref Vector2 size)
        {
            var portraits=receivers.Where(u=>u!=null).Select(CardMotion.ViewOf)
                .Where(view=>view!=null&&view.PortraitRenderer!=null).Select(view=>view.PortraitRenderer.bounds).ToArray();
            if(portraits.Length==0)return center;
            float minX=portraits.Min(b=>b.min.x),maxX=portraits.Max(b=>b.max.x);
            float minY=Mathf.Max(portraits.Min(b=>Mathf.Lerp(b.min.y,b.max.y,.28f)),
                receivers.Where(u=>u!=null).Min(VfxSpace.PortraitHudTop)+.025f*VfxSpace.CardSize);
            float maxY=portraits.Max(b=>Mathf.Lerp(b.min.y,b.max.y,.94f));
            size.x=Mathf.Min(size.x,(maxX-minX)/VfxSpace.CardSize);
            size.y=Mathf.Min(size.y,Mathf.Min(.62f,(maxY-minY)/VfxSpace.CardSize));
            center.x=Mathf.Clamp(center.x,minX+size.x*VfxSpace.CardSize*.5f,maxX-size.x*VfxSpace.CardSize*.5f);
            center.y=Mathf.Clamp(center.y,minY+size.y*VfxSpace.CardSize*.5f,maxY-size.y*VfxSpace.CardSize*.5f);
            return center;
        }

        /// <summary>회전된 피격 면 전체를 HUD 위 원화에 맞춘다. 판정 앵커와 콜백 시간은 바꾸지 않는다.</summary>
        private static Vector3 FitTargetVisual(Unit target,Vector3 center,ref Vector2 size,float angle)
        {
            var view=CardMotion.ViewOf(target);
            if(view?.PortraitRenderer==null)return center;
            Bounds b=view.PortraitRenderer.bounds;
            float card=VfxSpace.CardSize;
            float bottom=Mathf.Max(b.min.y,VfxSpace.PortraitHudTop(target))+.035f*card;
            float top=b.max.y-.025f*card;
            float availableHeight=Mathf.Max(.08f*card,top-bottom);
            float radians=angle*Mathf.Deg2Rad;
            float cos=Mathf.Abs(Mathf.Cos(radians)),sin=Mathf.Abs(Mathf.Sin(radians));
            Vector2 extent=new((cos*size.x+sin*size.y)*card,(sin*size.x+cos*size.y)*card);
            // 전체 시트 사각형을 짧은 가슴 띠에 넣으면 투명 여백까지 축소되어 타격이 사라진다.
            // 원래 획의 크기를 카드 1.55배까지 살리고, 정보판 경계는 합성 셰이더에서 페이드한다.
            float fit=Mathf.Min(1f,1.55f*card/Mathf.Max(.001f,Mathf.Max(extent.x,extent.y)));
            size*=fit;
            center.x=Mathf.Clamp(center.x,b.min.x-.12f*card,b.max.x+.12f*card);
            center.y=Mathf.Clamp(center.y,bottom+.12f*card,Mathf.Max(bottom+.12f*card,top-.10f*card));
            return center;
        }

        private static void PlayFormationSmokeRow(VfxPartData part,IReadOnlyList<Unit> receivers,
            Vector3 center,Vector2 size,Color body,Color edge,Color core,float angle,bool flipX,int peak,int layer,Action onPeak,bool finalTier=false)
        {
            // 연기는 퍼지는 면이다. 낮은 충돌 앵커로 하늘에 밀지 않고 최종 사각형을 받는 원화에 맞춘다.
            if(receivers.Count>1)
            {
                bool applied=false;
                foreach(Unit receiver in receivers.Where(unit=>unit!=null))
                    PlayFormationSmokeRow(part,new[]{receiver},VfxSpace.PortraitPoint(receiver),size,body,edge,core,
                        angle,flipX,peak,layer,()=>{if(!applied){applied=true;onPeak?.Invoke();}},finalTier);
                return;
            }
            foreach(Unit receiver in receivers.Where(unit=>unit!=null))
                VfxRunner.Run(InvokeAfterDelay(Mathf.Max(0,peak)/Mathf.Max(1f,part.fps),()=>PortraitAccentFx.SmokeSweep(receiver,false)));
            if(finalTier)body=Color.Lerp(body,new Color(.64f,.29f,.43f,body.a),.30f);
            body.a=Mathf.Min(body.a,.14f);
            center=FitPortraitVisual(receivers,center,ref size);
            if(edge.a>.01f)
            {
                Vector2 edgeSize=size*1.025f;
                Vector3 edgeCenter=FitPortraitVisual(receivers,center,ref edgeSize);
                VfxRunner.Run(HandpaintFx.PlayPoint(edgeCenter,layer,Sheet(part),edgeSize,edge,Color.clear,
                    null,angle,flipX,peak,Mathf.Min(finalTier?.28f:.18f,part.peakAlpha),.12f,true,calmLight:part.calmLight,desaturate:1f));
            }
            VfxRunner.Run(HandpaintFx.PlayPoint(center,layer,Sheet(part),size,body,core,
                onPeak,angle,flipX,peak,Mathf.Min(finalTier?.12f:.08f,part.peakAlpha),Mathf.Min(.20f,part.glowAlpha),true,calmLight:part.calmLight,desaturate:1f));
            Vector2 coreSize=new(size.x*.94f,size.y*.48f);
            VfxRunner.Run(HandpaintFx.PlayPoint(center,layer,Sheet(part),coreSize,core,core,
                null,angle,flipX,peak,finalTier?.20f:.10f,finalTier?.22f:.14f,true,calmLight:part.calmLight,desaturate:1f));
        }

        private static void PlayClawSupport(string partId, Unit target, Color body, Color core,
            float scale, int orderOffset)
        {
            VfxPartData support = VfxDefinitionCatalog.Part(partId);
            if (target == null || support == null || string.IsNullOrWhiteSpace(support.sheet)) return;
            Vector2 size = new(Mathf.Max(0.01f, support.scaleX * scale),
                Mathf.Max(0.01f, support.scaleY * scale));
            HandpaintFx.PlayAt(VfxSpace.WorldPoint(target), VfxSpace.SortingLayer(target), Sheet(support),
                size, body, core, 0f, orderOffset,
                Mathf.Min(0.46f, support.peakAlpha), Mathf.Min(0.34f, support.glowAlpha));
        }

        /// <summary>
        /// 한 번의 시전 뒤 YAML hit 슬롯을 각 timeOffset에 병렬로 연다.
        /// 판정 콜백은 triggersImpact가 붙은 정점 하나에서만 호출한다.
        /// </summary>
        public static IEnumerator PlaySingleTarget(string skillKey, Unit caster, Unit target,
            Action onImpact, float extraScale = 1f, float minimumDuration = 0f)
        {
            VfxSkillData skill = VfxDefinitionCatalog.Skill(skillKey);
            if (skill?.slots == null || caster == null || target == null)
            {
                onImpact?.Invoke();
                yield break;
            }

            PlayScreen(skillKey, caster, target);
            foreach (VfxSlotData slot in skill.slots.Where(item => item != null &&
                         (string.Equals(item.slot, "prepare", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(item.slot, "cast", StringComparison.OrdinalIgnoreCase))))
            {
                VfxRunner.Run(PlayDelayedSlot(skillKey, slot.slot, SlotIndex(skill, slot),
                    caster, target, VfxSpace.WorldPoint(caster), VfxSpace.SortingLayer(caster),
                    extraScale));
            }

            List<VfxSlotData> hits = skill.slots.Where(item => item != null &&
                string.Equals(item.slot, "hit", StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count == 0)
            {
                onImpact?.Invoke();
                yield break;
            }

            int completed = 0;
            bool invoked = false;
            for (int i = 0; i < hits.Count; i++)
            {
                int index = i;
                VfxSlotData hit = hits[i];
                VfxRunner.Run(PlayDelayedHit(skillKey, index, hit.timeOffset, caster, target,
                    hit.triggersImpact ? () =>
                    {
                        if (invoked) return;
                        invoked = true;
                        onImpact?.Invoke();
                    } : null, extraScale, () => completed++));
            }

            float elapsed = 0f;
            float guard = Mathf.Max(3f, minimumDuration + 1f);
            while ((completed < hits.Count || elapsed < minimumDuration) && guard > 0f)
            {
                elapsed += Time.deltaTime;
                guard -= Time.deltaTime;
                yield return null;
            }
            if (!invoked)
            {
                // 감시 한계 뒤 도착하는 정점도 같은 판정을 다시 호출할 수 없다.
                invoked = true;
                onImpact?.Invoke();
            }
        }

        /// <summary>시전자 카드에서 한 번 시전한 뒤 대상 위치의 hit만 짧은 시간차로 연다.</summary>
        public static IEnumerator PlayMultiTargetHits(string skillKey, Unit caster,
            IReadOnlyList<Unit> targets, Action<Unit> onImpact, float impactSpreadSeconds,
            float extraScale = 1f, bool playCasterSlots = true)
        {
            List<Unit> alive = targets?.Where(unit => unit != null && unit.isActive).ToList()
                               ?? new List<Unit>();
            if (caster == null || alive.Count == 0) yield break;

            IReadOnlyList<VfxTargetHit> hits = VfxSpace.BuildTargetHitSequence(alive, impactSpreadSeconds);
            PlayScreen(skillKey, caster, alive);
            VfxSkillData skill = VfxDefinitionCatalog.Skill(skillKey);
            if (playCasterSlots && skill?.slots != null)
            {
                foreach (VfxSlotData slot in skill.slots.Where(item => item != null &&
                             (string.Equals(item.slot, "prepare", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(item.slot, "cast", StringComparison.OrdinalIgnoreCase))))
                {
                    Vector3 point = VfxSpace.WorldPoint(caster);
                    PlaySlot(skillKey, slot.slot, SlotIndex(skill, slot), caster, alive[0], point,
                        VfxSpace.SortingLayer(caster), extraScale);
                }
            }

            int pending = 0;
            yield return VfxSpace.ResolveTargetHits(hits, hit =>
            {
                if (hit.Target == null) return;
                pending++;
                Unit captured = hit.Target;
                VfxRunner.Run(Chain(PlayHitSlot(skillKey, 0, caster, captured,
                    () => onImpact?.Invoke(captured), extraScale), () => pending--));
            });

            float guard = 2f;
            while (pending > 0 && guard > 0f)
            {
                guard -= Time.deltaTime;
                yield return null;
            }
        }

        public static void PlayAffectedSlot(string skillKey, int index, Unit caster, Unit target,
            float extraScale = 1f)
        {
            if (target == null) return;
            PlaySlot(skillKey, "affected", index, caster, target, VfxSpace.WorldPoint(target),
                VfxSpace.SortingLayer(target), extraScale);
        }

        public static void PlayScreen(string skillKey, Unit caster, Unit target)
            => PlayScreenAt(skillKey, caster, target, target != null ? VfxSpace.WorldPoint(target) : VfxSpace.WorldPoint(caster));

        /// <summary>광역 행동은 첫 대상 한 명이 아니라 실제 피격 진영의 중심으로 두 번째 포커스를 옮긴다.</summary>
        public static void PlayScreen(string skillKey, Unit caster, IReadOnlyList<Unit> targets)
        {
            Unit logTarget = targets?.FirstOrDefault(unit => unit != null && unit.isActive);
            Vector3 center = VfxSpace.WorldPoint(logTarget ?? caster);
            if (targets != null)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (Unit unit in targets)
                {
                    if (unit == null || !unit.isActive) continue;
                    sum += VfxSpace.WorldPoint(unit);
                    count++;
                }
                if (count > 0) center = sum / count;
            }
            PlayScreenAt(skillKey, caster, logTarget, center);
        }

        private static void PlayScreenAt(string skillKey, Unit caster, Unit target, Vector3 secondary)
        {
            VfxSlotData slot = Slot(skillKey, "screen");
            VfxPartData part = VfxDefinitionCatalog.Part(slot?.partId);
            if (part == null) return;
            Vector3 from = VfxSpace.WorldPoint(caster);
            Vector3 to = secondary;
            NotifySlot(skillKey, "screen", part.id, "start", 0, caster, target,
                Vector3.Lerp(from, to, 0.5f), 0f, false, VfxSpace.SortingLayer(caster), 0);
            // 파스텔 캐릭터의 행동에 반응하는 지원 연출도 같은 밝은 화면 문맥을 따른다.
            // 다른 캐릭터가 자기 행동을 할 때의 원래 화면 파라미터는 보존한다.
            int executingId = Managers.GameManager.Instance?.ActionScheduler?.ExecutingUnit?.ID ?? -1;
            // 표준 톤·흔들림 부품은 자신의 강도를 쓴다. 지원 캐릭터 감쇠가 전부를 무음화하지 않게 한다.
            bool independentScreen = part.id.StartsWith("screen.tone.", StringComparison.Ordinal)
                || part.id is "screen.shake.medium" or "screen.flash.front";
            bool calmScreen = !independentScreen && (part.id != "screen.focus.ultimate" || skillKey == "pygmalion.sharp_thorns" || caster?.ID is 61 or 62 or 64 or 66 or 67 || executingId is 61 or 62 or 64 or 66 or 67);
            bool actionFocus = part.id.StartsWith("screen.focus.", StringComparison.Ordinal)
                || part.id.StartsWith("screen.yv.",StringComparison.Ordinal);
            float focusStrength = actionFocus
                ? Mathf.Min(part.focusStrength, part.id == "screen.focus.ultimate" && calmScreen ? 0.24f : 1f)
                : calmScreen ? Mathf.Min(part.focusStrength, 0.08f) : part.focusStrength;
            SkillFx.PlayScreen(new SkillFxScreenCue
            {
                FocusStrength = focusStrength,
                FadeIn = calmScreen ? Mathf.Max(part.focusFadeIn, 0.16f) : part.focusFadeIn,
                Hold = part.focusHold,
                FadeOut = calmScreen ? Mathf.Max(part.focusFadeOut, 0.30f) : part.focusFadeOut,
                Tint = actionFocus ? new Color(0.16f, 0.23f, 0.34f) : calmScreen ? new Color(0.71f, 0.74f, 0.88f) : ParseColor(slot?.bodyColor, ParseColor(part.bodyColor, new Color(0.10f, 0.14f, 0.22f))),
                FlashColor = ParseColor(slot?.coreColor, ParseColor(part.coreColor, Color.white)),
                FlashStrength = calmScreen ? 0f : part.flashStrength,
                Shake = calmScreen ? 0f : part.shake,
            }, from, to);
        }

        public static void PlayAffectedPart(Unit target, string partId, Unit caster = null)
        {
            VfxPartData part = VfxDefinitionCatalog.Part(partId);
            if (target == null || part == null) return;
            Vector3 point = VfxSpace.WorldPoint(target);
            int layer = VfxSpace.SortingLayer(target);
            VfxAim aim = VfxSpace.AimFrom(caster, target);
            NotifySlot("status.auto", "affected", part.id, "start", 0, caster, target,
                point, aim.AngleDegrees, !aim.FacingRight, layer, 118);
            NotifyCustomPeak("status.auto", "affected", part, 0, caster, target,
                point, aim.AngleDegrees, !aim.FacingRight, layer, 0.13f);
            Color body = ParseColor(part.bodyColor, Color.white);
            Color core = ParseColor(part.coreColor, Color.white);
            ResolveSupportPalette(caster, ref body, ref core);
            Vector2 size = new(part.scaleX, part.scaleY);
            // 자동 상태 표시도 직접 시전과 같은 고유 문양을 쓴다. 상태 판정/부여 성공률은 바꾸지 않는다.
            if(part.id=="affected.yama.detonation")YamaVarunaFx.DetonationCue(target);
            if(part.id=="affected.yama.verdict")YamaVarunaFx.VerdictCue(target);
            if(part.id.StartsWith("affected.yama.",StringComparison.Ordinal))
            {
                body.a*=.36f;
                size*=.72f;
            }
            if (string.Equals(part.renderer, "statusMotif", StringComparison.OrdinalIgnoreCase))
            {
                StatusMotifFx.Play(target, part.id);
                return;
            }
            if (string.Equals(part.renderer, "beneficialAura", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part.renderer, "supportBuff", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportBuff(target, point, layer, size, body, core, part.inkAlpha > 0f);
                return;
            }
            if (string.Equals(part.renderer, "supportCleanse", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportCleanse(target, point, layer, size, body, core);
                return;
            }
            if (string.Equals(part.renderer, "supportHeal", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportHeal(target, point, layer, size, body, core);
                return;
            }
            if (string.Equals(part.renderer, "shieldMembrane", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part.renderer, "supportShield", StringComparison.OrdinalIgnoreCase))
            {
                PlaySupportShield(target, point, layer, size, body, core);
                return;
            }
            if(part.renderer=="yvShield"&&YamaVarunaFx.PlayMotif(part,caster,target,point,layer,size,0f))return;
            if (VedicTrioFx.PlayMotif(part, caster, target, point, layer, size, 0f)) return;
            if (string.IsNullOrWhiteSpace(part.sheet)) return;
            HandpaintSheet sheet = Sheet(part);
            Vector3 visualCenter = VisualCenterForAnchor(point, size, part, 0f, false);
            if (part.inkAlpha > 0f) DrawInk(point, layer, part, size, 0f, false, part.peakFrame);
            HandpaintFx.PlayAt(visualCenter, layer, sheet, size, body, core, 0f, 118,
                Mathf.Clamp01(part.peakAlpha), Mathf.Clamp01(part.glowAlpha), heldFrame: part.peakFrame,
                peakHoldSeconds: part.peakHoldSeconds, steadyBody: part.inkAlpha > 0f, desaturate: part.calmLight ? part.desaturate : 0f, calmLight: part.calmLight);
        }

        /// <summary>팩의 작은 불꽃을 카드 중앙과 적 방향 끝에 한 번씩 놓는 caster 공용 시전.</summary>
        internal static void PlayCasterTwin(VfxPartData part, VfxSlotData slot, Unit caster, Unit target,
            Vector3 center, int layer, Vector2 size, Color body, Color core, float angle, bool flipX, bool recolor = false)
        {
            if (string.IsNullOrWhiteSpace(part.sheet)) return;
            VfxPartData motif=part;
            bool generic=part.id=="cast.class.caster";
            if (caster != null && string.IsNullOrWhiteSpace(slot.bodyColor))
            {
                var element=ElementPalette.Parse(caster.Element);
                body=ElementPalette.PastelColorFor(element);
                if(element==BaseClasses.BaseEnums.UnitElement.Geo)body=BattleVfxStyle.Platinum(body);
                if(string.IsNullOrWhiteSpace(slot.coreColor))core=Color.Lerp(body,Color.white,.78f);
                recolor=true;
            }
            // 화염 시트는 투명 여백이 커서 공용 사각형 크기만 늘리면 실제 불꽃이 손에서 사라진다.
            // 손 바깥의 두 갈래를 세로로 세우고 동일 16칸 재생으로 움직임을 유지한다.
            if(generic && caster!=null && ElementPalette.Parse(caster.Element)==BaseClasses.BaseEnums.UnitElement.Pyro)
            {
                var casterView=CardMotion.ViewOf(caster);
                Vector3 hand=VfxSpace.CastPoint(caster,target);
                float side=VfxSpace.AimFrom(caster,target).Direction.x>=0f?1f:-1f;
                if(casterView?.PortraitRenderer!=null)
                    hand.x=casterView.PortraitRenderer.bounds.center.x+side*casterView.PortraitRenderer.bounds.size.x*.33f;
                var fire=new HandpaintSheet("Fire1_Red",4,4,64f,new Vector2(.5f,.5f));
                Vector2 flameSize=VfxSpace.FitCastSize(caster,new Vector2(.62f,.78f),0f);
                hand=BelowFace(caster,hand,flameSize,0f);
                Color flame=new(.91f,.43f,.20f,.96f), light=new(1f,.79f,.49f,.50f);
                int fireOrder=slot.orderOffset!=0?slot.orderOffset:30;
                for(int i=-1;i<=1;i+=2)
                {
                    Vector3 position=hand+Vector3.right*i*.055f*VfxSpace.CardSize;
                    HandpaintFx.PlayAt(position,layer,fire,flameSize*1.045f,new Color(.42f,.22f,.13f,.62f),Color.clear,
                        i*9f,fireOrder-1,.44f,0f,false,false,heldFrame:4,peakHoldSeconds:.03f,desaturate:1f,steadyBody:true,calmLight:true);
                    HandpaintFx.PlayAt(position,layer,fire,flameSize,flame,light,i*9f,fireOrder,
                        .88f,.12f,false,false,heldFrame:4,peakHoldSeconds:.03f,desaturate:1f,steadyBody:true,calmLight:true)?.ScaleEnvelope(.70f,1.40f,.32f);
                }
                casterView?.PulseVfxLight(flame,.12f,.16f);
                return;
            }
            // 저작 색은 유지하되 실루엣은 실제 캐릭터의 원소에 맞춘다.
            if(generic&&caster!=null)
            {
                var element=ElementPalette.Parse(caster.Element);
                recolor=true;
                {
                    string key=element switch
                    {
                        BaseClasses.BaseEnums.UnitElement.Geo or BaseClasses.BaseEnums.UnitElement.Cryo => "hit.crystal.shatter",
                        BaseClasses.BaseEnums.UnitElement.Electro or BaseClasses.BaseEnums.UnitElement.Void => "hit.yama.lightning",
                        BaseClasses.BaseEnums.UnitElement.Anemo or BaseClasses.BaseEnums.UnitElement.Dendro => "cast.wind.vortex",
                        BaseClasses.BaseEnums.UnitElement.Hydro => "hit.varuna.water",
                        BaseClasses.BaseEnums.UnitElement.Pyro => part.id,
                        _ => "hit.impact.small"
                    };
                    motif=VfxDefinitionCatalog.Part(key)??part;
                }
            }
            if(generic&&caster!=null)
            {
                Vector3 hand=VfxSpace.CastPoint(caster,target);
                Color signal=body;
                if(ElementPalette.Parse(caster.Element)==BaseClasses.BaseEnums.UnitElement.Geo)
                    signal=new Color(.85f,.63f,.24f,.86f);
                else signal=Color.Lerp(body,new Color(body.r*.55f,body.g*.58f,body.b*.65f,body.a),.28f);
                float card=VfxSpace.CardSize;
                int signalOrder=slot.orderOffset!=0?slot.orderOffset:30;
                SpriteFx.Play(VfxSpace.Ring,hand,new Color(.18f,.26f,.34f,.48f),false,
                    .29f*card,.42f*card,.22f,0f,20f,.03f,.48f,SpriteFx.Ease.OutCubic,layer,signalOrder);
                SpriteFx.Play(VfxArt.Diamond,hand,signal,false,
                    new Vector2(.16f,.24f)*card,new Vector2(.24f,.34f)*card,.22f,20f,35f,.035f,.48f,SpriteFx.Ease.OutCubic,layer,signalOrder+1);
            }
            size*=Mathf.Min(1f,.96f/Mathf.Max(size.x,size.y));
            // 신호는 얼굴 대신 손·가슴 높이에 둔다. 원소별 실루엣과 짧은 국소 반사가 주체를 알려 준다.
            center=VfxSpace.CastPoint(caster,target);
            VfxAim aim=VfxSpace.AimFrom(caster,target);
            Vector3 edge=center+aim.Direction*(.12f*VfxSpace.CardSize);
            size=VfxSpace.FitCastSize(caster,size,angle);
            HandpaintSheet sheet=Sheet(motif);
            if(generic&&caster!=null&&ElementPalette.Parse(caster.Element)==BaseClasses.BaseEnums.UnitElement.Pyro)
                sheet=new HandpaintSheet("Fire1_Red",4,4,64f,new Vector2(.5f,.5f));
            int order=slot.orderOffset!=0?slot.orderOffset:30;
            Vector2 centerSize=size*.72f;
            Vector3 centerVisual=BelowFace(caster,VisualCenterForAnchor(center,centerSize,motif,angle,flipX),centerSize,angle);
            Vector3 edgeVisual=BelowFace(caster,VisualCenterForAnchor(edge,size,motif,angle,flipX),size,angle);
            int peak=motif.peakFrame>=0?motif.peakFrame:Mathf.Max(0,motif.columns*motif.rows/2);
            // 밝은 판에서는 좁은 비가산 받침만 남긴다. 크기를 늘린 두꺼운 테두리는 만들지 않는다.
            Color underlay=new(.12f,.20f,.26f,.32f);
            HandpaintFx.PlayAt(edgeVisual,layer,sheet,size*1.04f,underlay,Color.clear,angle,order-1,
                .32f,0f,false,flipX,heldFrame:peak,peakHoldSeconds:.03f,desaturate:1f,steadyBody:true,calmLight:true);
            if(recolor&&!generic)
            {
                DrawInk(center,layer,motif,centerSize,angle,flipX,peak);
                DrawInk(edge,layer,motif,size,angle,flipX,peak);
            }
            HandpaintFx.PlayAt(centerVisual,layer,sheet,centerSize,body,core,angle,order,
                Mathf.Min(.64f,part.peakAlpha),Mathf.Min(.20f,part.glowAlpha),false,flipX,
                heldFrame:peak,peakHoldSeconds:.03f,desaturate:recolor?1f:0f,steadyBody:recolor,calmLight:true)?.ScaleEnvelope(.70f,1.28f,.32f);
            HandpaintFx.PlayAt(edgeVisual,layer,sheet,size,body,core,angle,order+1,
                Mathf.Min(.74f,part.peakAlpha),Mathf.Min(.20f,part.glowAlpha),false,flipX,
                heldFrame:peak,peakHoldSeconds:.03f,desaturate:recolor?1f:0f,steadyBody:recolor,calmLight:true)?.ScaleEnvelope(.70f,1.28f,.32f);
            var view=CardMotion.ViewOf(caster);
            if(view!=null)view.PulseVfxLight(body,.12f,.16f);
        }

        // 회전과 시트의 충돌 앵커까지 반영한 최종 사각형을 얼굴 아래로 내린다.
        // 고정 offset만 내리면 긴 호·총구광의 윗부분이 여전히 입과 턱을 가린다.
        private static void PlayMagicCastAccent(Unit caster,VfxPartData part,Vector3 point,int layer,Color core)
        {
            float c=VfxSpace.CardSize;
            bool grand=part.id.EndsWith("grand",StringComparison.Ordinal);
            bool warm=part.id is "cast.magic.fire" or "cast.magic.sculptor" or "cast.magic.geo" or "cast.magic.geo_grand";
            Vector2 extent=grand?new Vector2(.72f,.52f):new Vector2(.56f,.48f);
            point=BelowFace(caster,point,extent,0f);
            Color rim=warm?new Color(.45f,.32f,.25f,.65f):new Color(.18f,.34f,.50f,.65f);
            Color light=warm?new Color(1f,.83f,.60f,.86f):new Color(.77f,.91f,1f,.86f);
            float life=VfxTiming.AtLeastFrames(grand?.50f:.36f,6);
            SpriteFx.Play(VfxArt.RuneOrnate,point,rim,false,extent*c*.68f,extent*c,life,
                spin:grand?28f:-24f,fadeIn:.08f,fadeOutFrom:.56f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+33);
            SpriteFx.Play(VfxArt.RuneRadiant,point,light,false,extent*c*.64f,extent*c*.94f,life,
                spin:grand?28f:-24f,fadeIn:.08f,fadeOutFrom:.56f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+34);
            Sprite glyph=part.id.Contains("moon")?VfxArt.Crescent:warm?VfxArt.Diamond:VfxArt.Glint;
            SpriteFx.Play(glyph,point,Color.Lerp(core,light,.45f),false,.16f*c,.25f*c,life*.74f,
                fadeIn:.05f,fadeOutFrom:.52f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+35);
        }

        private static Vector3 BelowFace(Unit caster,Vector3 center,Vector2 size,float angle)
        {
            var view=CardMotion.ViewOf(caster);
            if(view?.PortraitRenderer==null)return center;
            Bounds bounds=view.PortraitRenderer.bounds;
            float radians=angle*Mathf.Deg2Rad;
            float halfX=(Mathf.Abs(Mathf.Cos(radians))*size.x+Mathf.Abs(Mathf.Sin(radians))*size.y)*VfxSpace.CardSize*.5f;
            float halfY=(Mathf.Abs(Mathf.Sin(radians))*size.x+Mathf.Abs(Mathf.Cos(radians))*size.y)*VfxSpace.CardSize*.5f;
            Camera camera=Camera.main;
            Rect hud=view.HudScreenRect(camera);
            float hudTop=bounds.min.y;
            if(camera!=null&&hud.height>0f)
            {
                float depth=camera.WorldToScreenPoint(bounds.center).z;
                hudTop=camera.ScreenToWorldPoint(new Vector3(hud.center.x,hud.yMax,depth)).y;
            }
            // 전신 스프라이트의 bounds에는 HUD 아래의 투명 여백도 포함된다.
            // 그 비율로 내리면 시전이 발밑에 놓인다. 실제 HUD 위 손·가슴 공간을 사용한다.
            float lower=hudTop+halfY+.025f*VfxSpace.CardSize;
            // 확대된 시전도 HUD 위 손·가슴 공간에 남기고 얼굴 아래에서 끝낸다.
            float upper=Mathf.Lerp(hudTop,bounds.max.y,.68f)-halfY;
            bool overlapsPortrait=center.x+halfX>bounds.min.x&&center.x-halfX<bounds.max.x;
            if(overlapsPortrait&&lower>upper)
            {
                float side=center.x>=bounds.center.x?1f:-1f;
                center.x=bounds.center.x+side*bounds.extents.x*.80f;
                center.y=Mathf.Max(lower,Mathf.Lerp(hudTop,bounds.max.y,.30f));
            }
            else center.y=overlapsPortrait?Mathf.Clamp(center.y,lower,upper):Mathf.Max(center.y,lower);
            return center;
        }

        // 어두운 판에서는 초록/금색 원화와 틴트가 곱해져 중간 명도로 묻힌다. 몸체색만 보정하고 크기는 유지한다.
        // 어두운 판에서는 원본의 녹색 채널만 곱하면 밝은 바람 틴트가 사라진다.
        // 명도에 동일 팔레트를 입히며 밝은 판과 기존 부품은 그대로 둔다.
        private static float WindOnDark(VfxPartData part)
            => part.darkDesaturate && BattlefieldBackdrop.CurrentBackgroundLuminance < 0.48f ? 1f : part.desaturate;

        private static void LiftLokapalaOnDarkBoard(VfxPartData part, ref Color body)
        {
            if (string.IsNullOrWhiteSpace(part.darkBodyColor)) return;
            float weight = 1f - Mathf.InverseLerp(0.12f, 0.48f, BattlefieldBackdrop.CurrentBackgroundLuminance);
            Color lit = ParseColor(part.darkBodyColor, body);
            lit.a = body.a;
            body = Color.Lerp(body, lit, weight * 0.85f);
        }

        // 밝은 바닥과 비슷한 휘도의 녹색·금빛 몸체를 크기 증가 없이 한 부품의 진한 외곽으로 분리한다.
        private static float PaintContrast(VfxPartData part)
            => part?.id is "hit.fire.burst" or "hit.fire.sculptor_burst" ? .50f :
                part?.id is "hit.explosion.large" or "hit.moonlight" or "hit.impact.large" or
                "hit.moonlight.changya" or "hit.fire.burst" or "hit.fire.sculptor_burst" ? .90f :
                part?.id=="cast.loki.shear" ? .75f : 0f;

        private static void DrawInk(Vector3 anchor, int layer, VfxPartData part, Vector2 size,
            float angle, bool flipX, int peak, Unit follow = null, bool followPortrait = false, float extraHold = 0f)
        {
            if (string.IsNullOrWhiteSpace(part.sheet) || part.inkAlpha <= 0f) return;
            Vector2 outer = size * 1.18f;
            Vector3 center = VisualCenterForAnchor(anchor, outer, part, angle, flipX);
            if(followPortrait && follow != null)center=FitTargetVisual(follow,center,ref outer,angle);
            Color ink = ParseColor(part.inkColor, new Color(0.09f, 0.065f, 0.10f, 0.94f));
            ink.a = part.inkAlpha;
            FlipbookFx fx = HandpaintFx.PlayAt(center, layer, Sheet(part), outer, ink, Color.clear,
                angle, 29, ink.a, 0f, false, flipX, peak, Mathf.Max(Mathf.Max(part.peakHoldSeconds, extraHold), part.renderer == "casterTwin" ? 0.10f : 0f), desaturate: 1f, steadyBody: true, startFrame: part.startFrame);
            if (follow != null) fx?.Follow(() => followPortrait ? VfxSpace.PortraitPoint(follow) : VfxSpace.WorldPoint(follow),
                center - (followPortrait ? VfxSpace.PortraitPoint(follow) : VfxSpace.WorldPoint(follow)));
            if (followPortrait) fx?.ClipPortraitHud(follow);
        }

        /// <summary>데이터가 적은 시작점·충돌점을 월드 목표점에 고정하도록 원화 중심을 역산한다.</summary>
        private static Vector3 VisualCenterForAnchor(Vector3 desiredAnchor, Vector2 size,
            VfxPartData part, float angle, bool flipX)
        {
            Vector2 offset = RotatedAnchorOffset(size, part, angle, flipX);
            return desiredAnchor - new Vector3(offset.x, offset.y, 0f);
        }

        private static Vector2 RotatedAnchorOffset(Vector2 size, VfxPartData part,
            float angle, bool flipX)
        {
            float anchorX = flipX ? 1f - part.impactAnchorX : part.impactAnchorX;
            Vector2 anchorFromCenter = new(
                (anchorX - 0.5f) * size.x,
                (part.impactAnchorY - 0.5f) * size.y);
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(
                anchorFromCenter.x * Mathf.Cos(radians) - anchorFromCenter.y * Mathf.Sin(radians),
                anchorFromCenter.x * Mathf.Sin(radians) + anchorFromCenter.y * Mathf.Cos(radians));
        }

        private enum SupportVisualKind
        {
            Buff,
            Cleanse,
            Heal,
            Shield,
        }

        /// <summary>강화는 머리 위 Impact1이 역재생으로 모이고, 끝점이 실제 상태 아이콘으로 내려간다.</summary>
        private static void PlaySupportBuff(Unit target, Vector3 point, int layer, Vector2 size,
            Color body, Color core, bool outline = false)
        {
            VfxRunner.Run(PlaySupportSequence(target, point, layer, body, core, SupportVisualKind.Buff, 1f, outline));
        }

        /// <summary>정화는 청백 Smoke5가 안으로 걷히고 하단 상태열이 응답해 제거를 읽힌다.</summary>
        private static void PlaySupportCleanse(Unit target, Vector3 point, int layer, Vector2 size,
            Color body, Color core)
            => VfxRunner.Run(PlaySupportSequence(target, point, layer, body, core,
                SupportVisualKind.Cleanse));

        /// <summary>치유는 녹백 Impact1 한 번과 체력 게이지 팝으로 의미를 고정한다.</summary>
        private static void PlaySupportHeal(Unit target, Vector3 point, int layer, Vector2 size,
            Color body, Color core, float healScale = 1f, bool outline = false)
            => VfxRunner.Run(PlaySupportSequence(target, point, layer, body, core,
                SupportVisualKind.Heal, healScale, outline));

        /// <summary>보호막은 은회색 Ball1 구체가 몸통에서 한 바퀴 돌고 보호막 게이지로 접힌다.</summary>
        private static void PlaySupportShield(Unit target, Vector3 point, int layer, Vector2 size,
            Color body, Color core)
        {
            VfxRunner.Run(PlaySupportSequence(target, point, layer, body, core, SupportVisualKind.Shield));
        }

        private static IEnumerator PlaySupportSequence(Unit target, Vector3 point, int layer,
            Color body, Color core, SupportVisualKind kind, float healScale = 1f, bool addInk = false)
        {
            if (target == null) yield break;
            float card = VfxSpace.CardSize;
            HandpaintSheet sheet;
            Vector2 visualSize;
            Vector3 visualPoint;
            bool reverse;
            bool shield = kind == SupportVisualKind.Shield;
            bool health = kind == SupportVisualKind.Heal;
            bool buff = kind == SupportVisualKind.Buff;
            switch (kind)
            {
                case SupportVisualKind.Buff:
                    // 흰 원본을 써야 시전자색/장미색 재착색이 녹색 채널에 오염되지 않는다.
                    sheet = HandpaintFx.Impact1White;
                    visualSize = new Vector2(0.84f, 0.72f);
                    // 얼굴을 덮지 않고 머리 위에서 수렴하도록 카드 중심보다 충분히 올린다.
                    visualPoint = VfxSpace.PortraitPoint(target) + Vector3.up * card * 0.31f;
                    reverse = true;
                    break;
                case SupportVisualKind.Cleanse:
                    sheet = HandpaintFx.Smoke5Blue;
                    visualSize = new Vector2(.68f, .62f);
                    visualPoint = StatusMotifFx.Anchor(target) + Vector3.up * card * .01f;
                    body = new Color(.72f,.91f,.88f,.25f);
                    reverse = true;
                    core = new Color(0.86f, 0.98f, 1f, 1f);
                    break;
                case SupportVisualKind.Heal:
                    // 흰 원본에 민트 팔레트를 입혀 녹색 원본의 네온 코어가 치유 꽃잎을 덮지 않게 한다.
                    sheet = HandpaintFx.Impact1White;
                    visualSize = (addInk ? new Vector2(.76f, .70f) : new Vector2(.70f, .64f)) * healScale;
                    visualPoint = StatusMotifFx.Anchor(target) + Vector3.down * card * 0.05f;
                    reverse = false;
                    body = new Color(.53f, .78f, .66f, .70f);
                    core = new Color(0.82f, 1f, 0.88f, 1f);
                    break;
                default:
                    sheet = HandpaintFx.Ball1Blue;
                    visualSize = new Vector2(.86f, .54f);
                    // 구체 중심을 가슴께로 내려 얼굴 영역을 비운다.
                    visualPoint = StatusMotifFx.Anchor(target) - Vector3.up * card * .09f;
                    reverse = false;
                    // 보호막은 시전자 속성과 무관하게 은회색으로 고정한다.
                    body = new Color(0.66f, 0.79f, 0.88f, 0.12f);
                    core = new Color(0.92f, 0.95f, 0.98f, 1f);
                    break;
            }

            // ink는 별개 버프가 아니라 같은 강화 표시의 대비 받침이다.
            if(buff && addInk) body=Color.Lerp(body,new Color(.88f,.78f,.48f,body.a),.20f);
            SkillFxCue cue = HandpaintFx.Cue(sheet, visualSize, body, core,
                shield ? 210f : 0f, false, sheet.Columns * sheet.Rows - 1,
                Vector2.zero, shield ? .12f : health ? .52f : kind==SupportVisualKind.Cleanse ? .24f : addInk ? .68f : .70f, shield ? .10f : health ? .10f : addInk ? .15f : .22f);
            if (addInk)
            {
                if (cue.FrameDelays.Length > 3) cue.FrameDelays[3] += 0.10f;
            }
            cue.ReverseFrames = reverse;
            cue.OrderOffset = 118;
            cue.Composite.ContactShadowColor = Color.clear;
            cue.Composite.GroundLightColor = Color.clear;
            // 밝은 배경 보정색도 슬롯 팔레트에서 만들지 않으면 녹색 원본이 장미색 버프에 남는다.
            cue.Composite.BrightBackdropLift = Color.Lerp(body, core, 0.58f);
            cue.Composite.BrightBackdropLiftStrength = addInk && !health ? 0.08f : health ? 0.25f : shield ? .12f : 0.46f;
            cue.Composite.GlowThreshold = 0.48f;
            cue.Composite.GlowSoftness = 0.20f;
            cue.Composite.GlowGain = health ? 1f : addInk ? .85f : 1.25f;
            // 원본 시트의 녹색/청색이 남지 않게 명도만 취한 뒤 시전자 팔레트로 다시 칠한다.
            cue.Composite.Desaturate = 1f;
            cue.Composite.StartScale = shield ? 0.90f : 1.04f;
            cue.Composite.CrestScale = shield ? 1.04f : 0.98f;
            cue.Composite.EndScale = shield ? 0.94f : 0.82f;
            if (addInk)
            {
                cue.Composite.StartScale = cue.Composite.CrestScale = cue.Composite.EndScale = health ? 1f : 1.08f;
                cue.Composite.BlendFraction = 0.20f;
            }
            // 실제 1배속 캡처에서도 의미가 최소 0.12초 읽히도록 원본 정점 칸을 유지한다.
            // 역재생은 배열의 첫 칸이 아니라 원본 정점의 역인덱스를 잡아야 빈 소멸 셀을 붙들지 않는다.
            if (cue.FrameDelays != null && cue.FrameDelays.Length > 0)
            {
                if (shield)
                {
                    // Ball1은 총 재생 0.45초 안에서 한 바퀴가 읽히도록 기본 셀을 30fps로 맞춘다.
                    for (int i = 0; i < cue.FrameDelays.Length; i++) cue.FrameDelays[i] = 1f / 45f;
                }
                int sourcePeak = Mathf.Clamp(cue.FrameDelays.Length / 2, 0, cue.FrameDelays.Length - 1);
                int holdFrame = reverse ? cue.FrameDelays.Length - 1 - sourcePeak : sourcePeak;
                cue.FrameDelays[holdFrame] += kind == SupportVisualKind.Cleanse ? 0.04f :
                    shield ? 0.25f : 0.15f;
            }
            if (addInk || buff)
            {
                // 받침도 같은 셀·역재생·유지 시간을 써야 몸체보다 먼저 사라지지 않는다.
                SkillFxCue ink = HandpaintFx.Cue(sheet, visualSize * (buff ? 1.07f : 1.18f),
                    health ? new Color(.22f, .46f, .41f, 1f) : new Color(.10f, .20f, .31f, 1f), Color.clear,
                    peakAlpha: health ? .38f : .42f, glowAlpha: 0f);
                ink.FrameDelays = (float[])cue.FrameDelays.Clone();
                ink.ReverseFrames = reverse;
                ink.OrderOffset = 117;
                ink.Composite.Desaturate = 1f;
                ink.Composite.ContactShadowColor = ink.Composite.GroundLightColor = Color.clear;
                ink.Composite.StartScale = ink.Composite.CrestScale = ink.Composite.EndScale = health ? 1f : 1.08f;
                ink.Composite.BlendFraction = 0.20f;
                SkillFx.PlayCue(ink, visualPoint, layer, VfxSpace.OverlaySortingOrder + 20);
            }
            var supportFx=SkillFx.PlayCue(cue, visualPoint, layer, VfxSpace.OverlaySortingOrder + 20);
            if(shield)supportFx?.ClipPortraitHud(target);
            if (shield)
            {
                // 선택 링과 구분되는 반구 표면. 가슴께에만 짧게 펼치고 얼굴/HUD를 비운다.
                SpriteFx.Play(VfxSpace.ShieldVeil,visualPoint,new Color(.35f,.58f,.76f,.68f),false,
                    new Vector2(.68f,.40f)*card,new Vector2(.84f,.50f)*card,.32f,
                    fadeIn:.03f,fadeOutFrom:.38f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+140);
                SpriteFx.Play(VfxSpace.ShieldVeil,visualPoint,new Color(.88f,.95f,1f,.34f),true,
                    new Vector2(.64f,.37f)*card,new Vector2(.79f,.46f)*card,.26f,
                    fadeIn:.02f,fadeOutFrom:.30f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+141);
            }
            if (health)
            {
                // 치유는 팩 충격 몸체 안에 녹백 십자광을 한 합성 부품으로 겹쳐 의미를 즉시 읽힌다.
                Color outline = SupportOutline(body, 0.96f);
                float length = card * 0.22f * healScale;
                float thickness = card * 0.042f * healScale;
                Vector3 symbolPoint=StatusMotifFx.Anchor(target)+new Vector3(target.IsEnemy?-.40f:.40f,-.08f)*card;
                symbolPoint.y=Mathf.Max(symbolPoint.y,VfxSpace.PortraitHudTop(target)+length*.5f+card*.03f);
                PlaySupportStroke(target, symbolPoint, layer, 0f, length, thickness,
                    outline, body, core, VfxSpace.OverlaySortingOrder + 130);
                PlaySupportStroke(target, symbolPoint, layer, 90f, length, thickness,
                    outline, body, core, VfxSpace.OverlaySortingOrder + 133);
            }
            else if(buff)
            {
                // 구매 원화의 수렴광 중심에 짧은 강화 별을 붙여 밝은 하늘에서도 즉시 읽힌다.
                Vector3 star=visualPoint+Vector3.up*.08f*card;
                SpriteFx.Play(VfxArt.Glint,star,new Color(.08f,.14f,.22f,addInk?.48f:.43f),false,
                    .31f*card,.34f*card,.40f,rotation:12f,
                    fadeIn:.02f,fadeOutFrom:.50f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
                SpriteFx.Play(VfxArt.Glint,star,new Color(.92f,.72f,.34f,.96f),false,
                    .21f*card,.23f*card,.40f,rotation:12f,
                    fadeIn:.02f,fadeOutFrom:.50f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
            }
            else if (kind == SupportVisualKind.Cleanse)
            {
                // 얼굴을 가리는 굵은 게이지 대신 몸 바깥의 작은 백민트 입자가 걷혀 올라간다.
                VfxRunner.Run(CleanseMotes(target,visualPoint,layer));
                VfxRunner.Run(CleanseRipples(visualPoint,layer));
            }

            float duration = cue.FrameDelays?.Sum() ?? 0.32f;
            yield return new WaitForSeconds(Mathf.Max(0.24f, duration));
            yield return PlayHudArrival(target, visualPoint, layer, core, shield, health);
        }

        private static IEnumerator CleanseRipples(Vector3 center,int layer)
        {
            var waves=new List<SpriteFx>();var origins=new List<Vector3>();float card=VfxSpace.CardSize;
            for(int i=0;i<2;i++)
            {
                Vector3 p=center+new Vector3(.02f,(i-.5f)*.085f,0)*card;
                Vector2 size=new Vector2(.46f,.17f)*card;
                waves.Add(SpriteFx.Play(VfxSpace.CleanseWave,p,new Color(.08f,.14f,.22f,.46f),false,
                    size*new Vector2(1.03f,1.55f),size*new Vector2(.48f,1.25f),.40f,
                    fadeIn:.02f,fadeOutFrom:.40f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+151));
                origins.Add(p);
                waves.Add(SpriteFx.Play(VfxSpace.CleanseWave,p,new Color(.73f,.94f,.86f,.84f),false,
                    size,size*new Vector2(.45f,.85f),.40f,fadeIn:.02f,fadeOutFrom:.40f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+152));
                origins.Add(p);
            }
            for(float age=0f;age<.40f;age+=Time.deltaTime)
            {
                float t=Mathf.Clamp01(age/.40f);
                for(int i=0;i<waves.Count;i++)if(waves[i]!=null)
                    waves[i].transform.position=origins[i]+new Vector3((i/2==0?-1f:1f)*.06f,.36f,0f)*card*t;
                yield return null;
            }
        }

        private static IEnumerator CleanseMotes(Unit target,Vector3 center,int layer)
        {
            var motes=new List<SpriteFx>();var origins=new List<Vector3>();
            for(int i=0;i<6;i++)
            {
                float side=i%2==0?-1f:1f;
                Vector3 point=center+new Vector3(side*(.18f+(i%3)*.035f),-.10f+(i/2)*.085f,0)*VfxSpace.CardSize;
                float size=VfxSpace.CardSize*(i%2==0?.13f:.10f);
                Sprite shape=i%2==0?VfxArt.Glint:VfxSpace.SoftDot;
                motes.Add(SpriteFx.Play(shape,point,new Color(.08f,.14f,.22f,.48f),false,
                    size*1.25f,size*.65f,.42f,fadeIn:.02f,fadeOutFrom:.40f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148));
                origins.Add(point);
                motes.Add(SpriteFx.Play(shape,point,new Color(.76f,.96f,.88f,.92f),false,size,size*.50f,.42f,
                    fadeIn:.02f,fadeOutFrom:.40f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149));
                origins.Add(point);
            }
            for(float age=0f;age<.42f;age+=Time.deltaTime)
            {
                float t=Mathf.Clamp01(age/.42f);
                for(int i=0;i<motes.Count;i++)if(motes[i]!=null)
                    motes[i].transform.position=origins[i]+new Vector3(((i/2)%2==0?-1f:1f)*.035f*t,.22f*t,0)*VfxSpace.CardSize;
                yield return null;
            }
        }

        private static IEnumerator PlayHudArrival(Unit target, Vector3 from, int layer, Color color,
            bool shield, bool health)
        {
            float card = VfxSpace.CardSize;
            Vector3 to = VfxSpace.WorldPoint(target) + Vector3.down * card * 0.41f;
            SpriteRenderer spark = VfxSpace.NewSprite(null, "SupportHudArrival", VfxArt.Glint,
                color, layer, VfxSpace.OverlaySortingOrder + 142, true);
            const float duration = 0.18f;
            for (float age = 0f; age < duration; age += Time.deltaTime)
            {
                float t = Mathf.Clamp01(age / duration);
                float eased = t * t * (3f - 2f * t);
                if (spark == null) break;
                spark.transform.position = Vector3.Lerp(from, to, eased);
                float scale = card * Mathf.Lerp(0.12f, 0.055f, eased);
                spark.transform.localScale = Vector3.one * scale;
                spark.color = new Color(color.r, color.g, color.b, 1f - 0.35f * t);
                yield return null;
            }
            if (spark != null) UnityEngine.Object.Destroy(spark.gameObject);

            Color ring = new(color.r, color.g, color.b, 0.92f);
            SpriteFx.Play(VfxArt.Shockwave, to, ring, true,
                new Vector2(card * 0.08f, card * 0.025f),
                new Vector2(card * 0.42f, card * 0.12f), 0.22f,
                fadeIn: 0.02f, fadeOutFrom: 0.52f,
                sortingLayer: layer, order: VfxSpace.OverlaySortingOrder + 143);
            CardMotion.ViewOf(target)?.PulseSupportHud(shield, health, color);
        }

        private static Color SupportOutline(Color body, float alpha)
            => new(Mathf.Min(body.r * 0.18f, 0.10f), Mathf.Min(body.g * 0.18f, 0.12f),
                Mathf.Min(body.b * 0.18f, 0.14f), alpha);

        private static void PlaySupportRing(Unit target, Vector3 point, int layer, Vector2 size,
            Color outline, Color body, Color core, int order)
        {
            float card = VfxSpace.CardSize;
            Vector2 from = new(size.x * card * 0.38f, size.y * card * 0.11f);
            Vector2 to = new(size.x * card * 0.88f, size.y * card * 0.23f);
            SpriteFx.Play(VfxArt.Shockwave, point, outline, false, from * 1.16f, to * 1.16f, 0.54f,
                fadeIn: 0.06f, fadeOutFrom: 0.70f, sortingLayer: layer, order: order)?.Follow(target?.transform);
            SpriteFx.Play(VfxArt.Shockwave, point, body, false, from, to, 0.54f,
                fadeIn: 0.06f, fadeOutFrom: 0.70f, sortingLayer: layer, order: order + 1)?.Follow(target?.transform);
            SpriteFx.Play(VfxArt.Shockwave, point, core, true, from * 0.84f, to * 0.84f, 0.54f,
                fadeIn: 0.06f, fadeOutFrom: 0.70f, sortingLayer: layer, order: order + 2)?.Follow(target?.transform);
        }

        private static void PlaySupportStroke(Unit target, Vector3 point, int layer, float angle,
            float length, float thickness, Color outline, Color body, Color core, int order)
        {
            Vector2 from = new(length * 0.56f, thickness * 0.72f);
            Vector2 to = new(length, thickness);
            outline.a=Mathf.Min(outline.a,.46f);
            SpriteFx.Play(VfxSpace.GlyphLine, point, outline, false, from * 1.13f, to * 1.13f, 0.54f,
                angle, 0f, 0.06f, 0.70f, SpriteFx.Ease.OutBack, layer, order)?.Follow(target?.transform);
            SpriteFx.Play(VfxSpace.GlyphLine, point, body, false, from, to, 0.54f,
                angle, 0f, 0.06f, 0.70f, SpriteFx.Ease.OutBack, layer, order + 1)?.Follow(target?.transform);
            SpriteFx.Play(VfxSpace.GlyphLine, point, core, true, from * 0.72f, to * 0.72f, 0.54f,
                angle, 0f, 0.06f, 0.70f, SpriteFx.Ease.OutBack, layer, order + 2)?.Follow(target?.transform);
        }

        private static void NotifyCustomPeak(string skillKey, string slotName, VfxPartData part, int index,
            Unit caster, Unit target, Vector3 point, float angle, bool flipX, int layer, float delay)
        {
            VfxRunner.Run(NotifyCustomPeakAfterDelay(skillKey, slotName, part.id, index, caster, target,
                point, angle, flipX, layer, delay));
        }

        private static IEnumerator InvokeAfterDelay(float delay, Action callback)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            callback?.Invoke();
        }

        private static IEnumerator NotifyCustomPeakAfterDelay(string skillKey, string slotName, string partId,
            int index, Unit caster, Unit target, Vector3 point, float angle, bool flipX, int layer, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            NotifySlot(skillKey, slotName, partId, "peak", index, caster, target, point,
                angle, flipX, layer, 118);
        }

        private static HandpaintSheet Sheet(VfxPartData part)
            => new(part.sheet, Mathf.Max(1, part.columns), Mathf.Max(1, part.rows),
                Mathf.Max(1f, part.fps), new Vector2(0.5f, 0.5f));

        private static void NotifySlot(string skillKey, string slotName, string partId, string phase,
            int slotIndex, Unit caster, Unit target, Vector3 point, float angle, bool mirrored,
            int sortingLayerId, int sortingOrder)
        {
            SlotPlayed?.Invoke(new VfxSlotPlaybackEvent
            {
                SkillKey = skillKey,
                SlotName = slotName,
                PartId = partId,
                Phase = phase,
                SlotIndex = slotIndex,
                Caster = caster,
                Target = target,
                WorldPoint = point,
                Angle = angle,
                Mirrored = mirrored,
                SortingLayerId = VfxSpace.ResolveSortingLayer(sortingLayerId),
                SortingOrder = sortingOrder,
                Realtime = Time.realtimeSinceStartup,
            });
        }

        private static IEnumerator PlayDelayedSlot(string skillKey, string slotName, int index,
            Unit caster, Unit target, Vector3 point, int layer, float extraScale)
        {
            float delay = Mathf.Max(0f, Offset(skillKey, slotName, index));
            if (delay > 0f) yield return new WaitForSeconds(delay);
            PlaySlot(skillKey, slotName, index, caster, target, point, layer, extraScale);
        }

        private static IEnumerator PlayDelayedHit(string skillKey, int index, float delay,
            Unit caster, Unit target, Action onPeak, float extraScale, Action onComplete)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return PlayHitSlot(skillKey, index, caster, target, onPeak, extraScale);
            onComplete?.Invoke();
        }

        private static IEnumerator Chain(IEnumerator routine, Action onComplete)
        {
            yield return routine;
            onComplete?.Invoke();
        }

        private static int SlotIndex(VfxSkillData skill, VfxSlotData selected)
        {
            int index = 0;
            foreach (VfxSlotData slot in skill.slots)
            {
                if (slot == selected) return index;
                if (slot != null && string.Equals(slot.slot, selected.slot,
                        StringComparison.OrdinalIgnoreCase)) index++;
            }
            return 0;
        }

        private static void ResolveDirection(VfxSlotData slot, Unit caster, Unit target,
            float routeAngle, bool routeFacingRight, out float angle, out bool flipX)
        {
            string mode = slot.direction?.Trim().ToLowerInvariant() ?? "castertotarget";
            if (mode == "fixed")
            {
                angle = slot.angleOffset;
                flipX = false;
                return;
            }
            if (!float.IsNaN(routeAngle))
            {
                angle = routeAngle + slot.angleOffset;
                flipX = false;
                return;
            }
            VfxAim aim = VfxSpace.AimFrom(caster, target);
            if (mode == "incomingimpact")
            {
                // 피격 원화는 충돌점에서 공격자 쪽으로 몸체가 뻗는다. 이동 방향의 반대 벡터를 쓰면
                // 적 공격은 카드 오른쪽, 아군 공격은 카드 왼쪽에 몸체가 남고 앵커만 초상화에 맞는다.
                angle = aim.AngleDegrees + 180f + slot.angleOffset;
                flipX = false;
                return;
            }
            if (mode == "horizontalfacing")
            {
                // Smoke3처럼 자체가 좌우 확산을 그린 시트는 목표의 상하 위치로 회전하지 않는다.
                // 진영 방향은 좌우 반전만 적용해 수평 실루엣과 하단 접촉 앵커를 보존한다.
                angle = slot.angleOffset;
                flipX = aim.MirrorHorizontal(slot.sourceFacesRight);
                return;
            }
            angle = mode == "radial" ? slot.angleOffset :
                mode == "stroke" ? aim.StrokeRotation(Mathf.Abs(slot.angleOffset)) :
                aim.AngleDegrees + slot.angleOffset;
            // casterToTarget는 회전만으로 진행 방향이 이미 맞는다. 다시 반전하면 총구 원화가 역방향이 된다.
            flipX = mode == "stroke" && aim.MirrorHorizontal(slot.sourceFacesRight);
        }

        private static Color ParseColor(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string normalized = value[0] == '#' ? value : "#" + value;
            return ColorUtility.TryParseHtmlString(normalized, out Color color) ? color : fallback;
        }

        /// <summary>
        /// 적 공용 부품은 실루엣과 명암 합성은 공유하되 데이터의 vfxColor로 몸체를 물들인다.
        /// 코어는 같은 색을 백색 쪽으로 끌어 정점이 밝은 전장에서도 묻히지 않게 한다.
        /// </summary>
        private static void ResolveCasterPalette(VfxSlotData slot, Unit caster, ref Color body, ref Color core)
        {
            if (slot?.useCasterColor != true || caster == null ||
                string.IsNullOrWhiteSpace(caster.VfxColorHex) ||
                !ColorUtility.TryParseHtmlString(caster.VfxColorHex, out Color accent))
                return;

            float bodyAlpha = body.a;
            float coreAlpha = core.a;
            accent.a = 1f;
            body = Color.Lerp(body, accent, 0.82f);
            core = Color.Lerp(accent, Color.white, 0.72f);
            body.a = bodyAlpha;
            core.a = coreAlpha;
        }

        /// <summary>
        /// 자동 지원 피격은 시전자 전용 슬롯색이 없으므로 원소색을 기본으로 삼는다.
        /// 몸체는 낮은 알파의 형태, 코어는 백색 쪽 밝은 가장자리만 맡겨 초상화 색을 바꾸지 않는다.
        /// </summary>
        private static void ResolveSupportPalette(Unit caster, ref Color body, ref Color core)
        {
            if (caster == null) return;
            Color accent;
            if (!string.IsNullOrWhiteSpace(caster.VfxColorHex) &&
                ColorUtility.TryParseHtmlString(caster.VfxColorHex, out Color parsed))
                accent = parsed;
            else
                accent = ElementPalette.ColorFor(caster.Element);

            float bodyAlpha = body.a;
            float coreAlpha = core.a;
            accent.a = 1f;
            body = Color.Lerp(body, accent, 0.78f);
            core = Color.Lerp(accent, Color.white, 0.74f);
            body.a = bodyAlpha;
            core.a = coreAlpha;
        }

        private static void ResolveClawPalette(VfxSlotData slot, Unit caster,
            ref Color body, ref Color edge, ref Color core)
        {
            if (slot?.useCasterColor != true || caster == null ||
                string.IsNullOrWhiteSpace(caster.VfxColorHex) ||
                !ColorUtility.TryParseHtmlString(caster.VfxColorHex, out Color accent))
                return;

            float bodyAlpha = body.a;
            float edgeAlpha = edge.a;
            float coreAlpha = core.a;
            accent.a = 1f;
            // 야수도 검정 밑칠은 잃지 않고, 원색 몸체와 백색 하이라이트만 데이터 색으로 바꾼다.
            body = Color.Lerp(body, accent * 0.16f, 0.22f);
            edge = Color.Lerp(edge, accent, 0.88f);
            core = Color.Lerp(accent, Color.white, 0.78f);
            body.a = bodyAlpha;
            edge.a = edgeAlpha;
            core.a = coreAlpha;
        }
    }

    /// <summary>상태가 실제로 붙은 순간 분류 기본값 또는 상태 키 덮어쓰기를 재생한다.</summary>
    public static class StatusAffectedVfx
    {
        public const string SuppressPartId = "none";
        private static string _supportVariant;

        /// <summary>실제 Heal 호출 안에서 자동 지원 부품만 선택한다. 예외·중첩 호출 뒤에는 이전 설정을 되돌린다.</summary>
        public static IDisposable SupportVariant(string skillKey) => new SupportScope(skillKey);

        private sealed class SupportScope : IDisposable
        {
            private readonly string _prior;
            public SupportScope(string skillKey) { _prior = _supportVariant; _supportVariant = skillKey; }
            public void Dispose() => _supportVariant = _prior;
        }

        public static void PlaySupport(Unit owner, Unit caster, string partId)
        {
            if (owner == null || !owner.isActive || string.IsNullOrWhiteSpace(partId)) return;
            string variant = _supportVariant;
            if (partId == "hit.support.heal" && !string.IsNullOrWhiteSpace(variant))
                VfxComposer.PlayAffectedSlot(variant, 0, caster, owner);
            else VfxComposer.PlayAffectedPart(owner, partId, caster);
        }

        public static void Play(Unit owner, UnitStatus status)
        {
            if (owner == null || status == null || !owner.isActive) return;
            if (string.Equals(status.AffectedVfxPartId, SuppressPartId,
                    StringComparison.OrdinalIgnoreCase)) return;
            string partId = !string.IsNullOrWhiteSpace(status.AffectedVfxPartId)
                ? status.AffectedVfxPartId
                : VfxDefinitionCatalog.StatusPart(status.Key);
            if (string.IsNullOrWhiteSpace(partId)) partId = DefaultPart(status);
            if (VfxDefinitionCatalog.Part(partId)?.affectedDelay > 0f)
                VfxRunner.Run(PlayAffectedAfterHit(owner, status.Caster, partId));
            else if (!string.IsNullOrWhiteSpace(partId)) VfxComposer.PlayAffectedPart(owner, partId, status.Caster);
        }

        private static IEnumerator PlayAffectedAfterHit(Unit owner, Unit caster, string partId)
        {
            // 상태·명중은 이미 적용됐다. 연출만 원 공격 피격의 꼬리 뒤에 이어 얼굴을 이중으로 덮지 않는다.
            yield return new WaitForSeconds(VfxDefinitionCatalog.Part(partId).affectedDelay);
            if (owner != null && owner.isActive) VfxComposer.PlayAffectedPart(owner, partId, caster);
        }

        private static string DefaultPart(UnitStatus status)
        {
            string key = status.Key?.ToLowerInvariant() ?? string.Empty;
            if (key.StartsWith("cc_") || status.StatusId is 5920 or 5921 or 5922 or 5923)
                return key.Contains("frozen") ? "affected.control.frozen" : key.Contains("airborne") ? "affected.control.airborne" :
                    key.Contains("bind") ? "affected.control.bind" : "affected.control.stun";
            if (status.Effects != null && status.Effects.Any(effect => effect.EffectObject?.IsDamageOverTime == true))
                return key.Contains("poison") ? "affected.dot.poison" : key.Contains("curse") ? "affected.dot.curse" :
                    key.Contains("shock") || key.Contains("electro") ? "affected.dot.shock" : "affected.dot.burn";
            if (status.IsBeneficial || status.Category == BaseClasses.BaseEnums.StatusCategory.Positive)
                return key.Contains("shield") ? "hit.support.shield" :
                    key.Contains("heal") || key.Contains("regen") ? "hit.support.heal" : "hit.support.buff";
            if (status.CountsAsDebuff || status.Category == BaseClasses.BaseEnums.StatusCategory.Negative)
                return key.Contains("slow") || key.Contains("speed_down")
                    ? "affected.debuff.slow"
                    : key.Contains("mark") ? "affected.debuff.mark" : "affected.debuff.fall";
            return null;
        }
    }
}
