#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Passive.Reward;
using Combat;
using Core;
using Entities;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCoreAuraSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            DebugMode.AllyInvincible = DebugMode.EnemyInvincible = false;
            var allies = AliveAllies(); var enemy = AliveEnemies().First();
            foreach (Unit unit in allies.Concat(AliveEnemies()))
            {
                foreach (var code in unit.ActivePassiveCodes.ToList()) code.StopCode();
                unit.DebugClearStatuses(); unit.SetShield(0);
            }
            Unit low = allies[0], high = allies[1], actor = allies[2];
            low.TryGrantRewardCode(619); high.TryGrantRewardCode(721);
            var lowCode = low.ActivePassiveCodes.Single(x => x.CatalogId == 619);
            var highCode = high.ActivePassiveCodes.Single(x => x.CatalogId == 721);
            int Hit()
            {
                enemy.RestoreHpOutsideCombat(enemy.HpMax); enemy.SetShield(0);
                var damage = new DamageContext(actor, 100, BaseEnums.CodeType.Effect,
                    new List<int> { DamageTag.TrueDamage, DamageTag.SummonAttack });
                enemy.TakeDamage(damage); return damage.ResolvedDamage;
            }
            void Check(string name, int expected)
            {
                int actual = Hit(); YvCheck(name, expected.ToString(), actual.ToString(), System.Math.Abs(actual - expected) <= 1);
            }
            lowCode.CastCode(); highCode.CastCode();
            Check("상위 오라 하나만 적용", 130);
            highCode.StopCode();
            Check("상위 종료 후 하위 오라 유지", 115);
            lowCode.StopCode();
            Check("모든 오라 종료 후 제거", 100);
            lowCode.CastCode(); highCode.CastCode(); lowCode.StopCode();
            Check("하위 종료 후 상위 오라 유지", 130);
            highCode.StopCode();
            lowCode.CastCode(); highCode.CastCode(); low.Die(enemy);
            Check("하위 보유자 사망 후 상위 유지", 130);
            high.Die(enemy);
            Check("전 보유자 사망 후 오라 제거", 100);
            Unit first = allies[3], second = allies[4];
            first.TryGrantRewardCode(814); second.TryGrantRewardCode(814);
            var firstCode = first.ActivePassiveCodes.Single(x => x.CatalogId == 814);
            var secondCode = second.ActivePassiveCodes.Single(x => x.CatalogId == 814);
            firstCode.CastCode(); secondCode.CastCode();
            Check("동일 오라 중복 합산 없음", 110);
            firstCode.StopCode();
            Check("동일 오라 한쪽 종료 후 유지", 110);
            secondCode.StopCode();
            Check("고정 오라 전부 종료 후 제거", 100);
            float critBefore = actor.StatValue(BaseEnums.StatType.CritRate);
            first.TryGrantRewardCode(816);
            var holy = first.ActivePassiveCodes.Single(x => x.CatalogId == 816);
            holy.CastCode();
            YvCheck("홀리 오라 실제 치명타 캐시", "+10%p", actor.StatValue(BaseEnums.StatType.CritRate).ToString(),
                Mathf.Abs(actor.StatValue(BaseEnums.StatType.CritRate) - critBefore - .10f) < .001f);
            first.Die(enemy);
            YvCheck("홀리 보유자 사망 즉시 치명타 복원", "자기 턴 이전 기초치", actor.StatValue(BaseEnums.StatType.CritRate).ToString(),
                Mathf.Abs(actor.StatValue(BaseEnums.StatType.CritRate) - critBefore) < .001f);
            second.TryGrantRewardCode(816); actor.TryGrantRewardCode(816);
            var secondHoly = second.ActivePassiveCodes.Single(x => x.CatalogId == 816);
            var actorHoly = actor.ActivePassiveCodes.Single(x => x.CatalogId == 816);
            secondHoly.CastCode(); actorHoly.CastCode(); second.Die(enemy);
            YvCheck("홀리 다른 보유자 생존 시 유지", "+10%p 유지", actor.StatValue(BaseEnums.StatType.CritRate).ToString(),
                Mathf.Abs(actor.StatValue(BaseEnums.StatType.CritRate) - critBefore - .10f) < .001f);
            actorHoly.StopCode();
            YvCheck("홀리 마지막 코드 종료 즉시 복원", "기초치", actor.StatValue(BaseEnums.StatType.CritRate).ToString(),
                Mathf.Abs(actor.StatValue(BaseEnums.StatType.CritRate) - critBefore) < .001f);
            var deathListeners = typeof(Unit).GetField("AnyUnitDied", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as System.Delegate;
            int retained = deathListeners?.GetInvocationList().Count(callback => ReferenceEquals(callback.Target, holy) ||
                ReferenceEquals(callback.Target, secondHoly) || ReferenceEquals(callback.Target, actorHoly)) ?? 0;
            YvCheck("홀리 사망·종료 정적 구독 정리", "구독0", retained.ToString(), retained == 0);
            GridManager.Instance.OnRoundEnd();
            YvCheck("라운드 종료 공유 오라 상태 정리", "잔여0", actor.ActiveStatuses.Count.ToString(),
                !actor.ActiveStatuses.Any(x => x.Key == RewardAura.KeyOf("summon_damage") || x.Key == RewardAura.KeyOf("leadership")));
            yield return CaptureStill("core_aura_multi_holder_cleanup"); OsFinish(enabled);
        }
    }
}
#endif
