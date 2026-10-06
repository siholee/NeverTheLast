#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Effects.Negative;
using Effects.Vfx;
using Entities;
using Entities.Status;
using UnityEngine;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 야마 · 바루나(Detail_08 §15 · §16)의 <b>메커니즘</b> 검증 시나리오 `yama_varuna_core`.
    /// 연출이 아니라 공용 피해 · 원소 · 상태 · 보호막 API의 실제 결과와 스케줄러의 실제 행동 실행을 확인한다.
    /// 치명타가 끼면 안 되는 비교는 치명 없는 정확한 원시 피해 정보(탐침)로만 잰다. 결과는 `[야마바루나검증]` 줄과 mechanics에 남긴다.
    ///
    /// 연출 하네스(Sol 담당)가 같은 장면을 재사용할 수 있게, 장면 준비 · 실제 행동 실행 · 즉시 발동/제방 이벤트 기록은
    /// <see cref="YvPrepareScene"/> · <see cref="YvRunScheduled"/> · <see cref="YvRecorder"/>로 나눠 둔다.
    /// </summary>
    public sealed partial class VerifyHarnessRunner
    {
        private const int YamaId = 61;
        private const int VarunaId = 66;
        private static readonly int[] PartyFiveYamaVarunaIds = { YamaId, VarunaId, IndraId, ChandraId, PygmalionId };

        // 검증 전용 상태 ID. 보상 코드 구간(95000~98999)과 캐릭터 구간(70000번대) 밖이다.
        private const int YvHpBoostId = 99901;
        private const int YvStatId = 99902;
        private const int YvReactionDotId = 99903;
        private const int YvControlNegativeId = 99904;
        private const int YvResistId = 99905;
        private const string YvHpBoostKey = "verify_yv_hp";
        private const float YvTolerance = 0.012f;

        private readonly List<string> _yvFailures = new();

        // ── 연출 하네스가 재사용하는 기록기 ────────────────────────────────

        /// <summary>
        /// 실제 행동 하나 동안의 피해 · 원소 · 보호막 · 즉시 발동 · 제방 이벤트를 모은다. 구독은 <see cref="Dispose"/>가 푼다.
        /// 연출 하네스는 같은 기록기로 "실제로 피해가 들어간 프레임"과 "누적 보호막이 붙은 프레임"을 맞춰 볼 수 있다.
        /// </summary>
        internal sealed class YvRecorder : IDisposable
        {
            public readonly List<DamageContext> Incoming = new();
            public readonly List<(Unit Target, DamageContext Context)> IncomingTargets = new();
            public readonly List<DamageResolvedContext> Dealt = new();
            public readonly List<(Unit Source, Unit Target, BaseEnums.UnitElement Element)> Elements = new();
            public readonly List<(Unit Target, Unit Source, int Amount)> Shields = new();
            public readonly List<(Unit Trigger, Unit Target, BaseEffect Effect, DamageContext Context, int RawDamage)> Detonations = new();
            public readonly List<(Unit Trigger, Unit Target, int Delivered)> DetonationEnds = new();
            public readonly List<(Unit Varuna, int ActionId, int Amount)> Levees = new();
            public readonly List<(Unit Grantor, Unit Target, UnitStatus Status)> Negatives = new();
            public readonly List<(Unit Unit, ActionScheduler.ActionKind Kind, Unit TurnOwner, int ActionId)> Started = new();
            public readonly List<(Unit Unit, ActionScheduler.ActionKind Kind)> Finished = new();
            public readonly List<(Unit Source, Unit Target, string Name)> Reactions = new();

            public YvRecorder()
            {
                Unit.AnyDamageIncoming += OnIncoming;
                Unit.AnyDamageDealt += OnDealt;
                Unit.AnyCombatElementGranted += OnElement;
                Unit.AnyShieldGranted += OnShield;
                Unit.AnyNegativeStatusGranted += OnNegative;
                Unit.AnyElementalReaction += OnReaction;
                DamageOverTimeDetonation.AnyDetonationHit += OnDetonation;
                DamageOverTimeDetonation.AnyDetonationFinished += OnDetonationEnd;
                VarunaLawLevee.AnyLeveeShield += OnLevee;
                ActionScheduler.AnyActionStarted += OnStarted;
                ActionScheduler.AnyActionFinished += OnFinished;
            }

            public void Dispose()
            {
                Unit.AnyDamageIncoming -= OnIncoming;
                Unit.AnyDamageDealt -= OnDealt;
                Unit.AnyCombatElementGranted -= OnElement;
                Unit.AnyShieldGranted -= OnShield;
                Unit.AnyNegativeStatusGranted -= OnNegative;
                Unit.AnyElementalReaction -= OnReaction;
                DamageOverTimeDetonation.AnyDetonationHit -= OnDetonation;
                DamageOverTimeDetonation.AnyDetonationFinished -= OnDetonationEnd;
                VarunaLawLevee.AnyLeveeShield -= OnLevee;
                ActionScheduler.AnyActionStarted -= OnStarted;
                ActionScheduler.AnyActionFinished -= OnFinished;
            }

            private void OnIncoming(Unit target, DamageContext context)
            {
                Incoming.Add(context);
                IncomingTargets.Add((target, context));
            }

            private void OnDealt(DamageResolvedContext context) => Dealt.Add(context);
            private void OnElement(Unit source, Unit target, BaseEnums.UnitElement element) => Elements.Add((source, target, element));
            private void OnShield(Unit target, Unit source, int amount) => Shields.Add((target, source, amount));
            private void OnNegative(Unit grantor, Unit target, UnitStatus status) => Negatives.Add((grantor, target, status));
            private void OnReaction(Unit source, Unit target, string name) => Reactions.Add((source, target, name));
            private void OnLevee(Unit varuna, int actionId, int amount) => Levees.Add((varuna, actionId, amount));
            private void OnFinished(Unit unit, ActionScheduler.ActionKind kind) => Finished.Add((unit, kind));

            private void OnStarted(Unit unit, ActionScheduler.ActionKind kind, string label)
            {
                ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
                Started.Add((unit, kind, scheduler?.TurnOwner, scheduler?.CurrentActionId ?? -1));
            }

            // 원시 한 번분은 발동 직후 같은 효과에게 다시 물어 비교 기준으로 남긴다(상태를 바꾸지 않는 질의다).
            private void OnDetonation(Unit trigger, Unit target, BaseEffect effect, DamageContext context)
                => Detonations.Add((trigger, target, effect, context, effect?.CreateDamageOverTimeTick()?.Damage ?? 0));

            private void OnDetonationEnd(Unit trigger, Unit target, int delivered) => DetonationEnds.Add((trigger, target, delivered));
        }

        // ── 판정 기록 ─────────────────────────────────────────────

        private void YvCheck(string check, string expected, string actual, bool passed)
        {
            Debug.Log($"[야마바루나검증] {(passed ? "통과" : "실패")} — {check}: 기대 {expected}, 실제 {actual}");
            RecordMechanic(check, expected, actual, passed);
            if (!passed) _yvFailures.Add(check);
        }

        private static bool Near(float actual, float expected, float tolerance = YvTolerance)
            => Mathf.Abs(actual - expected) <= tolerance;

        // ── 장면 · 실행 도우미(연출 하네스 재사용) ─────────────────────────

        /// <summary>
        /// 실제 전투를 연 뒤 자동 진행만 멈춘다. 패시브 · 오라 · HUD는 라운드 시작 경로 그대로 살아 있고,
        /// 이후 행동은 <see cref="YvRunScheduled"/>가 실제 스케줄러로 한 건씩 돌린다. 돌려받은 값은 종료 때 복구한다.
        /// </summary>
        internal IEnumerator YvPrepareScene(VerifyScenarioRequest config, Action<bool> wasEnabled)
        {
            yield return PrepareStage(config, CombatStage(config, 6, 6), true, true, false);
            FreezeBattleForManualCapture();
            GameManager game = GameManager.Instance;
            wasEnabled?.Invoke(game.enabled);
            game.enabled = false;
            game.gameState = BaseEnums.GameState.RoundInProgress;
            game.uiManager?.HidePreparationPhasePanel();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            foreach (Unit enemy in AliveEnemies()) YvBoostEnemy(enemy);
            yield return WaitUnscaled(0.4f);
        }

        /// <summary>
        /// 실제 스케줄러를 새 라운드 시계로 열고 <paramref name="setup"/>으로 예약한 뒤, <paramref name="done"/>까지 프레임마다 Tick한다.
        /// 행동 번호 · 턴 주인 · 우선순위 · 이벤트 순서가 실전과 같다. 끝나면 시계를 닫고 남은 시전 표시를 정리한다.
        /// </summary>
        internal IEnumerator YvRunScheduled(Action<ActionScheduler> setup, Func<bool> done, float timeout = 20f)
        {
            ActionScheduler scheduler = GameManager.Instance.ActionScheduler;
            scheduler.BeginRound();
            setup?.Invoke(scheduler);
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done() && Time.realtimeSinceStartup < deadline)
            {
                scheduler.Tick(Time.deltaTime);
                yield return null;
            }
            if (!done()) YvCheck("실제 행동 실행 제한 시간", "완료", "시간 초과", false);
            scheduler.EndRound();
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies()))
            {
                unit.StopAllCoroutines();
                unit.isCasting = false;
            }
            yield return WaitUnscaled(0.2f);
        }

        /// <summary>적이 탐침 · 즉시 발동을 여러 번 맞고도 서 있도록 최대 체력만 크게 올린다(피해 공식은 그대로).</summary>
        private static void YvBoostEnemy(Unit enemy)
        {
            if (enemy == null || !enemy.isActive || enemy.HasStatusKey(YvHpBoostKey)) return;
            UnitStatus boost = BuffStatus.Create(YvHpBoostId, YvHpBoostKey, "검증 체력", null, enemy,
                new StatModifierEffect(BaseEnums.StatType.MaxHp, 200000f), duration: 0);
            boost.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            enemy.AddStatus(boost);
            enemy.RestoreHpOutsideCombat(enemy.HpMax);
        }

        private static UnitStatus YvAddTestStatus(Unit owner, int id, string key, BaseEffect effect,
            BaseEnums.StatusCategory category = BaseEnums.StatusCategory.Positive, bool skipHit = true)
        {
            UnitStatus status = BuffStatus.Create(id, key, "검증 " + key, null, owner, effect, duration: 0,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace, category: category);
            status.SkipEffectHitRoll = skipHit;
            status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            owner.AddStatus(status);
            return status;
        }

        /// <summary>치명 없는 정확한 원시 피해 한 번을 공용 경로로 넣고 실제로 깎인 양을 돌려준다.</summary>
        private static int YvProbe(Unit attacker, Unit target, BaseEnums.CodeType type, int amount = 1000)
        {
            List<int> tags = type == BaseEnums.CodeType.Effect
                ? new List<int>()
                : new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack };
            var context = new DamageContext(attacker, amount, type, tags, false);
            target.TakeDamage(context);
            return context.ResolvedDamage;
        }

        // ── 지속피해 부여(실제 효과 클래스 · 실제 상태 정의와 같은 모양) ────────────────

        private static UnitStatus YvPoison(Unit source, Unit target, int duration = 3)
        {
            var poison = new UnitStatus(1, source, target) { SkipEffectHitRoll = true, Duration = duration };
            poison.AddEffect(new DamageOverTimeEffect(1001, 20f));
            target.AddStatus(poison);
            return target.GetStatuses().LastOrDefault(status => status.StatusId == 1);
        }

        private static UnitStatus YvBurn(Unit source, Unit target)
        {
            ElementalReaction.ApplyBurnWithoutContest(source, target, 3);
            return target.GetStatus(ElementalReaction.BurnStatusId);
        }

        private static UnitStatus YvReactionDot(Unit source, Unit target, int perTurn = 150)
        {
            UnitStatus status = BuffStatus.Create(YvReactionDotId, "verify_yv_reaction_dot", "검증 반응 지속피해",
                source, target, new ReactionDotEffect(perTurn), duration: 3,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration, category: BaseEnums.StatusCategory.Negative);
            status.SkipEffectHitRoll = true;
            target.AddStatus(status);
            return target.GetStatus(YvReactionDotId);
        }

        private static UnitStatus YvWeathering(Unit source, Unit target, int perAction = 140)
        {
            UnitStatus status = BuffStatus.Create(ElementalReaction.WeatheringStatusId, ElementalReaction.WeatheringStatusKey,
                "풍화", source, target, new WeatheringEffect(perAction), duration: 3,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration, category: BaseEnums.StatusCategory.Negative);
            status.SkipEffectHitRoll = true;
            target.AddStatus(status);
            return target.GetStatus(ElementalReaction.WeatheringStatusId);
        }

        /// <summary>반응이 남기는 감전과 같은 ID · 출처별 키 · 효과. 반응 쿨다운 없이 출처를 바꿔 여러 개를 세울 때 쓴다.</summary>
        private static UnitStatus YvShock(Unit source, Unit target, int perTurn = 120, int duration = 0)
        {
            string key = $"reaction_shock_{source.GetEntityId()}";
            UnitStatus status = BuffStatus.Create(ElementalReaction.ShockStatusId, key, "감전", source, target,
                new ReactionDotEffect(perTurn), duration: duration,
                stackPolicy: BaseEnums.StatusStackPolicy.ExtendDuration, category: BaseEnums.StatusCategory.Negative);
            status.SkipEffectHitRoll = true;
            target.AddStatus(status);
            return target.GetStatuses().FirstOrDefault(item => item.Key == key);
        }

        /// <summary>구간 사이에 적을 깨끗한 상태로 되돌린다. 반응 내부 쿨다운이 풀리도록 적의 턴 수만 한 번 넘긴다.</summary>
        private static void YvResetEnemy(Unit enemy)
        {
            if (enemy == null || !enemy.isActive) return;
            enemy.RemoveAllNegativeStatuses();
            enemy.ResetCombatElements();
            enemy.BeginTurn();
            enemy.ClearUltimateResource();
            enemy.RestoreHpOutsideCombat(enemy.HpMax);
        }

        private static PassiveCode YvCode(Unit unit, int catalogId)
            => unit?.ActivePassiveCodes.FirstOrDefault(code => code != null && code.CatalogId == catalogId);

        private static string Tags(DamageContext context)
            => context?.DamageTags == null ? "-" : string.Join(",", context.DamageTags);

        private static readonly int[] DirectAttackTags =
        {
            DamageTag.NormalAttack, DamageTag.UltAttack, DamageTag.AdditionalAttack, DamageTag.CounterAttack,
            DamageTag.PassiveAttack, DamageTag.SpecialAttack, DamageTag.ContactAttack, DamageTag.NonContactAttack,
            DamageTag.Slash, DamageTag.Pierce, DamageTag.Shot, DamageTag.Strike, DamageTag.Spell,
        };

        // ── 시나리오 ──────────────────────────────────────────────

        private IEnumerator ScenarioYamaVarunaCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            bool wasEnabled = true;
            yield return YvPrepareScene(config, value => wasEnabled = value);

            Unit Find(int id) => AliveAllies().FirstOrDefault(unit => unit.ID == id);
            Unit yama = Find(YamaId), varuna = Find(VarunaId), indra = Find(IndraId), chandra = Find(ChandraId),
                pyg = Find(PygmalionId);
            if (yama == null || varuna == null || indra == null || chandra == null || pyg == null)
            {
                FailCurrent("야마 · 바루나 · 인드라 · 찬드라 · 피그말리온 편성을 찾지 못했다. lineup은 party5_yama_varuna여야 한다.");
                yield break;
            }
            List<Unit> enemies = AliveEnemies().OrderByDescending(unit => unit.Priority).ToList();
            if (enemies.Count < 4)
            {
                FailCurrent($"적이 4체 이상 필요하다(현재 {enemies.Count}). stage를 바꿔라.");
                yield break;
            }

            YvDataChecks(yama, varuna);
            YvGrowthAndSlots(yama, varuna);
            yield return YvDamageOverTime(yama, chandra, indra, varuna, pyg, enemies);
            yield return YvRewardCompatibility(yama, chandra, indra, varuna, pyg, enemies);
            yield return YvVerdict(yama, indra, varuna, pyg, AliveEnemies());
            yield return CaptureStill("yama_varuna_verdict");
            yield return YvVaruna(varuna, indra, AliveEnemies());
            yield return YvGate(indra, varuna, yama, chandra);
            yield return YvRoundLifecycle(yama, indra);
            yield return YvDeathGate(yama, indra, varuna, chandra);
            yield return CaptureStill("yama_varuna_core");

            // 다음 시나리오가 깨끗한 판을 받도록 라운드를 정상 경로로 닫고 쓰러진 야마를 세운다.
            GameManager.Instance.ActionScheduler.EndRound();
            GridManager.Instance.OnRoundEnd();
            yama.ReviveAfterBattle();
            GameManager game = GameManager.Instance;
            game.gameState = BaseEnums.GameState.Preparation;
            game.enabled = wasEnabled;

            if (_yvFailures.Count > 0)
                FailCurrent($"{_yvFailures.Count}건 실패: " + string.Join(" / ", _yvFailures));
            _yvFailures.Clear();
        }

        // ── 0. 데이터 · 등록 ─────────────────────────────────────────

        private void YvDataChecks(Unit yama, Unit varuna)
        {
            YvCheck("야마 코드 등록", "P261 · N61 · SP61(+75) · U61",
                $"{yama.ActivePassiveCodes.FirstOrDefault()?.GetType().Name} · {yama.ActiveNormalCode?.GetType().Name} · " +
                $"{yama.ActiveSpecialCode?.GetType().Name}({yama.ActiveSpecialCode?.OpenOrder}) · {yama.ActiveUltimateCode?.GetType().Name}",
                yama.ActivePassiveCodes.FirstOrDefault() is YamaDeathVerdict &&
                yama.ActiveNormalCode is Codes.Normal.YamaRollOfDeathLightning &&
                yama.ActiveSpecialCode is Codes.Special.YamaDeathRound && yama.ActiveSpecialCode.OpenOrder == 75 &&
                yama.ActiveUltimateCode is Codes.Ultimate.YamaLastArrival);
            YvCheck("바루나 코드 등록", "P266 · N66 · SP66(-50) · U66",
                $"{varuna.ActivePassiveCodes.FirstOrDefault()?.GetType().Name} · {varuna.ActiveNormalCode?.GetType().Name} · " +
                $"{varuna.ActiveSpecialCode?.GetType().Name}({varuna.ActiveSpecialCode?.OpenOrder}) · {varuna.ActiveUltimateCode?.GetType().Name}",
                varuna.ActivePassiveCodes.FirstOrDefault() is VarunaLawLevee &&
                varuna.ActiveNormalCode is Codes.Normal.VarunaSacredRope &&
                varuna.ActiveSpecialCode is Codes.Special.VarunaOverflowingJudgment && varuna.ActiveSpecialCode.OpenOrder == -50 &&
                varuna.ActiveUltimateCode is Codes.Ultimate.VarunaUnderSameLaw);
            bool spell = new Codes.Base.Code[]
            {
                yama.ActiveNormalCode, yama.ActiveSpecialCode, yama.ActiveUltimateCode,
                varuna.ActiveNormalCode, varuna.ActiveSpecialCode, varuna.ActiveUltimateCode,
            }.All(code => code != null && code.AttackForm == DamageTag.Spell);
            YvCheck("공격 형태", "여섯 공격 코드 모두 #주문", spell ? "모두 주문" : "주문 아님 있음", spell);
            YvCheck("원소 · 소속 · 마나", "야마 Electro/Lokapala/120 · 바루나 Hydro/Lokapala/100",
                $"{yama.Element}/{yama.HasUnitTag("Lokapala")}/{yama.ManaMax} · {varuna.Element}/{varuna.HasUnitTag("Lokapala")}/{varuna.ManaMax}",
                yama.Element == "Electro" && varuna.Element == "Hydro" && yama.HasUnitTag("Lokapala") &&
                varuna.HasUnitTag("Lokapala") && yama.HasUnitTag("Vedic") && varuna.HasUnitTag("Vedic") &&
                yama.ManaMax == 120 && varuna.ManaMax == 100);
            YvCheck("전투 성향", "야마 지배·특수·방출·신속 / 바루나 지원·방어·부여·특수",
                $"{string.Join(",", yama.ArchetypeTags)} / {string.Join(",", varuna.ArchetypeTags)}",
                new[] { "Domination", "Special", "Burst", "Swift" }.All(yama.HasArchetypeTag) &&
                new[] { "Support", "Shielding", "Infusion", "Special" }.All(varuna.HasArchetypeTag));
        }

        // ── 1. 성장 · q · 상한 · 보상 7칸 ─────────────────────────────

        private void YvGrowthAndSlots(Unit yama, Unit varuna)
        {
            int yamaLevel = yama.Level, varunaLevel = varuna.Level;
            var yamaSpec = new (BaseEnums.StatType Stat, float Base, float Growth)[]
            {
                (BaseEnums.StatType.MaxHp, 900, 80), (BaseEnums.StatType.Attack, 102, 6.8f),
                (BaseEnums.StatType.SpecialAttack, 85, 5.2f), (BaseEnums.StatType.Defense, 56, 4.2f),
                (BaseEnums.StatType.SpecialDefense, 62, 4.6f), (BaseEnums.StatType.Speed, 106, 0),
            };
            var varunaSpec = new (BaseEnums.StatType Stat, float Base, float Growth)[]
            {
                (BaseEnums.StatType.MaxHp, 1120, 100), (BaseEnums.StatType.Attack, 82, 5.4f),
                (BaseEnums.StatType.SpecialAttack, 86, 5.2f), (BaseEnums.StatType.Defense, 90, 6.8f),
                (BaseEnums.StatType.SpecialDefense, 78, 5.7f), (BaseEnums.StatType.Speed, 102, 0),
            };
            foreach (int level in new[] { 1, 20, 500 })
            {
                yama.DebugSetLevel(level);
                varuna.DebugSetLevel(level);
                string Mismatch(Unit unit, (BaseEnums.StatType Stat, float Base, float Growth)[] spec)
                    => string.Join(",", spec.Where(row => !Near(unit.BaseStat(row.Stat), row.Base + row.Growth * (level - 1), 0.05f))
                        .Select(row => $"{row.Stat}={unit.BaseStat(row.Stat):0.##}"));
                string yamaBad = Mismatch(yama, yamaSpec), varunaBad = Mismatch(varuna, varunaSpec);
                bool crit = Near(yama.BaseStat(BaseEnums.StatType.CritRate), 0.05f, 0.0001f) &&
                            Near(yama.BaseStat(BaseEnums.StatType.CritDamage), 0.50f, 0.0001f) &&
                            Near(varuna.BaseStat(BaseEnums.StatType.CritRate), 0.05f, 0.0001f);
                YvCheck($"Lv.{level} 선형 성장", "Detail_08 §15.1 · §16.1 표",
                    $"야마 [{(yamaBad == "" ? "일치" : yamaBad)}] · 바루나 [{(varunaBad == "" ? "일치" : varunaBad)}] · 치명 {(crit ? "5/50" : "불일치")}",
                    yamaBad == "" && varunaBad == "" && crit);

                float q = YamaDeathVerdict.NormalRatio(yama), u = YamaDeathVerdict.UltimateRatio(yama);
                YvCheck($"Lv.{level} 기본 q", "q 48.7~49.2% · U 73.1~73.8% = min(1, 1.5q)",
                    $"q {q:P2} · U {u:P2}", q >= 0.486f && q <= 0.4925f && Near(u, Mathf.Min(1f, 1.5f * q), 0.0001f) &&
                                            u >= 0.7295f && u <= 0.739f);
            }

            // 보상 7칸 예(505/605/506/606/710/811/906)와 710 활성 구간(+속도 15) — 문서 §15.5의 70.8~73.1% · U 상한 100%
            int[] seven = { 505, 605, 506, 606, 710, 811, 906 };
            yama.DebugSetLevel(20);
            foreach (int id in yama.RewardCodeIds.ToList()) yama.RemoveRewardCode(id);
            bool allImplemented = seven.All(id => Codes.Passive.Reward.RewardCodeFactory.IsImplemented(id) &&
                                                  Codes.Base.RewardCodeCatalog.Find(id) != null);
            bool granted = seven.All(id => yama.TryGrantRewardCode(id));
            YvCheck("보상 7칸 장착", "7종 모두 실존 · 구현 · 장착", $"구현 {allImplemented} · 장착 {granted} · 칸 {yama.RewardCodeIds.Count}",
                allImplemented && granted && yama.RewardCodeIds.Count == Unit.RewardCodeSlotCount);
            yama.CanReceiveRewardCode(503, out _, out bool needsOverwrite, out _);
            float beforeDefense = yama.StatValue(BaseEnums.StatType.Defense);
            bool stored = yama.TryGrantRewardCode(503);
            YvCheck("8번째 코드", "활성7 유지 · 503 보관 · 방어력/패시브 미적용", $"보관 {stored}, needsOverwrite {needsOverwrite}",
                stored && !needsOverwrite && yama.RewardCodeIds.Count == Unit.RewardCodeSlotCount &&
                yama.StoredRewardCodeIds.Contains(503) && Near(beforeDefense, yama.StatValue(BaseEnums.StatType.Defense)) &&
                !yama.ActivePassiveCodes.Any(code => code.CatalogId == 503));
            yama.RemoveRewardCode(503);
            YvCheck("상위 보유 시 하위 거부", "811 보유 중 617 거부", yama.TryGrantRewardCode(617) ? "받음" : "거부",
                !yama.RewardCodeIds.Contains(617));
            UnitStatus spellAcceleration = YvAddTestStatus(yama, YvStatId, "yv_710_speed",
                new StatModifierEffect(BaseEnums.StatType.Speed, 15f));
            foreach (int level in new[] { 1, 20, 500 })
            {
                yama.DebugSetLevel(level);
                float q = YamaDeathVerdict.NormalRatio(yama), u = YamaDeathVerdict.UltimateRatio(yama);
                YvCheck($"Lv.{level} 7칸 · 710 활성 q", "q 70.8~73.1% · U 상한 100%", $"q {q:P2} · U {u:P2}",
                    q >= 0.705f && q <= 0.734f && Near(u, 1f, 0.0001f));
            }
            yama.RemoveStatusByKey(spellAcceleration.Key);
            foreach (int id in seven) yama.RemoveRewardCode(id);
            YvCheck("보상 코드 정리", "0칸", $"{yama.RewardCodeIds.Count}칸", yama.RewardCodeIds.Count == 0);

            // q 상한 — 특공 · 속도를 크게 올려도 1을 넘지 않는다
            UnitStatus pump = YvAddTestStatus(yama, YvStatId, "yv_q_cap_spatk",
                new StatModifierEffect(BaseEnums.StatType.SpecialAttack, 100000f));
            UnitStatus pumpSpeed = YvAddTestStatus(yama, YvStatId + 100, "yv_q_cap_speed",
                new StatModifierEffect(BaseEnums.StatType.Speed, 500f));
            YvCheck("q 상한", "q = U = 1", $"q {YamaDeathVerdict.NormalRatio(yama):0.####} · U {YamaDeathVerdict.UltimateRatio(yama):0.####}",
                Near(YamaDeathVerdict.NormalRatio(yama), 1f, 0.0001f) && Near(YamaDeathVerdict.UltimateRatio(yama), 1f, 0.0001f));
            yama.RemoveStatusByKey(pump.Key);
            yama.RemoveStatusByKey(pumpSpeed.Key);

            yama.DebugSetLevel(yamaLevel);
            varuna.DebugSetLevel(varunaLevel);
        }

        // ── 2. 지속피해 즉시 발동 ────────────────────────────────────

        private IEnumerator YvDamageOverTime(Unit yama, Unit chandra, Unit indra, Unit varuna, Unit pyg, List<Unit> enemies)
        {
            Unit target = enemies[0];
            YvResetEnemy(target);
            UnitStatus poison = YvPoison(pyg, target);
            UnitStatus burn = YvBurn(chandra, target);
            UnitStatus reactionDot = YvReactionDot(indra, target);
            UnitStatus weathering = YvWeathering(varuna, target);
            var statuses = new[] { poison, burn, reactionDot, weathering };
            YvCheck("감전 없는 네 지속피해", "독 · 최대 체력 비율 화상 · 반응 지속피해 · 풍화, 감전 0",
                $"{statuses.Count(status => status != null)}종 · 감전 {YamaDeathVerdict.IsShocked(target)}",
                statuses.All(status => status != null) && !YamaDeathVerdict.IsShocked(target));
            if (statuses.Any(status => status == null)) yield break;

            // 자연 발동 기준 — 효과 자신의 경로(풍화는 행동 시작 이벤트)로 한 번씩 낸다. 경과 턴은 상태가 세므로 그대로다.
            var natural = new Dictionary<BaseEffect, DamageContext>();
            var created = new Dictionary<BaseEffect, DamageContext>();
            foreach (UnitStatus status in statuses)
            {
                BaseEffect effect = status.Effects[0].EffectObject;
                created[effect] = effect.CreateDamageOverTimeTick();
                using var recorder = new YvRecorder();
                if (effect is WeatheringEffect)
                    target.Invoke(BaseEnums.UnitEventType.OnNormalActivates, new EventContext(target));
                else
                    effect.OnOwnerTurn();
                natural[effect] = recorder.IncomingTargets.Where(item => item.Target == target)
                    .Select(item => item.Context).FirstOrDefault(context => context.CodeType == BaseEnums.CodeType.Effect);
            }
            bool sameContext = statuses.All(status =>
            {
                BaseEffect effect = status.Effects[0].EffectObject;
                DamageContext a = natural[effect], b = created[effect];
                return a != null && b != null && a.Attacker == b.Attacker && a.Damage == b.Damage &&
                       a.CodeType == b.CodeType && a.DamageTags.SequenceEqual(b.DamageTags);
            });
            YvCheck("자연 발동과 공용 원시 컨텍스트", "네 효과 모두 시전자 · 피해 · 종류 · 태그 동일",
                string.Join(" / ", statuses.Select(status => natural[status.Effects[0].EffectObject] is { } c
                    ? $"{status.StatusName}:{c.Attacker?.UnitName}/{c.Damage}/{c.CodeType}/[{Tags(c)}]" : $"{status.StatusName}:없음")),
                sameContext);

            var before = statuses.ToDictionary(status => status, status => (status.Duration, status.ElapsedTurns, status.RemainingTurns));
            int countBefore = target.GetStatuses().Count;
            float q = YamaDeathVerdict.NormalRatio(yama);
            List<(Unit Trigger, Unit Target, BaseEffect Effect, DamageContext Context, int RawDamage)> hits;
            int delivered;
            using (var recorder = new YvRecorder())
            {
                delivered = DamageOverTimeDetonation.Detonate(target, q, yama);
                hits = recorder.Detonations.ToList();
            }
            YvCheck("즉시 발동 횟수", "네 지속피해 각 1회", $"{delivered}회", delivered == 4 && hits.Count == 4);
            bool preserved = hits.All(hit =>
            {
                natural.TryGetValue(hit.Effect, out DamageContext nat);
                return nat != null && hit.Context.Attacker == nat.Attacker && hit.Context.CodeType == BaseEnums.CodeType.Effect &&
                       hit.Context.DamageTags.SequenceEqual(nat.DamageTags) && !hit.Context.IsCrit &&
                       hit.Context.Damage == Mathf.RoundToInt(hit.RawDamage * q);
            });
            YvCheck("출처 · 종류 · 태그 보존, 금액 = 원시 × q", "시전자 그대로 · Effect · 태그 동일 · 치명 없음",
                string.Join(" / ", hits.Select(hit => $"{hit.Context.Attacker?.UnitName} {hit.RawDamage}×{q:0.###}={hit.Context.Damage} [{Tags(hit.Context)}]")),
                preserved);
            bool noDirectTags = hits.All(hit => !hit.Context.DamageTags.Any(tag => DirectAttackTags.Contains(tag)));
            YvCheck("즉시 발동에 직접 공격 태그 없음", "일반 · 궁극기 · 특수 · 접촉 · 주문 태그 없음",
                noDirectTags ? "없음" : "붙음", noDirectTags);
            bool resolvedRatio = hits.All(hit =>
            {
                natural.TryGetValue(hit.Effect, out DamageContext nat);
                return nat != null && Mathf.Abs(hit.Context.ResolvedDamage - nat.ResolvedDamage * q) <= 2f;
            });
            YvCheck("경감 · 주는 피해 보정 한 번", "실제 피해 ≈ 자연 발동 실제 피해 × q (±2)",
                string.Join(" / ", hits.Select(hit => $"{(natural.TryGetValue(hit.Effect, out DamageContext nat) ? nat?.ResolvedDamage : null)}→{hit.Context.ResolvedDamage}")),
                resolvedRatio);
            bool unchanged = statuses.All(status => target.GetStatuses().Contains(status) &&
                                                    before[status] == (status.Duration, status.ElapsedTurns, status.RemainingTurns));
            YvCheck("지속 · 경과 · 중첩 불변", "네 상태의 Duration · ElapsedTurns · 남은 턴 · 개수 그대로",
                $"상태 {countBefore}→{target.GetStatuses().Count} · {(unchanged ? "불변" : "변함")}",
                unchanged && countBefore == target.GetStatuses().Count);

            // 특공 · 속도가 q를 움직이고, 즉시 발동량이 그 q를 그대로 따른다
            UnitStatus spatk = YvAddTestStatus(yama, YvStatId, "yv_q_spatk", new StatModifierEffect(BaseEnums.StatType.SpecialAttack, 200f));
            float qSpAtk = YamaDeathVerdict.NormalRatio(yama);
            UnitStatus speed = YvAddTestStatus(yama, YvStatId + 100, "yv_q_speed", new StatModifierEffect(BaseEnums.StatType.Speed, 30f));
            float qSpeed = YamaDeathVerdict.NormalRatio(yama);
            List<(Unit Trigger, Unit Target, BaseEffect Effect, DamageContext Context, int RawDamage)> raised;
            using (var recorder = new YvRecorder())
            {
                DamageOverTimeDetonation.Detonate(target, qSpeed, yama);
                raised = recorder.Detonations.ToList();
            }
            yama.RemoveStatusByKey(spatk.Key);
            yama.RemoveStatusByKey(speed.Key);
            YvCheck("특공 · 속도와 q", "q < q(특공+200) < q(특공+200, 속도+30), 발동량이 새 q를 따른다",
                $"{q:0.####} → {qSpAtk:0.####} → {qSpeed:0.####}",
                qSpAtk > q && qSpeed > qSpAtk && raised.Count == 4 &&
                raised.All(hit => hit.Context.Damage == Mathf.RoundToInt(hit.RawDamage * qSpeed)));

            // 실제 일반행동 — 피해 → 번개 부착 → 그 대상의 모든 지속피해를 q로
            target.ResetCombatElements();
            yama.currentNormalTarget = target;
            using (var recorder = new YvRecorder())
            {
                float expectedQ = YamaDeathVerdict.NormalRatio(yama);
                yama.CastNormalCode();
                yield return WaitForCasting(yama, 8f);
                DamageContext direct = recorder.IncomingTargets.Where(item => item.Context.Attacker == yama &&
                    item.Context.CodeType == BaseEnums.CodeType.Normal).Select(item => item.Context).FirstOrDefault();
                Unit hitTarget = recorder.IncomingTargets.FirstOrDefault(item => item.Context == direct).Target;
                var mine = recorder.Detonations.Where(hit => hit.Trigger == yama).ToList();
                int electro = recorder.Elements.Count(item => item.Source == yama && item.Element == BaseEnums.UnitElement.Electro);
                YvCheck("명부의 낙뢰 실제 행동", "단일 · 일반 · 비접촉 · 주문 직접 피해 → 번개 1회 → 그 대상 지속피해 4회(q)",
                    $"[{Tags(direct)}] → 번개 {electro} → 발동 {mine.Count}회, 대상 {hitTarget?.UnitName}",
                    direct != null && direct.HasTag(DamageTag.SingleTarget) && direct.HasTag(DamageTag.NormalAttack) &&
                    direct.HasTag(DamageTag.NonContactAttack) && direct.HasTag(DamageTag.Spell) && electro == 1 &&
                    hitTarget == target && mine.Count == 4 && mine.All(hit => hit.Target == target &&
                        hit.Context.Damage == Mathf.RoundToInt(hit.RawDamage * expectedQ)));
            }

            // 실제 특수행동 — 400% 직접 피해만, 번개 · 즉시 발동 없음, 궁극기 판정 없음
            using (var recorder = new YvRecorder())
            {
                yama.CastSpecialCode();
                yield return WaitForCasting(yama, 8f);
                var special = recorder.Incoming.Where(context => context.Attacker == yama && context.CodeType == BaseEnums.CodeType.Special).ToList();
                bool tagsOk = special.Count > 0 && special.All(context => context.HasTag(DamageTag.SpecialAttack) &&
                    context.HasTag(DamageTag.AllTarget) && context.HasTag(DamageTag.Spell) && !context.HasTag(DamageTag.UltAttack));
                int electro = recorder.Elements.Count(item => item.Source == yama);
                int detonations = recorder.Detonations.Count(hit => hit.Trigger == yama);
                YvCheck("죽음의 순회 실제 행동", "적 전체 #특수 #주문, #궁극기 없음, 번개 0, 즉시 발동 0",
                    $"{special.Count}대상 [{Tags(special.FirstOrDefault())}] · 번개 {electro} · 발동 {detonations}",
                    tagsOk && special.Count == AliveEnemies().Count && electro == 0 && detonations == 0);
            }

            // 실제 초월기 — 대상별 피해 → 번개 → 지속피해 min(1, 1.5q)
            foreach (Unit enemy in AliveEnemies())
            {
                enemy.ResetCombatElements();
                if (enemy != target) YvPoison(pyg, enemy);
            }
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            float expectedU = YamaDeathVerdict.UltimateRatio(yama);
            using (var recorder = new YvRecorder())
            {
                yield return YvRunScheduled(_ => yama.FillUltimateResource(),
                    () => recorder.Finished.Any(item => item.Unit == yama && item.Kind == ActionScheduler.ActionKind.Ultimate));
                var ult = recorder.Incoming.Where(context => context.Attacker == yama && context.CodeType == BaseEnums.CodeType.Ultimate).ToList();
                int electro = recorder.Elements.Count(item => item.Source == yama && item.Element == BaseEnums.UnitElement.Electro);
                var mine = recorder.Detonations.Where(hit => hit.Trigger == yama).ToList();
                int targets = recorder.DetonationEnds.Count(item => item.Trigger == yama);
                YvCheck("언젠가 다다를 마지막 실제 행동", "적 전체 #궁극기 #주문 → 대상별 번개 → 지속피해 min(1, 1.5q)",
                    $"직접 {ult.Count} [{Tags(ult.FirstOrDefault())}] · 번개 {electro} · 발동 대상 {targets} · 발동 {mine.Count}회 · U {expectedU:0.####}",
                    ult.Count >= 4 && ult.All(context => context.HasTag(DamageTag.UltAttack) && context.HasTag(DamageTag.AllTarget) &&
                                                         context.HasTag(DamageTag.Spell)) &&
                    electro == ult.Count && targets == ult.Count && mine.Count >= ult.Count &&
                    mine.All(hit => hit.Context.Damage == Mathf.RoundToInt(hit.RawDamage * expectedU)));
            }
        }

        // ── 3. 보상 · 정화 · 정산 · 사망 호환 ───────────────────────────

        private IEnumerator YvRewardCompatibility(Unit yama, Unit chandra, Unit indra, Unit varuna, Unit pyg, List<Unit> enemies)
        {
            Unit target = enemies[0];
            YvResetEnemy(target);
            UnitStatus poison = YvPoison(pyg, target);
            UnitStatus reactionDot = YvReactionDot(indra, target);
            BaseEffect poisonFx = poison.Effects[0].EffectObject, reactionFx = reactionDot.Effects[0].EffectObject;
            float q = YamaDeathVerdict.NormalRatio(yama);

            (int Natural, int Detonated) Measure(BaseEffect effect)
            {
                DamageContext raw = effect.CreateDamageOverTimeTick();
                target.TakeDamage(raw);
                int detonated = 0;
                void OnHit(Unit trigger, Unit unit, BaseEffect source, DamageContext context)
                {
                    if (source == effect) detonated = context.ResolvedDamage;
                }
                DamageOverTimeDetonation.AnyDetonationHit += OnHit;
                DamageOverTimeDetonation.Detonate(target, q, yama);
                DamageOverTimeDetonation.AnyDetonationHit -= OnHit;
                return (raw.ResolvedDamage, detonated);
            }

            var basePoison = Measure(poisonFx);
            var baseReaction = Measure(reactionFx);

            // 811 — 원 시전자(피그말리온)의 주는 피해 보정이 자연 · 즉시 발동에 각각 한 번
            pyg.TryGrantRewardCode(811);
            YvCode(pyg, 811)?.CastCode();
            var pactPoison = Measure(poisonFx);
            YvCheck("811 한 번 적용", "자연 ×1.35 · 즉시 발동/자연 = q",
                $"자연 {basePoison.Natural}→{pactPoison.Natural} ({pactPoison.Natural / (float)Mathf.Max(1, basePoison.Natural):0.###}) · 발동/자연 {pactPoison.Detonated / (float)Mathf.Max(1, pactPoison.Natural):0.###} (q {q:0.###})",
                // 원 피해와 보정 피해가 각각 정수화되므로 작은 피해의 비율 오차 대신 최대 2 피해의 반올림 오차를 허용한다.
                Mathf.Abs(pactPoison.Natural - basePoison.Natural * 1.35f) <= 2f &&
                Mathf.Abs(pactPoison.Detonated - pactPoison.Natural * q) <= 2f);
            pyg.RemoveRewardCode(811);

            // 903 — 반응 태그 피해에만, 한 번
            chandra.TryGrantRewardCode(903);
            YvCode(chandra, 903)?.CastCode();
            var oceanReaction = Measure(reactionFx);
            var oceanPoison = Measure(poisonFx);
            YvCheck("903 반응 태그에만 한 번", "반응 자연 ×1.45 · 독 ×1 · 즉시 발동/자연 = q",
                $"반응 {baseReaction.Natural}→{oceanReaction.Natural} · 독 {basePoison.Natural}→{oceanPoison.Natural} · 반응 발동/자연 {oceanReaction.Detonated / (float)Mathf.Max(1, oceanReaction.Natural):0.###}",
                Mathf.Abs(oceanReaction.Natural - baseReaction.Natural * 1.45f) <= 2f &&
                Mathf.Abs(oceanPoison.Natural - basePoison.Natural) <= 1 &&
                Mathf.Abs(oceanReaction.Detonated - oceanReaction.Natural * q) <= 2f);
            chandra.RemoveRewardCode(903);

            // 야마의 공격력 · 치명 · 주는 피해 보너스는 남의 지속피해에 실리지 않는다
            UnitStatus atk = YvAddTestStatus(yama, YvStatId, "yv_yama_atk", new StatModifierEffect(BaseEnums.StatType.Attack, 10000f));
            UnitStatus crit = YvAddTestStatus(yama, YvStatId + 100, "yv_yama_crit", new StatModifierEffect(BaseEnums.StatType.CritRate, 1f));
            UnitStatus outgoing = YvAddTestStatus(yama, YvStatId + 200, "yv_yama_out", new OutgoingDamageMultiplierEffect(3f));
            var boosted = Measure(poisonFx);
            yama.RemoveStatusByKey(atk.Key);
            yama.RemoveStatusByKey(crit.Key);
            yama.RemoveStatusByKey(outgoing.Key);
            YvCheck("야마 보정이 남의 지속피해에 없음", "야마 공격력 +10000 · 치확 100% · 주는 피해 ×3에도 발동량 동일",
                $"{basePoison.Detonated} → {boosted.Detonated}", Mathf.Abs(boosted.Detonated - basePoison.Detonated) <= 1);

            // 사바흐 조건 — 실제 피해 이벤트가 Effect · 피해 > 0 · 대상에 지속피해 보유, 0 피해 이벤트 없음
            using (var recorder = new YvRecorder())
            {
                DamageOverTimeDetonation.Detonate(target, q, yama);
                var dealt = recorder.Dealt.Where(item => item.Target == target).ToList();
                YvCheck("사바흐 충전 조건 호환", "즉시 발동 피해 이벤트 = Effect · 피해 > 0 · 지속피해 보유 대상",
                    $"{dealt.Count}건 · {string.Join(",", dealt.Select(item => item.DamageDealt))}",
                    dealt.Count == 2 && dealt.All(item => item.DamageContext.CodeType == BaseEnums.CodeType.Effect &&
                                                          item.DamageDealt > 0 && item.Target.HasDamageOverTimeStatus()));
            }

            // 정화 · 만료 뒤에는 발동할 것이 없다
            target.RemoveAllNegativeStatuses();
            using (var recorder = new YvRecorder())
            {
                int delivered = DamageOverTimeDetonation.Detonate(target, 1f, yama);
                YvCheck("정화 뒤 즉시 발동 0", "0회 · 피해 0", $"{delivered}회 · 피해 이벤트 {recorder.Dealt.Count}",
                    delivered == 0 && recorder.Dealt.Count == 0);
            }
            UnitStatus shortPoison = YvPoison(pyg, target, 1);
            target.BeginTurn();
            using (var recorder = new YvRecorder())
            {
                int delivered = DamageOverTimeDetonation.Detonate(target, 1f, yama);
                YvCheck("만료 뒤 즉시 발동 0", "1턴 독이 만료된 뒤 0회", $"독 {(target.GetStatuses().Contains(shortPoison) ? "남음" : "만료")} · {delivered}회",
                    !target.GetStatuses().Contains(shortPoison) && delivered == 0 && recorder.Detonations.Count == 0);
            }

            // 813 — 지속피해 중 처치되면 기존 사망 훅에서 한 번 나눠 준다. 즉시 발동이 끼지 않는다.
            Unit victim = enemies[2];
            YvResetEnemy(victim);
            YvPoison(pyg, victim);
            YvBurn(chandra, victim);
            pyg.TryGrantRewardCode(813);
            YvCode(pyg, 813)?.CastCode();
            using (var recorder = new YvRecorder())
            {
                victim.TakeDamage(new DamageContext(pyg, 50000000, BaseEnums.CodeType.Normal,
                    new List<int> { DamageTag.SingleTarget, DamageTag.TrueDamage }));
                List<Unit> survivors = AliveEnemies();
                var shares = recorder.Dealt.Where(item => item.Attacker == pyg && item.Target != victim &&
                    item.DamageContext.CodeType == BaseEnums.CodeType.Effect && item.DamageContext.DamageTags.Count == 0).ToList();
                YvCheck("813 월식 정산 한 번", "생존 적마다 Effect 피해 1회 · 즉시 발동 0",
                    $"처치 {!victim.isActive} · 정산 {shares.Count}/{survivors.Count} · 발동 {recorder.Detonations.Count}",
                    !victim.isActive && shares.Count == survivors.Count &&
                    shares.Select(item => item.Target).Distinct().Count() == survivors.Count && recorder.Detonations.Count == 0);
            }
            pyg.RemoveRewardCode(813);

            // 대상이 쓰러지면 남은 발동을 멈춘다
            Unit dying = enemies[3];
            YvResetEnemy(dying);
            YvPoison(pyg, dying);
            YvBurn(chandra, dying);
            YvReactionDot(indra, dying);
            dying.ModifyHp(1);
            using (var recorder = new YvRecorder())
            {
                int delivered = DamageOverTimeDetonation.Detonate(dying, 1f, yama);
                YvCheck("죽은 대상 발동 중지", "첫 발동에서 쓰러지고 남은 2개는 멈춘다",
                    $"{delivered}회 · 생존 {dying.isActive}", delivered == 1 && !dying.isActive && recorder.Detonations.Count == 1);
            }
            yield return WaitUnscaled(0.3f);
        }

        // ── 4. 죽음의 선고 ───────────────────────────────────────────

        private IEnumerator YvVerdict(Unit yama, Unit indra, Unit varuna, Unit pyg, List<Unit> enemies)
        {
            Unit target = enemies.First();
            Unit other = enemies.First(unit => unit != target);
            YvResetEnemy(target);
            YvResetEnemy(other);

            int baseDirect = YvProbe(pyg, target, BaseEnums.CodeType.Normal);
            int baseEffect = YvProbe(pyg, target, BaseEnums.CodeType.Effect);
            int baseYama = YvProbe(yama, target, BaseEnums.CodeType.Normal);
            int baseEnemy = YvProbe(other, target, BaseEnums.CodeType.Normal);
            YvCheck("감전 0 — 선고 없음", "선고 0개 · 피해 배율 1", $"선고 {YamaDeathVerdict.HasVerdict(target)}",
                !YamaDeathVerdict.HasVerdict(target));

            // 감전 1 — 실제 반응(바루나 물 + 인드라 번개). 명중이 흔들리지 않게 인드라 특공만 잠시 올린다.
            UnitStatus sure = YvAddTestStatus(indra, YvStatId, "yv_indra_hit", new StatModifierEffect(BaseEnums.StatType.SpecialAttack, 100000f));
            List<(Unit Grantor, Unit Target, UnitStatus Status)> negatives;
            using (var recorder = new YvRecorder())
            {
                target.GrantCombatElement(BaseEnums.UnitElement.Hydro, source: varuna);
                target.GrantCombatElement(BaseEnums.UnitElement.Electro, source: indra);
                negatives = recorder.Negatives.ToList();
                YvCheck("실제 감전 반응", "물 + 번개 → 감전(인드라 출처)", $"반응 [{string.Join(",", recorder.Reactions.Select(item => item.Name))}] · 감전 {YamaDeathVerdict.IsShocked(target)}",
                    YamaDeathVerdict.IsShocked(target));
                YvCheck("바루나 행동 밖 부착은 제방 없음", "스케줄러 행동이 아닌 부착 → 제방 0", $"{recorder.Levees.Count}",
                    recorder.Levees.Count == 0);
            }
            indra.RemoveStatusByKey(sure.Key);
            UnitStatus verdict = target.GetStatuses().FirstOrDefault(status => status.Key == YamaDeathVerdict.MarkKey);
            YvCheck("선고 1개 · 해로운 상태", "Negative · 디버프로 셈 · 명중/저항 건너뜀",
                verdict == null ? "없음" : $"{verdict.Category} · 디버프 {verdict.CountsAsDebuff} · 명중 생략 {verdict.SkipEffectHitRoll} · 저항 생략 {verdict.SkipStatusResistanceRoll}",
                verdict != null && verdict.Category == BaseEnums.StatusCategory.Negative && verdict.CountsAsDebuff &&
                verdict.SkipEffectHitRoll && verdict.SkipStatusResistanceRoll);
            var verdictEvents = negatives.Where(item => item.Status?.Key == YamaDeathVerdict.MarkKey).ToList();
            YvCheck("NegativeStatusGranted 실제 발생", "선고 부여 1회(부여자 야마)",
                $"{verdictEvents.Count}회 · {verdictEvents.FirstOrDefault().Grantor?.UnitName}",
                verdictEvents.Count == 1 && verdictEvents[0].Grantor == yama && verdictEvents[0].Target == target);

            float Ratio(int now, int baseline) => now / (float)Mathf.Max(1, baseline);
            int direct = YvProbe(pyg, target, BaseEnums.CodeType.Normal);
            int effect = YvProbe(pyg, target, BaseEnums.CodeType.Effect);
            int own = YvProbe(yama, target, BaseEnums.CodeType.Normal);
            int enemy = YvProbe(other, target, BaseEnums.CodeType.Normal);
            YvCheck("선고 +20% — 아군 직접 · Effect · 야마, 적 진영 공격은 제외",
                "×1.2 · ×1.2 · ×1.2 · ×1.0",
                $"×{Ratio(direct, baseDirect):0.###} · ×{Ratio(effect, baseEffect):0.###} · ×{Ratio(own, baseYama):0.###} · ×{Ratio(enemy, baseEnemy):0.###}",
                Near(Ratio(direct, baseDirect), 1.2f) && Near(Ratio(effect, baseEffect), 1.2f) &&
                Near(Ratio(own, baseYama), 1.2f) && Near(Ratio(enemy, baseEnemy), 1f));

            // 사바흐 — 실제 빈틈 포착 효과가 선고를 디버프 하나로 센다
            var sabah = new SabahDebuffDamageEffect { Target = pyg };
            int debuffs = target.GetAllStatuses().Count(status => status.CountsAsDebuff);
            float sabahMultiplier = sabah.OutgoingDamageModifier(pyg, target, null);
            YvCheck("사바흐 빈틈 포착 호환", "감전 + 선고 = 디버프 2 → ×1.08",
                $"디버프 {debuffs} → ×{sabahMultiplier:0.###}", debuffs == 2 && Near(sabahMultiplier, 1.08f, 0.0001f));

            // 출처가 다른 감전 두 개에도 선고는 하나, 곱하지 않는다
            UnitStatus second;
            using (var recorder = new YvRecorder())
            {
                second = YvShock(yama, target);
                int verdictAdds = recorder.Negatives.Count(item => item.Status?.Key == YamaDeathVerdict.MarkKey);
                int shocks = target.GetAllStatuses(ElementalReaction.ShockStatusId).Count;
                int verdicts = target.GetAllStatuses(YamaDeathVerdict.MarkStatusId).Count;
                int stacked = YvProbe(pyg, target, BaseEnums.CodeType.Normal);
                YvCheck("출처 다른 감전 2개 — 선고 1개", "감전 2 · 선고 1 · 새 선고 이벤트 0 · ×1.2(×1.44 아님)",
                    $"감전 {shocks} · 선고 {verdicts} · 이벤트 {verdictAdds} · ×{Ratio(stacked, baseDirect):0.###}",
                    shocks == 2 && verdicts == 1 && verdictAdds == 0 && Near(Ratio(stacked, baseDirect), 1.2f));
            }
            // 같은 출처의 감전은 키가 같아 새로 쌓이지 않고 지속만 늘어난다(출처별 중복 검사)
            YvShock(yama, target);
            YvCheck("같은 출처 감전 재부여", "출처별 키 — 감전 2 그대로 · 선고 1",
                $"감전 {target.GetAllStatuses(ElementalReaction.ShockStatusId).Count} · 선고 {target.GetAllStatuses(YamaDeathVerdict.MarkStatusId).Count}",
                target.GetAllStatuses(ElementalReaction.ShockStatusId).Count == 2 &&
                target.GetAllStatuses(YamaDeathVerdict.MarkStatusId).Count == 1);

            // 원본 감전이 남아 있으면 선고만 지워도 다시 붙는다 — 명중 0에 가깝고 저항 100%여도
            UnitStatus wall = YvAddTestStatus(target, YvStatId, "yv_spdef_wall", new StatModifierEffect(BaseEnums.StatType.SpecialDefense, 1e9f));
            UnitStatus resist = YvAddTestStatus(target, YvResistId, "yv_resist_all", new YvResistAllEffect());
            UnitStatus probeHit = BuffStatus.Create(YvControlNegativeId, "yv_control_hit", "검증 대조 해로운 상태", pyg, target,
                new MarkerBuffEffect(), duration: 0, category: BaseEnums.StatusCategory.Negative);
            target.AddStatus(probeHit);
            UnitStatus probeResist = BuffStatus.Create(YvControlNegativeId + 1, "yv_control_resist", "검증 대조 해로운 상태", pyg, target,
                new MarkerBuffEffect(), duration: 0, category: BaseEnums.StatusCategory.Negative);
            probeResist.SkipEffectHitRoll = true;
            target.AddStatus(probeResist);
            bool gatesWork = !target.HasStatusKey("yv_control_hit") && !target.HasStatusKey("yv_control_resist");
            using (var recorder = new YvRecorder())
            {
                target.RemoveStatusByKey(YamaDeathVerdict.MarkKey);
                int reAdds = recorder.Negatives.Count(item => item.Status?.Key == YamaDeathVerdict.MarkKey);
                YvCheck("선고만 제거 → 감전이 있어 복구", "대조 상태는 명중/저항에 막히고, 선고는 즉시 다시 붙는다(명중 판정 0)",
                    $"대조 차단 {gatesWork} · 선고 {YamaDeathVerdict.HasVerdict(target)} · 재부여 이벤트 {reAdds}",
                    gatesWork && YamaDeathVerdict.HasVerdict(target) && reAdds == 1);
            }
            target.RemoveStatusByKey(wall.Key);
            target.RemoveStatusByKey(resist.Key);

            // 감전 하나를 지워도 남은 감전이 있으면 유지, 마지막 감전이 사라지면 즉시 0
            string indraShockKey = $"reaction_shock_{indra.GetEntityId()}";
            target.RemoveStatusByKey(indraShockKey);
            bool keptAfterOne = YamaDeathVerdict.HasVerdict(target) && YamaDeathVerdict.IsShocked(target);
            target.RemoveStatusByKey(second.Key);
            int afterLast = YvProbe(pyg, target, BaseEnums.CodeType.Normal);
            YvCheck("마지막 감전 제거", "하나 남았을 때 유지 → 마지막 제거 시 선고 0 · ×1.0",
                $"하나 제거 후 {keptAfterOne} · 마지막 제거 후 선고 {YamaDeathVerdict.HasVerdict(target)} · ×{Ratio(afterLast, baseDirect):0.###}",
                keptAfterOne && !YamaDeathVerdict.HasVerdict(target) && Near(Ratio(afterLast, baseDirect), 1f));

            // 정화 — 감전과 선고를 함께 걷으면 어느 순서든 둘 다 사라진다
            YvShock(indra, target);
            YvShock(yama, target);
            target.RemoveAllNegativeStatuses();
            YvCheck("정화로 감전 · 선고 함께 제거", "감전 0 · 선고 0",
                $"감전 {YamaDeathVerdict.IsShocked(target)} · 선고 {YamaDeathVerdict.HasVerdict(target)}",
                !YamaDeathVerdict.IsShocked(target) && !YamaDeathVerdict.HasVerdict(target));

            // 이미 감전된 적 — 특성이 꺼진 동안 붙은 감전도 다시 켤 때 선고를 받는다
            var passive = yama.ActivePassiveCodes.OfType<YamaDeathVerdict>().First();
            YvShock(indra, target);
            passive.StopCode();
            bool clearedOnStop = !YamaDeathVerdict.HasVerdict(target);
            YvShock(varuna, other);
            bool silentWhileStopped = !YamaDeathVerdict.HasVerdict(other);
            passive.CastCode();
            YvCheck("구독 해제 · 이미 존재한 감전", "끄면 선고 정리 · 꺼진 동안 새 감전에 반응 없음 · 켜면 두 적 모두 선고",
                $"정리 {clearedOnStop} · 무반응 {silentWhileStopped} · 재개 {YamaDeathVerdict.HasVerdict(target)}/{YamaDeathVerdict.HasVerdict(other)}",
                clearedOnStop && silentWhileStopped && YamaDeathVerdict.HasVerdict(target) && YamaDeathVerdict.HasVerdict(other));

            // 전장 이탈 — 야마가 필드에 없으면 표식이 남아 있어도 효력 0(전투 중 필드 → 대기석 이동은 막혀 있다)
            Cell cell = yama.currentCell;
            yama.currentCell = null;
            int offField = YvProbe(pyg, target, BaseEnums.CodeType.Normal);
            yama.currentCell = cell;
            int back = YvProbe(pyg, target, BaseEnums.CodeType.Normal);
            YvCheck("야마 전장 밖 효력 0", "필드 밖 ×1.0 · 복귀 ×1.2",
                $"×{Ratio(offField, baseDirect):0.###} · ×{Ratio(back, baseDirect):0.###}",
                Near(Ratio(offField, baseDirect), 1f) && Near(Ratio(back, baseDirect), 1.2f));
            yield return WaitUnscaled(0.3f);
        }

        /// <summary>검증용 — 모든 해로운 상태를 저항한다. 선고가 저항 판정을 건너뛰는지 대조한다.</summary>
        private sealed class YvResistAllEffect : BaseEffect
        {
            public YvResistAllEffect() : base(0) { }
            public override float NegativeStatusResistanceChanceModifier(Unit unit, UnitStatus status) => unit == Target ? 1f : 0f;
        }

        // ── 5. 법이 만든 제방 · 바루나 보호막 ──────────────────────────

        private IEnumerator YvVaruna(Unit varuna, Unit indra, List<Unit> enemies)
        {
            foreach (Unit enemy in enemies) YvResetEnemy(enemy);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            int Expected(Unit owner, float ratio) => Mathf.RoundToInt(owner.StatValue(BaseEnums.StatType.Defense) * ratio);

            // 일반행동 — 기존 보호막 위에 누적, 행동 한 번에 한 번
            varuna.AddShield(77);
            int shieldBefore = varuna.ShieldCurr;
            int levee = Expected(varuna, VarunaLawLevee.DefenseRatio);
            using (var recorder = new YvRecorder())
            {
                yield return YvRunScheduled(scheduler => scheduler.AdvanceAction(varuna, 1f),
                    () => recorder.Finished.Any(item => item.Unit == varuna && item.Kind == ActionScheduler.ActionKind.Normal));
                DamageContext direct = recorder.Incoming.FirstOrDefault(context => context.Attacker == varuna && context.CodeType == BaseEnums.CodeType.Normal);
                int hydro = recorder.Elements.Count(item => item.Source == varuna && item.Element == BaseEnums.UnitElement.Hydro);
                YvCheck("금줄 실제 행동 · 제방 누적", $"단일 #일반 #주문 → 물 1 → 제방 1회 {levee} · 보호막 {shieldBefore}+{levee}",
                    $"[{Tags(direct)}] · 물 {hydro} · 제방 {recorder.Levees.Count}회 {recorder.Levees.FirstOrDefault().Amount} · 보호막 {shieldBefore}→{varuna.ShieldCurr}",
                    direct != null && direct.HasTag(DamageTag.NormalAttack) && direct.HasTag(DamageTag.SingleTarget) &&
                    direct.HasTag(DamageTag.Spell) && hydro == 1 && recorder.Levees.Count == 1 &&
                    recorder.Levees[0].Amount == levee && varuna.ShieldCurr == shieldBefore + levee);
            }

            // 초월기 — 적 전체 물(먼저 번개를 깔아 감전으로 즉시 소비) · 제방 한 번 · 아군 전원 방어력 100%
            foreach (Unit enemy in AliveEnemies())
            {
                YvResetEnemy(enemy);
                enemy.GrantCombatElement(BaseEnums.UnitElement.Electro, source: indra);
            }
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            int party = Expected(varuna, 1f);
            using (var recorder = new YvRecorder())
            {
                yield return YvRunScheduled(_ => varuna.FillUltimateResource(),
                    () => recorder.Finished.Any(item => item.Unit == varuna && item.Kind == ActionScheduler.ActionKind.Ultimate));
                int hydro = recorder.Elements.Count(item => item.Source == varuna && item.Element == BaseEnums.UnitElement.Hydro);
                int consumed = recorder.Reactions.Count(item => item.Source == varuna);
                bool stillWet = AliveEnemies().Any(enemy => enemy.HasAttachedElement(BaseEnums.UnitElement.Hydro));
                var partyShields = recorder.Shields.Where(item => item.Source == varuna).ToList();
                List<Unit> field = AliveAllies().Where(unit => unit.IsOnField).ToList();
                bool everyone = field.All(ally => partyShields.Count(item => item.Target == ally && item.Amount == party) == 1);
                DamageContext ult = recorder.Incoming.FirstOrDefault(context => context.Attacker == varuna && context.CodeType == BaseEnums.CodeType.Ultimate);
                YvCheck("동일한 법 아래 — 광역 제방 1회", $"물 {AliveEnemies().Count}명 · 감전으로 소비 · 제방 1회 · 아군 {field.Count}명(자신 포함) 각 {party}",
                    $"[{Tags(ult)}] · 물 {hydro} · 반응 {consumed} · 물 남음 {stillWet} · 제방 {recorder.Levees.Count}회 · 파티 보호막 {partyShields.Count(item => item.Amount == party)}건",
                    ult != null && ult.HasTag(DamageTag.UltAttack) && ult.HasTag(DamageTag.AllTarget) && ult.HasTag(DamageTag.Spell) &&
                    hydro == AliveEnemies().Count && consumed == hydro && !stillWet && recorder.Levees.Count == 1 && everyone);
            }

            // 같은 턴의 일반행동 + 초월기 — 서로 다른 행동이라 제방 두 번
            foreach (Unit enemy in AliveEnemies()) YvResetEnemy(enemy);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            void FillOnNormal(Unit unit, ActionScheduler.ActionKind kind, string label)
            {
                if (unit == varuna && kind == ActionScheduler.ActionKind.Normal) varuna.FillUltimateResource();
            }
            using (var recorder = new YvRecorder())
            {
                ActionScheduler.AnyActionStarted += FillOnNormal;
                yield return YvRunScheduled(scheduler => scheduler.AdvanceAction(varuna, 1f),
                    () => recorder.Finished.Any(item => item.Unit == varuna && item.Kind == ActionScheduler.ActionKind.Ultimate));
                ActionScheduler.AnyActionStarted -= FillOnNormal;
                var mine = recorder.Started.Where(item => item.Unit == varuna).ToList();
                YvCheck("같은 턴 일반행동 · 초월기 — 제방 2회", "일반 → 초월기(턴 주인 바루나) · 서로 다른 행동 번호 · 제방 2회",
                    $"{string.Join("→", mine.Select(item => $"{item.Kind}#{item.ActionId}(턴 {item.TurnOwner?.UnitName})"))} · 제방 {string.Join(",", recorder.Levees.Select(item => "#" + item.ActionId))}",
                    mine.Count == 2 && mine[0].Kind == ActionScheduler.ActionKind.Normal && mine[1].Kind == ActionScheduler.ActionKind.Ultimate &&
                    mine.All(item => item.TurnOwner == varuna) && recorder.Levees.Count == 2 &&
                    recorder.Levees[0].ActionId != recorder.Levees[1].ActionId);
            }

            // 특수행동 — 물 없음 · 제방 없음 · 아군 전원 방어력 100%
            List<int> specialShields = null;
            IEnumerator Special(Action<List<(Unit Target, Unit Source, int Amount)>, YvRecorder> inspect)
            {
                using var recorder = new YvRecorder();
                yield return YvRunScheduled(scheduler => scheduler.EnqueueSpecial(varuna, "yv", varuna.ActiveSpecialCode.CodeName, varuna.CastSpecialCode),
                    () => recorder.Finished.Any(item => item.Unit == varuna && item.Kind == ActionScheduler.ActionKind.Special));
                inspect(recorder.Shields.Where(item => item.Source == varuna).ToList(), recorder);
            }
            yield return Special((shields, recorder) =>
            {
                specialShields = shields.Select(item => item.Amount).ToList();
                DamageContext sp = recorder.Incoming.FirstOrDefault(context => context.Attacker == varuna && context.CodeType == BaseEnums.CodeType.Special);
                int hydro = recorder.Elements.Count(item => item.Source == varuna);
                List<Unit> field = AliveAllies().Where(unit => unit.IsOnField).ToList();
                YvCheck("넘치는 판결 — 물 · 제방 없음", $"#특수 #주문(#궁극기 없음) · 물 0 · 제방 0 · 아군 {field.Count}명 각 {party}",
                    $"[{Tags(sp)}] · 물 {hydro} · 제방 {recorder.Levees.Count} · 보호막 {string.Join(",", shields.Select(item => item.Amount))}",
                    sp != null && sp.HasTag(DamageTag.SpecialAttack) && !sp.HasTag(DamageTag.UltAttack) && sp.HasTag(DamageTag.Spell) &&
                    hydro == 0 && recorder.Levees.Count == 0 && shields.Count == field.Count &&
                    shields.All(item => item.Amount == party) && field.All(ally => shields.Any(item => item.Target == ally)));
            });

            // 체력 · 특공 · 공격력 · 치명 · 주는 피해는 보호막을 바꾸지 않는다
            var noise = new[]
            {
                YvAddTestStatus(varuna, YvStatId, "yv_v_hp", new StatModifierEffect(BaseEnums.StatType.MaxHp, 5000f)),
                YvAddTestStatus(varuna, YvStatId + 100, "yv_v_spatk", new StatModifierEffect(BaseEnums.StatType.SpecialAttack, 5000f)),
                YvAddTestStatus(varuna, YvStatId + 200, "yv_v_atk", new StatModifierEffect(BaseEnums.StatType.Attack, 5000f)),
                YvAddTestStatus(varuna, YvStatId + 300, "yv_v_crit", new StatModifierEffect(BaseEnums.StatType.CritRate, 1f)),
                YvAddTestStatus(varuna, YvStatId + 400, "yv_v_out", new OutgoingDamageMultiplierEffect(3f)),
            };
            yield return Special((shields, _) =>
            {
                List<int> now = shields.Select(item => item.Amount).ToList();
                YvCheck("보호막은 방어력만 본다", "체력 · 특공 · 공격력 +5000, 치확 100%, 주는 피해 ×3에도 동일",
                    $"{string.Join(",", specialShields ?? new List<int>())} → {string.Join(",", now)}",
                    specialShields != null && now.SequenceEqual(specialShields));
            });
            foreach (UnitStatus status in noise) varuna.RemoveStatusByKey(status.Key);

            // 방어 · 624 · 810 · 625 — Lv.20 6칸 예(Detail_08 §16.4: 보유자 약 585 · 나머지 약 488), 보정은 공용 경로에서 한 번
            int level = varuna.Level;
            varuna.DebugSetLevel(20);
            foreach (int id in varuna.RewardCodeIds.ToList()) varuna.RemoveRewardCode(id);
            int[] codes = { 603, 503, 906, 624, 810, 625 };
            foreach (int id in codes) varuna.TryGrantRewardCode(id);
            foreach (int id in new[] { 624, 810, 625 }) YvCode(varuna, id)?.CastCode();
            float defense = varuna.StatValue(BaseEnums.StatType.Defense);
            yield return Special((shields, _) =>
            {
                int self = shields.Where(item => item.Target == varuna).Select(item => item.Amount).FirstOrDefault();
                var others = shields.Where(item => item.Target != varuna).Select(item => item.Amount).ToList();
                // 공용 경로는 정수 기본량 round(290.38) = 290에 보정 배율을 곱한다. 문서의 "약 585 · 약 488"과 ±1 안이다.
                YvCheck("방어 · 624 · 810 · 625 보정 한 번", "방어력 290.38 · 자신(625) 290×2.016≈585 · 아군 290×1.68≈487(문서 약 488, ±1)",
                    $"방어력 {defense:0.##} · 자신 {self} · 아군 {string.Join(",", others)}",
                    Near(defense, 219.2f * 1.27f + 12f, 0.05f) && Mathf.Abs(self - 585) <= 1 && others.Count > 0 &&
                    others.All(amount => Mathf.Abs(amount - 488) <= 1) &&
                    self == Mathf.RoundToInt(Mathf.Round(defense) * 1.2f * 1.4f * 1.2f) &&
                    others.All(amount => amount == Mathf.RoundToInt(Mathf.Round(defense) * 1.2f * 1.4f)));
            });
            foreach (int id in codes) varuna.RemoveRewardCode(id);
            varuna.DebugSetLevel(level);
            YvCheck("바루나 보상 코드 정리", "0칸", $"{varuna.RewardCodeIds.Count}칸", varuna.RewardCodeIds.Count == 0);
        }

        // ── 6. 로카팔라 실제 개방 ────────────────────────────────────

        private static int YvPendingSpecials()
        {
            var field = typeof(SpecialAction).GetField("Pending",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (field?.GetValue(null) as HashSet<Unit>)?.Count ?? -1;
        }

        private IEnumerator YvGate(Unit indra, Unit varuna, Unit yama, Unit chandra)
        {
            foreach (Unit enemy in AliveEnemies()) YvResetEnemy(enemy);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            List<Unit> participants = SpecialAction.Participants(indra);
            int[] expected = participants.OrderBy(unit => unit.ActiveSpecialCode.OpenOrder).Select(unit => unit.ID).ToArray();
            YvCheck("로카팔라 참가자(실제 편성)", "바루나 -50 → 인드라 +50 → 야마 +75 → 찬드라 +100",
                string.Join("→", participants.OrderBy(unit => unit.ActiveSpecialCode.OpenOrder)
                    .Select(unit => $"{unit.UnitName} {unit.ActiveSpecialCode.OpenOrder:+0;-0;0}")),
                expected.SequenceEqual(new[] { VarunaId, IndraId, YamaId, ChandraId }));

            int perSpecial = Mathf.RoundToInt(10 * indra.ManaEfficiencyCurr);
            using (var recorder = new YvRecorder())
            {
                yield return YvRunScheduled(_ => indra.FillUltimateResource(),
                    () => recorder.Finished.Count(item => item.Kind == ActionScheduler.ActionKind.Special && !item.Unit.IsEnemy) >= 4);
                int[] order = recorder.Started.Where(item => item.Kind == ActionScheduler.ActionKind.Special && !item.Unit.IsEnemy)
                    .Select(item => item.Unit.ID).ToArray();
                YvCheck("팔방의 소집 실제 예약 순서", "66→63→61→60", string.Join("→", order),
                    order.SequenceEqual(new[] { VarunaId, IndraId, YamaId, ChandraId }));
                YvCheck("묶음 종료 · 소집 순환", $"대기 0 · 인드라 마나 {perSpecial * 3}(다른 셋 × {perSpecial})",
                    $"대기 {YvPendingSpecials()} · 마나 {indra.ManaCurr}", YvPendingSpecials() == 0 && indra.ManaCurr == perSpecial * 3);
            }
        }

        // ── 7. 라운드 종료 · 시작 ───────────────────────────────────

        private IEnumerator YvRoundLifecycle(Unit yama, Unit indra)
        {
            List<Unit> enemies = AliveEnemies();
            foreach (Unit enemy in enemies) YvShock(indra, enemy);
            bool before = enemies.All(YamaDeathVerdict.HasVerdict);
            GameManager.Instance.ActionScheduler.EndRound();
            GridManager.Instance.OnRoundEnd();
            bool cleared = enemies.All(enemy => !YamaDeathVerdict.HasVerdict(enemy) && !YamaDeathVerdict.IsShocked(enemy));
            YvShock(indra, enemies[0]);
            bool unsubscribed = !YamaDeathVerdict.HasVerdict(enemies[0]);
            YvCheck("라운드 종료 정리", "종료 전 선고 → 종료 후 상태 · 선고 0 · 구독 해제(새 감전 무반응)",
                $"전 {before} · 정리 {cleared} · 무반응 {unsubscribed} · 묶음 대기 {YvPendingSpecials()}",
                before && cleared && unsubscribed && YvPendingSpecials() == 0);

            // 다음 라운드 시작 — 시작 전에 붙어 있던 감전도 선고를 받는다
            GridManager.Instance.OnRoundStart();
            foreach (Unit enemy in AliveEnemies()) YvBoostEnemy(enemy);
            foreach (Unit unit in AliveAllies().Concat(AliveEnemies())) unit.ClearUltimateResource();
            YvCheck("라운드 시작 재구독", "이미 감전된 적에게 선고", $"{YamaDeathVerdict.HasVerdict(enemies[0])}",
                YamaDeathVerdict.HasVerdict(enemies[0]));
            yield return WaitUnscaled(0.3f);
        }

        // ── 8. 묶음 도중 야마 사망 ───────────────────────────────────

        private IEnumerator YvDeathGate(Unit yama, Unit indra, Unit varuna, Unit chandra)
        {
            List<Unit> enemies = AliveEnemies();
            foreach (Unit enemy in enemies) YvShock(indra, enemy);
            bool marked = enemies.All(YamaDeathVerdict.HasVerdict);
            void KillOnFirst(Unit unit, ActionScheduler.ActionKind kind, string label)
            {
                if (unit == varuna && kind == ActionScheduler.ActionKind.Special && yama.isActive) yama.Die(null);
            }
            using (var recorder = new YvRecorder())
            {
                ActionScheduler.AnyActionStarted += KillOnFirst;
                yield return YvRunScheduled(_ => indra.FillUltimateResource(),
                    () => recorder.Finished.Count(item => item.Kind == ActionScheduler.ActionKind.Special && !item.Unit.IsEnemy) >= 3 &&
                          YvPendingSpecials() == 0);
                ActionScheduler.AnyActionStarted -= KillOnFirst;
                int[] order = recorder.Started.Where(item => item.Kind == ActionScheduler.ActionKind.Special && !item.Unit.IsEnemy)
                    .Select(item => item.Unit.ID).ToArray();
                YvCheck("묶음 도중 야마 사망", "66→63→60 · 대기 0(남은 예약 정리)", $"{string.Join("→", order)} · 대기 {YvPendingSpecials()}",
                    order.SequenceEqual(new[] { VarunaId, IndraId, ChandraId }) && YvPendingSpecials() == 0 && !yama.isActive);
            }

            Unit target = AliveEnemies().First();
            bool cleaned = AliveEnemies().All(enemy => !YamaDeathVerdict.HasVerdict(enemy));
            YvShock(varuna, target);
            bool silent = !YamaDeathVerdict.HasVerdict(target);
            YvCheck("야마 사망 정리", "사망 전 선고 → 사망 후 선고 0 · 새 감전 무반응",
                $"전 {marked} · 정리 {cleaned} · 무반응 {silent}", marked && cleaned && silent);
        }
    }
}
#endif
