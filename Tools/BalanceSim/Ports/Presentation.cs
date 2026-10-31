// 화면 호스트의 대체 API. 피해·상태는 호출자가 전달한 기존 판정 콜백만 실행한다.
using System; using System.Collections; using System.Collections.Generic; using System.Linq; using Entities; using UnityEngine; using BaseClasses; using Managers; namespace Effects.Vfx {
public static class AnubisFx {
public static IEnumerator HeartScale(Unit caster, Unit target, bool empowered, Action onImpact) { yield return CardMotion.Charge(caster,target,(_,__)=>onImpact?.Invoke()); }
public static void Book(Unit caster) {  }
public static void Soul(Unit caster, bool whole) {  }
}
public static class BastetFx {
public static IEnumerator CatPunch(Unit caster, Unit target, bool additional, Action onImpact) { yield return VfxComposer.PlaySingleTarget("bastet.cat_punch",caster,target,onImpact,1,.38f); }
public static IEnumerator GuardIntervention(Unit bastet, Unit guard) { yield break; }
}
public static class CardLifecycleFx {
public static void PlayDestroy(Unit unit) {  }
}
public static class CardMotion {
public static IEnumerator Charge(Unit attacker, Unit target, Action<Vector3, Vector3> onImpact) { foreach(float duration in new[]{.10f,.075f,.13f}) for(float t=0;t<duration;t+=Time.deltaTime)yield return null;onImpact?.Invoke(default,default);foreach(float duration in new[]{.075f,.24f}) for(float t=0;t<duration;t+=Time.deltaTime)yield return null; }
public static void Throw(Unit caster, Unit target, float strength = 1f) {  }
public static void RakeReaction(Unit target, Vector3 direction, float strength = 0.55f) {  }
public static IEnumerator InterruptionWindup(Unit attacker, Unit target) { yield break; }
public static IEnumerator Channel(Unit caster, float riseTime = 0.18f, float holdTime = 0.2f, float settleTime = 0.24f) { yield break; }
}
public static class ChandraFx {
public static IEnumerator Sachi(Unit caster, Unit target, Action onImpact, float scale = 1f) { yield return VfxComposer.PlaySingleTarget("chandra.sachi",caster,target,onImpact); }
public static IEnumerator Soma(Unit caster, Unit buffTarget, IReadOnlyList<Unit> allies, Action onSeal) { onSeal?.Invoke();yield break; }
public static IEnumerator Changya(Unit caster, IReadOnlyList<Unit> allies, IReadOnlyList<Unit> targets,
            Action onSeal, Action<Unit> onHit) { onSeal?.Invoke();foreach(var item in targets) onHit?.Invoke(item);yield break; }
public static void Moonlight(Unit unit, bool gained) {  }
}
public static class EgyptianReworkFx {
public static Sprite GuardianGlyph(int index) { return default; }
public static IEnumerator Chant(Unit caster,bool blood,bool ultimate=false,bool purchased=false) { yield return new WaitForSeconds(ultimate?.48f:.36f); }
public static IEnumerator Slash(Unit caster,Unit target,Action impact) { yield return CardMotion.Charge(caster,target,(_,__)=>impact?.Invoke()); }
}
public static class ElementPalette {
public static BaseEnums.UnitElement Parse(string value) => Enum.TryParse<BaseEnums.UnitElement>(value,true,out var result)?result:BaseEnums.UnitElement.None;
public static Color ColorFor(BaseEnums.UnitElement element) { return default; }
public static Color ColorFor(string elementName) { return default; }
}
public static class ElementReactionFx {
public static void Attachment(Unit target, BaseEnums.UnitElement element) {  }
public static void Reaction(Unit target, string name) {  }
}
public static class EmberFx {
public static IEnumerator Volley(Unit caster, Unit target, Action onImpact) { yield return VfxComposer.PlaySingleTarget("himiko.ember",caster,target,onImpact); }
public static IEnumerator Slash(Unit caster, Unit target, Action onImpact) { yield return VfxComposer.PlaySingleTarget("pygmalion.ember",caster,target,onImpact,1,.60f); }
public static void Blaze(Unit target, bool detonate) {  }
}
public static class EnemyNormalFx {
public static bool UsesGroundRake(Unit caster) { return default; }
public static bool UsesHeavyStrike(Unit caster) { return default; }
public static IEnumerator HeavyStrike(Unit caster, Unit target, Action onHit) { onHit?.Invoke();yield break; }
public static IEnumerator OriginalNormal(Unit caster, Unit target, Action onHit) { onHit?.Invoke();yield break; }
public static IEnumerator GroundRake(Unit caster, Unit target, Action onHit) { onHit?.Invoke();yield break; }
public static IEnumerator MirroredVolley(Unit caster, Unit target, Action onHit) { onHit?.Invoke();yield break; }
public static IEnumerator BeastHeavyClaw(Unit caster, Unit target, Action onHit) { onHit?.Invoke();yield break; }
public static IEnumerator VanguardStrike(Unit caster, Unit target, Action onHit) { onHit?.Invoke();yield break; }
public static IEnumerator UltimateSingle(string skillKey, Unit caster, Unit target,
            Action onHit, float recoil = 0.62f, float minimumDuration = 0.42f) { onHit?.Invoke();yield break; }
public static IEnumerator RepeatedUltimate(string skillKey, Unit caster, int count,
            float interval, Func<Unit> pickTarget, Func<Unit, Action> buildImpact,
            float castLead = 0f, float extraScale = 1f, float castDelay = 0f,
            bool repeatCastPerHit = false)
{
    Unit first=pickTarget?.Invoke();if(caster==null||first==null)yield break;
    if(castLead>0)yield return new WaitForSeconds(castLead);
    int pending=0;
    IEnumerator Impact(Unit target,Action action) { yield return PresentationClock.Hit(skillKey,action);pending--; }
    for(int i=0;i<count;i++)
    {
        Unit target=i==0?first:pickTarget?.Invoke();if(target==null)break;
        Action impact=buildImpact?.Invoke(target);pending++;VfxRunner.Run(Impact(target,impact));
        if(i+1<count)yield return new WaitForSeconds(interval);
    }
    while(pending>0)yield return null;
}
}
public static class GeoFx {
public static IEnumerator RadiantVolley(Unit caster, Unit target, Action onHit, float scale = 1f) { yield return VfxComposer.PlaySingleTarget("sei.ground",caster,target,onHit); }
public static void CrystalGained(MonoBehaviour runner, Unit from, Unit to) {  }
// 결정은 선택 후 비행하는 동안 다른 결정과 겹친다. 즉시 적중시키면 처치 후 재선정 확률이 바뀐다.
public static IEnumerator BigBang(Unit caster, int shards, Func<Unit> pickTarget, Action<Unit> onHit)
{
    yield return new WaitForSeconds(.04f);
    int landed=0;
    IEnumerator Impact(Unit target) { yield return new WaitForSeconds(.242f);onHit?.Invoke(target);landed++; }
    for(int i=0;i<shards;i++)
    {
        var target=pickTarget?.Invoke();
        if(target==null) {landed++;continue;}
        VfxRunner.Run(Impact(target));
        if(i+1<shards) yield return new WaitForSeconds(.075f);
    }
    while(landed<shards) yield return null;
}
public static IEnumerator SongOfGenesis(Unit caster, IReadOnlyList<Unit> allies, Action<Unit> onReach) { foreach(var item in allies) onReach?.Invoke(item);yield break; }
public static void StoneShield(Unit ally, float strength = 1f, string composedSkillKey = "sei.genesis") {  }
public static void ConcentrationBroken(Unit caster) {  }
}
public static class ImpactFx {
public static void Slam(Vector3 position, Vector3 direction, Color accent, float scale = 1f) {  }
public static void TrueSpark(Vector3 position) {  }
}
public static class JeanFx {
public static IEnumerator HolyStrike(Unit caster, Unit target, bool counter, Action onImpact) { yield return VfxComposer.PlaySingleTarget(counter?"jean.iron_sanction":"jean.holy_strike",caster,target,onImpact,1,counter?.34f:.46f); }
public static IEnumerator TensionRising(Unit caster, Action onPeak) { onPeak?.Invoke();yield break; }
}
public static class LokapalaFx {
public static IEnumerator Normal(string key, Unit caster, Unit target, Action onImpact) { yield return VfxComposer.PlaySingleTarget(key,caster,target,onImpact); }
public static IEnumerator Area(string key, Unit caster, IReadOnlyList<Unit> targets, Action onImpact) { onImpact?.Invoke();yield break; }
public static IEnumerator Gather(Unit caster, Action openGate) { openGate?.Invoke();yield break; }
public static void TreasuryShield(Unit caster, Unit target, int previousShield) {  }
public static void ResourceGained(Unit caster, SfxActionSlot slot = SfxActionSlot.Passive) {  }
}
public static class NordFx {
public static IEnumerator SurtrStrike(Unit caster, Unit target, Action impact) { yield return new WaitForSeconds(.16f);impact?.Invoke();yield return new WaitForSeconds(.25f); }
public static IEnumerator SurtrUltimate(Unit caster, IReadOnlyList<Unit> targets, bool empowered, Action impact)
{ yield return new WaitForSeconds(.28f);yield return new WaitForSeconds(.19f);impact?.Invoke();yield return new WaitForSeconds(.43f); }
public static IEnumerator FreyaPrayer(Unit caster) { yield break; }
public static void FreyaEmergency(Unit target) {  }
}
public static class OrionSkadiFx {
public static ProjectileStyle Arrow(bool ice = false, float size = 1f,bool purchased=false) => new ProjectileStyle {Duration=.30f};
public static IEnumerator Star(Unit caster, Unit target, Action impact) { yield return ProjectileFx.Fly(caster,target,Arrow(false,1.8f,true),impact); }
public static IEnumerator Rain(Unit caster, IReadOnlyList<Unit> targets, Action impact, bool ice = false) { impact?.Invoke();yield break; }
}
public static class ProjectileFx {
public static ProjectileStyle ElementStyle(BaseEnums.UnitElement element, float scale = 1f) { return new ProjectileStyle {Duration=.36f}; }
public static IEnumerator Fly(Unit from, Unit to, ProjectileStyle style, Action onImpact) { yield return new WaitForSeconds(style?.Duration??.36f);onImpact?.Invoke(); }
}
public static class PurchasedArcherFx {
public static void RosterPreparation(Unit caster,Unit target,bool attack=true) { }
public static void WeaveCast(Unit caster) {  }
public static void WeaveReceive(Unit target,Unit caster=null) {  }
}
public static class PurchasedProjectileFx {
public static IEnumerator Fly(Unit caster,Unit target,float seconds,Action onImpact,
            float weight=.85f,Unit bounceFrom=null,Vector3? lastBouncePoint=null,bool paintContact=true,
            bool minimumFlightFrames=true,bool elementalInk=false) { if(seconds>0)yield return new WaitForSeconds(seconds);onImpact?.Invoke(); }
}
public static class PurchasedSupportThreadsFx {
public enum Kind { Order, Receive, Heal, Ward }
public static void Play(Unit caster,Unit target,Kind kind,bool prepareCaster=true) {  }
}
public static class SabahFx {
public static IEnumerator ShadowAmbush(Unit caster, Unit target, Action onImpact) { yield return new WaitForSeconds(.224f);yield return PresentationClock.Hit("sabah.lightning_slash",onImpact); }
public static IEnumerator LightningSweep(Unit caster, IReadOnlyList<Unit> targets,
            Action<Unit> onImpact) { foreach(var item in targets) onImpact?.Invoke(item);yield break; }
public static IEnumerator Azrael(Unit caster, IReadOnlyList<Unit> targets, Action<Unit> onHit)
{
    if(targets==null||targets.Count==0)yield break;
    Vector3 center=targets.Aggregate(Vector3.zero,(sum,target)=>sum+VfxSpace.PortraitPoint(target))/targets.Count;
    yield return VfxComposer.PlayHorizontalSweepHit("sabah.azrael",1,caster,targets[0],center,targets,onHit);
}
}
public static class ShiFx {
public static IEnumerator DestructionSlash(Unit caster, Unit target, Action onImpact,
            float scale = 1f) { yield return new WaitForSeconds(.128f);yield return PresentationClock.Hit("shi.destruction_slash",onImpact); }
}
public static class SummonAuthorsFx {
public static IEnumerator Prayer(Unit unit, bool theatre, bool ultimate = false) { yield break; }
public static void Restore(Unit unit) {  }
}
public static class UchiFx {
public static IEnumerator PrepareStrike(Unit clone) { for(float t=0;t<.24f;t+=Time.deltaTime)yield return null; }
public static void Strike(Unit clone, Unit target, float scale = 1f, bool ultimate = false) {  }
public static void Guard(Unit clone, Unit attacker) {  }
public static IEnumerator PrepareUltimate(Unit caster, IReadOnlyList<Unit> clones) { yield return new WaitForSeconds(.24f); }
}
public static class VedicTrioFx {
public static IEnumerator Single(string key, Unit caster, Unit target, Action onImpact) { yield return VfxComposer.PlaySingleTarget(key,caster,target,onImpact); }
public static IEnumerator SuryaAdditional(Unit caster, Unit target, bool empowered, Action onImpact) { onImpact?.Invoke();yield break; }
public static IEnumerator AgniAdditional(Unit caster, IReadOnlyList<Unit> targets, Unit centralTarget,
            bool empowered, Action onImpact) { onImpact?.Invoke();yield break; }
public static IEnumerator AgniUltimate(Unit caster, IReadOnlyList<Unit> beneficiaries, Action onBuff) { onBuff?.Invoke();yield break; }
public static IEnumerator AgniSpecialSetup(Unit caster, Action onSetup) { onSetup?.Invoke();yield break; }
public static void Reload(Unit caster, int actualAmmo) {  }
}
public static class VfxSpace {
public static Vector3 PortraitPoint(Unit unit) => WorldPoint(unit);
public static Vector3 WorldPoint(Unit unit) => unit?.currentCell!=null?new Vector3(unit.currentCell.xPos,-unit.currentCell.DisplaySlot):unit?.SummonOwner!=null?WorldPoint(unit.SummonOwner):default;
public static Vector3 ContactPoint(Unit unit) => WorldPoint(unit);
public static int SortingLayer(Unit unit) { return default; }
public static VfxAim AimFrom(Unit caster, Unit target) { return default; }
}
}
