#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Buffs;
using Effects.Negative;
using Entities;
using Helpers;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioOvergrowthCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); _cfActionIdSequence = 0;
            var catalog = CampaignBuildCatalog();
            config.lineup = catalog.teams.Single(spec => spec.aliases.Contains("사바흐/best")).key;
            yield return EnsureLineup(config);
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            Unit sabah = CfAlly(3), gaudi = CfAlly(4), enemy = AliveEnemies().First();
            void Check(string label, bool passed, string actual = "") => YvCheck("과성장 " + label, "현행 사양", actual, passed);
            Check("5인·신규 팩토리·원소", AliveAllies().Count == 5 && gaudi.Element == "Dendro" &&
                gaudi.ActivePassiveCodes.OfType<GaudiSagradaFamilia>().Any() && gaudi.ActiveNormalCode is GaudiNormalAttack &&
                gaudi.ActiveUltimateCode is GaudiImmortalLegacy && sabah.ActiveNormalCode is SabahLightningSlash);
            Check("원화 리소스", SpriteResource.LoadPortrait("GAUDI_PORTRAIT") != null && SpriteResource.LoadStanding("GAUDI_STANDING") != null);
            var cues = new YamlDotNet.Serialization.DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                .Deserialize<SfxCueDataList>(Resources.Load<TextAsset>("Data/95_sfx_cues").text);
            bool linked = true;
            foreach (var slot in new[] { Managers.SfxActionSlot.Normal, Managers.SfxActionSlot.Ultimate, Managers.SfxActionSlot.Passive })
            {
                var definition = Effects.Vfx.VfxDefinitionCatalog.Skill("gaudi." + slot.ToString().ToLowerInvariant());
                var cue = cues.cues.SingleOrDefault(c => c.name == SfxCueRoutes.Resolve(gaudi, slot, Managers.SfxCuePhase.Hit));
                linked &= definition != null && definition.slots.All(s => Effects.Vfx.VfxDefinitionCatalog.Part(s.partId) != null) &&
                    cue != null && cue.clips.All(c => Resources.Load<AudioClip>("Audio/Sfx/" + c) != null);
            }
            Check("3 VFX·3 SFX 경로", linked);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) { CfNoCrit(unit); unit.ClearUltimateResource(); }
            foreach (Unit ally in AliveAllies()) CfEnsureEffectHit(ally, AliveEnemies());
            DebugMode.AllyInvincible = true; DebugMode.EnemyInvincible = false;
            var passive = gaudi.ActivePassiveCodes.OfType<GaudiSagradaFamilia>().Single();
            Check("전투 시작 전원 보호막", AliveAllies().All(u => u.ShieldCurr >= passive.BaseShieldAmount));
            int openingShield = gaudi.ShieldCurr;
            passive.CastCode();
            Check("동일 전투 시작 방호 중복 없음", gaudi.ShieldCurr == openingShield);
            Check("우수한 방어 기본 보호막 조회", ShieldCapability.StrongestBaseAmount(gaudi) == gaudi.ActiveUltimateCode.BaseShieldAmount);
            float defense = enemy.DefCurr, specialDefense = enemy.SpDefCurr;
            enemy.ResetCombatElements();
            enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: sabah);
            enemy.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: gaudi);
            var reaction = enemy.GetAllStatuses().SingleOrDefault(s => s.Key == ElementalReaction.ActivationStatusKey);
            float cut = .3f * gaudi.SpAtkCurr / (gaudi.SpAtkCurr + UnitStats.ArmorConstant(gaudi.Level));
            Check("반응 이름·디버프·기존 키", reaction != null && reaction.CountsAsDebuff &&
                ElementalReaction.AllReactions().Any(r => r.Name == "과성장" && r.Stat == BaseEnums.StatType.SpecialDefense));
            Check("실제 특방 감소·방어 유지", Mathf.Abs(enemy.SpDefCurr - specialDefense * (1 - cut)) <= 1f && enemy.DefCurr == defense);
            Check("기존 취약 배율 중복 없음", reaction != null && reaction.Effects.All(e => e.EffectObject is not MitigationVulnerabilityEffect));
            Check("양 원소 소모·아즈라엘 충전", !enemy.HasAttachedElement(BaseEnums.UnitElement.Dendro) &&
                !enemy.HasAttachedElement(BaseEnums.UnitElement.Electro) && sabah.ManaCurr == 1);
            enemy.RemoveAllNegativeStatuses(sabah);
            Check("정화 후 특방 원복", !enemy.HasStatusKey(ElementalReaction.ActivationStatusKey) && enemy.SpDefCurr == specialDefense);
            sabah.ClearUltimateResource();
            using (var log = new YvRecorder())
            {
                yield return NordAction(sabah, Kind.Normal, s => s.AdvanceAction(sabah, 1));
                var hit = log.Dealt.FirstOrDefault(h => h.Attacker == sabah && h.DamageContext.CodeType == BaseEnums.CodeType.Normal);
                Check("검격 특공 계수·직접 특수피해", hit != null && hit.DamageContext.Damage == Mathf.RoundToInt(110 + sabah.SpAtkCurr) &&
                    hit.DamageContext.HasTag(DamageTag.SpecialDamage) && DamageMitigation.TryGetDefenseStat(hit.DamageContext, out var stat) && stat == BaseEnums.StatType.SpecialDefense);
            }
            using (var log = new YvRecorder())
            {
                yield return NordAction(sabah, Kind.Ultimate, _ => sabah.FillUltimateResource());
                var hits = log.Dealt.Where(h => h.Attacker == sabah && h.DamageContext.CodeType == BaseEnums.CodeType.Ultimate).ToList();
                Check("아즈라엘 전원1회·특공·특수", hits.Count == AliveEnemies().Count() && hits.All(h =>
                    h.DamageContext.Damage == Mathf.RoundToInt(150 + sabah.SpAtkCurr) && h.DamageContext.HasTag(DamageTag.SpecialDamage)));
                Check("아즈라엘 고유 자원 소모", sabah.ManaCurr == 0);
            }
            foreach (Unit target in AliveEnemies()) target.ResetCombatElements();
            yield return NordAction(gaudi, Kind.Normal, s => s.AdvanceAction(gaudi, 1));
            Check("일반 풀 부착", AliveEnemies().Any(u => u.HasAttachedElement(BaseEnums.UnitElement.Dendro)));
            foreach (Unit ally in AliveAllies()) ally.RemoveShield(ally.ShieldCurr);
            yield return NordAction(gaudi, Kind.Ultimate, _ => gaudi.FillUltimateResource());
            Check("초월 전원 방400% 보호막", AliveAllies().All(u => u.ShieldCurr == gaudi.ActiveUltimateCode.BaseShieldAmount));
            Check("초월 전원 풀 부착", AliveEnemies().All(u => u.HasAttachedElement(BaseEnums.UnitElement.Dendro)));
            enemy.RemoveAllNegativeStatuses(sabah); enemy.ResetCombatElements();
            enemy.GrantCombatElement(BaseEnums.UnitElement.Dendro, source: gaudi);
            enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: sabah);
            Check("역순 부착도 같은 특방 감소", enemy.HasStatusKey(ElementalReaction.ActivationStatusKey) && enemy.SpDefCurr < specialDefense);
            for (int i = 0; i < 4; i++) yield return NordAction(enemy, Kind.Normal, s => s.AdvanceAction(enemy, 1));
            Check("기간 만료 시 특방 복원", !enemy.HasStatusKey(ElementalReaction.ActivationStatusKey) && enemy.SpDefCurr == specialDefense);
            passive.StopCode();
            Check("정지 시 자기 경감 정리", !gaudi.HasStatusKey(GaudiSagradaFamilia.Key));
            DebugMode.AllyInvincible = false;
            OsFinish(enabled);
        }
    }
}
#endif
