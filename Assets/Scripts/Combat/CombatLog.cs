using System.Collections.Generic;
using BaseClasses;
using Entities;
using Entities.Status;
using Managers;

namespace Combat
{
    /// <summary>
    /// 한 판의 전투 기록. 결과 화면의 [전투 로그]가 읽는다.
    ///
    /// 전투는 자동이라 플레이어가 할 수 있는 일은 편성을 고치는 것뿐인데, 무엇 때문에 졌는지 모르면 무엇을
    /// 고칠지도 모른다. 콘솔 로그는 개발자용이고 다른 로그에 섞여 흘러간다. 여기에는 <b>플레이어가 읽을 말로</b>
    /// 행동 · 피해 · 쓰러짐 · 반응 · 회피 · 해로운 상태만 순서대로 모은다.
    ///
    /// 전부 기존 전역 이벤트를 듣기만 한다 — 게임 상태를 바꾸지 않는다.
    /// </summary>
    public static class CombatLog
    {
        public enum Kind
        {
            Info,
            Action,
            Damage,
            Defeat,
            Reaction,
            Evade,
            Status,
        }

        public readonly struct Entry
        {
            public readonly int Turn;
            public readonly Kind Kind;
            public readonly string Text;

            /// <summary>이 줄의 주인공이 아군인가. 줄 색을 가른다.</summary>
            public readonly bool Ally;

            /// <summary>아군이 쓰러진 줄처럼 원인을 찾을 때 먼저 봐야 하는 줄.</summary>
            public readonly bool Important;

            public Entry(int turn, Kind kind, string text, bool ally, bool important)
            {
                Turn = turn;
                Kind = kind;
                Text = text;
                Ally = ally;
                Important = important;
            }
        }

        /// <summary>한 판에 쌓는 줄의 상한. 지속 피해가 많은 긴 판에서도 메모리가 늘지 않게 한다.</summary>
        public const int MaxEntries = 3000;

        private static readonly List<Entry> Entries = new();
        private static bool _subscribed;
        private static int _dropped;

        /// <summary>아군별 받은 피해 합계(이름 → 양). 원인 요약이 "누가 가장 많이 맞았나"를 읽는다.</summary>
        public static readonly Dictionary<string, long> AllyDamageTaken = new();

        /// <summary>적별로 아군에게 넣은 피해 합계. 원인 요약이 "누가 가장 위협적이었나"를 읽는다.</summary>
        public static readonly Dictionary<string, long> EnemyThreat = new();

        public static bool Recording { get; private set; }
        public static IReadOnlyList<Entry> Current => Entries;

        /// <summary>상한을 넘겨 버린 줄 수. 0이 아니면 화면이 앞부분이 잘렸다고 알린다.</summary>
        public static int Dropped => _dropped;

        public static void Begin(IEnumerable<Unit> party)
        {
            if (!_subscribed)
            {
                ActionScheduler.AnyActionStarted += OnAction;
                Unit.AnyDamageDealt += OnDamage;
                Unit.AnyUnitDied += OnDied;
                Unit.AnyElementalReaction += OnReaction;
                Unit.AnyHitNullified += OnNullified;
                Unit.AnyEvaded += OnEvaded;
                Unit.AnyNegativeStatusGranted += OnNegativeStatus;
                _subscribed = true;
            }

            Entries.Clear();
            AllyDamageTaken.Clear();
            EnemyThreat.Clear();
            _dropped = 0;
            Recording = true;

            var names = new List<string>();
            if (party != null)
                foreach (Unit unit in party)
                    if (unit != null) names.Add(unit.UnitName);
            Add(Kind.Info, names.Count > 0 ? $"전투 시작 — 출전 {string.Join(", ", names)}" : "전투 시작", true, false);
        }

        /// <summary>전투 종료. 끝난 이유를 마지막 줄로 남기고 기록을 멈춘다.</summary>
        public static void End(bool victory, string reason)
        {
            if (!Recording) return;
            Add(Kind.Info, $"전투 종료 — {(victory ? "승리" : "패배")} · {reason}", victory, !victory);
            Recording = false;
        }

        // ── 수신 ─────────────────────────────────────────────────────

        private static void OnAction(Unit unit, ActionScheduler.ActionKind kind, string label)
        {
            if (!Recording || unit == null) return;

            string codeName = kind switch
            {
                ActionScheduler.ActionKind.Normal => DisplayName(unit.ActiveNormalCode, label),
                ActionScheduler.ActionKind.Ultimate => DisplayName(unit.ActiveUltimateCode, label),
                _ => string.IsNullOrWhiteSpace(label) ? "" : label,
            };
            string kindName = kind switch
            {
                ActionScheduler.ActionKind.Normal => "일반행동",
                ActionScheduler.ActionKind.Ultimate => "궁극기",
                ActionScheduler.ActionKind.Special => "특수행동",
                ActionScheduler.ActionKind.Coordinated => "협동행동",
                _ => "추가행동",
            };

            string detail = string.IsNullOrEmpty(codeName) || codeName == kindName
                ? kindName
                : $"{kindName} 「{codeName}」";
            Add(Kind.Action, $"{unit.UnitName} — {detail}", !unit.IsEnemy,
                kind == ActionScheduler.ActionKind.Ultimate);
        }

