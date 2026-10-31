using System;
using System.Collections;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;
namespace UnityEngine
{
    public interface ISerializationCallbackReceiver { void OnBeforeSerialize(); void OnAfterDeserialize(); }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
}
namespace Effects.Vfx
{
    public enum CardAuraStyle { Glow }
    public interface ICardAura { CardAuraStyle AuraStyle {get;} Color AuraColor {get;} }
    public sealed class ProjectileStyle { public float Duration,BodySize; public Color BodyColor; }
    public struct VfxAim { public Vector3 Direction; public float AngleDegrees; public bool FacingRight; }
    public static class VfxComposer
    {
        public static IEnumerator PlaySingleTarget(string key,Unit caster,Unit target,Action onImpact,float extraScale=1f,float minimumDuration=0f) { yield return PresentationClock.Hit(key,onImpact,true,minimumDuration); }
        public static IEnumerator PlayMultiTargetHits(string key,Unit caster,IReadOnlyList<Unit> targets,Action<Unit> onHit,float spreadSeconds=.05f)
        { foreach(var target in targets) onHit(target);yield break; }
        public static void PlayScreen(string key,Unit caster,Unit target=null) { }
        public static void PlaySlot(string key,string slot,int index,Unit caster,Unit target,Vector3 point,int sortingLayer,float extraScale=1f,float routeAngle=float.NaN,bool routeFacingRight=true) { }
        public static void PlayScreen(string key,Unit caster,IReadOnlyList<Unit> targets) { }
        public static void PlayAffectedSlot(string key,int index,Unit caster,Unit target,float extraScale=1f) { }
        public static void PlayAffectedPart(Unit target,string partId,Unit caster=null) { }
        public static IEnumerator PlayHorizontalSweepHit(string key,int index,Unit caster,Unit directionTarget,Vector3 center,IReadOnlyList<Unit> targets,Action<Unit> impact)
        {
            float elapsed=0;
            foreach(var item in Combat.CombatSweepTiming.Plan(targets))
            {
                var target=item.Target;
                float due=item.Due;
                if(due>elapsed)yield return new WaitForSeconds(due-elapsed);
                elapsed=due;impact(target);
            }
        }
    }
    public static class StatusAffectedVfx
    {
        public const string SuppressPartId="none";
        private class Scope : IDisposable { public void Dispose() { } }
        public static IDisposable SupportVariant(string key) => new Scope();
        public static void PlaySupport(Unit owner,Unit caster,string partId) { }
        public static void PlayShield(Unit owner,Unit caster) { }
        public static void Play(Unit owner,Entities.Status.UnitStatus status,bool refresh=false) { }
    }
    public static class VfxRunner { private static readonly object Owner=new(); public static Coroutine Run(IEnumerator routine)=>CoroutineLoop.Start(Owner,routine); }
    public enum VfxStyleCharacter { Sabah }
    public static class VfxStyleSettings { public static bool UseHandpaint(VfxStyleCharacter value)=>true; }
    public static class VerseReaperFx { public static void Contact(Unit caster,Unit target,float scale=1f,bool particles=false) { } }
}
namespace Effects
{
    public static class CombatFeedback
    {
        public static void PlayDamage(Unit target,DamageContext context,int hpLost,int shieldLost) { }
        public static void PlayEvade(Unit target) { }
        public static void PlayEffectResisted(Unit target) { }
        public static void PlayNullified(Unit target) { }
        public static void PlayHeal(Unit target,int amount) { }
    }
    public static class StatusPopup
    {
        public static void Show(Unit target,Entities.Status.UnitStatus status,bool refresh) { }
        public static void ShowElement(Unit target,BaseEnums.UnitElement element,bool refresh) { }
        public static void ShowHitWard(Unit target,int remaining) { }
        public static void ShowGuard(Unit target) { }
        public static void ShowToughnessBreak(Unit target) { }
    }
    public static class BattlefieldBackdrop
    {
        public static void ClearFocus() { }
        public static void PushFocus(float strength,float attack,float hold,float release,Color tint,Vector3 point,Vector3? secondary=null) { }
    }
    public enum CombatNumberKind { HealthCost }
    public static class DamagePopup { public static void Show(Unit unit,int amount,bool crit,Color color,CombatNumberKind kind,float delay) { } }
}
