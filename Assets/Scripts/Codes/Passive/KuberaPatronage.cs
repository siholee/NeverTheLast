using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;

namespace Codes.Passive
{
    /// <summary>
    /// 쿠베라 — 특성 「재보천의 후원」(Detail_08 §11.1). 필드에 있는 동안
    ///   · 후원 — 아군 소환수가 주는 피해 +20%(소환수 판정을 받는 아군 포함, 소환 방식 무관)
    ///   · 보물 — 아군이 소환수를 생성할 때마다 보물 +1(쿠베라 초월기가 만든 소환수는 세지 않는다)
    ///   · 이자 — 승리하면 이번 전투에서 아군이 생성한 소환수 1기당 전투 골드 +1%(최대 +25%)
    /// </summary>
    public sealed class KuberaPatronage : UniquePassiveCode
    {
        private const int AuraStatusId = 70650;
        private const string AuraKey = "kubera_patronage";
        private const float PatronageShare = 0.20f;
        private const float InterestPerSummon = 0.01f;
        private const int InterestCapSummons = 25;

        /// <summary>쿠베라 초월기가 외부 생성에 붙이는 표식. 이 생성은 보물을 주지 않는다.</summary>
        public static readonly object TreasuryTag = new();

        private bool _subscribed;
        private int _summonsThisBattle;

        public KuberaPatronage(PassiveCodeContext context) : base(context) => CodeName = "재보천의 후원";

        public override void CastCode()
        {
            if (Caster == null) return;
            _summonsThisBattle = 0;
            foreach (Unit ally in FieldAllies())
            {
                ally.AddStatus(BuffStatus.Create(AuraStatusId, AuraKey, CodeName, Caster, ally,
                    new PatronageEffect(Caster), duration: 0, category: BaseEnums.StatusCategory.Neutral,
                    description: "쿠베라가 필드에 있는 동안 소환수 피해 +20%."));
            }
            // 골드 러쉬의 "이번 전투" 중첩은 전투마다 처음부터 센다.
            (Caster.ActiveSpecialCode as Codes.Special.KuberaGoldRush)?.ResetBattle();

            if (_subscribed) return;
            Summons.AnySummonCreated += OnAnySummonCreated;
            _subscribed = true;
        }

        public override void StopCode()
        {
            if (_subscribed) Summons.AnySummonCreated -= OnAnySummonCreated;
            _subscribed = false;
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return;
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            foreach (Unit ally in pool.Where(unit => unit != null)) ally.RemoveStatusByKey(AuraKey);
        }

        private void OnAnySummonCreated(Unit owner, Unit summon, SummonSource source, object sourceTag)
        {
            if (Caster == null || !Caster.isActive || !Caster.IsOnField) return;
            if (owner == null || owner.IsEnemy != Caster.IsEnemy) return;
            if (ReferenceEquals(sourceTag, TreasuryTag)) return;

            Caster.AddUltimateResource(1);
            Effects.Vfx.LokapalaFx.ResourceGained(Caster);
            if (_summonsThisBattle >= InterestCapSummons) return;
            _summonsThisBattle++;
            // 이자는 승리 정산 때만 골드에 얹힌다(GameManager.GrantClearGold).
            GameManager.Instance?.AddBattleGoldBonus(InterestPerSummon);
        }

        private List<Unit> FieldAllies()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && unit.isActive && unit.IsOnField).ToList();
        }

        /// <summary>소환수 피해인가 — 소환수 판정을 받는 아군(피그말리온)이 쳤거나, 소환수가 친 피해(소환자 명의)인가.</summary>
        public static bool IsSummonDamage(Unit attacker, DamageContext context)
            => attacker != null && (attacker.CountsAsSummon || (context != null && context.HasTag(DamageTag.SummonAttack)));

        /// <summary>
        /// 쿠베라 고유 계열(후원 · 보고 개방)의 소환수 피해 몫. 같은 쿠베라가 건 몫끼리는 <b>더한다</b>
        /// (Detail_08 §11.4 — 20% + 15% = 35%). 다른 출처의 배율과는 평소처럼 곱한다.
        /// </summary>
        internal interface ISummonDamageShare
        {
            /// <summary>지금 보태는 몫(0.20 = +20%). 꺼져 있으면 0.</summary>
            float SummonDamageShare { get; }
        }

        /// <summary>
        /// <see cref="Unit"/>은 효과마다 주는 피해 배율을 곱하므로, 계열에서 처음 켜진 효과 하나만 1 + Σ몫을 내고
        /// 나머지는 1을 낸다. 후원이 꺼지면(쿠베라 사망 · 이탈) 남은 보고 개방이 선두가 되어 자기 몫만 낸다.
        /// </summary>
        internal static float FamilyMultiplier(BaseEffect self, Unit attacker, DamageContext context)
        {
            if (self == null || attacker != self.Target || !IsSummonDamage(attacker, context)) return 1f;
            float total = 0f;
            BaseEffect leader = null;
            foreach (BaseEffect effect in attacker.CombatEffects())
            {
                if (effect is not ISummonDamageShare member || effect.Caster != self.Caster) continue;
                float share = member.SummonDamageShare;
                if (share <= 0f) continue;
                leader ??= effect;
                total += share;
            }
            return ReferenceEquals(leader, self) ? 1f + total : 1f;
        }

        private sealed class PatronageEffect : BaseEffect, ISummonDamageShare
        {
            private readonly Unit _kubera;
            public PatronageEffect(Unit kubera) : base(0) => _kubera = kubera;

            public float SummonDamageShare
                => _kubera != null && _kubera.isActive && _kubera.IsOnField ? PatronageShare : 0f;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => FamilyMultiplier(this, attacker, context);
        }
    }
}
