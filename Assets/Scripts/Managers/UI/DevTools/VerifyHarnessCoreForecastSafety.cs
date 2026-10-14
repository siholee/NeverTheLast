#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using BaseClasses;
using Combat;
using Core;
using Effects.Buffs;
using Entities;
using UnityEngine;
namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCoreForecastSafety(VerifyScenarioRequest config)
        {
            _yvFailures.Clear(); bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            var scheduler = GameManager.Instance.ActionScheduler;
            var actor = AliveAllies().First(); var enemy = AliveEnemies().First();
            scheduler.BeginRound();
            void Compare(string label, int count = 12)
            {
                var expected = scheduler.ForecastOrder(count);
                var actual = scheduler.ForecastOrderCached(count);
                bool same = actual.Count == expected.Count;
                for (int i = 0; same && i < expected.Count; i++)
                    same = actual[i].Unit == expected[i].Unit && actual[i].Kind == expected[i].Kind &&
                        actual[i].StableKey == expected[i].StableKey && actual[i].Label == expected[i].Label &&
                        actual[i].ActionsAhead == expected[i].ActionsAhead && actual[i].IsQueued == expected[i].IsQueued;
                YvCheck(label, "기존 순서·표시와 동일", actual.Count.ToString(), same);
            }
            Compare("초기 행동서열 캐시", 8);
            var first = scheduler.ForecastOrderCached(8);
            YvCheck("입력 불변 예측 재사용", "같은 결과 개체", "참조", ReferenceEquals(first, scheduler.ForecastOrderCached(8)));
            Compare("요청 길이 변경", 3); Compare("빈 서열 요청", 0); Compare("확장 서열 요청");
            scheduler.AdvanceAction(actor, .65f); Compare("행동 앞당김·추월 몫 변경");
            scheduler.DelayAction(actor, .45f); Compare("행동 지연 변경");
            actor.AddStatus(BuffStatus.Create(997960, "qa_forecast_speed", "검증 속도", actor, actor,
                new StatModifierEffect(BaseEnums.StatType.Speed, 35f)));
            Compare("속도 변경 후 미래 서열 갱신");
            actor.RemoveStatusByKey("qa_forecast_speed"); Compare("속도 효과 제거 후 갱신");
            scheduler.EnqueueAdditional(actor, "qa_forecast_add", "검증 추가", () => { });
            scheduler.EnqueueSpecial(actor, "qa_forecast_special", "검증 특수", () => { });
            Compare("예약 추가·종류 우선순위 갱신");
            scheduler.EnqueuePriorityAdditional(enemy, "qa_forecast_priority", "검증 선제", () => { });
            Compare("선제 예약 갱신");
            for (int tick = 0; tick < 20; tick++)
            {
                scheduler.Tick(.1f); Compare("실행·큐 제거·턴 수 갱신 " + tick);
                yield return null;
            }
            var summon = GridManager.Instance.SpawnSummon(actor, new SummonSpec
            {
                Id = 997, Kind = "qa.forecast", Name = "검증 소환수", Hp = 10, Speed = 100,
                NormalCodeId = 500, PortraitKey = actor.PortraitPath
            });
            scheduler.Tick(.001f); Compare("소환 참가자 합류 갱신");
            summon.Die(enemy); Compare("소환 사망 즉시 제외");
            enemy.Die(actor); Compare("적 사망 즉시 제외");
            scheduler.EndRound(); Compare("라운드 종료 캐시 참조 정리");
            scheduler.BeginRound(); Compare("새 라운드 입력 재생성");
            GameManager.Instance.DebugResetBattle();
            yield return CaptureStill("core_forecast_cache_inputs"); OsFinish(enabled);
        }
    }
}
#endif
