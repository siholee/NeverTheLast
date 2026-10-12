using System;
using BaseClasses;
using Entities;

namespace Codes.Passive.Reward
{
    /// <summary>충전량은 코드 인스턴스에 저장하고, 활성 보유자만 사망 이벤트를 듣는다.</summary>
    public sealed class CapacitorCode : RewardPassiveCode
    {
        public CapacitorCode(PassiveCodeContext context,int id):base(context,id){}
        public override float PermanentStatFlatModifier(BaseEnums.StatType stat)
        {
            if(Entry==null || !Enum.TryParse(Entry.capacitorStat,true,out BaseEnums.StatType own)||own!=stat)return 0;
            float amount=(Caster?.ActiveRewardInstance(RewardId)?.charge??0)*Entry.capacitorPerCharge;
            return stat is BaseEnums.StatType.CritRate or BaseEnums.StatType.CritDamage?amount/100:amount;
        }
        protected override void OnCast()=>Unit.AnyUnitDied+=OnDeath;
        protected override void OnStop()=>Unit.AnyUnitDied-=OnDeath;
        private void OnDeath(Unit dead,Unit attacker)
        {
            if(!HolderWorking || dead==null || dead==Caster || !dead.IsOnField)return;
            Caster.GainRewardCharge(RewardId,dead.IsSummon ? 0.25f : 1f);
        }
    }
}
