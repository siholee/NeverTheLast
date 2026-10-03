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
    /// <summary>피그말리온 — 갈라테아. 자신을 소환수로 판정하고 전장의 아군 피해를 증폭한다.</summary>
    public sealed class PygmalionGalatea : UniquePassiveCode
    {
        private const int IdentityStatusId = 70200;
        private const int AuraStatusId = 70201;
        private const string IdentityKey = "pygmalion_summon_identity";
        private const string AuraKey = "pygmalion_galatea_aura";

        public PygmalionGalatea(PassiveCodeContext context) : base(context) => CodeName = "갈라테아";

        public override void CastCode()
        {
            if (Caster == null) return;
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Cast, CodeName);
            Caster.AddStatus(BuffStatus.Create(
                IdentityStatusId, IdentityKey, "갈라테아", Caster, Caster,
                new SummonIdentityEffect(), duration: 0,
                category: BaseEnums.StatusCategory.Neutral,
                description: "이 유닛은 소환수로도 간주됩니다."));

            foreach (Unit ally in AliveAllies())
            {
                ally.AddStatus(BuffStatus.Create(
                    AuraStatusId, AuraKey, "갈라테아의 축복", Caster, ally,
                    new GalateaDamageEffect(Caster), duration: 0,
                    stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                    isBeneficial: true,
                    description: "피그말리온이 필드에 있는 동안 가하는 피해 +15%. 소환수는 +30%."));
            }
            ActionAnnouncer.AnnouncePassive(Caster, this);
            AudioManager.PlayAction(Caster, SfxActionSlot.Passive, SfxCuePhase.Hit, CodeName);
        }

        public override void StopCode()
        {
            if (Caster != null) Caster.RemoveStatusByKey(IdentityKey);
            foreach (Unit ally in AliveAllies(includeInactive: true)) ally.RemoveStatusByKey(AuraKey);
        }

        private List<Unit> AliveAllies(bool includeInactive = false)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || Caster == null) return new List<Unit>();
            List<Unit> pool = Caster.IsEnemy ? grid.enemyList : grid.heroList;
            return pool.Where(unit => unit != null && (includeInactive || unit.isActive) &&
                                      unit.IsEnemy == Caster.IsEnemy).ToList();
        }

        private sealed class SummonIdentityEffect : BaseEffect
        {
            public SummonIdentityEffect() : base(0) { }
            public override bool CountsAsSummon(Unit unit) => unit == Target;
        }

        private sealed class GalateaDamageEffect : BaseEffect
        {
            private readonly Unit _pygmalion;
            public GalateaDamageEffect(Unit pygmalion) : base(0) => _pygmalion = pygmalion;

            public override bool IsBeneficial => true;

            public override float OutgoingDamageModifier(Unit attacker, Unit target, DamageContext context)
            {
                if (attacker != Target || _pygmalion == null || !_pygmalion.isActive || !_pygmalion.IsOnField)
                    return 1f;
                return attacker.CountsAsSummon ? 1.30f : 1.15f;
            }
        }
    }
}
