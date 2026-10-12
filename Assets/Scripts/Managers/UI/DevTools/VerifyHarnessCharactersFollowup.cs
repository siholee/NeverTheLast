#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Codes.Passive;
using Codes.Special;
using Codes.Ultimate;
using Combat;
using Core;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Entities.Status;
using UnityEngine;
using Kind = Managers.ActionScheduler.ActionKind;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 나폴레옹 지휘 · 로키/펜리르(Detail_08 §20 · §22)의 메커니즘 검증.
    ///   · `characters_followup_core` (party5_command_fenrir: 나폴레옹 · 로키 · 수리야 · 마리 · 우치)
    ///   · `characters_followup_melee` (party5_command_melee: 나폴레옹 · 시 · 사바흐 · 바스테트 · 잔)
    /// 실제 스케줄러로 행동을 한 건씩 돌리고 이벤트 · 자원 · 상태 · 피해 컨텍스트만 판정한다. 치명이 끼면 안 되는 비교는
    /// 치명 확률을 0으로 만든 검증 상태 아래에서 잰다. 결과는 mechanics 기록과 `[야마바루나검증]` 로그 줄에 남는다.
    /// </summary>
    public sealed partial class VerifyHarnessRunner
    {
        private const int CfNapoleonId = 161;
        private const int CfLokiId = 80;
        private const int CfMarieId = 160;
        private static readonly int[] PartyFiveCommandFenrirIds = { CfNapoleonId, CfLokiId, SuryaId, CfMarieId, UchiId };
        private static readonly int[] PartyFiveCommandMeleeIds = { CfNapoleonId, ShiId, SabahId, BastetId, JeanId };

        // 검증 전용 상태 ID. 캐릭터 구간(70000번대)과 다른 검증(999xx 앞쪽) 밖이다.
        private const int CfNoCritId = 99970;
        private const int CfDefId = 99971;
        private const int CfPriorityId = 99972;
        private const int CfImmuneId = 99973;
        private const int CfHitId = 99974;
        private const int CfResistId = 99975;
        private const int CfCritId = 99976;

        private static readonly List<int> CfAdditionalTags = new()
            { DamageTag.SingleTarget, DamageTag.AdditionalAttack, DamageTag.NonContactAttack, DamageTag.Shot };

        private sealed class CfImmune : BaseEffect
        {
            public CfImmune() : base(0, 0) { }
            public override float ReceivingDamageModifier(Unit unit, DamageContext context) => 0f;
        }

        /// <summary>한 실행 동안의 행동 시작/끝 · 일반행동 완료 · 피해 · 지휘 · 추격 · 표식 · 행동치 조작을 모은다.</summary>
        private sealed class CfLog : IDisposable
        {
            public readonly List<(Unit Unit, Kind Kind)> Started = new();
            public readonly List<(Unit Unit, Kind Kind)> Finished = new();
            public readonly List<DamageResolvedContext> Dealt = new();
            public readonly List<(Unit Source, Unit Target, BaseEnums.UnitElement Element)> Elements = new();
            public readonly List<(Unit Target, int Participants)> Volleys = new();
            public readonly List<(Unit Target, int Attacks, int Hits, bool Cancelled)> VolleyEnds = new();
            public readonly List<(Unit Target, LokiUnboundWolf.HuntEvent Kind, bool Flex)> Hunts = new();
            public readonly List<(Unit Target, bool Success)> Marks = new();
            public readonly List<(Unit Unit, bool Advanced)> OrderAdjusted = new();
            public readonly List<bool> CommanderCastingAtHit = new();
            public readonly Dictionary<Unit, int> NormalResolved = new();
            public readonly Dictionary<Unit, float> ProgressAtVolleyStart = new();
            public readonly Dictionary<Unit, float> ProgressAtVolleyEnd = new();
            public int BuffHoldersAtVolleyStart = -1;
            public Action<DamageResolvedContext> OnHit;

            private readonly List<Unit> _watch;
            private readonly List<(Unit Unit, Action<EventContext> Handler)> _resolvers = new();
            private Unit _commander;
            private bool _volleyActive;

            public CfLog(IEnumerable<Unit> watch)
            {
                _watch = watch.Where(unit => unit != null).Distinct().ToList();
                foreach (Unit unit in _watch)
                {
                    Unit captured = unit;
                    Action<EventContext> handler = _ => NormalResolved[captured] = NormalResolved.GetValueOrDefault(captured) + 1;
                    unit.AddListener(BaseEnums.UnitEventType.OnNormalActionResolved, handler);
                    _resolvers.Add((unit, handler));
                }
                ActionScheduler.AnyActionStarted += OnStarted;
                ActionScheduler.AnyActionFinished += OnFinished;
                ActionScheduler.AnyActionOrderAdjusted += OnOrder;
                Unit.AnyDamageDealt += OnDealt;
                Unit.AnyCombatElementGranted += OnElement;
                NapoleonGrandVolley.AnyVolleyStarted += OnVolley;
                NapoleonGrandVolley.AnyVolleyFinished += OnVolleyEnd;
                LokiUnboundWolf.AnyHunt += OnHunt;
                LokiTwilight.AnyMarkAttempt += OnMark;
            }

            public void Dispose()
            {
                foreach ((Unit unit, Action<EventContext> handler) in _resolvers)
                    if (unit != null) unit.RemoveListener(BaseEnums.UnitEventType.OnNormalActionResolved, handler);
                _resolvers.Clear();
                ActionScheduler.AnyActionStarted -= OnStarted;
                ActionScheduler.AnyActionFinished -= OnFinished;
                ActionScheduler.AnyActionOrderAdjusted -= OnOrder;
                Unit.AnyDamageDealt -= OnDealt;
                Unit.AnyCombatElementGranted -= OnElement;
                NapoleonGrandVolley.AnyVolleyStarted -= OnVolley;
                NapoleonGrandVolley.AnyVolleyFinished -= OnVolleyEnd;
                LokiUnboundWolf.AnyHunt -= OnHunt;
                LokiTwilight.AnyMarkAttempt -= OnMark;
            }

            public bool Did(Unit unit, Kind kind) => Finished.Any(entry => entry.Unit == unit && entry.Kind == kind);
            public int StartedCount(Unit unit, Kind kind) => Started.Count(entry => entry.Unit == unit && entry.Kind == kind);
            public int HuntCount(LokiUnboundWolf.HuntEvent kind) => Hunts.Count(entry => entry.Kind == kind);

            private void OnStarted(Unit unit, Kind kind, string label) => Started.Add((unit, kind));
            private void OnFinished(Unit unit, Kind kind) => Finished.Add((unit, kind));
            private void OnOrder(Unit unit, bool advanced) => OrderAdjusted.Add((unit, advanced));
            private void OnElement(Unit source, Unit target, BaseEnums.UnitElement element) => Elements.Add((source, target, element));
            private void OnHunt(Unit loki, Unit target, LokiUnboundWolf.HuntEvent kind, bool flex) => Hunts.Add((target, kind, flex));
            private void OnMark(Unit loki, Unit target, bool success) => Marks.Add((target, success));

            private void OnDealt(DamageResolvedContext context)
            {
                Dealt.Add(context);
                if (_volleyActive && _commander != null && context.Attacker != _commander) CommanderCastingAtHit.Add(_commander.isCasting);
                OnHit?.Invoke(context);
            }

            private void OnVolley(Unit commander, Unit target, int participants)
            {
                _commander = commander;
                _volleyActive = true;
                Volleys.Add((target, participants));
                ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
                ProgressAtVolleyStart.Clear();
                foreach (Unit unit in _watch.Where(unit => unit != commander && unit.isActive))
                    ProgressAtVolleyStart[unit] = scheduler?.ActionProgress(unit) ?? -1f;
                BuffHoldersAtVolleyStart = AliveAllies().Count(unit => unit.HasStatusKey(NapoleonGrandBattery.BuffKey));
            }

            private void OnVolleyEnd(Unit commander, Unit target, int attacks, int hits, bool cancelled)
            {
                _volleyActive = false;
                VolleyEnds.Add((target, attacks, hits, cancelled));
                ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
                ProgressAtVolleyEnd.Clear();
                foreach (Unit unit in ProgressAtVolleyStart.Keys)
                    ProgressAtVolleyEnd[unit] = unit != null && unit.isActive ? scheduler?.ActionProgress(unit) ?? -1f : -1f;
            }
        }

        private sealed class CfForcedCritical : BaseEffect
        {
            private readonly Unit _target;
            public CfForcedCritical(Unit target):base(0,0)=>_target=target;
            public override bool ForcesCriticalAgainst(Unit attacker,Unit target,BaseEnums.CodeType type)=>target==_target;
            public override float CritDamageBonusAgainst(Unit attacker,Unit target,BaseEnums.CodeType type)=>target == _target ? .25f : 0f;
        }
        private static IEnumerator CfFixtureCommand(NormalCode code,Unit target,Unit commander)
        {
            try { yield return code.PerformCommandedAttack(target,commander); }
            finally { if(commander!=null) commander.isCasting=false; }
        }

        // ── 실행 도우미 ──────────────────────────────────────────

        /// <summary>
        /// 새 행동 시계에서 <paramref name="setup"/>으로 예약하고, <paramref name="done"/>과 함께 실행 · 예약 · 시전이 모두 비면 멈춘다.
        /// 다음 턴이 열리기 전에 멈추므로 다른 유닛의 행동이 판정에 섞이지 않는다.
        /// </summary>
        // 이 시나리오는 패시브를 유지한 채 AV 시계만 새로 연다. 실전의 한 전투처럼 행동 ID는 재사용하지 않는다.
        // 게임 규칙/구독/펜리르 차례 예산은 변경하지 않고 검증 시계의 번호만 이어 붙인다.
        private int _cfActionIdSequence;
        private static readonly System.Reflection.FieldInfo CfActionIdField = typeof(ActionScheduler).GetField(
            "_actionIdSequence", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private IEnumerator CfRun(Action<ActionScheduler> setup, Func<bool> done, float timeout = 20f)
        {
            ActionScheduler scheduler = GameManager.Instance.ActionScheduler;
            if (CfActionIdField == null)
            {
                YvCheck("검증 시계 행동 ID 필드", "_actionIdSequence 존재", "없음", false);
                yield break;
            }
            _cfActionIdSequence = Mathf.Max(_cfActionIdSequence, (int)CfActionIdField.GetValue(scheduler));
            yield return YvRunScheduled(s =>
            {
                CfActionIdField.SetValue(s, _cfActionIdSequence);
                setup?.Invoke(s);
            }, () => done() && scheduler.ExecutingUnit == null &&
                scheduler.DebugQueuedCount(Kind.Additional) == 0 &&
                scheduler.DebugQueuedCount(Kind.PriorityAdditional) == 0 &&
                scheduler.DebugQueuedCount(Kind.Special) == 0 &&
                !AliveAllies().Any(unit => unit.isCasting) &&
                !(GridManager.Instance?.summonList?.Any(unit => unit != null && unit.isActive && unit.isCasting) ?? false), timeout);
            _cfActionIdSequence = (int)CfActionIdField.GetValue(scheduler);
        }

        // 현재 명중에는 별도 질의 훅이 없다. 필요한 최소 특공(최고 특방의 두 배)만 더해 반응 계수를 폭증시키지 않는다.
        private static void CfEnsureEffectHit(Unit source, IEnumerable<Unit> targets)
        {
            float resist = targets.Where(target => target != null && target.isActive)
                .Select(target => target.StatValue(BaseEnums.StatType.SpecialDefense)).DefaultIfEmpty(1f).Max();
            float flat = Mathf.Max(0f, 2f * Mathf.Max(1f, resist) + 1f - source.StatValue(BaseEnums.StatType.SpecialAttack));
            YvAddTestStatus(source, CfHitId, "verify_cf_hit", new StatModifierEffect(BaseEnums.StatType.SpecialAttack, flat));
        }

        private bool CfPreyValid(Unit prey, LokiTwilight twilight, string section)
        {
            bool ok = prey != null && prey.isActive && prey.HpCurr > 0 && twilight.MarkedTarget == prey && prey.HasStatusKey(twilight.MarkKey);
            YvCheck(section + " 사냥감 fixture", "살아있는 현재 표식 대상",
                $"{prey?.UnitName} / HP {prey?.HpCurr} / 현재 {twilight.MarkedTarget?.UnitName} / 활성 {prey?.isActive}", ok);
            return ok;
        }

        private static Unit CfAlly(int id) => AliveAllies().FirstOrDefault(unit => unit.ID == id);

        private static Unit CfFenrir(Unit loki)
            => Summons.Of(loki).FirstOrDefault(unit => unit.SummonKind == LokiUnboundWolf.FenrirKind);

        private static void CfNoCrit(Unit unit)
        {
            if (unit == null || unit.HasStatusKey("verify_cf_nocrit")) return;
            YvAddTestStatus(unit, CfNoCritId, "verify_cf_nocrit",
                new StatModifierEffect(BaseEnums.StatType.CritRate, -Mathf.Max(0f, unit.StatValue(BaseEnums.StatType.CritRate)) - 1f));
        }

        private static float CfModifier(Unit holder, string key, Func<BaseEffect, float> query)
        {
            UnitStatus status = holder?.GetAllStatuses().FirstOrDefault(item => item.Key == key);
            BaseEffect effect = status?.Effects.FirstOrDefault()?.EffectObject;
            return effect == null ? -1f : query(effect);
        }

        private static DamageContext CfNormalProbe(Unit attacker)
            => new(attacker, 100, BaseEnums.CodeType.Normal,
                new List<int> { DamageTag.SingleTarget, DamageTag.NormalAttack, DamageTag.NonContactAttack, DamageTag.Shot });

        private static DamageContext CfAdditionalProbe(Unit attacker)
            => new(attacker, 100, BaseEnums.CodeType.Passive, new List<int>(CfAdditionalTags));

        private static void CfHit(Unit attacker, Unit target, List<int> tags, BaseEnums.CodeType type = BaseEnums.CodeType.Passive)
        {
            if (target == null || !target.isActive) return;
            target.TakeDamage(new DamageContext(attacker, 50, type, new List<int>(tags)) { IsDamageOverTime = type == BaseEnums.CodeType.Effect });
        }

        private void CfStats(Unit unit, string name, float hp, float hpG, float atk, float atkG, float spAtk, float spAtkG,
            float def, float defG, float spDef, float spDefG, float speed, int mana)
        {
            if (unit == null)
            {
                YvCheck(name + " 스탯", "유닛 존재", "없음", false);
                return;
            }
            int levels = unit.Level - 1;
            bool Is(BaseEnums.StatType stat, float baseValue, float growth)
                => Near(unit.Stats.BaseValue(stat), baseValue, 0.001f) && Near(unit.Stats.Growth(stat), growth, 0.001f) &&
                   Near(unit.BaseStat(stat), baseValue + growth * levels, 0.01f);
            bool ok = Is(BaseEnums.StatType.MaxHp, hp, hpG) && Is(BaseEnums.StatType.Attack, atk, atkG) &&
                      Is(BaseEnums.StatType.SpecialAttack, spAtk, spAtkG) && Is(BaseEnums.StatType.Defense, def, defG) &&
                      Is(BaseEnums.StatType.SpecialDefense, spDef, spDefG) && Is(BaseEnums.StatType.Speed, speed, 0f) &&
                      Is(BaseEnums.StatType.CritRate, 0.05f, 0f) && Is(BaseEnums.StatType.CritDamage, 0.50f, 0f) &&
                      unit.ManaMax == mana;
            YvCheck(name + " 기초값 · 성장(Lv." + unit.Level + ")",
                $"HP {hp}+{hpG} · 공 {atk}+{atkG} · 특공 {spAtk}+{spAtkG} · 방 {def}+{defG} · 특방 {spDef}+{spDefG} · 속 {speed} · 치 5/50 · 마나 {mana}",
                $"HP {unit.BaseStat(BaseEnums.StatType.MaxHp)} 공 {unit.BaseStat(BaseEnums.StatType.Attack)} 속 {unit.BaseStat(BaseEnums.StatType.Speed)} 마나 {unit.ManaMax}", ok);
        }

        private void CfFinish(bool wasEnabled)
        {
            GameManager.Instance.enabled = wasEnabled;
            if (_yvFailures.Count > 0) FailCurrent(string.Join(" / ", _yvFailures));
            _yvFailures.Clear();
        }

        // ══════════════════════════════════════════════════════════
        // characters_followup_core
        // ══════════════════════════════════════════════════════════

        private IEnumerator ScenarioCharactersFollowupCore(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            _cfActionIdSequence = 0;
            bool wasEnabled = true;
            yield return YvPrepareScene(config, enabled => wasEnabled = enabled);
            Unit napoleon = CfAlly(CfNapoleonId), loki = CfAlly(CfLokiId), surya = CfAlly(SuryaId),
                marie = CfAlly(CfMarieId), uchi = CfAlly(UchiId);
            List<Unit> foes = AliveEnemies();
            if (napoleon == null || loki == null || surya == null || marie == null || uchi == null || foes.Count < 2)
            {
                YvCheck("편성 party5_command_fenrir", "161 · 80 · 67 · 160 · 180 + 적 2기 이상",
                    string.Join(",", AliveAllies().Select(unit => unit.ID)) + " / 적 " + foes.Count, false);
                CfFinish(wasEnabled);
                yield break;
            }

            var orders = napoleon.ActivePassiveCodes.OfType<NapoleonOrders>().FirstOrDefault();
            var volley = napoleon.ActiveSpecialCode as NapoleonGrandVolley;
            var battery = napoleon.ActiveUltimateCode as NapoleonGrandBattery;
            var wolf = loki.ActivePassiveCodes.OfType<LokiUnboundWolf>().FirstOrDefault();
            var twilight = loki.ActiveUltimateCode as LokiTwilight;
            var execution = surya.ActivePassiveCodes.OfType<SuryaExecution>().FirstOrDefault();
            Unit fenrir = CfFenrir(loki);
            YvCheck("코드 등록", "나폴레옹 P361/N161/SP161/U161 · 로키 P280/N80/U80 · 펜리르 N501",
                $"{orders?.CodeName}/{napoleon.ActiveNormalCode?.CodeName}/{volley?.CodeName}/{battery?.CodeName} · {wolf?.CodeName}/{loki.ActiveNormalCode?.CodeName}/{twilight?.CodeName} · {fenrir?.ActiveNormalCode?.CodeName}",
                orders != null && napoleon.ActiveNormalCode is NapoleonCommandShot && volley != null && battery != null &&
                wolf != null && loki.ActiveNormalCode is LokiTrickWind && twilight != null && execution != null &&
                fenrir?.ActiveNormalCode is FenrirClaw);
            if (orders == null || volley == null || battery == null || wolf == null || twilight == null || execution == null || fenrir == null)
            {
                CfFinish(wasEnabled);
                yield break;
            }

            // ── 1. 스탯 · 데이터 ──
            CfStats(napoleon, "나폴레옹", 1000, 90, 78, 5, 80, 5.2f, 62, 4.6f, 70, 5, 90, 140);
            CfStats(loki, "로키", 1050, 94, 100, 6.4f, 84, 5.6f, 64, 4.6f, 68, 4.8f, 98, 140);
            UnitData lokiData = GameManager.Instance.unitDataList.units.FirstOrDefault(unit => unit.id == CfLokiId);
            YvCheck("로키 노르드 · 바람 · 해금 · 원화", "Nord / Anemo / Unlocked / LOKI",
                $"{string.Join(",", loki.UnitTags)} / {loki.Element} / {lokiData?.characterType} / {lokiData?.portrait}",
                loki.HasUnitTag("Nord") && loki.Element == "Anemo" && CharacterSelectionManager.IsUnlocked(lokiData) &&
                lokiData?.portrait == "LOKI_PORTRAIT" && lokiData.standing == "LOKI_STANDING");
            YvCheck("나폴레옹 물 · 특수행동 슬롯", "Hydro / SP161", napoleon.Element + "/" + napoleon.HasSpecialAction,
                napoleon.Element == "Hydro" && napoleon.HasSpecialAction);
            int fenrirHp = Mathf.RoundToInt(loki.HpMax * LokiUnboundWolf.InheritRatio);
            YvCheck("펜리르 동반 소환", "902 · 동반형 · 1기 · 속도 90 · 대상 불가 · 행동 순서 참가",
                $"{fenrir.ID}/{Summons.StyleOf(loki)}/{Summons.Of(loki).Count}/{fenrir.SpeedCurr}/{fenrir.IsUntargetable}/{fenrir.TakesScheduledTurns}",
                fenrir.ID == LokiUnboundWolf.FenrirId && Summons.StyleOf(loki) == SummonStyle.Companion && Summons.Of(loki).Count == 1 &&
                Near(fenrir.SpeedCurr, 90f) && fenrir.IsUntargetable && fenrir.TakesScheduledTurns);
            YvCheck("펜리르 계승 60%", $"HP {fenrirHp} · 공 {loki.StatValue(BaseEnums.StatType.Attack) * .6f:F1} · 방 {loki.StatValue(BaseEnums.StatType.Defense) * .6f:F1} · 특방 {loki.StatValue(BaseEnums.StatType.SpecialDefense) * .6f:F1}",
                $"HP {fenrir.HpMax} · 공 {fenrir.StatValue(BaseEnums.StatType.Attack):F1} · 방 {fenrir.StatValue(BaseEnums.StatType.Defense):F1} · 특방 {fenrir.StatValue(BaseEnums.StatType.SpecialDefense):F1}",
                Mathf.Abs(fenrir.HpMax - fenrirHp) <= 1 &&
                Near(fenrir.StatValue(BaseEnums.StatType.Attack), loki.StatValue(BaseEnums.StatType.Attack) * .6f, 0.6f) &&
                Near(fenrir.StatValue(BaseEnums.StatType.Defense), loki.StatValue(BaseEnums.StatType.Defense) * .6f, 0.6f) &&
                Near(fenrir.StatValue(BaseEnums.StatType.SpecialDefense), loki.StatValue(BaseEnums.StatType.SpecialDefense) * .6f, 0.6f));

            // ── 2. 나폴레옹 특성: 오라 · 개전 선행 ──
            YvCheck("오라 아군별 1개", "전원 1", string.Join(",", AliveAllies().Select(unit => unit.GetAllStatuses().Count(s => s.Key == NapoleonOrders.AuraKey))),
                AliveAllies().All(unit => unit.GetAllStatuses().Count(s => s.Key == NapoleonOrders.AuraKey) == 1));
            float napoleonOpen = -1f, othersOpen = -1f;
            yield return CfRun(s =>
            {
                napoleonOpen = s.ActionProgress(napoleon);
                othersOpen = AliveAllies().Where(unit => unit != napoleon).Max(unit => s.ActionProgress(unit));
            }, () => true);
            YvCheck("개전 행동 게이지(라운드 시작 조정 경로)", "나폴레옹 0.25 / 다른 아군 0", $"{napoleonOpen:F3} / {othersOpen:F3}",
                Near(napoleonOpen, NapoleonOrders.OpeningAdvance) && Near(othersOpen, 0f));
            Unit probeTarget = foes[0];
            float auraNormal = CfModifier(surya, NapoleonOrders.AuraKey, effect => effect.OutgoingDamageModifier(surya, probeTarget, CfNormalProbe(surya)));
            float auraAdditional = CfModifier(surya, NapoleonOrders.AuraKey, effect => effect.OutgoingDamageModifier(surya, probeTarget, CfAdditionalProbe(surya)));
            float auraDot = CfModifier(surya, NapoleonOrders.AuraKey, effect => effect.OutgoingDamageModifier(surya, probeTarget,
                new DamageContext(surya, 100, BaseEnums.CodeType.Effect, new List<int>()) { IsDamageOverTime = true }));
            float auraOther = CfModifier(surya, NapoleonOrders.AuraKey, effect => effect.OutgoingDamageModifier(marie, probeTarget, CfNormalProbe(marie)));
            YvCheck("오라 일반 공격 +15%만", "1.15 / 추가 1 / 지속 1 / 보유자 외 1", $"{auraNormal:F3}/{auraAdditional:F3}/{auraDot:F3}/{auraOther:F3}",
                Near(auraNormal, 1.15f) && Near(auraAdditional, 1f) && Near(auraDot, 1f) && Near(auraOther, 1f));

            // ── 3. 지휘 참여자 ──
            Unit clone = Summons.Create(uchi);
            List<Unit> participants = NapoleonCommand.Participants(napoleon);
            var expected = new HashSet<Unit> { loki, surya, marie, fenrir, clone };
            YvCheck("지휘 참여자", "로키 · 수리야 · 마리 · 펜리르 · 분신 (나폴레옹 · 우치 분신술 제외)",
                string.Join(",", participants.Select(unit => unit.UnitName)),
                participants.Count == expected.Count && participants.All(expected.Contains) && !participants.Contains(uchi) &&
                clone != null && participants.Contains(clone) && !participants.Contains(napoleon));
            if (clone != null && clone.isActive) clone.Die(null);
            marie.isControlled = true;
            bool controlledExcluded = !NapoleonCommand.Participants(napoleon).Contains(marie);
            marie.isControlled = false;
            YvCheck("제어된 아군 제외", "마리 제외", controlledExcluded.ToString(), controlledExcluded);

            // ── 4. 대상 선정 ──
            foreach (Unit enemy in foes) enemy.RestoreHpOutsideCombat(enemy.HpMax);
            Unit a = foes[0], b = foes[1];
            a.ModifyHp(60000);
            b.ModifyHp(90000);
            YvCheck("가장 쉬운 처치(현재 체력)", a.UnitName, NapoleonCommand.PickTarget(napoleon)?.UnitName, NapoleonCommand.PickTarget(napoleon) == a);
            YvAddTestStatus(b, CfPriorityId, "verify_cf_priority", new TrioPriority());
            YvCheck("우선도 무시", a.UnitName, NapoleonCommand.PickTarget(napoleon)?.UnitName, NapoleonCommand.PickTarget(napoleon) == a);
            YvAddTestStatus(a, CfDefId, "verify_cf_def", new StatModifierEffect(BaseEnums.StatType.Defense, 100000f));
            YvCheck("방어력 반영", b.UnitName, NapoleonCommand.PickTarget(napoleon)?.UnitName, NapoleonCommand.PickTarget(napoleon) == b);
            a.RemoveStatusByKey("verify_cf_def");
            a.AddShield(10);
            YvCheck("일반 HP보호막은 유효체력에만 포함", a.UnitName + " / HP+10 / 타수보호막 아님",
                NapoleonCommand.PickTarget(napoleon)?.UnitName + " / " + NapoleonCommand.EffectiveHp(a),
                NapoleonCommand.PickTarget(napoleon) == a && !NapoleonCommand.IsHitWarded(a) && NapoleonCommand.EffectiveHp(a) == a.HpCurr + 10);
            YvAddTestStatus(a, 99977, "verify_cf_selection_ward", new StatModifierEffect(BaseEnums.StatType.Defense, 0f));
            YvCheck("타수보호막만 후순위", b.UnitName, NapoleonCommand.PickTarget(napoleon)?.UnitName,
                NapoleonCommand.IsHitWarded(a) && NapoleonCommand.PickTarget(napoleon) == b);
            foreach (Unit enemy in foes.Where(enemy => enemy != a))
                YvAddTestStatus(enemy, 99977, "verify_cf_selection_ward", new StatModifierEffect(BaseEnums.StatType.Defense, 0f));
            Unit shieldPick = NapoleonCommand.PickTarget(napoleon);
            YvCheck("전원 타수보호막이면 차선 대상 허용", "타수보호막 적 하나", shieldPick?.UnitName,
                shieldPick != null && NapoleonCommand.IsHitWarded(shieldPick));
            foreach (Unit enemy in foes) { enemy.RemoveStatusByKey("verify_cf_selection_ward"); enemy.RemoveShield(enemy.ShieldCurr); }
            YvAddTestStatus(a, CfImmuneId, "verify_cf_immune", new CfImmune());
            YvCheck("무적 제외", "a 제외", NapoleonCommand.PickTarget(napoleon)?.UnitName + " / " + NapoleonCommand.IsInvincible(a, napoleon),
                NapoleonCommand.PickTarget(napoleon) != a && NapoleonCommand.IsInvincible(a, napoleon));
            a.RemoveStatusByKey("verify_cf_immune");
            b.RemoveStatusByKey("verify_cf_priority");
            YvCheck("복구 후 원래 대상", a.UnitName, NapoleonCommand.PickTarget(napoleon)?.UnitName, NapoleonCommand.PickTarget(napoleon) == a);

            // ── 5. 일제 사격: 실제 일반 공격 전달 · 최종 50% · 참여자 무행동 ──
            foreach (Unit unit in new[] { loki, surya, marie }) CfNoCrit(unit);
            // 표식 · 원소 부착은 효과 명중을 굴린다. 적중 후처리 유지를 결정적으로 보려고 명중만 확정한다.
            CfEnsureEffectHit(surya, foes);
            CfEnsureEffectHit(marie, foes);
            execution.Consume(a);
            foreach (Unit unit in AliveAllies()) unit.ClearUltimateResource();
            int Scaled(float atk, float ratio) => Mathf.Max(1, Mathf.RoundToInt(atk * ratio));
            int expectMarie = Scaled(marie.StatValue(BaseEnums.StatType.Attack), .60f);
            int expectLoki = Scaled(loki.StatValue(BaseEnums.StatType.Attack), LokiTrickWind.AttackRatio);
            int expectSurya = Scaled(surya.StatValue(BaseEnums.StatType.Attack), SuryaNameBullet.AttackRatio);
            int expectFenrir = Scaled(loki.StatValue(BaseEnums.StatType.Attack), FenrirClaw.AttackRatio);
            int volleysBefore = volley.VolleysPerformed;
            var fireCodes = new[] { loki, surya, marie, fenrir }.Select(unit => unit.ActiveNormalCode as BaseNormalCode).ToList();
            var manaBefore = new[] { loki, surya, marie }.ToDictionary(unit => unit, unit => unit.ManaCurr);
            using (var log = new CfLog(new[] { napoleon, loki, surya, marie, uchi, fenrir }))
            {
                yield return CfRun(s => s.AdvanceAction(napoleon, 1), () => log.Did(napoleon, Kind.Special));
                List<DamageResolvedContext> hits = log.Dealt.Where(hit => hit.Target == a && hit.DamageContext.HasTag(DamageTag.NormalAttack)).ToList();
                DamageResolvedContext marieHit = hits.FirstOrDefault(hit => hit.Attacker == marie);
                DamageResolvedContext suryaHit = hits.FirstOrDefault(hit => hit.Attacker == surya);
                DamageResolvedContext lokiHit = hits.FirstOrDefault(hit => hit.Attacker == loki && !hit.DamageContext.HasTag(DamageTag.SummonAttack));
                DamageResolvedContext fenrirHit = hits.FirstOrDefault(hit => hit.Attacker == loki && hit.DamageContext.HasTag(DamageTag.SummonAttack));
                YvCheck("나폴레옹 일반 1 → 특수 1", "N 1 / SP 1 / 일제사격 1", $"{log.StartedCount(napoleon, Kind.Normal)}/{log.StartedCount(napoleon, Kind.Special)}/{volley.VolleysPerformed - volleysBefore}",
                    log.StartedCount(napoleon, Kind.Normal) == 1 && log.StartedCount(napoleon, Kind.Special) == 1 && volley.VolleysPerformed - volleysBefore == 1);
                YvCheck("나폴레옹 직접 피해 0 · 일반행동 마나 +20", "0 / " + Mathf.RoundToInt(Unit.ManaPerNormalAction * napoleon.ManaEfficiencyCurr),
                    log.Dealt.Count(hit => hit.Attacker == napoleon) + " / " + napoleon.ManaCurr,
                    log.Dealt.All(hit => hit.Attacker != napoleon) && napoleon.ManaCurr == Mathf.RoundToInt(Unit.ManaPerNormalAction * napoleon.ManaEfficiencyCurr) &&
                    log.NormalResolved.GetValueOrDefault(napoleon) == 1);
                YvCheck("참여자 일반행동 시작 · 완료 0", "0 / 0",
                    new[] { loki, surya, marie, fenrir }.Sum(unit => log.StartedCount(unit, Kind.Normal)) + " / " +
                    new[] { loki, surya, marie, fenrir }.Sum(unit => log.NormalResolved.GetValueOrDefault(unit)),
                    new[] { loki, surya, marie, fenrir }.All(unit => log.StartedCount(unit, Kind.Normal) == 0 && log.NormalResolved.GetValueOrDefault(unit) == 0) &&
                    log.Started.All(entry => entry.Unit == napoleon));
                YvCheck("참여자 마나 변화 0", "0/0/0",
                    string.Join("/", manaBefore.Select(pair => pair.Key.ManaCurr - pair.Value)),
                    manaBefore.All(pair => pair.Key.ManaCurr == pair.Value));
                YvCheck("참여자 행동 게이지 불변", "시작 = 끝",
                    string.Join(",", log.ProgressAtVolleyStart.Select(pair => $"{pair.Key.UnitName}:{pair.Value:F3}->{log.ProgressAtVolleyEnd.GetValueOrDefault(pair.Key, -9f):F3}")),
                    log.ProgressAtVolleyStart.Count > 0 && log.ProgressAtVolleyStart.All(pair => Near(pair.Value, log.ProgressAtVolleyEnd.GetValueOrDefault(pair.Key, -9f), 0.0001f)));
                YvCheck("고정 대상에 4명 · 각 1회", "마리 · 수리야 · 로키 · 펜리르 → " + a.UnitName,
                    string.Join(",", hits.Select(hit => hit.Attacker.UnitName + (hit.DamageContext.HasTag(DamageTag.SummonAttack) ? "(펜리르)" : ""))),
                    hits.Count == 4 && marieHit != null && suryaHit != null && lokiHit != null && fenrirHit != null &&
                    log.Dealt.Where(hit => hit.DamageContext.HasTag(DamageTag.NormalAttack)).All(hit => hit.Target == a));
                YvCheck("일반 공격 · 공격 형태 태그 유지", "사격/사격/베기/베기+소환",
                    $"{Tags(marieHit?.DamageContext)} | {Tags(suryaHit?.DamageContext)} | {Tags(lokiHit?.DamageContext)} | {Tags(fenrirHit?.DamageContext)}",
                    marieHit?.DamageContext.HasTag(DamageTag.Shot) == true && suryaHit?.DamageContext.HasTag(DamageTag.Shot) == true &&
                    lokiHit?.DamageContext.HasTag(DamageTag.Slash) == true && fenrirHit?.DamageContext.HasTag(DamageTag.Slash) == true &&
                    hits.All(hit => hit.DamageContext.CodeType == BaseEnums.CodeType.Normal));
                YvCheck("원시 피해 100% · 컨텍스트 최종 배율 50%", $"{expectMarie}/{expectSurya}/{expectLoki}/{expectFenrir}",
                    $"{marieHit?.DamageContext.Damage}/{suryaHit?.DamageContext.Damage}/{lokiHit?.DamageContext.Damage}/{fenrirHit?.DamageContext.Damage}",
                    marieHit?.DamageContext.Damage == expectMarie && suryaHit?.DamageContext.Damage == expectSurya &&
                    lokiHit?.DamageContext.Damage == expectLoki && fenrirHit?.DamageContext.Damage == expectFenrir &&
                    hits.All(hit => Near(hit.DamageContext.OutgoingDamageMultiplier, .50f)));
                YvCheck("적중 후처리 유지(수리야 표식 +1 · 마리 얼음)", "표식 1 / 얼음 부착",
                    execution.Marks(a) + " / " + log.Elements.Any(entry => entry.Source == marie && entry.Target == a && entry.Element == BaseEnums.UnitElement.Cryo),
                    execution.Marks(a) == 1 && log.Elements.Any(entry => entry.Source == marie && entry.Target == a && entry.Element == BaseEnums.UnitElement.Cryo));
                YvCheck("전원 전달 동안 나폴레옹 시전 유지", "참여자 피해 시점 모두 isCasting",
                    string.Join(",", log.CommanderCastingAtHit), log.CommanderCastingAtHit.Count >= 4 && log.CommanderCastingAtHit.All(value => value));
                YvCheck("일제사격 결과 · 지휘 범위 해제", "4건 · 적중 4 · 취소 없음 · 범위 0",
                    $"{log.VolleyEnds.FirstOrDefault().Attacks}/{log.VolleyEnds.FirstOrDefault().Hits}/{log.VolleyEnds.FirstOrDefault().Cancelled}/{fireCodes.Count(code => code != null && code.IsCommanded)}",
                    log.VolleyEnds.Count == 1 && log.VolleyEnds[0].Attacks == 4 && log.VolleyEnds[0].Hits == 4 && !log.VolleyEnds[0].Cancelled &&
                    fireCodes.All(code => code != null && !code.IsCommanded));
                YvCheck("특수행동 피해는 추격 방아쇠 아님(자유 추격 0)", "추격 0", log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted).ToString(),
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 0);
            }

            // 공격형 분신도 실제 원본 일격을 쏜 뒤 실제 사망한다. 비공격 분신술은 참여하지 않는다.
            clone = Summons.Create(uchi);
            CfNoCrit(uchi);
            bool commandedCloneDied = false;
            Unit commandedClone = clone;
            void CloneDeath(Unit dead, Unit killer) { if (dead == commandedClone) commandedCloneDied = true; }
            Unit.AnyUnitDied += CloneDeath;
            using (var log = new CfLog(new[] { napoleon, uchi, clone }))
            {
                yield return CfRun(s => s.AdvanceAction(napoleon,1), () => log.Did(napoleon,Kind.Special));
                DamageResolvedContext cloneHit = log.Dealt.FirstOrDefault(hit => hit.Attacker == uchi && hit.DamageContext.HasTag(DamageTag.SummonAttack));
                YvCheck("분신 지휘 원본 일격·실제 소멸·별도행동 없음", "소환일격1 / 최종.5 / 실제사망 / 분신 일반0",
                    $"{cloneHit?.DamageContext.OutgoingDamageMultiplier} / {commandedCloneDied} / {log.StartedCount(commandedClone,Kind.Normal)}",
                    cloneHit != null && cloneHit.Target == a && Near(cloneHit.DamageContext.OutgoingDamageMultiplier,.5f) &&
                    commandedCloneDied && log.StartedCount(commandedClone,Kind.Normal)==0 && log.NormalResolved.GetValueOrDefault(commandedClone)==0);
            }
            Unit.AnyUnitDied -= CloneDeath;

            // 실제 세이 N1(고정55 + 방어50%)을 빌려 최종 배율을 공통 HP피해까지 검증한다.
            var flatCode = new SeiGroundLeveling(new NormalCodeContext { Caster=marie });
            int flatRaw = Mathf.Max(1,Mathf.RoundToInt(55f + marie.StatValue(BaseEnums.StatType.Defense)*.5f));
            using (var log = new CfLog(new[] { napoleon,marie }))
            {
                yield return CfRun(s => s.EnqueueSpecial(napoleon,"cf_flat","고정값 포함 지휘",() =>
                { napoleon.isCasting=true; napoleon.StartCoroutine(CfFixtureCommand(flatCode,a,napoleon)); }), () => log.Did(napoleon,Kind.Special));
                DamageResolvedContext hit = log.Dealt.FirstOrDefault(item => item.Attacker==marie && item.DamageContext.HasTag(DamageTag.Spell) && item.DamageContext.HasTag(DamageTag.NormalAttack));
                float armor = a.StatValue(BaseEnums.StatType.Defense);
                float k = UnitStats.ArmorConstant(marie.Level);
                int expectedFlat = Mathf.Max(1,Mathf.RoundToInt(flatRaw*.5f*1.15f*k/(k+armor)));
                YvCheck("고정55 포함 원시100%·최종50%·실제HP피해", $"raw{flatRaw} / scale.5 / damage{expectedFlat}",
                    $"{hit?.DamageContext.Damage} / {hit?.DamageContext.OutgoingDamageMultiplier} / {hit?.DamageDealt}",
                    hit != null && hit.DamageContext.Damage==flatRaw && Near(hit.DamageContext.OutgoingDamageMultiplier,.5f) && hit.DamageDealt==expectedFlat);
            }
            var endedScope = new CommandedAttackScope(napoleon,marie,a);
            endedScope.End();
            var lateContext = CfNormalProbe(marie);
            endedScope.Stamp(lateContext);
            YvCheck("종료된 지휘 토큰의 늦은 컨텍스트 차단", "허용false", CommandedAttackScope.Allows(lateContext).ToString(), !CommandedAttackScope.Allows(lateContext));
            var stoppedCommand = flatCode.BeginCommandedAttack(a,napoleon);
            var stoppedContext = CfNormalProbe(marie);
            stoppedCommand?.Scope.Stamp(stoppedContext);
            flatCode.StopCode();
            YvCheck("참가자 외부Stop은 지휘 범위 즉시 닫음", "범위false / 허용false", $"{flatCode.IsCommanded}/{CommandedAttackScope.Allows(stoppedContext)}",
                !flatCode.IsCommanded && !CommandedAttackScope.Allows(stoppedContext));
            stoppedCommand?.End();

            // 세이의 실제 바위 부착 + 앞선 얼음은 경화 HP쉴드를 만들므로 다음 대상 선정 fixture와 분리한다.
            a.RemoveShield(a.ShieldCurr);
            // ── 6. 수리야 종언탄 실제 대체 ──
            execution.Mark(a, 2);
            YvCheck("종언탄 fixture 지정 대상 확인",a.UnitName,NapoleonCommand.PickTarget(napoleon)?.UnitName,NapoleonCommand.PickTarget(napoleon)==a);
            int expectFinale = Scaled(surya.StatValue(BaseEnums.StatType.Attack), SuryaNameBullet.FinaleRatio);
            using (var log = new CfLog(new[] { napoleon, surya }))
            {
                yield return CfRun(s => s.AdvanceAction(napoleon, 1), () => log.Did(napoleon, Kind.Special));
                DamageResolvedContext finale = log.Dealt.FirstOrDefault(hit => hit.Attacker == surya && hit.Target == a);
                YvCheck("지휘 중 종언탄(표식 2)", $"{expectFinale} · 방어 0.75 · 표식 소모 · 불 부착",
                    $"{finale?.DamageContext.Damage} · {finale?.DamageContext.DefenseStatMultiplier} · {execution.Marks(a)} · {log.Elements.Any(entry => entry.Source == surya && entry.Element == BaseEnums.UnitElement.Pyro)}",
                    finale?.DamageContext.Damage == expectFinale && Near(finale.DamageContext.DefenseStatMultiplier, SuryaNameBullet.DefenseMultiplier) &&
                    execution.Marks(a) == 0 && log.Elements.Any(entry => entry.Source == surya && entry.Target == a && entry.Element == BaseEnums.UnitElement.Pyro));
            }

            // ── 7. 나폴레옹 초월기: 스냅샷 · 갱신 · 행동치 ──
            using (var log = new CfLog(new[] { napoleon }))
            {
                yield return CfRun(s => napoleon.FillUltimateResource(), () => log.Did(napoleon, Kind.Ultimate));
                float ult = CfModifier(surya, NapoleonGrandBattery.BuffKey, effect => effect.OutgoingDamageModifier(surya, a, CfNormalProbe(surya)));
                float ultAdditional = CfModifier(surya, NapoleonGrandBattery.BuffKey, effect => effect.OutgoingDamageModifier(surya, a, CfAdditionalProbe(surya)));
                float aura = CfModifier(surya, NapoleonOrders.AuraKey, effect => effect.OutgoingDamageModifier(surya, a, CfNormalProbe(surya)));
                var advanced = log.OrderAdjusted.Where(entry => entry.Advanced).Select(entry => entry.Unit).ToHashSet();
                var shouldAdvance = AliveAllies().Concat(new[] { fenrir }).ToList();
                YvCheck("U 마나 소모 · 실제 버프 완료", "마나 0 · 완료", napoleon.ManaCurr + " / " + battery.CompletedNormally,
                    napoleon.ManaCurr == 0 && battery.CompletedNormally);
                YvCheck("U 아군 전원 버프 1개 · 피해 없음", "전원 1 / 피해 0",
                    string.Join(",", AliveAllies().Select(unit => unit.GetAllStatuses().Count(s => s.Key == NapoleonGrandBattery.BuffKey))) + " / " + log.Dealt.Count,
                    AliveAllies().All(unit => unit.GetAllStatuses().Count(s => s.Key == NapoleonGrandBattery.BuffKey) == 1) && log.Dealt.Count == 0);
                YvCheck("U 1 + 치피 50% × 30%", "1.15 · 추가행동 1 · 오라와 독립 1.3225",
                    $"{ult:F3} · {ultAdditional:F3} · {ult * aura:F4}",
                    Near(battery.SnapshotBonus, napoleon.StatValue(BaseEnums.StatType.CritDamage) * .30f) && Near(ult, 1.15f) &&
                    Near(ultAdditional, 1f) && Near(ult * aura, 1.15f * 1.15f));
                YvCheck("U 행동 게이지 20% 앞당김(늦춤 없음)", "아군 5 + 펜리르",
                    string.Join(",", advanced.Select(unit => unit.UnitName)) + " / 늦춤 " + log.OrderAdjusted.Count(entry => !entry.Advanced),
                    shouldAdvance.All(advanced.Contains) && log.OrderAdjusted.All(entry => entry.Advanced) && battery.RemainingNormals == 2);
            }
            YvAddTestStatus(napoleon, CfCritId, "verify_cf_crit", new StatModifierEffect(BaseEnums.StatType.CritDamage, 0.5f));
            using (var log = new CfLog(new[] { napoleon }))
                yield return CfRun(s => napoleon.FillUltimateResource(), () => log.Did(napoleon, Kind.Ultimate));
            float raised = battery.SnapshotBonus;
            using (var log = new CfLog(new[] { napoleon }))
                yield return CfRun(s => napoleon.FillUltimateResource(), () => log.Did(napoleon, Kind.Ultimate));
            YvCheck("U 같은 출처 갱신 · 자기 증폭 없음", "치피 100% → 0.30 · 재시전 0.30 · 아군별 1개",
                $"{raised:F3} · {battery.SnapshotBonus:F3} · " + string.Join(",", AliveAllies().Select(unit => unit.GetAllStatuses().Count(s => s.Key == NapoleonGrandBattery.BuffKey))),
                Near(raised, .30f) && Near(battery.SnapshotBonus, .30f) &&
                AliveAllies().All(unit => unit.GetAllStatuses().Count(s => s.Key == NapoleonGrandBattery.BuffKey) == 1));
            napoleon.RemoveStatusByKey("verify_cf_crit");
            float snapshotKept = CfModifier(surya, NapoleonGrandBattery.BuffKey, effect => effect.OutgoingDamageModifier(surya, a, CfNormalProbe(surya)));
            YvCheck("U 스냅샷(치피가 내려도 유지)", "1.30", snapshotKept.ToString("F3"), Near(snapshotKept, 1.30f));

            // ── 8. 지속: 나폴레옹 일반행동 2회, 두 번째 일제사격까지 ──
            using (var log = new CfLog(new[] { napoleon }))
            {
                yield return CfRun(s => s.AdvanceAction(napoleon, 1), () => log.Did(napoleon, Kind.Special));
                YvCheck("U 첫 일반행동 후 유지", "남은 1 · 버프 유지", battery.RemainingNormals + " · " + AliveAllies().Count(unit => unit.HasStatusKey(NapoleonGrandBattery.BuffKey)),
                    battery.RemainingNormals == 1 && AliveAllies().All(unit => unit.HasStatusKey(NapoleonGrandBattery.BuffKey)));
            }
            using (var log = new CfLog(new[] { napoleon }))
            {
                yield return CfRun(s => s.AdvanceAction(napoleon, 1), () => log.Did(napoleon, Kind.Special));
                YvCheck("U 두 번째 일반행동의 일제사격까지 덮은 뒤 해제", "사격 시작 때 전원 · 끝난 뒤 0",
                    log.BuffHoldersAtVolleyStart + " → " + AliveAllies().Count(unit => unit.HasStatusKey(NapoleonGrandBattery.BuffKey)),
                    log.BuffHoldersAtVolleyStart == AliveAllies().Count && AliveAllies().All(unit => !unit.HasStatusKey(NapoleonGrandBattery.BuffKey)) &&
                    battery.RemainingNormals == 0 && !battery.ExpiryDeferred);
            }

            // 지휘 원소/종언탄 후처리 검사가 끝났으므로 해당 검증의 명중 보정은 여기서 해제한다.
            surya.RemoveStatusByKey("verify_cf_hit");
            marie.RemoveStatusByKey("verify_cf_hit");

            // ── 9. 로키 사냥감: 효과 명중 실패 → 행동마다 재시도 → 우선도 최고 하나 ──
            foreach (Unit enemy in AliveEnemies()) enemy.RemoveStatusByKey(twilight.MarkKey);
            foreach (Unit enemy in AliveEnemies())
                YvAddTestStatus(enemy, CfResistId, "verify_cf_resist", new StatModifierEffect(BaseEnums.StatType.SpecialDefense, 100000000f));
            using (var log = new CfLog(new[] { marie }))
            {
                yield return CfRun(s =>
                {
                    s.EnqueueAdditional(marie, "cf_mark_retry_1", "재시도 1", () => { });
                    s.EnqueueAdditional(marie, "cf_mark_retry_2", "재시도 2", () => { });
                }, () => log.Did(marie, Kind.Additional) && log.Finished.Count(entry => entry.Unit == marie) >= 2);
                YvCheck("사냥감 명중 실패 → 행동마다 1회 재시도", "표식 없음 · 실패 시도 = 행동 수(2)",
                    $"{twilight.MarkedTarget?.UnitName ?? "없음"} · {log.Marks.Count(entry => !entry.Success)}/{log.Started.Count}",
                    twilight.MarkedTarget == null && twilight.NeedsMark && log.Marks.Count == log.Started.Count && log.Marks.All(entry => !entry.Success));
            }
            foreach (Unit enemy in AliveEnemies()) enemy.RemoveStatusByKey("verify_cf_resist");
            CfEnsureEffectHit(loki, AliveEnemies());
            Unit top = AliveEnemies().FirstOrDefault(enemy => enemy != a && enemy != b) ?? b;
            YvAddTestStatus(top, CfPriorityId, "verify_cf_priority", new TrioPriority());
            using (var log = new CfLog(new[] { marie }))
            {
                yield return CfRun(s => s.EnqueueAdditional(marie, "cf_mark_retry_3", "재시도 3", () => { }), () => log.Did(marie, Kind.Additional));
                YvCheck("사냥감 = 우선도 최고 하나(체력 무관)", top.UnitName + " · 1개",
                    $"{twilight.MarkedTarget?.UnitName} · {AliveEnemies().Count(enemy => enemy.HasStatusKey(twilight.MarkKey))}",
                    twilight.MarkedTarget == top && AliveEnemies().Count(enemy => enemy.HasStatusKey(twilight.MarkKey)) == 1 &&
                    twilight.MarkKey.Contains(loki.GetEntityId().ToString()));
            }
            if (AliveEnemies().Count >= 3)
            {
                top.RemoveStatusByKey("verify_cf_priority");
                YvAddTestStatus(b, CfPriorityId, "verify_cf_priority", new TrioPriority());
                top.Die(null);
                YvCheck("사냥감 사망 즉시 재배치", b.UnitName, twilight.MarkedTarget?.UnitName, twilight.MarkedTarget == b);
            }
            else YvCheck("사냥감 사망 재배치", "적 3기 이상", AliveEnemies().Count.ToString(), false);
            Unit prey = twilight.MarkedTarget;
            if (prey == null)
            {
                YvCheck("사냥감 확보", "표식 대상", "없음", false);
                CfFinish(wasEnabled);
                yield break;
            }
            float preyAdditional = CfModifier(prey, twilight.MarkKey, effect => effect.ReceivingDamageModifier(prey, CfAdditionalProbe(marie)));
            float preyNormal = CfModifier(prey, twilight.MarkKey, effect => effect.ReceivingDamageModifier(prey, CfNormalProbe(marie)));
            YvCheck("사냥감 아군 직접 추가행동 피해 +15%", "1.15 / 일반 1", $"{preyAdditional:F3} / {preyNormal:F3}",
                Near(preyAdditional, 1.15f) && Near(preyNormal, 1f));

            // ── 10. 펜리르 일반 차례: 로키 명의 · 사냥감 우선 · 구간 초기화 · 자기 재귀 없음 ──
            if (!CfPreyValid(prey, twilight, "10구간")) { CfFinish(wasEnabled); yield break; }
            CfNoCrit(loki);
            loki.AddCombatResource(LokiUnboundWolf.FlexResourceId, -wolf.Flex);
            int expectClaw = Mathf.Max(1, Mathf.RoundToInt(loki.StatValue(BaseEnums.StatType.Attack) * FenrirClaw.AttackRatio));
            using (var log = new CfLog(new[] { loki, fenrir }))
            {
                yield return CfRun(s => s.AdvanceAction(fenrir, 1), () => log.Did(fenrir, Kind.Normal));
                DamageResolvedContext claw = log.Dealt.FirstOrDefault(hit => hit.DamageContext.HasTag(DamageTag.SummonAttack));
                YvCheck("펜리르 일반: 로키 명의 · 사냥감 · 공격력 60%", $"{loki.UnitName} · {prey.UnitName} · {expectClaw} · 일반+소환",
                    $"{claw?.Attacker?.UnitName} · {claw?.Target?.UnitName} · {claw?.DamageContext.Damage} · {Tags(claw?.DamageContext)}",
                    claw != null && claw.Attacker == loki && claw.Target == prey && claw.DamageContext.Damage == expectClaw &&
                    claw.DamageContext.HasTag(DamageTag.NormalAttack) && claw.DamageContext.HasTag(DamageTag.SummonAttack));
                YvCheck("펜리르 자기 공격은 추격 방아쇠 아님 · 구간 0", "추격 0 · 구간 0",
                    log.Hunts.Count + " · " + wolf.WindowUsed, log.Hunts.Count == 0 && wolf.WindowUsed == 0);
            }

            YvAddTestStatus(loki,99978,"verify_cf_conditionalcrit",new CfForcedCritical(prey));
            float baseCrit = loki.StatValue(BaseEnums.StatType.CritDamage);
            float marieCritBonus = marie.StatValue(BaseEnums.StatType.CritDamage)*.40f;
            int critClaw = Mathf.Max(1,Mathf.RoundToInt(Mathf.Max(0,Mathf.RoundToInt(loki.StatValue(BaseEnums.StatType.Attack)*FenrirClaw.AttackRatio))*(1f+baseCrit+.25f)));
            using (var log = new CfLog(new[] { fenrir }))
            {
                yield return CfRun(s=>s.AdvanceAction(fenrir,1),()=>log.Did(fenrir,Kind.Normal));
                var hit=log.Dealt.FirstOrDefault(item=>item.Attacker==loki && item.DamageContext.HasTag(DamageTag.SummonAttack));
                YvCheck("펜리르 일반 치명: 로키 조건부치피·마리 추공오라 제외", critClaw.ToString(),hit?.DamageContext.Damage.ToString(),
                    hit!=null && hit.DamageContext.IsCrit && hit.DamageContext.Damage==critClaw);
            }
            int critBite = Mathf.Max(1,Mathf.RoundToInt(Mathf.Max(0,Mathf.RoundToInt(loki.StatValue(BaseEnums.StatType.Attack)*LokiUnboundWolf.FollowUpRatio))*(1f+baseCrit+.25f+marieCritBonus)));
            using (var log = new CfLog(new[] { marie,fenrir }))
            {
                yield return CfRun(s=>s.EnqueueAdditional(marie,"cf_actorcrit","실제펜리르 추공치명",()=>CfHit(marie,prey,CfAdditionalTags)),
                    ()=>log.Did(marie,Kind.Additional)&&wolf.PendingCount==0);
                var hit=log.Dealt.FirstOrDefault(item=>item.Attacker==loki && item.DamageContext.HasTag(DamageTag.SummonAttack));
                YvCheck("펜리르 추가행동 치명: 로키 효과+실제펜리르 마리오라", $"{critBite} / 마리+.20p",hit?.DamageContext.Damage.ToString(),
                    hit!=null && hit.DamageContext.IsCrit && hit.DamageContext.Damage==critBite && log.StartedCount(fenrir,Kind.Additional)==1);
            }
            // 조건부 치명은 사냥감 한 대상만 강제한다. 앞선 지휘가 쌓은 수리야 종언탄 준비도는 치명 검사의 표적 선정에 섞지 않는다.
            foreach (Unit enemy in AliveEnemies())
            {
                execution.Consume(enemy);
                enemy.RemoveShield(enemy.ShieldCurr);
                if (enemy == prey) enemy.ModifyHp(30000);
                else enemy.RestoreHpOutsideCombat(enemy.HpMax);
            }
            var critParticipants = NapoleonCommand.Participants(napoleon);
            Unit critPick = NapoleonCommand.PickTarget(napoleon);
            YvCheck("지휘 조건부치명 표적 fixture", $"{prey.UnitName}#{prey.GetEntityId()}",
                $"선정 {critPick?.UnitName}#{critPick?.GetEntityId()} / " + string.Join(" | ", AliveEnemies().Select(enemy =>
                    $"{enemy.UnitName}#{enemy.GetEntityId()} HP{enemy.HpCurr} 피해{NapoleonCommand.EstimateVolley(enemy,critParticipants,napoleon)} 점수{NapoleonCommand.KillScore(enemy,critParticipants,napoleon):F2}")),
                critPick == prey);
            using (var log = new CfLog(new[] { napoleon,fenrir }))
            {
                yield return CfRun(s=>s.AdvanceAction(napoleon,1),()=>log.Did(napoleon,Kind.Special));
                var hit=log.Dealt.FirstOrDefault(item=>item.Attacker==loki && item.DamageContext.HasTag(DamageTag.SummonAttack));
                YvCheck("지휘 펜리르 치명: 소유자 효과·최종.5·마리추공오라 제외",critClaw.ToString(),
                    $"{hit?.DamageContext.Damage} / 대상 {hit?.Target?.UnitName}#{hit?.Target?.GetEntityId()} / 치명 {hit?.DamageContext.IsCrit}",
                    hit!=null && hit.Target==prey && hit.DamageContext.IsCrit && hit.DamageContext.Damage==critClaw && Near(hit.DamageContext.OutgoingDamageMultiplier,.5f));
            }
            loki.RemoveStatusByKey("verify_cf_conditionalcrit");
            using(var reset=new CfLog(new[]{fenrir}))
                yield return CfRun(s=>s.AdvanceAction(fenrir,1),()=>reset.Did(fenrir,Kind.Normal));

            // ── 11. 추격: 다단 1회 · 덧붙는/지속/반응 제외 · 차례 사이 3회 상한 · 자기 재귀 없음 ──
            if (!CfPreyValid(prey, twilight, "11구간")) { CfFinish(wasEnabled); yield break; }
            int expectBite = Mathf.Max(1, Mathf.RoundToInt(loki.StatValue(BaseEnums.StatType.Attack) * LokiUnboundWolf.FollowUpRatio));
            using (var log = new CfLog(new[] { loki, fenrir }))
            {
                yield return CfRun(s =>
                {
                    s.EnqueueAdditional(surya, "cf_bonus", "덧붙는 피해", () => CfHit(surya, prey, new List<int> { DamageTag.AdditionalAttack, DamageTag.BonusDamage }));
                    s.EnqueueAdditional(surya, "cf_dot", "지속피해", () => CfHit(surya, prey, new List<int>(), BaseEnums.CodeType.Effect));
                    s.EnqueueAdditional(surya, "cf_reaction", "원소 반응", () => CfHit(surya, prey, new List<int> { DamageTag.ElementalReaction }));
                    s.EnqueueAdditional(marie, "cf_multi", "다단 추가행동", () =>
                    {
                        CfHit(marie, prey, CfAdditionalTags);
                        CfHit(marie, prey, CfAdditionalTags);
                        CfHit(marie, prey, CfAdditionalTags);
                    });
                    s.EnqueueAdditional(surya, "cf_a1", "추가행동 1", () => CfHit(surya, prey, CfAdditionalTags));
                    s.EnqueueAdditional(napoleon, "cf_a2", "추가행동 2", () => CfHit(napoleon, prey, CfAdditionalTags));
                    s.EnqueueAdditional(uchi, "cf_a3", "상한 초과 1", () => CfHit(uchi, prey, CfAdditionalTags));
                    s.EnqueueAdditional(loki, "cf_a4", "상한 초과 2(로키 자신)", () => CfHit(loki, prey, CfAdditionalTags));
                }, () => log.Finished.Count(entry => entry.Kind == Kind.Additional && entry.Unit != fenrir) >= 8 && wolf.PendingCount == 0);
                List<DamageResolvedContext> bites = log.Dealt.Where(hit => hit.Attacker == loki && hit.DamageContext.HasTag(DamageTag.SummonAttack)).ToList();
                YvCheck("추격 수락 3 · 상한 2 · 실행 3", "3 / 2 / 3 (다단 1회 · 덧붙는 · 지속 · 반응 제외)",
                    $"{log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted)} / {log.HuntCount(LokiUnboundWolf.HuntEvent.CapReached)} / {log.HuntCount(LokiUnboundWolf.HuntEvent.Fired)}",
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 3 && log.HuntCount(LokiUnboundWolf.HuntEvent.CapReached) == 2 &&
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Fired) == 3 && wolf.WindowUsed == 3);
                YvCheck("추격 피해: 로키 공격력 55% · 추가행동 · 소환 · 찌르기", $"{expectBite} × 3",
                    string.Join(",", bites.Select(hit => hit.DamageContext.Damage)) + " | " + Tags(bites.FirstOrDefault()?.DamageContext),
                    bites.Count == 3 && bites.All(hit => hit.DamageContext.Damage == expectBite && hit.Target == prey &&
                        hit.DamageContext.HasTag(DamageTag.AdditionalAttack) && hit.DamageContext.HasTag(DamageTag.Pierce)));
                YvCheck("펜리르 추격은 재귀하지 않음", "펜리르 추가행동 3", log.StartedCount(fenrir, Kind.Additional).ToString(),
                    log.StartedCount(fenrir, Kind.Additional) == 3);
            }
            int huntsOutside = 0;
            void CountOutside(Unit owner, Unit target, LokiUnboundWolf.HuntEvent kind, bool flex) => huntsOutside++;
            LokiUnboundWolf.AnyHunt += CountOutside;
            CfHit(marie, prey, CfAdditionalTags);
            LokiUnboundWolf.AnyHunt -= CountOutside;
            YvCheck("행동 밖 탐침은 방아쇠 아님", "0", huntsOutside.ToString(), huntsOutside == 0);

            using (var log = new CfLog(new[] { fenrir }))
                yield return CfRun(s => s.AdvanceAction(fenrir, 1), () => log.Did(fenrir, Kind.Normal));
            YvCheck("펜리르 일반 시작에 구간 초기화", "0", wolf.WindowUsed.ToString(), wolf.WindowUsed == 0);
            using (var log = new CfLog(new[] { marie, fenrir }))
            {
                yield return CfRun(s => s.EnqueueAdditional(marie, "cf_after_reset", "초기화 뒤 추가행동", () => CfHit(marie, prey, CfAdditionalTags)),
                    () => log.Did(marie, Kind.Additional) && wolf.PendingCount == 0);
                YvCheck("초기화 뒤 다시 추격", "수락 1 · 실행 1", log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) + " · " + log.HuntCount(LokiUnboundWolf.HuntEvent.Fired),
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 1 && log.HuntCount(LokiUnboundWolf.HuntEvent.Fired) == 1 && wolf.WindowUsed == 1);
            }

            // ── 12. 로키 초월기 · 자유 추격 ──
            if (!CfPreyValid(prey, twilight, "12구간")) { CfFinish(wasEnabled); yield break; }
            using (var log = new CfLog(new[] { loki, fenrir }))
            {
                yield return CfRun(s => loki.FillUltimateResource(), () => log.Did(loki, Kind.Ultimate) && wolf.PendingCount == 0);
                DamageResolvedContext ultHit = log.Dealt.FirstOrDefault(hit => hit.Attacker == loki && hit.DamageContext.HasTag(DamageTag.UltAttack));
                int expectUlt = Mathf.Max(1, Mathf.RoundToInt(loki.StatValue(BaseEnums.StatType.Attack) * LokiTwilight.AttackRatio));
                UnitStatus wound = prey.GetAllStatuses().FirstOrDefault(status => status.Key == twilight.WoundKey);
                float heal = CfModifier(prey, twilight.WoundKey, effect => effect.HealingReceivedMultiplierModifier(prey));
                YvCheck("로키 U 사냥감 160% · 완료 · 마나 0", $"{prey.UnitName} · {expectUlt} · 완료 · 0",
                    $"{ultHit?.Target?.UnitName} · {ultHit?.DamageContext.Damage} · {twilight.CompletedNormally} · {loki.ManaCurr}",
                    ultHit != null && ultHit.Target == prey && ultHit.DamageContext.Damage == expectUlt && twilight.CompletedNormally && loki.ManaCurr == 0);
                YvCheck("로키 U 받는 치유 -30% · 대상 일반행동 2회", "0.7 · 일반행동 기간 · 2",
                    $"{heal:F2} · {wound?.NormalActionDuration} · {wound?.RemainingTurns}",
                    wound != null && Near(heal, .7f) && wound.NormalActionDuration && wound.RemainingTurns == LokiTwilight.WoundNormals);
                YvCheck("로키 U 자유 추격 2 · U 자신은 방아쇠 아님", "2 · 추격 0", wolf.Flex + " · " + log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted),
                    wolf.Flex == LokiUnboundWolf.FlexMax && log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 0);
            }
            YvAddTestStatus(prey, CfPriorityId, "verify_cf_priority", new TrioPriority());
            using (var log = new CfLog(new[] { marie, fenrir }))
            {
                yield return CfRun(s => s.AdvanceAction(marie, 1), () => log.Did(marie, Kind.Normal) && wolf.PendingCount == 0);
                YvCheck("자유 추격은 일반행동 피해도 연다", "수락(자유) 1 · 실행 1 · 자유 1 남음",
                    $"{log.Hunts.Count(entry => entry.Kind == LokiUnboundWolf.HuntEvent.Accepted && entry.Flex)} · {log.HuntCount(LokiUnboundWolf.HuntEvent.Fired)} · {wolf.Flex}",
                    log.Hunts.Count(entry => entry.Kind == LokiUnboundWolf.HuntEvent.Accepted && entry.Flex) == 1 &&
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Fired) == 1 && wolf.Flex == 1);
            }
            using (var log = new CfLog(new[] { surya, fenrir }))
            {
                yield return CfRun(s => s.EnqueueAdditional(surya, "cf_flex_first", "자유 우선", () =>
                {
                    CfHit(surya, prey, CfAdditionalTags);
                    CfHit(surya, prey, CfAdditionalTags);
                }), () => log.Did(surya, Kind.Additional) && wolf.PendingCount == 0);
                YvCheck("추가행동이어도 자유 추격 먼저 · 이중 사용 없음", "수락 1(자유) · 자유 0 · 구간 3",
                    $"{log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted)}({log.Hunts.FirstOrDefault().Flex}) · {wolf.Flex} · {wolf.WindowUsed}",
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 1 && log.Hunts.First(entry => entry.Kind == LokiUnboundWolf.HuntEvent.Accepted).Flex &&
                    wolf.Flex == 0 && wolf.WindowUsed == 3);
            }
            loki.AddCombatResource(LokiUnboundWolf.FlexResourceId, 1);
            using (var log = new CfLog(new[] { loki, fenrir }))
            {
                yield return CfRun(s => loki.FillUltimateResource(), () => log.Did(loki, Kind.Ultimate) && wolf.PendingCount == 0);
                YvCheck("자유 추격 갱신 상한(1 → 2, 3 아님) · 상한 중 자유 보존", "2 · 상한 1",
                    wolf.Flex + " · " + log.HuntCount(LokiUnboundWolf.HuntEvent.CapReached),
                    wolf.Flex == LokiUnboundWolf.FlexMax && log.HuntCount(LokiUnboundWolf.HuntEvent.CapReached) == 1 &&
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 0);
            }
            using (var log = new CfLog(new[] { marie, fenrir }))
            {
                yield return CfRun(s => s.AdvanceAction(marie, 1), () => log.Did(marie, Kind.Normal) && wolf.PendingCount == 0);
                YvCheck("자유 추격도 차례 사이 3회 상한", "상한 1 · 수락 0 · 자유 2 유지",
                    $"{log.HuntCount(LokiUnboundWolf.HuntEvent.CapReached)} · {log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted)} · {wolf.Flex}",
                    log.HuntCount(LokiUnboundWolf.HuntEvent.CapReached) == 1 && log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 0 && wolf.Flex == 2);
            }

            // ── 13. 취소된 예약의 반환 ──
            if (!CfPreyValid(prey, twilight, "13구간")) { CfFinish(wasEnabled); yield break; }
            using (var log = new CfLog(new[] { fenrir }))
                yield return CfRun(s => s.AdvanceAction(fenrir, 1), () => log.Did(fenrir, Kind.Normal));
            using (var log = new CfLog(new[] { marie, fenrir }))
            {
                yield return CfRun(s => s.EnqueueAdditional(marie, "cf_cancel", "취소 검증", () =>
                {
                    CfHit(marie, prey, CfAdditionalTags);
                    fenrir.isControlled = true;
                }), () => log.Did(marie, Kind.Additional) && wolf.PendingCount == 0);
                fenrir.isControlled = false;
                YvCheck("실행 전 취소 → 자유 · 구간 반환", "수락 1 · 취소 1 · 실행 0 · 자유 2 · 구간 0",
                    $"{log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted)} · {log.HuntCount(LokiUnboundWolf.HuntEvent.Cancelled)} · {log.HuntCount(LokiUnboundWolf.HuntEvent.Fired)} · {wolf.Flex} · {wolf.WindowUsed}",
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 1 && log.HuntCount(LokiUnboundWolf.HuntEvent.Cancelled) == 1 &&
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Fired) == 0 && wolf.Flex == 2 && wolf.WindowUsed == 0);
            }

            // ── 14. 정지 · 재개: 표식 · 자원 · 구독 · 소환 규칙 정리 ──
            wolf.StopCode();
            YvCheck("로키 정지 정리", "구독 해제 · 표식 0 · 자유 0 · 소환 규칙 해제",
                $"{wolf.Subscribed} · {AliveEnemies().Count(enemy => enemy.HasStatusKey(twilight.MarkKey))} · {wolf.Flex} · {Summons.IsSummoner(loki)}",
                !wolf.Subscribed && AliveEnemies().All(enemy => !enemy.HasStatusKey(twilight.MarkKey)) && wolf.Flex == 0 &&
                !Summons.IsSummoner(loki) && twilight.MarkedTarget == null && !twilight.NeedsMark);
            using (var log = new CfLog(new[] { marie }))
            {
                yield return CfRun(s => s.EnqueueAdditional(marie, "cf_stopped", "정지 중 추가행동", () => CfHit(marie, prey, CfAdditionalTags)),
                    () => log.Did(marie, Kind.Additional));
                YvCheck("정지 중 방아쇠 · 재표식 없음", "추격 0 · 표식 시도 0", log.Hunts.Count + " · " + log.Marks.Count, log.Hunts.Count == 0 && log.Marks.Count == 0);
            }
            Unit retiredFenrir = fenrir;
            wolf.CastCode();
            fenrir = wolf.Fenrir;
            YvCheck("로키 재개 — 새 펜리르 · 표식 복구", "펜리르 1기(새 개체) · 표식 1",
                $"{Summons.Of(loki).Count} · {fenrir != retiredFenrir} · {AliveEnemies().Count(enemy => enemy.HasStatusKey(twilight.MarkKey))}",
                Summons.Of(loki).Count == 1 && fenrir != null && fenrir != retiredFenrir && wolf.Subscribed && twilight.MarkedTarget != null);

            // ── 15. 펜리르 사망: 남은 예약 반환 · 고아 자원 없음 ──
            prey = twilight.MarkedTarget;
            using (var log = new CfLog(new[] { marie }))
            {
                yield return CfRun(s => s.EnqueueAdditional(marie, "cf_fenrir_death", "펜리르 사망", () =>
                {
                    CfHit(marie, prey, CfAdditionalTags);
                    fenrir.Die(null);
                }), () => log.Did(marie, Kind.Additional) && wolf.PendingCount == 0);
                YvCheck("펜리르 사망 정리", "수락 1 · 취소 1 · 대기 0 · 소환 0 · 지휘 참여 제외",
                    $"{log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted)} · {log.HuntCount(LokiUnboundWolf.HuntEvent.Cancelled)} · {wolf.PendingCount} · {Summons.Of(loki).Count}",
                    log.HuntCount(LokiUnboundWolf.HuntEvent.Accepted) == 1 && log.HuntCount(LokiUnboundWolf.HuntEvent.Cancelled) == 1 &&
                    wolf.PendingCount == 0 && Summons.Of(loki).Count == 0 && wolf.Fenrir == null &&
                    !NapoleonCommand.Participants(napoleon).Any(unit => unit.SummonKind == LokiUnboundWolf.FenrirKind));
            }

            // ── 16. 사망 정리: 나폴레옹(오라 · 버프) · 로키(표식 · 구독) ──
            using (var log = new CfLog(new[] { napoleon }))
                yield return CfRun(s => napoleon.FillUltimateResource(), () => log.Did(napoleon, Kind.Ultimate));
            napoleon.Die(null);
            YvCheck("나폴레옹 사망 → 오라 · 초월기 버프 해제", "보유 아군 0",
                AliveAllies().Count(unit => unit.HasStatusKey(NapoleonOrders.AuraKey) || unit.HasStatusKey(NapoleonGrandBattery.BuffKey)).ToString(),
                AliveAllies().All(unit => !unit.HasStatusKey(NapoleonOrders.AuraKey) && !unit.HasStatusKey(NapoleonGrandBattery.BuffKey)));
            marie.TryGrantRewardCode(540);
            marie.CastPassiveCode();
            float chargeBeforeReset = marie.ActiveRewardInstance(540)?.charge ?? -1;
            wolf.StopCode();
            wolf.CastCode();
            fenrir = wolf.Fenrir;
            YvCheck("펜리르 정지·재생성은 축전기 사망 충전 없음", chargeBeforeReset.ToString(),
                marie.ActiveRewardInstance(540)?.charge.ToString(),
                Near(marie.ActiveRewardInstance(540)?.charge ?? -2, chargeBeforeReset) && fenrir != null);
            float chargeBeforeDeath = marie.ActiveRewardInstance(540)?.charge ?? -1;
            bool actualFenrirDeath = false;
            Unit deathFixture = fenrir;
            void WatchFenrirDeath(Unit dead, Unit killer) { if (dead == deathFixture) actualFenrirDeath = true; }
            Unit.AnyUnitDied += WatchFenrirDeath;
            loki.Die(null);
            Unit.AnyUnitDied -= WatchFenrirDeath;
            float chargeAfterDeath = marie.ActiveRewardInstance(540)?.charge ?? -2;
            YvCheck("로키 사망→펜리르 실제사망·축전기 0.25", "실제펜리르사망 / 저장+1.25(본체1+소환수.25)",
                $"{actualFenrirDeath} / {chargeBeforeDeath}->{chargeAfterDeath}", actualFenrirDeath && Near(chargeAfterDeath-chargeBeforeDeath,1.25f));
            YvCheck("로키 사망 → 표식 · 구독 해제", "표식 0 · 구독 해제",
                $"{foes.Count(enemy => enemy != null && enemy.HasStatusKey(twilight.MarkKey))} · {wolf.Subscribed} / " + string.Join(",",foes.Where(enemy=>enemy!=null&&enemy.HasStatusKey(twilight.MarkKey)).Select(enemy=>$"{enemy.UnitName}:active={enemy.isActive},hp={enemy.HpCurr},key={twilight.MarkKey}")),
                foes.All(enemy => enemy == null || !enemy.HasStatusKey(twilight.MarkKey)) && !wolf.Subscribed);

            yield return CaptureStill("characters_followup_core");
            CfFinish(wasEnabled);
        }

        // ══════════════════════════════════════════════════════════
        // characters_followup_melee
        // ══════════════════════════════════════════════════════════

        private IEnumerator ScenarioCharactersFollowupMelee(VerifyScenarioRequest config)
        {
            _yvFailures.Clear();
            _cfActionIdSequence = 0;
            bool wasEnabled = true;
            yield return YvPrepareScene(config, enabled => wasEnabled = enabled);
            Unit napoleon = CfAlly(CfNapoleonId), shi = CfAlly(ShiId), sabah = CfAlly(SabahId),
                bastet = CfAlly(BastetId), jean = CfAlly(JeanId);
            List<Unit> foes = AliveEnemies();
            if (napoleon == null || shi == null || sabah == null || bastet == null || jean == null || foes.Count == 0)
            {
                YvCheck("편성 party5_command_melee", "161 · 2 · 3 · 122 · 7",
                    string.Join(",", AliveAllies().Select(unit => unit.ID)), false);
                CfFinish(wasEnabled);
                yield break;
            }
            var volley = napoleon.ActiveSpecialCode as NapoleonGrandVolley;
            var sabahCode = sabah.ActiveNormalCode as SabahLightningSlash;
            var shiCode = shi.ActiveNormalCode as ShiGhostSlash;
            Unit[] melee = { shi, sabah, bastet, jean };
            List<Unit> participants = NapoleonCommand.Participants(napoleon);
            YvCheck("근접 지휘 참여자", "시 · 사바흐 · 바스테트 · 잔", string.Join(",", participants.Select(unit => unit.UnitName)),
                participants.Count == 4 && melee.All(participants.Contains));
            foreach (Unit unit in melee) CfNoCrit(unit);
            foreach (Unit enemy in foes) enemy.RestoreHpOutsideCombat(enemy.HpMax);
            Unit a = foes[0];
            a.ModifyHp(60000);

            // 사바흐 박자: 실제 일반행동 두 번 뒤 다음은 번개 베기(대체행동) 차례
            for (int i = 0; i < 2; i++)
                using (var log = new CfLog(new[] { sabah }))
                    yield return CfRun(s => s.AdvanceAction(sabah, 1), () => log.Did(sabah, Kind.Normal));
            bool substituteBefore = sabahCode != null && sabahCode.IsSubstituteAction;
            string sabahNameBefore = sabahCode?.CodeName;
            shi.AddCombatResource(ShiFinalBell.ResourceId, 2 - shi.GetCombatResource(ShiFinalBell.ResourceId));
            foreach (Unit unit in AliveAllies()) unit.ClearUltimateResource();
            var meleeMana = melee.ToDictionary(unit => unit, unit => unit.ManaCurr);
            int volleysBefore = volley?.VolleysPerformed ?? 0;
            int expectShi = Mathf.Max(1, Mathf.RoundToInt(shi.StatValue(BaseEnums.StatType.Attack) * ShiGhostSlash.AttackRatio));
            using (var log = new CfLog(new[] { napoleon, shi, sabah, bastet, jean }))
            {
                yield return CfRun(s => s.AdvanceAction(napoleon, 1), () => log.Did(napoleon, Kind.Special));
                List<DamageResolvedContext> normals = log.Dealt.Where(hit => hit.DamageContext.HasTag(DamageTag.NormalAttack)).ToList();
                DamageResolvedContext shiHit = normals.FirstOrDefault(hit => hit.Attacker == shi);
                List<DamageResolvedContext> sabahHits = normals.Where(hit => hit.Attacker == sabah).ToList();
                YvCheck("다단 · 부가 피해가 있어도 나폴레옹 특수 1회", "SP 1 · 일제사격 1 · 전체 피해 ≥ 4",
                    $"{log.StartedCount(napoleon, Kind.Special)} · {(volley?.VolleysPerformed ?? 0) - volleysBefore} · {log.Dealt.Count}",
                    log.StartedCount(napoleon, Kind.Special) == 1 && (volley?.VolleysPerformed ?? 0) - volleysBefore == 1 && log.Dealt.Count >= 4);
                YvCheck("근접 참여자 일반행동 · 마나 · 턴 0", "시작 0 · 완료 0 · 마나 변화 0",
                    melee.Sum(unit => log.StartedCount(unit, Kind.Normal)) + " · " + melee.Sum(unit => log.NormalResolved.GetValueOrDefault(unit)) + " · " + melee.Sum(unit => unit.ManaCurr - meleeMana[unit]),
                    melee.All(unit => log.StartedCount(unit, Kind.Normal) == 0 && log.NormalResolved.GetValueOrDefault(unit) == 0 && unit.ManaCurr == meleeMana[unit]) &&
                    log.Started.All(entry => entry.Unit == napoleon));
                YvCheck("근접 접촉 태그 유지(바스테트 · 잔 · 사바흐)", "접촉 · 타격/베기",
                    string.Join(" | ", normals.Where(hit => hit.Attacker != shi).Select(hit => hit.Attacker.UnitName + ":" + Tags(hit.DamageContext))),
                    normals.Where(hit => hit.Attacker == bastet || hit.Attacker == jean).All(hit => hit.DamageContext.HasTag(DamageTag.ContactAttack) && hit.DamageContext.HasTag(DamageTag.Strike)) &&
                    normals.Any(hit => hit.Attacker == bastet) && normals.Any(hit => hit.Attacker == jean));
                YvCheck("사바흐 대체행동 차례여도 단일 검격 · 박자 보존", "단일 1회 · 대체 차례 유지 · 이름 복구",
                    $"{sabahHits.Count}({Tags(sabahHits.FirstOrDefault()?.DamageContext)}) · {sabahCode?.IsSubstituteAction} · {sabahCode?.CodeName}",
                    substituteBefore && sabahHits.Count == 1 && sabahHits[0].DamageContext.HasTag(DamageTag.SingleTarget) &&
                    sabahCode.IsSubstituteAction && sabahCode.CodeName == sabahNameBefore);
                YvCheck("시 멸참 실제 경로(원시100% · 최종50% · 만종 누적3)", $"{expectShi} · 만종 3",
                    $"{shiHit?.DamageContext.Damage} · {shi.GetCombatResource(ShiFinalBell.ResourceId)}",
                    shiHit != null && shiHit.DamageContext.Damage == expectShi && Near(shiHit.DamageContext.OutgoingDamageMultiplier,.5f) && shi.GetCombatResource(ShiFinalBell.ResourceId) == 3);
                YvCheck("근접 전달 동안 나폴레옹 시전 유지 · 범위 해제", "모두 isCasting · 범위 0",
                    string.Join(",", log.CommanderCastingAtHit) + " · " + melee.Count(unit => (unit.ActiveNormalCode as BaseNormalCode)?.IsCommanded == true),
                    log.CommanderCastingAtHit.Count >= 4 && log.CommanderCastingAtHit.All(value => value) &&
                    melee.All(unit => (unit.ActiveNormalCode as BaseNormalCode)?.IsCommanded == false));
            }

            // 지휘 범위가 다음 실제 일반행동으로 새지 않는다.
            shi.ClearUltimateResource();
            int shiManaBefore = shi.ManaCurr;
            // 상시형 자원(만종)은 행동 마나를 받지 않는다. 마나형일 때만 +20을 기대한다.
            int expectShiMana = shi.UltimateResourceType == BaseEnums.UltimateResourceType.Mana
                ? Mathf.Min(shi.ManaMax, shiManaBefore + Mathf.RoundToInt(Unit.ManaPerNormalAction * shi.ManaEfficiencyCurr))
                : shiManaBefore;
            int expectShiFull = Mathf.Max(1, Mathf.RoundToInt(shi.StatValue(BaseEnums.StatType.Attack) * ShiGhostSlash.AttackRatio));
            using (var log = new CfLog(new[] { shi }))
            {
                yield return CfRun(s => s.AdvanceAction(shi, 1), () => log.Did(shi, Kind.Normal));
                DamageResolvedContext own = log.Dealt.FirstOrDefault(hit => hit.Attacker == shi && hit.DamageContext.HasTag(DamageTag.NormalAttack));
                YvCheck("다음 실제 일반행동은 원래 100% · 일반행동 마나", $"{expectShiFull} · 완료 1 · 마나 {expectShiMana}",
                    $"{own?.DamageContext.Damage} · {log.NormalResolved.GetValueOrDefault(shi)} · {shi.ManaCurr}",
                    own != null && own.DamageContext.Damage == expectShiFull && log.NormalResolved.GetValueOrDefault(shi) == 1 &&
                    shi.ManaCurr == expectShiMana);
            }

            // 나폴레옹이 일제사격 도중 쓰러지면 남은 판정을 취소하고 대상을 바꾸지 않는다.
            using (var log = new CfLog(new[] { napoleon, shi, sabah, bastet, jean }))
            {
                bool killed = false;
                log.OnHit = hit =>
                {
                    if (killed || hit.Attacker == napoleon || !melee.Contains(hit.Attacker) || CommandedAttackScope.Of(hit.DamageContext) == null) return;
                    killed = true;
                    napoleon.Die(null);
                };
                yield return CfRun(s => s.AdvanceAction(napoleon, 1), () => log.VolleyEnds.Count > 0, 25f);
                (Unit Target, int Attacks, int Hits, bool Cancelled) end = log.VolleyEnds.FirstOrDefault();
                // 사망 뒤 스케줄러가 여는 다른 유닛의 정상 차례는 이 지휘의 재표적이 아니다.
                List<DamageResolvedContext> commandedHits = log.Dealt.Where(hit => volley.LastScopes.Contains(CommandedAttackScope.Of(hit.DamageContext))).ToList();
                List<Unit> struck = commandedHits.Select(hit => hit.Target).Distinct().ToList();
                YvCheck("지휘자 사망 → 남은 판정 취소 · 재표적 없음 · 범위 해제", "취소 · 적중 < 지휘 건수 · 대상 1 · 범위 0",
                    $"{end.Cancelled} · {end.Hits}/{end.Attacks} · {struck.Count} · {melee.Count(unit => (unit.ActiveNormalCode as BaseNormalCode)?.IsCommanded == true)}",
                    killed && end.Cancelled && end.Hits < end.Attacks && struck.Count == 1 && commandedHits.Count == end.Hits &&
                    melee.All(unit => (unit.ActiveNormalCode as BaseNormalCode)?.IsCommanded == false));
            }

            yield return CaptureStill("characters_followup_melee");
            CfFinish(wasEnabled);
        }
    }
}
#endif