        private static void OnDamage(DamageResolvedContext context)
        {
            if (!Recording || context == null || context.DamageDealt <= 0) return;

            Unit attacker = context.Attacker;
            Unit target = context.Target;
            DamageContext damage = context.DamageContext;
            bool dot = damage != null && damage.CodeType == BaseEnums.CodeType.Effect;
            bool crit = damage != null && damage.IsCrit;

            string tags = (crit ? " · 치명타" : "") + (dot ? " · 지속 피해" : "");
            string from = attacker != null ? attacker.UnitName : "?";
            string to = target != null ? target.UnitName : "?";
            string hp = target != null ? $"  (남은 체력 {System.Math.Max(0, target.HpCurr):N0}/{target.HpMax:N0})" : "";

            // 아군이 맞은 줄은 원인 추적에 중요하다. 한 방에 최대 체력의 30% 이상이면 강조한다.
            bool allyHit = target != null && !target.IsEnemy;
            if (allyHit)
            {
                AllyDamageTaken[to] = AllyDamageTaken.GetValueOrDefault(to) + context.DamageDealt;
                if (attacker != null && attacker.IsEnemy)
                    EnemyThreat[from] = EnemyThreat.GetValueOrDefault(from) + context.DamageDealt;
            }
            bool heavy = allyHit && target.HpMax > 0 && context.DamageDealt >= target.HpMax * 0.3f;
            Add(Kind.Damage, $"{from} → {to}  {context.DamageDealt:N0} 피해{tags}{hp}",
                attacker != null && !attacker.IsEnemy, heavy);
        }

        private static void OnDied(Unit unit, Unit killer)
        {
            if (!Recording || unit == null) return;
            string by = killer != null && killer != unit ? $" ({killer.UnitName}의 공격)" : "";
            bool ally = !unit.IsEnemy;
            Add(Kind.Defeat, ally ? $"아군 {unit.UnitName} 전투 불능{by}" : $"적 {unit.UnitName} 처치{by}",
                !ally, ally);
        }

        private static void OnReaction(Unit source, Unit target, string reaction)
        {
            if (!Recording) return;
            Add(Kind.Reaction, $"원소 반응 「{reaction}」 — {Name(source)} → {Name(target)}",
                source != null && !source.IsEnemy, false);
        }

        private static void OnNullified(Unit attacker, Unit target)
        {
            if (!Recording) return;
            Add(Kind.Evade, $"{Name(target)}이(가) {Name(attacker)}의 타격을 무효화",
                target != null && !target.IsEnemy, false);
        }

        private static void OnEvaded(Unit attacker, Unit target)
        {
            if (!Recording) return;
            Add(Kind.Evade, $"{Name(target)}이(가) {Name(attacker)}의 공격을 회피",
                target != null && !target.IsEnemy, false);
        }

        private static void OnNegativeStatus(Unit grantor, Unit target, UnitStatus status)
        {
            if (!Recording || status == null) return;
            string turns = status.Duration > 0 ? $" {status.Duration}턴" : "";
            bool ally = target != null && !target.IsEnemy;
            Add(Kind.Status, $"{Name(target)}에게 「{status.StatusName}」{turns}" +
                             (grantor != null && grantor != target ? $" ({grantor.UnitName})" : ""),
                !ally, false);
        }

        // ── 내부 ─────────────────────────────────────────────────────

        private static void Add(Kind kind, string text, bool ally, bool important)
        {
            if (Entries.Count >= MaxEntries)
            {
                // 뒤쪽(패배 직전)이 원인에 더 가깝다. 앞에서부터 버린다.
                Entries.RemoveAt(1);
                _dropped++;
            }

            int turn = GameManager.Instance?.ActionScheduler?.TurnsTaken ?? 0;
            Entries.Add(new Entry(turn, kind, text, ally, important));
        }

        private static string Name(Unit unit) => unit != null ? unit.UnitName : "?";

        private static string DisplayName(Codes.Base.Code code, string fallback)
        {
            string verbal = code != null ? Codes.Base.CodeCatalog.Find(code)?.verbalName : null;
            if (!string.IsNullOrWhiteSpace(verbal)) return verbal;
            return string.IsNullOrWhiteSpace(fallback) ? "" : fallback;
        }
    }
}
