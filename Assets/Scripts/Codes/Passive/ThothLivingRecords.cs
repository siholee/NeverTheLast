using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Codes.Normal;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>소환 개수가 아닌, 현재 소환수를 보유한 필드 아군 본체의 수를 센다.</summary>
    public sealed class ThothLivingRecords : UniquePassiveCode, ISummonerPolicy
    {
        public const string Kind = "thoth_scribe";
        public const int ScribeId = 903;
        private readonly List<Unit> _holders = new();
        private bool _working;
        private int _remaining;
        private System.Action<EventContext> _death;
        public string AuraKey => $"thoth_records_{Caster?.GetEntityId()}";
        public bool Working => _working && Caster != null && Caster.isActive && Caster.IsOnField;
        public Unit Scribe => Caster == null ? null : Summons.Of(Caster).FirstOrDefault(x => x.SummonKind == Kind);
        public int Remaining => Scribe != null ? _remaining : 0;
        public int OwnerCount => Working ? SummonSupportCombat.Allies(Caster).Count(x => x.CountsAsSummon || Summons.Of(x).Count > 0) : 0;
        public float DamageBonus => Working ? Mathf.Min(.45f, OwnerCount * Mathf.Min(.12f, .06f + .04f * SummonSupportCombat.PowerRatio(Caster))) : 0;
        public SummonStyle Style => SummonStyle.Companion;
        public int Cap => 1;
        public SummonSpec Spec => new()
        {
            Id = ScribeId, Kind = Kind, Name = "서기관", Element = "Hydro", PortraitKey = "PENSEE_PORTRAIT",
            Hp = Mathf.Max(1, Mathf.RoundToInt((Caster?.HpMax ?? 1) * .35f)), Speed = 90,
            NormalCodeId = ThothScribeHeal.CodeId, Targetable = true,
        };
        public void OnOverflow(Unit owner, Unit oldest) { }
        public ThothLivingRecords(PassiveCodeContext context) : base(context) => CodeName = "살아 있는 기록";
        public override void CastCode()
        {
            if (_working || Caster == null) return;
            _working = true; _remaining = 0;
            Summons.Register(Caster, this);
            BindAllies();
            Summons.AnySummonCreated += Created;
            ActionScheduler.AnyActionStarted += Started;
            ActionScheduler.AnyActionFinished += Finished;
            _death = _ => Teardown(true);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _death);
        }
        private void Created(Unit owner, Unit summon, SummonSource source, object tag)
        { if (Working && owner != null && owner.IsEnemy == Caster.IsEnemy) BindAllies(); }
        private void Started(Unit actor, ActionScheduler.ActionKind kind, string label) { if (Working) BindAllies(); }
        private void BindAllies()
        {
            foreach (Unit ally in SummonSupportCombat.Allies(Caster, true))
            {
                if (ally.HasStatusKey(AuraKey)) continue;
                ally.AddStatus(BuffStatus.Create(73124, AuraKey, CodeName, Caster, ally, new RecordsEffect(this),
                    duration: 0, isBeneficial: true, description: "소환수를 보유한 아군 본체 수와 토트의 특공에 비례해 소환 피해 증가(최대45%). 서기관이 있으면 받는 피해12% 감소."));
                if (!_holders.Contains(ally)) _holders.Add(ally);
            }
        }
        public Unit SummonScribe()
        {
            if (!Working) return null;
            Unit scribe = Scribe;
            if (scribe == null)
            {
                scribe = Summons.Create(Caster);
                if (scribe == null) return null;
                scribe.gameObject.AddComponent<Entities.View.SummonCardView>().Bind(scribe);
                scribe.AddStatus(BuffStatus.Create(73125, "thoth_scribe_defense", "서기관의 몸", Caster, scribe,
                    new StatModifierEffect(BaseEnums.StatType.Defense, Caster.DefCurr * .60f),
                    duration: 0, category: BaseEnums.StatusCategory.Neutral));
                scribe.AddStatus(BuffStatus.Create(73126, "thoth_scribe_spdef", "서기관의 몸", Caster, scribe,
                    new StatModifierEffect(BaseEnums.StatType.SpecialDefense, Caster.StatValue(BaseEnums.StatType.SpecialDefense) * .60f),
                    duration: 0, category: BaseEnums.StatusCategory.Neutral));
            }
            _remaining = 3;
            BindAllies();
            return scribe;
        }
        private void Finished(Unit actor, ActionScheduler.ActionKind kind)
        {
            if (!Working || actor != Caster || kind != ActionScheduler.ActionKind.Normal || Scribe == null) return;
            if (--_remaining <= 0) Scribe.Die(null); // 수명 소멸은 기존 소환 공통 사망 규칙. 치명상 방지로 수명을 연장하지 않는다.
        }
        public override void StopCode() => Teardown(false);
        private void Teardown(bool ownerDied)
        {
            _working = false;
            Summons.AnySummonCreated -= Created;
            ActionScheduler.AnyActionStarted -= Started;
            ActionScheduler.AnyActionFinished -= Finished;
            Caster?.RemoveListener(BaseEnums.UnitEventType.OnDeath, _death);
            foreach (Unit unit in _holders) unit?.RemoveStatusByKey(AuraKey);
            _holders.Clear(); _remaining = 0;
            if (!ownerDied)
            {
                Unit scribe = Scribe;
                if (scribe != null)
                {
                    scribe.StopAllCoroutines(); scribe.ActiveNormalCode?.StopCode(); scribe.isCasting = false;
                    scribe.isActive = false;
                    GridManager.Instance?.ForgetSummon(scribe);
                }
            }
            Summons.Unregister(Caster);
        }
        private sealed class RecordsEffect : BaseEffect
        {
            private readonly ThothLivingRecords _source;
            public RecordsEffect(ThothLivingRecords source) : base(0) => _source = source;
            public override bool IsBeneficial => true;
            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
                => attacker == Target && SummonSupportCombat.IsSummonDamage(context) ? 1 + _source.DamageBonus : 1;
            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => unit == Target && _source.Working && _source.Scribe != null ? .88f : 1;
        }
    }
}
