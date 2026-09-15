using System.Linq;
using BaseClasses;
using Codes.Passive;
using Effects.Buffs;
using Entities;

namespace Codes.Ultimate
{
    /// <summary>
    /// 잔 U — 오를레앙의 성녀.
    /// <c>최고의 방어</c> 중첩을 최대치까지 되돌리고 STR +5%를 얻는다. 중첩된다.
    /// </summary>
    public sealed class JeanMaidOfOrleans : SimpleUltimate
    {
        private const float StrMultiplier = 1.05f;

        public JeanMaidOfOrleans(UltimateCodeContext context)
            : base(context, "오를레앙의 성녀", 0.35f) { }

        protected override void Resolve()
        {
            if (Caster == null) return;

            foreach (var passive in Caster.ActivePassiveCodes.OfType<JeanBestDefense>())
            {
                passive.RefillStacks();
            }

            // 겹칠수록 세지는 것이 이 궁극기의 전부라 Stack 정책을 쓴다.
            Caster.AddStatus(BuffStatus.Create(
                JeanStatusIds.MaidOfOrleans, "jean_maid_of_orleans", CodeName,
                Caster, Caster,
                new PrimaryStatMultiplierEffect(StrMultiplier, BaseEnums.PrimaryStat.STR),
                stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                isBeneficial: true,
                description: "STR이 5% 증가합니다. 중첩됩니다."));
        }
    }
}
