using BaseClasses;
namespace Codes.Passive.Reward
{
    /// <summary>마나 수정·궁극의 초월·더블 캐스팅. 전투 자원은 Unit의 공용 소비/충전 경로가 관리한다.</summary>
    public sealed class UltimateManaRewardCode : RewardPassiveCode
    {
        public const int Initial = 514, Overflow = 772, Double = 919;
        public int Id { get; }
        public UltimateManaRewardCode(PassiveCodeContext context, int id) : base(context, id) => Id = id;
        protected override void OnCast() { if (Id == Initial) Caster.GrantInitialMana(Initial, 20); }
        protected override void OnStop() => Caster?.ClearManaRewardState(Id);
    }
}
