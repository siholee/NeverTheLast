using System;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;

namespace Codes.Passive
{
    /// <summary>
    /// 옛 '열화 전수본' 제도의 유일한 생존자.
    ///
    /// 고유 패시브를 서포터가 열화판으로 넘겨주던 제도는 폐지되었고
    /// (<c>UniquePassiveCode</c>는 전부 전수 불가다), 230·231·233은 함께 삭제했다.
    /// 232만 <b>전수 가능한 독립 해금 패시브 '주문 가속'</b>으로 재편입되어 남아 있다.
    /// 더 이상 열화본이 아니므로 이름에서도 '(전수)' 표기를 뗐다.
    /// </summary>
    internal static class InheritedInnateStatusIds
    {
        public const int AsclepiusNashorsTooth = 5953;
    }

    /// <summary>주문 가속: 궁극기 사용 후 2턴간 DEX +20. 전수 가능한 공용 코드다.</summary>
    public sealed class InheritedAsclepiusNashorsTooth : PassiveCode
    {
        private Action<EventContext> _castHandler;
        private Action<EventContext> _cleanupHandler;
        private bool _registered;

        public InheritedAsclepiusNashorsTooth(PassiveCodeContext context) : base(context)
        {
            CodeName = "주문 가속";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _castHandler = _ => ApplyDexBuff();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnUltimateActivates, _castHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        private void ApplyDexBuff()
        {
            Caster?.AddStatus(BuffStatus.Create(
                InheritedInnateStatusIds.AsclepiusNashorsTooth,
                "inherited_asclepius_nashors_tooth", CodeName,
                Caster, Caster, new PrimaryStatBonusBuffEffect(BaseEnums.PrimaryStat.DEX, 20),
                duration: 2,
                stackPolicy: BaseEnums.StatusStackPolicy.Replace,
                isBeneficial: true,
                description: "궁극기 사용 후 2턴간 DEX가 20 증가합니다."));
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnUltimateActivates, _castHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            Caster.RemoveStatusByKey("inherited_asclepius_nashors_tooth");
            _registered = false;
        }
    }
}
