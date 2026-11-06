#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Codes.Passive.Reward;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private void TmCheck(string label, bool ok, string actual = "") => YvCheck("신규3인·마나 " + label, "확정 사양", actual, ok);
        private void TmEquip(Unit unit, params int[] ids)
        {
            foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
            unit.ApplyRewardCodeInventory(ids.Concat(Enumerable.Repeat(0, 7 - ids.Length)).ToList(), new List<int>());
            foreach (int id in ids) YvCode(unit, id)?.CastCode();
        }
        private void TmQuiet()
        {
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.ActiveUltimateCode?.StopCode(); unit.ClearUltimateResource(); unit.RemoveShield(unit.ShieldCurr); CfNoCrit(unit);
            }
        }
        private sealed class TmManaEfficiency : BaseEffect
        {
            public TmManaEfficiency() : base(0) { }
            public override float ManaRecoveryMultiplierModifier(Unit unit) => 1.5f;
        }
        private static DamageContext TmHit(Unit actor, Unit target, float reduction = 0, int damage = 1)
        {
            var hit = new DamageContext(actor, damage, BaseEnums.CodeType.Normal, new() { DamageTag.NormalAttack, DamageTag.SingleTarget, DamageTag.TrueDamage }) { ToughnessReduction = reduction };
            target.TakeDamage(hit); return hit;
        }
        private IEnumerator ScenarioTrioMana(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            yield return YvPrepareScene(config, x => enabled = x); yield return EtPrepareEnemies(config);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            Unit oct = CfAlly(101), agr = CfAlly(100), hor = CfAlly(120), orp = CfAlly(25);
            var foes = AliveEnemies(); var first = foes.First(); TmQuiet();
            var fleet = oct.ActivePassiveCodes.OfType<OctaviaEncirclement>().Single();
            var formation = agr.ActivePassiveCodes.OfType<AgrippaUnsinkableFormation>().Single();
            var falcon = hor.ActivePassiveCodes.OfType<HorusShatteringHorizon>().Single();
            TmCheck("원화·팩토리·마나", new[] { oct, agr, hor }.All(x => x.ActiveNormalCode != null && x.ActiveUltimateCode != null && Helpers.SpriteResource.LoadPortrait(x.PortraitPath, out _) != null) && oct.ManaMax == 100 && agr.ManaMax == 120 && hor.ManaMax == 100);
            TmCheck("호루스 특방30%·삭감 보너스 없음", Mathf.Abs(falcon.PermanentStatPercentModifier(BaseEnums.StatType.SpecialDefense) - .30f) < .0001f && Mathf.Abs(Toughness.Efficiency(hor) - 1) < .0001f);
            foreach (Unit foe in foes) { EhStat(foe, BaseEnums.StatType.SpecialDefense, 100); foe.ClearToughness(); }
            EhStat(oct, BaseEnums.StatType.SpecialAttack, 400); float defense = hor.StatValue(BaseEnums.StatType.SpecialDefense);
            fleet.CastCode();
            TmCheck("옥타비아 전원 특방 오라", Mathf.Abs(hor.StatValue(BaseEnums.StatType.SpecialDefense) - defense - 120) < .01f);
            foreach (Unit foe in foes) fleet.ClearMarks(foe);
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(oct, Kind.Normal, s => s.AdvanceAction(oct, 1), Application.isBatchMode ? null : "trio_octavia_normal");
                var hits = log.Dealt.Where(x => x.Attacker == oct && x.DamageContext.CodeType == BaseEnums.CodeType.Normal).ToList();
                TmCheck("옥타비아 일반 공70%·광역", hits.Count == foes.Count && hits.All(x => x.DamageContext.Damage == Mathf.RoundToInt(oct.StatValue(BaseEnums.StatType.Attack) * .70f)));
                TmCheck("공격받은 모든 적 포위1", foes.All(x => fleet.Marks(x) == 1));
                TmCheck("옥타비아 일반 물 부착 없음", !log.Elements.Any(x => x.Source == oct));
            }
            long before = fleet.Marks(first);
            yield return NordAction(hor, Kind.Additional, s => s.EnqueueAdditional(hor, "tm_multi", "다단 검증", () => { for (int i = 0; i < 3; i++) TmHit(hor, first); }));
            TmCheck("다단은 같은 행동·대상1중첩", fleet.Marks(first) == before + 1);
            before = fleet.Marks(first);
            first.TakeDamage(new DamageContext(hor, 1, BaseEnums.CodeType.Effect, new() { DamageTag.BonusDamage, DamageTag.TrueDamage }));
            first.TakeDamage(new DamageContext(hor, 1, BaseEnums.CodeType.Effect, new() { DamageTag.TrueDamage }) { IsDamageOverTime = true });
            TmCheck("덧붙임·지딜은 포위 제외", fleet.Marks(first) == before);
            hor.ModifyHp(hor.HpMax / 2); float hpRatio = hor.HpCurr / (float)hor.HpMax;
            EhStat(oct, BaseEnums.StatType.SpecialAttack, 600);
            TmCheck("특공 변경 즉시 특방 갱신·HP비율 보존", Mathf.Abs(hor.StatValue(BaseEnums.StatType.SpecialDefense) - defense - 180) < .01f && Mathf.Abs(hor.HpCurr / (float)hor.HpMax - hpRatio) < .002f);
            for (int i = 0; i < 25; i++) TmHit(hor, first);
            TmCheck("포위 상한·턴 만료 없음", fleet.Marks(first) >= 25);
            long stacks = fleet.Marks(first);
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(oct, Kind.Ultimate, s => oct.FillUltimateResource(), Application.isBatchMode ? null : "trio_octavia_ultimate");
                int expected = Mathf.RoundToInt(oct.StatValue(BaseEnums.StatType.Attack) * 1.8f * (1 + stacks * .08f));
                TmCheck("초월기 대상별 포위 배율", log.Dealt.Any(x => x.Attacker == oct && x.Target == first && x.DamageContext.HasTag(DamageTag.UltAttack) && x.DamageContext.Damage == expected), expected.ToString());
                TmCheck("적중 후 포위 전부 소비", foes.All(x => fleet.Marks(x) == 0));
                TmCheck("초월기 물 부착", log.Elements.Any(x => x.Source == oct && x.Element == BaseEnums.UnitElement.Hydro));
            }
            fleet.StopCode();
            TmCheck("오라 정지 특방 원복", Mathf.Abs(hor.StatValue(BaseEnums.StatType.SpecialDefense) - defense) < .01f);
            TmHit(hor, first); TmCheck("정지 후 포위 생성 없음", fleet.Marks(first) == 0);
            foreach (Unit foe in foes) YvResetEnemy(foe);
            falcon.CastCode();
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(hor, Kind.Normal, s => s.AdvanceAction(hor, 1), Application.isBatchMode ? null : "trio_horus_normal");
                TmCheck("파성 공140%·단일", log.Dealt.Count(x => x.Attacker == hor) == 1 && log.Dealt.Any(x => x.Attacker == hor && x.DamageContext.Damage == Mathf.RoundToInt(hor.StatValue(BaseEnums.StatType.Attack) * 1.4f)));
            }
            hor.ClearUltimateResource();
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(hor, Kind.Ultimate, s => hor.FillUltimateResource(), Application.isBatchMode ? null : "trio_horus_release");
                TmCheck("해방 무피해·3회 상태", !log.Dealt.Any(x => x.Attacker == hor) && hor.GetStatuses().Single(x => x.Key == HorusSolarRelease.ReleaseKey).RemainingTurns == 3);
            }
            int announcementStart = _announcements.Count;
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(hor, Kind.Normal, s => s.AdvanceAction(hor, 1), Application.isBatchMode ? null : "trio_horus_sun");
                var hits = log.Dealt.Where(x => x.Attacker == hor).ToList();
                int main = Mathf.RoundToInt(hor.StatValue(BaseEnums.StatType.Attack) + hor.StatValue(BaseEnums.StatType.SpecialAttack) * 1.6f);
                int side = Mathf.RoundToInt(hor.StatValue(BaseEnums.StatType.Attack) * .5f + hor.StatValue(BaseEnums.StatType.SpecialAttack) * .8f);
                TmCheck("강화 소사 공·특공 합산·전원", hits.Count == foes.Count && hits.Count(x => x.DamageContext.Damage == main) == 1 && hits.Count(x => x.DamageContext.Damage == side) == foes.Count - 1);
                TmCheck("강화 사격 배너는 일륜 소사", _announcements.Skip(announcementStart).Any(x => x.Kind == ActionAnnouncementKind.Normal && x.Name == "일륜 소사"));
                TmCheck("강화 일반은 일반 태그만", hits.All(x => x.DamageContext.HasTag(DamageTag.NormalAttack) && !x.DamageContext.HasTag(DamageTag.AdditionalAttack)));
            }
            int mana = hor.ManaCurr;
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(hor, Kind.Additional, s => s.EnqueueAdditional(hor, "tm_sun", "일륜 소사", () => ((HorusStarbreaker)hor.ActiveNormalCode).CastSunAdditional(first)));
                TmCheck("추공 일반+추가 이중 피해 판정", log.Dealt.Count(x => x.Attacker == hor) == foes.Count && log.Dealt.Where(x => x.Attacker == hor).All(x => x.DamageContext.HasTag(DamageTag.NormalAttack) && x.DamageContext.HasTag(DamageTag.AdditionalAttack)));
            }
            TmCheck("추공은 마나20·해방 기간 소비 없음", hor.ManaCurr == mana && hor.GetStatuses().Single(x => x.Key == HorusSolarRelease.ReleaseKey).RemainingTurns == 2);
            yield return NordAction(hor, Kind.Normal, s => s.AdvanceAction(hor, 1)); hor.ClearUltimateResource();
            yield return NordAction(hor, Kind.Normal, s => s.AdvanceAction(hor, 1));
            TmCheck("세 번째 강화 공격 후 해방 종료", !hor.HasStatusKey(HorusSolarRelease.ReleaseKey));
            var provider = orp.ActivePassiveCodes.OfType<OrpheusCheatedDeath>().Single(); provider.CastCode(); falcon.StopCode();
            foreach (Unit foe in foes)
            {
                YvResetEnemy(foe); foe.ClearToughness(); foe.GrantToughness(90, orp);
                TmHit(orp, foe, foe == first ? 60 : 75);
            }
            falcon.CastCode(); hor.ClearUltimateResource();
            int additional = 0; void End(Unit actor, Kind kind) { if (actor == hor && ActionScheduler.IsAdditional(kind)) additional++; }
            ActionScheduler.AnyActionFinished += End;
            try
            {
                using (var log = new CfLog(AliveAllies()))
                {
                    yield return NordAction(orp, Kind.Additional, s => s.EnqueueAdditional(orp, "tm_break", "격파 연쇄", () => TmHit(orp, first, 30)));
                    TmCheck("다른 아군 격파→소사→다른 격파 연쇄", additional == foes.Count && foes.All(x => x.ToughnessCurr == 0), additional.ToString());
                    TmCheck("격파당1추공·연쇄 유한", log.Dealt.Count(x => x.Attacker == hor) == foes.Count * foes.Count);
                    TmCheck("연쇄 추공 일반완료 마나 없음", hor.ManaCurr == (foes.Count - 1) * Unit.ManaPerKill, hor.ManaCurr.ToString());
                    TmCheck("격파는 실제 사망 아님", foes.All(x => x.isActive));
                }
            }
            finally { ActionScheduler.AnyActionFinished -= End; }
            yield return NordAction(first, Kind.Normal, s => s.AdvanceAction(first, 1));
            TmCheck("적 실제 일반행동 시작에 강인도 회복", first.ToughnessCurr == 90);
            falcon.StopCode(); provider.StopCode(); foreach (Unit foe in foes) foe.ClearToughness();
            foreach (Unit ally in AliveAllies()) { ally.ClearUltimateResource(); ally.RemoveShield(ally.ShieldCurr); }
            int shield = Mathf.RoundToInt(agr.StatValue(BaseEnums.StatType.Defense) * .8f);
            formation.CastCode(); formation.CastCode();
            TmCheck("아그리파 개전 방80%·중복 활성 무효", AliveAllies().All(x => x.ShieldCurr == shield));
            var shields = AliveAllies().ToDictionary(x => x, x => x.ShieldCurr);
            using (var log = new CfLog(AliveAllies()))
            {
                yield return NordAction(agr, Kind.Normal, s => s.AdvanceAction(agr, 1), Application.isBatchMode ? null : "trio_agrippa_normal");
                TmCheck("아그리파 일반 비공격·마나20", !log.Dealt.Any(x => x.Attacker == agr) && agr.ManaCurr == 20);
                TmCheck("아그리파 일반 전원 방40% 누적", AliveAllies().All(x => x.ShieldCurr - shields[x] == Mathf.RoundToInt(agr.DefCurr * .4f)));
            }
            YvAddTestStatus(hor, 99809, "tm_efficiency", new TmManaEfficiency());
            shields = AliveAllies().ToDictionary(x => x, x => x.ShieldCurr);
            yield return NordAction(agr, Kind.Ultimate, s => agr.FillUltimateResource(), Application.isBatchMode ? null : "trio_agrippa_ultimate");
            TmCheck("아그리파 초월 전원 방160% 누적", AliveAllies().All(x => x.ShieldCurr - shields[x] == Mathf.RoundToInt(agr.DefCurr * 1.6f)));
            TmCheck("다른 마나형20%·효율 미적용·자신 제외", hor.ManaCurr == 20 && oct.ManaCurr == 20 && orp.ManaCurr == 20 && agr.ManaCurr == 0 && CfAlly(2).ManaCurr == CfAlly(2).ManaMax);
            var probe = new DamageContext(hor, 100, BaseEnums.CodeType.Ultimate, new() { DamageTag.UltAttack, DamageTag.TrueDamage }) { ToughnessReduction = 0 };
            first.TakeDamage(probe); TmCheck("보호막 보유 초월기 피해20%", probe.ResolvedDamage == 120, probe.ResolvedDamage.ToString());
            hor.RemoveShield(hor.ShieldCurr); probe = new DamageContext(hor, 100, BaseEnums.CodeType.Ultimate, new() { DamageTag.UltAttack, DamageTag.TrueDamage }) { ToughnessReduction = 0 };
            first.TakeDamage(probe); TmCheck("보호막 없으면 증폭 종료", probe.ResolvedDamage == 100);
            formation.StopCode(); TmCheck("정지 오라 정리·유한 보호막 유지", !oct.HasStatusKey(formation.AuraKey) && oct.ShieldCurr > 0);
            hor.RemoveStatusByKey("tm_efficiency"); OsFinish(enabled);
        }

        private IEnumerator ScenarioManaRewards(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0; bool enabled = true;
            yield return YvPrepareScene(config, x => enabled = x); yield return EtPrepareEnemies(config);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false; TmQuiet();
            Unit hero = CfAlly(120), agr = CfAlly(100), oct = CfAlly(101), shi = CfAlly(2);
            foreach (Unit unit in AliveAllies()) { unit.FillUltimateResource(); unit.PrepareRoundResources(); }
            TmCheck("매 전투 마나0·상시 자원 유지", AliveAllies().Where(x => x.UltimateResourceType == BaseEnums.UltimateResourceType.Mana).All(x => x.ManaCurr == 0) && shi.ManaCurr == shi.ManaMax);
            var pool = RewardManager.Instance.BuildCodePool();
            foreach (int id in new[] { 514, 772, 919 }) TmCheck("보상 풀·팩토리·상점 " + id, RewardCodeFactory.IsImplemented(id) && RewardCodeCatalog.Find(id).shopFixed && pool.Any(x => x.codeId == id));
            TmEquip(hero, 514, 772, 919, 803); hero.PrepareRoundResources(); hero.CastPassiveCode();
            TmCheck("마나 수정20+사전준비50 합산", hero.ManaCurr == 70, hero.ManaCurr.ToString());
            YvCode(hero, 514).CastCode(); YvCode(hero, 803).CastCode();
            TmCheck("개전 보상 재시전 중복 방지", hero.ManaCurr == 70);
            var tonic = new Managers.ItemData { id = 5105, name = "집중의 비약", tonicStartManaPercent = 30 };
            TonicEffect.Apply(hero, tonic); TonicEffect.Apply(hero, tonic);
            TmCheck("비약30 합산·중복 방지", hero.ManaCurr == 100 && hero.StoredUltimateMana == 0);
            YvAddTestStatus(hero, 99808, "tm_efficiency", new TmManaEfficiency());
            hero.RecoverMana(20); TmCheck("효율은 최초 획득에1회·초과30 저장", hero.StoredUltimateMana == 30);
            hero.AddUltimateResource(10000); TmCheck("저장 마나 상한100%", hero.StoredUltimateMana == hero.ManaMax);
            hero.ClearManaRewardState(772); hero.AddUltimateResource(30);
            int spent = 0; void Spending(Unit actor, int amount) { if (actor == hero) spent += amount; }
            Unit.AnyUltimateManaSpent += Spending;
            try { yield return NordAction(hero, Kind.Ultimate, s => hero.FillUltimateResource()); }
            finally { Unit.AnyUltimateManaSpent -= Spending; }
            TmCheck("초월기 소비100·저장30 이동·재증폭 없음", spent == 100 && hero.ManaCurr == 30 && hero.StoredUltimateMana == 0, spent + "/" + hero.ManaCurr);
            hero.RemoveStatusByKey("tm_efficiency"); hero.RemoveStatusByKey("tonic");
            TmEquip(hero, 772, 919); hero.PrepareRoundResources(); hero.CastPassiveCode();
            int casts = 0, replays = 0, manaSpent = 0;
            void Cast(Unit actor) { if (actor == hero) { casts++; if (actor.IsUltimateReplay) replays++; } }
            void Paid(Unit actor, int amount) { if (actor == hero) manaSpent += amount; }
            Unit.AnyUltimateCast += Cast; Unit.AnyUltimateManaSpent += Paid;
            try
            {
                yield return NordAction(hero, Kind.Ultimate, s => hero.FillUltimateResource());
                yield return NordAction(hero, Kind.Ultimate, s => hero.FillUltimateResource());
                TmCheck("정상2회는 복제 없음", casts == 2 && replays == 0 && hero.OriginalUltimateCount == 2);
                yield return CfRun(s => hero.AddUltimateResource(hero.ManaMax * 2), () => casts >= 5 && !hero.HasPendingUltimateCopy && !hero.isCasting);
                TmCheck("3회째 복제+저장분 정상 재시전", casts == 5 && replays == 1 && hero.OriginalUltimateCount == 4, casts + "/" + replays + "/" + hero.OriginalUltimateCount);
                TmCheck("무료 복제는 마나 미소비·카운트 제외", manaSpent == 400 && hero.ManaCurr == 0 && hero.StoredUltimateMana == 0, manaSpent.ToString());
                TmCheck("해방 재시전3회 갱신", hero.GetStatuses().Single(x => x.Key == HorusSolarRelease.ReleaseKey).RemainingTurns == 3);
            }
            finally { Unit.AnyUltimateCast -= Cast; Unit.AnyUltimateManaSpent -= Paid; }
            TmEquip(hero); TmCheck("보상 해제 저장·카운트·복제 정리", hero.StoredUltimateMana == 0 && hero.OriginalUltimateCount == 0 && !hero.HasPendingUltimateCopy);
            TmEquip(hero, 919); hero.PrepareRoundResources(); hero.CastPassiveCode();
            hero.RemoveStatusByKey(HorusSolarRelease.ReleaseKey); hero.AddUltimateResource(20); hero.CastUltimateCode();
            TmCheck("자원 부족 직접 시전은 소비·발동 없음", hero.ManaCurr == 20 && !hero.isCasting && hero.OriginalUltimateCount == 0);
            yield return NordAction(hero, Kind.Ultimate, s => hero.FillUltimateResource());
            yield return NordAction(hero, Kind.Ultimate, s => hero.FillUltimateResource());
            hero.RemoveStatusByKey(HorusSolarRelease.ReleaseKey);
            void CancelThird(Unit actor) { if (actor == hero && !hero.IsUltimateReplay) hero.ActiveUltimateCode.StopCode(); }
            Unit.AnyUltimateCast += CancelThird;
            try { yield return NordAction(hero, Kind.Ultimate, s => hero.FillUltimateResource()); }
            finally { Unit.AnyUltimateCast -= CancelThird; }
            TmCheck("세 번째 원본 취소 시 무료 복제 폐기", hero.OriginalUltimateCount == 3 && !hero.HasPendingUltimateCopy && !hero.HasStatusKey(HorusSolarRelease.ReleaseKey));
            TmEquip(oct, 919); oct.PrepareRoundResources(); oct.CastPassiveCode();
            yield return NordAction(oct, Kind.Ultimate, s => oct.FillUltimateResource());
            yield return NordAction(oct, Kind.Ultimate, s => oct.FillUltimateResource());
            int partialHits = 0;
            void ControlDuringVolley(DamageResolvedContext damage)
            {
                if (damage.Attacker == oct && damage.DamageContext.HasTag(DamageTag.UltAttack) && ++partialHits == 1)
                    oct.ControlStarts(new ControlContext(AliveEnemies().First(), 1));
            }
            Unit.AnyDamageDealt += ControlDuringVolley;
            try { yield return NordAction(oct, Kind.Ultimate, s => oct.FillUltimateResource()); }
            finally { Unit.AnyDamageDealt -= ControlDuringVolley; }
            TmCheck("광역 첫 타 뒤 제어는 후속·무료 복제 취소", partialHits == 1 && oct.isControlled && oct.OriginalUltimateCount == 3 && !oct.HasPendingUltimateCopy);
            oct.ControlEnds(); TmEquip(oct);
            TmEquip(shi, 514, 772); shi.PrepareRoundResources(); shi.CastPassiveCode(); shi.AddUltimateResource(1000);
            TmCheck("상시형은 초기/초과 마나 없음", shi.ManaCurr == shi.ManaMax && shi.StoredUltimateMana == 0);
            TmEquip(shi);
            TmEquip(agr, 919); agr.PrepareRoundResources(); agr.CastPassiveCode();
            foreach (Unit ally in AliveAllies()) ally.ClearUltimateResource();
            var shields = AliveAllies().ToDictionary(x => x, x => x.ShieldCurr);
            int agrCasts = 0; void AgrCast(Unit actor) { if (actor == agr) agrCasts++; }
            Unit.AnyUltimateCast += AgrCast;
            try
            {
                yield return NordAction(agr, Kind.Ultimate, s => agr.FillUltimateResource());
                yield return NordAction(agr, Kind.Ultimate, s => agr.FillUltimateResource());
                yield return CfRun(s => agr.FillUltimateResource(), () => agrCasts >= 4 && !agr.HasPendingUltimateCopy && !agr.isCasting);
            }
            finally { Unit.AnyUltimateCast -= AgrCast; }
            TmCheck("지원 초월 복제도 보호막·충전 정상", agrCasts == 4 && hero.ManaCurr == 80 && oct.ManaCurr == 80 && agr.ManaCurr == 0 && oct.ShieldCurr - shields[oct] == 4 * Mathf.RoundToInt(agr.DefCurr * 1.6f));
            TmEquip(hero, 514, 772, 919); hero.ApplyRewardCodeInventory(new[] { 514, 772, 919, 0, 0, 0, 0 }, new[] { 803 });
            var saved = RunManager.Instance.CaptureDebugSnapshot();
            var instanceIds = hero.RewardCodeSlotInstances.Where(x => x != null).Select(x => x.instanceId).ToArray();
            RunManager.Instance.RestoreDebugSnapshot(JsonUtility.FromJson<RunSaveData>(JsonUtility.ToJson(saved)));
            hero = CfAlly(120); agr = CfAlly(100); oct = CfAlly(101);
            TmCheck("v17 신규유닛·활성/보관·인스턴스 유지", RunSaveData.CurrentVersion == 17 && agr != null && oct != null && hero.RewardCodeSlots.Take(3).SequenceEqual(new[] { 514, 772, 919 }) && hero.StoredRewardCodeIds.Contains(803) && hero.RewardCodeSlotInstances.Where(x => x != null).Select(x => x.instanceId).SequenceEqual(instanceIds));
            foreach (Unit ally in AliveAllies()) ally.FillUltimateResource();
            CombatRoundLifecycle.End(AliveAllies(), AliveEnemies()); CombatRoundLifecycle.Begin(AliveAllies(), AliveEnemies());
            TmCheck("불러온 마나도 새 전투0+개전20", hero.ManaCurr == 20 && hero.StoredUltimateMana == 0 && hero.OriginalUltimateCount == 0);
            TmCheck("다른 마나형 새 전투0", agr.ManaCurr == 0 && oct.ManaCurr == 0);
            yield return CaptureStill("mana_rewards_final_state");
            OsFinish(enabled);
        }

        private IEnumerator ScenarioTrioManaNatural(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); yield return PrepareStage(config, Stage(config, 8), false, false, false, 4);
            var game = GameManager.Instance; int breaks = 0, suns = 0, ults = 0;
            void Broken(CombatDefeatContext c) { breaks++; }
            void Started(Unit actor, Kind kind, string label) { if (actor?.ID == 120 && ActionScheduler.IsAdditional(kind)) suns++; }
            void Cast(Unit actor) { if (actor?.IsEnemy == false) ults++; }
            Toughness.Broken += Broken; ActionScheduler.AnyActionStarted += Started; Unit.AnyUltimateCast += Cast;
            try
            {
                var party = AliveAllies(); foreach (Unit unit in party) { unit.DebugSetLevel(20); unit.RestoreHpOutsideCombat(unit.HpMax); }
                game.StartRound(); float deadline = Time.realtimeSinceStartup + 90;
                while (game.gameState == BaseEnums.GameState.RoundInProgress && Time.realtimeSinceStartup < deadline) yield return null;
                var result = CombatLog.Current.LastOrDefault(x => x.Text.StartsWith("전투 종료 — "));
                TmCheck("자연 진행 실제 전투 종료", result.Text != null && game.gameState is BaseEnums.GameState.RoundEnd or BaseEnums.GameState.RewardSelection, game.gameState + "/" + result.Text);
                TmCheck("자연 진행 격파·추가행동 관측", breaks > 0 && suns > 0, breaks + "/" + suns + "/초월" + ults);
                TmCheck("자연 진행 행동 시계 유한", game.ActionScheduler.ActionCount < 2000, game.ActionScheduler.ActionCount.ToString());
            }
            finally { Toughness.Broken -= Broken; ActionScheduler.AnyActionStarted -= Started; Unit.AnyUltimateCast -= Cast; }
            OsFinish(true);
        }
    }
}
#endif
