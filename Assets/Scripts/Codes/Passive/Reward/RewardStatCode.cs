using System;
using System.Collections.Generic;
using BaseClasses;
using Codes.Base;
using Effects.Buffs;

namespace Codes.Passive.Reward
{
    /// <summary>
    /// 데이터만으로 되는 보상 코드 — T1 깡 스탯, T2 퍼센트 스탯 · 큰 깡 수치, 피해 태그 증가(형태 · 궁극기 · 추가행동 · 반격 · 원소 반응), 영웅의 자질.
    /// 수치는 `20_codes.yaml`의 <c>flatStats</c> · <c>percentStats</c> · <c>damageTag</c>가 원본이다(Detail_17 §4).
    ///
    /// 스탯은 상태가 아니라 <see cref="PassiveCode.PermanentStatFlatModifier"/>로 준다 — 전투 밖 캐릭터 창에도 보여야 해서다.
    /// 피해 태그 증가만 전투 중 상태로 건다.
    /// </summary>
    public sealed class RewardStatCode : RewardPassiveCode
    {
        private readonly Dictionary<BaseEnums.StatType, float> _flat = new();
        private readonly Dictionary<BaseEnums.StatType, float> _percent = new();

        public RewardStatCode(PassiveCodeContext context, int rewardId) : base(context, rewardId)
        {
            Read(Entry?.flatStats, _flat, isPercent: false);
            Read(Entry?.percentStats, _percent, isPercent: true);
        }

        private static void Read(Dictionary<string, float> source, Dictionary<BaseEnums.StatType, float> target, bool isPercent)
        {
            if (source == null) return;
            foreach (KeyValuePair<string, float> pair in source)
            {
                if (!Enum.TryParse(pair.Key, true, out BaseEnums.StatType stat)) continue;
                // 표에는 사람이 읽는 단위로 적는다(퍼센트 12, 치명타 4%p). 스탯 계산은 비율을 쓴다.
                bool ratio = isPercent || stat == BaseEnums.StatType.CritRate || stat == BaseEnums.StatType.CritDamage;
                target[stat] = ratio ? pair.Value / 100f : pair.Value;
            }
        }

        public override float PermanentStatFlatModifier(BaseEnums.StatType stat)
            => _flat.TryGetValue(stat, out float value) ? value : 0f;

        public override float PermanentStatPercentModifier(BaseEnums.StatType stat)
            => _percent.TryGetValue(stat, out float value) ? value : 0f;

        protected override void OnCast()
        {
            if (Entry == null || Entry.damageTag <= 0 || Entry.damageTagBonus <= 0f) return;
            int tag = Entry.damageTag;
            float multiplier = 1f + Entry.damageTagBonus / 100f;
            AddStatus(Caster, new ConditionalOutgoingDamageMultiplierEffect(
                context => context != null && context.HasTag(tag), multiplier));
        }
    }
}
