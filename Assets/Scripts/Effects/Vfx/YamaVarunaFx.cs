using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Codes.Passive;
using Combat;
using Entities;
using Effects.Vfx.Presets;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>실제 추가 발동·보호막 사건만 표시한다. 구독 수명은 해당 행동의 연출 코루틴에 묶인다.</summary>
    public sealed class YamaVarunaFx : IDisposable
    {
        private readonly string _key;
        private bool _disposed;
        private readonly Action<EventContext> _cleanup;
        private readonly Unit _caster;
        private readonly HashSet<Unit> _shieldTargets = new();

        private YamaVarunaFx(string key, Unit caster)
        {
            _key = key;
            _caster = caster;
            DamageOverTimeDetonation.AnyDetonationFinished += OnDetonation;
            Unit.AnyShieldGranted += OnShield;
            VarunaLawLevee.AnyLeveeShield += OnLevee;
            _cleanup = _ => Dispose();
            caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
            caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            ActionScheduler.AnyActionFinished += OnActionFinished;
        }

        public static IDisposable Observe(string key, Unit caster)
            => caster != null && (key.StartsWith("yama.") || key.StartsWith("varuna.")) ? new YamaVarunaFx(key, caster) : null;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DamageOverTimeDetonation.AnyDetonationFinished -= OnDetonation;
            Unit.AnyShieldGranted -= OnShield;
            VarunaLawLevee.AnyLeveeShield -= OnLevee;
            _caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanup);
            _caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanup);
            ActionScheduler.AnyActionFinished -= OnActionFinished;
        }

        public static bool PlayMotif(VfxPartData part, Unit caster, Unit target, Vector3 point,
            int layer, Vector2 size, float angle)
        {
            if (part.renderer == "yvShield")
            {
                float cardSize = VfxSpace.CardSize;
                Unit receiver=target??caster;
                Vector3 shield=StatusMotifFx.Anchor(receiver);
                shield.y=VfxSpace.PortraitHudTop(receiver)+.12f*cardSize;
                Vector2 rimSize = new Vector2(.22f,.18f)*cardSize;
                int rimOrder = VfxSpace.OverlaySortingOrder + 40;
                // 몸에서 떨어진 선택 고리 대신 실제 흉부 축에 얇은 막을 펼친다. 얼굴/HUD는 비운다.
                SpriteFx.Play(VfxSpace.VarunaBarrier,shield,new Color(.15f,.25f,.38f,.82f),false,
                    rimSize*.94f,rimSize*1.10f,.32f,fadeIn:.08f,fadeOutFrom:.55f,
                    sortingLayer:layer,order:rimOrder)?.Follow(receiver.transform);
                SpriteFx.Play(VfxSpace.VarunaBarrier,shield,new Color(.67f,.87f,.96f,.84f),false,
                    rimSize*.89f,rimSize*1.02f,.32f,fadeIn:.08f,fadeOutFrom:.55f,
                    sortingLayer:layer,order:rimOrder+1)?.Follow(receiver.transform);
                return true;
            }
            if (part.renderer == "yvYamaImpact")
            {
                float cardSize = VfxSpace.CardSize;
                int impactOrder = VfxSpace.OverlaySortingOrder + 42;
                // 400% 판결은 같은 낙뢰의 크기가 아니라 짧은 금백 척추와 접점 정점으로 구별한다.
                Vector2 spine = new Vector2(0.62f, 0.045f) * cardSize;
                SpriteFx.Play(VfxArt.Streak, point, new Color(0.72f, 0.58f, 0.85f, 0.74f), false,
                    spine * new Vector2(1f, 2.2f), spine * new Vector2(1f, 2.2f), 0.18f,
                    90f, fadeIn: 0.24f, fadeOutFrom: 0.54f, sortingLayer: layer, order: impactOrder);
                SpriteFx.Play(VfxArt.Streak, point, new Color(1f, 0.94f, 0.75f, 0.90f), false,
                    spine, spine, 0.16f, 90f, fadeIn: 0.24f, fadeOutFrom: 0.48f,
                    sortingLayer: layer, order: impactOrder + 1);
                HandpaintFx.PlayAt(point, layer, HandpaintFx.Impact1Yellow, size,
                    new Color(0.77f, 0.66f, 0.86f), new Color(1f, 0.95f, 0.82f),
                    heldFrame: 3, peakAlpha: 0.70f, glowAlpha: 0.26f, desaturate: 1f, steadyBody: true, calmLight: true);
                return true;
            }
            if (part.renderer != "yvLaw" && part.renderer != "yvLawHit" && part.renderer != "yvVerdict") return false;
            float card = VfxSpace.CardSize;
            bool verdict = part.renderer == "yvVerdict";
            bool hit = part.renderer == "yvLawHit";
            Color gold = new Color(0.91f, 0.81f, 0.59f, 0.70f);
            Color ink = new Color(0.67f, 0.59f, 0.45f, 0.28f);
            point += Vector3.down * card * (hit ? 0.10f : 0.12f);
            if (verdict)
            {
                // 판결은 문자 타일이나 원자 궤도와 구분되는 봉랍·저울의 고유 인장이다.
                JudgmentSeal(point + Vector3.up * card * .10f, layer);
                return true;
            }
            Vector2 loop = new Vector2(hit ? 0.78f : 0.60f, hit ? 0.34f : 0.28f) * card;
            int order = VfxSpace.OverlaySortingOrder + 36;
            // 금장 의복의 매듭을 작은 교차 고리로 옮긴다. 얼굴·하단 HUD에 닿는 전신 원은 쓰지 않는다.
            for (int side = -1; side <= 1; side += 2)
            {
                float turn = side * (verdict ? 52f : 28f);
                SpriteFx.Play(VfxSpace.Ring, point, ink, false, loop * 1.12f, loop * 1.12f,
                    0.40f, turn, fadeIn: 0.24f, fadeOutFrom: 0.50f, sortingLayer: layer, order: order);
                SpriteFx.Play(VfxSpace.Ring, point, gold, false, loop, loop,
                    0.40f, turn, fadeIn: 0.24f, fadeOutFrom: 0.50f, sortingLayer: layer, order: order + 1);
            }
            if (!hit)
            {
                Color body = verdict ? new Color(0.72f, 0.59f, 0.84f) : new Color(0.52f, 0.78f, 0.86f);
                Color core = verdict ? new Color(0.92f, 0.82f, 0.97f) : new Color(0.80f, 0.96f, 1f);
                HandpaintFx.PlayAt(point, layer,
                    new HandpaintSheet(part.sheet, part.columns, part.rows, part.fps, new Vector2(0.5f, 0.5f)),
                    size * 0.72f, body, core, heldFrame: part.peakFrame, peakAlpha: 0.65f,
                    glowAlpha: 0.20f, desaturate: 1f, steadyBody: true, calmLight: true);
            }
            return true;
        }

        public static void DetonationCue(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+Vector3.right*.20f*c;
            int layer=VfxSpace.SortingLayer(target);int order=VfxSpace.OverlaySortingOrder+145;
            SpriteFx.Play(VfxSpace.PressureRing,p,new Color(.28f,.25f,.44f,.78f),false,
                .14f*c,.40f*c,.24f,fadeIn:0f,fadeOutFrom:.50f,sortingLayer:layer,order:order);
            for(int i=0;i<6;i++)
            {
                float a=i*60f;Vector3 direction=Quaternion.Euler(0f,0f,a)*Vector3.right;
                Vector2 length=new(.13f*c,.024f*c);
                SpriteFx.Play(VfxSpace.GlyphLine,p+direction*.14f*c,new Color(.72f,.61f,.89f,.94f),false,
                    length,length*new Vector2(.55f,.75f),.20f,a,fadeIn:0f,fadeOutFrom:.42f,
                    sortingLayer:layer,order:order+1);
            }
        }

        public static void VerdictCue(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+Vector3.right*.22f*c;
            int layer=VfxSpace.SortingLayer(target);
            JudgmentSeal(p,layer);
            SpriteFx.Play(VfxSpace.GlyphLine,p+Vector3.up*.15f*c,new Color(.32f,.28f,.47f,.88f),false,
                new Vector2(.018f,.20f)*c,new Vector2(.018f,.05f)*c,.20f,fadeIn:0f,fadeOutFrom:.55f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+145);
        }

        public static void BalanceCue(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.22f,.02f)*c;
            int layer=VfxSpace.SortingLayer(target);int order=VfxSpace.OverlaySortingOrder+146;
            void Line(Vector3 center,Vector2 size)
            {
                SpriteFx.Play(VfxSpace.GlyphLine,center,new Color(.20f,.28f,.41f,.88f),false,
                    size*c,size*c,.24f,fadeIn:0f,fadeOutFrom:.55f,sortingLayer:layer,order:order);
            }
            Line(p+Vector3.up*.055f*c,new(.28f,.016f));Line(p,new(.022f,.22f));
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 pan=p+Vector3.right*side*.11f*c;
                Line(pan+Vector3.up*.015f*c,new(.012f,.09f));
                SpriteFx.Play(VfxSpace.Crescent,pan-Vector3.up*.045f*c,new Color(.89f,.80f,.57f,.95f),false,
                    new Vector2(.11f,.055f)*c,new Vector2(.11f,.055f)*c,.24f,180f,
                    fadeIn:0f,fadeOutFrom:.55f,sortingLayer:layer,order:order+1);
            }
        }

        public static void LawInvocation(Unit caster,Unit target)
        {
            if(caster==null)return;
            float card=VfxSpace.CardSize;
            Vector3 center=VfxSpace.CastPoint(caster,target);
            center.x+=(caster.IsEnemy?-1f:1f)*.08f*card;
            center.y=VfxSpace.PortraitHudTop(caster)+.19f*card;
            int layer=VfxSpace.SortingLayer(caster),order=VfxSpace.OverlaySortingOrder+144;
            void Stroke(Vector3 offset,Vector2 size)
                => SpriteFx.Play(VfxSpace.GlyphLine,center+offset*card,new Color(.16f,.25f,.39f,.94f),false,
                    size*card,size*card,.36f,fadeIn:.06f,fadeOutFrom:.64f,sortingLayer:layer,order:order,preserveInk:true);
            // 초월기에서만 법의 저울을 손 바깥에 펼친다. 전장 전체 섬광과 공격 궤적은 만들지 않는다.
            Stroke(new(0f,.055f),new(.28f,.018f));Stroke(Vector3.zero,new(.018f,.19f));
            Stroke(new(0f,-.10f),new(.13f,.018f));
            for(int side=-1;side<=1;side+=2)
            {
                Stroke(new(side*.11f,.005f),new(.012f,.09f));
                SpriteFx.Play(VfxSpace.Crescent,center+new Vector3(side*.11f,-.052f)*card,
                    new Color(.97f,.80f,.46f,.94f),false,new Vector2(.13f,.060f)*card,
                    new Vector2(.13f,.060f)*card,.36f,180f,fadeIn:.06f,fadeOutFrom:.64f,
                    sortingLayer:layer,order:order+1);
            }
        }

        private static void JudgmentSeal(Vector3 point,int layer)
        {
            float c=VfxSpace.CardSize;
            Vector3 center=point+Vector3.right*.08f*c;
            int order=VfxSpace.OverlaySortingOrder+38;
            Color navy=new(.17f,.24f,.36f,.90f);
            Color wax=new(.93f,.85f,.72f,.94f);
            void Mark(Sprite sprite,Vector3 offset,Vector2 size,Color tint,int depth,float angle=0f)
                => SpriteFx.Play(sprite,center+offset*c,tint,false,size*c,size*c,.40f,angle,
                    fadeIn:.06f,fadeOutFrom:.60f,sortingLayer:layer,order:order+depth);
            Mark(VfxSpace.Ring,Vector3.zero,Vector2.one*.36f,new(.20f,.29f,.42f,.68f),0);
            // 불규칙한 봉랍 외곽은 판결 문양이며 제어 상태를 뜻하는 가짜 아이콘이 아니다.
            for(int i=0;i<8;i++)
            {
                Vector3 petal=Quaternion.Euler(0f,0f,i*45f)*Vector3.right*.108f;
                Mark(VfxSpace.HardDot,petal,Vector2.one*.075f,navy,1);
                Mark(VfxSpace.HardDot,petal,Vector2.one*.058f,wax,2);
            }
            Mark(VfxSpace.HardDot,Vector3.zero,Vector2.one*.255f,navy,1);
            Mark(VfxSpace.HardDot,Vector3.zero,Vector2.one*.226f,wax,2);
            Mark(VfxSpace.GlyphLine,new(0f,.035f),new(.15f,.013f),navy,3);
            Mark(VfxSpace.GlyphLine,new(0f,-.007f),new(.12f,.014f),navy,3,90f);
            for(int side=-1;side<=1;side+=2)
            {
                Mark(VfxSpace.GlyphLine,new(side*.067f,.006f),new(.062f,.011f),navy,3,90f);
                Mark(VfxSpace.Crescent,new(side*.067f,-.021f),new(.068f,.036f),navy,3,180f);
            }
            Mark(VfxArt.Diamond,new(0f,.052f),new(.029f,.043f),navy,4);
            Mark(VfxSpace.Crescent,new(0f,-.060f),new(.081f,.026f),navy,3);
        }

        private void OnActionFinished(Unit unit, ActionScheduler.ActionKind kind)
        {
            if (unit == _caster) Dispose();
        }

        private void OnDetonation(Unit trigger, Unit target, int delivered)
        {
            if (trigger != _caster || delivered <= 0 || _key == "yama.special") return;
            // 직접 피격 정점에서 이어지되 첫 몸체가 걷힌 뒤 짧은 잔광 하나로 모든 지속피해의 정산을 묶는다.
            VfxRunner.Run(DetonationTail(target));
        }

        private IEnumerator DetonationTail(Unit target)
        {
            yield return new WaitForSeconds(0.12f);
            if (target == null || !target.isActive) yield break;
            VfxComposer.PlayAffectedSlot(_key, 0, _caster, target);
            AudioManager.PlayCue("yama.detonation");
        }

        private void OnShield(Unit target, Unit source, int amount)
        {
            if (source != _caster || _caster.ID != 66 || amount <= 0 || target == null) return;
            // 초월기의 제방과 파티 보호막은 같은 정점에 누적된다. 양은 실제 HUD가 보여 주고 구체는 대상당 하나다.
            if (_shieldTargets.Add(target))
            {
                VfxComposer.PlayAffectedSlot(_key, 0, _caster, target);
                CardMotion.ViewOf(target)?.PulseSupportHud(true, false, new Color(0.35f, 0.78f, 1f));
            }
        }

        private void OnLevee(Unit varuna, int actionId, int amount)
        {
            if (varuna != _caster || amount <= 0) return;
            AudioManager.PlayAction(varuna, SfxActionSlot.Passive, SfxCuePhase.Resolve);
        }
    }
}
