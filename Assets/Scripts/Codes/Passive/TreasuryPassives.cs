using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;
namespace Codes.Passive
{
    public sealed class MansaRoyalWarrant : UniquePassiveCode
    {
        public MansaRoyalWarrant(PassiveCodeContext context) : base(context) => CodeName = "왕실의 구매권";
        private int _turns;
        private bool _listening;
        private string Key => "mansa_procession_" + Caster.GetInstanceID();
        public override void CastCode()
        {
            StopCode(); Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath); _listening = true;
        }
        public void GrantProcession(float amount)
        {
            _turns = 2;
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
                ally.AddStatus(BuffStatus.Create(72001, Key, "황금의 행렬", Caster, ally,
                    new ProcessionEffect(amount), duration: 0, isBeneficial: true,
                    description: $"공격력 · 특수공격력 +{amount:P0}. 만사 무사 일반행동 2회 지속."));
        }
        private void OnDeath(EventContext _) => StopCode();
        private void OnNormal(EventContext _) { if (_turns > 0 && --_turns == 0) RemoveBuffs(); }
        private void RemoveBuffs()
        {
            if (GridManager.Instance != null) foreach (Unit ally in GridManager.Instance.heroList.Where(u => u != null)) ally.RemoveStatusByKey(Key);
        }
        public override void StopCode()
        {
            if (_listening && Caster != null) Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal);
            if (_listening && Caster != null) Caster.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath);
            _listening = false; _turns = 0; if (Caster != null) RemoveBuffs();
        }
        private sealed class ProcessionEffect : BaseEffect
        {
            public ProcessionEffect(float amount) : base(0, amount) { }
            public override bool IsBeneficial => true;
            public override float StatPercentModifier(Unit unit, BaseEnums.StatType stat) => unit == Target &&
                stat is BaseEnums.StatType.Attack or BaseEnums.StatType.SpecialAttack ? Coefficient : 0;
        }
    }
    public sealed class IshtarGoldenMagazine : UniquePassiveCode
    {
        public int Stacks { get; private set; }
        private bool _listening;
        public IshtarGoldenMagazine(PassiveCodeContext context) : base(context) => CodeName = "황금 탄창";
        public override void CastCode()
        {
            StopCode(); PartyTreasury.Changed += OnTreasury;
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal);
            Caster.AddListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath); _listening = true;
        }
        private void OnTreasury(Unit actor, int delta)
        {
            if (actor != Caster || delta >= 0 || !PartyTreasury.CanUse(Caster)) return;
            Stacks = Mathf.Min(12, Stacks - delta);
            Caster.AddStatus(BuffStatus.Create(72201, "ishtar_magazine", CodeName, Caster, Caster,
                new MagazineEffect(this), duration: 0, isBeneficial: true,
                description: $"초월기 피해 +{Stacks * 4}%. 다음 자기 일반행동 종료 시 소멸."));
        }
        private void OnDeath(EventContext _) => StopCode();
        private void OnNormal(EventContext _) { Stacks = 0; Caster.RemoveStatusByKey("ishtar_magazine"); }
        public override void StopCode()
        {
            if (_listening) { PartyTreasury.Changed -= OnTreasury; Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormal); }
            if (_listening) Caster?.RemoveListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnDeath);
            _listening = false; Stacks = 0; Caster?.RemoveStatusByKey("ishtar_magazine");
        }
        private sealed class MagazineEffect : BaseEffect
        {
            private readonly IshtarGoldenMagazine _source;
            public MagazineEffect(IshtarGoldenMagazine source) : base(0) => _source = source;
            public override bool IsBeneficial => true;
            public override float UltimateDamageBonus(Unit actor) => actor == Target ? .04f * _source.Stacks : 0;
        }
    }
}
