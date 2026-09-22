using System.Collections;
using BaseClasses;
using Codes.Base;
using Codes.Passive;
using Combat;
using Effects.Buffs;
using Entities;
using Managers;

namespace Codes.Ultimate
{
    /// <summary>위고 U — 팡세를 3턴간 소환한다.</summary>
    public sealed class HugoBraveAdvocate : UltimateCode
    {
        public HugoBraveAdvocate(UltimateCodeContext context) : base(context)
        {
            CodeName = "용기 있는 변호사";
            CastingDelay = 0.5f;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Resolve());
        }

        private IEnumerator Resolve()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (cast) GridManager.Instance?.SpawnSummon(Caster, SummonCatalog.Pensee(Caster));
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
        public override void StopCode() { if (Caster != null) Caster.isCasting = false; }
    }

    /// <summary>팡세 U — 아군 전체가 받는 피해를 3턴간 25% 줄인다.</summary>
    public sealed class PenseeProtection : UltimateCode
    {
        public PenseeProtection(UltimateCodeContext context) : base(context)
        {
            CodeName = "사유의 방벽";
            CastingDelay = 0.4f;
        }

        public override void CastCode()
        {
            if (!HasValidTarget()) return;
            Caster.isCasting = true;
            CurrSkillCoroutine = Caster.StartCoroutine(Resolve());
        }

        private IEnumerator Resolve()
        {
            bool cast = false;
            yield return WaitForCast(result => cast = result);
            if (cast)
            {
                Unit source = Caster.SummonOwner ?? Caster;
                foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
                    ally.AddStatus(BuffStatus.Create(
                        HugoStatusIds.PenseeDefense, $"pensee_defense_{source.GetEntityId()}", CodeName,
                        source, ally, new ReceivingDamageMultiplierEffect(0.75f),
                        duration: 3,
                        stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                        isBeneficial: true,
                        description: "받는 피해가 25% 감소합니다."));
            }
            StopCode();
        }

        public override bool HasValidTarget() => Caster != null && Caster.isActive;
        public override void StopCode() { if (Caster != null) Caster.isCasting = false; }
    }
}
