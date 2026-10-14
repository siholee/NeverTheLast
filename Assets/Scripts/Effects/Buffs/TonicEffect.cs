using System;
using System.Collections.Generic;
using BaseClasses;
using Effects.Base;
using Entities;
using Managers;
using UnityEngine;

namespace Effects.Buffs
{
    /// <summary>
    /// 전투 비약 하나의 효과(Detail_17 §6). 수치는 `40_items.yaml`의 tonic* 필드가 원본이다.
    /// 한 전투 동안만 걸린다 — 상태 지속 0(라운드 끝까지)으로 붙이고 라운드 종료가 걷는다.
    /// </summary>
    public sealed class TonicEffect : BaseEffect
    {
        private const int StatusId = 96000;
        private const string StatusKey = "tonic";

        private readonly Dictionary<BaseEnums.StatType, float> _percent = new();
        private readonly Dictionary<BaseEnums.StatType, float> _flat = new();
        private readonly float _damageTakenMultiplier;
        private bool _negateAvailable;

        private TonicEffect(ItemData item) : base(0)
        {
            Read(item.tonicPercentStats, _percent, percent: true);
            Read(item.tonicFlatStats, _flat, percent: false);
            _damageTakenMultiplier = 1f + item.tonicDamageTakenPercent / 100f;
            _negateAvailable = item.tonicNegateFirstHarmful;
            Category = BaseEnums.EffectCategory.Positive;
        }

        private static void Read(Dictionary<string, float> source, Dictionary<BaseEnums.StatType, float> target, bool percent)
        {
            if (source == null) return;
            foreach (KeyValuePair<string, float> pair in source)
            {
                if (!Enum.TryParse(pair.Key, true, out BaseEnums.StatType stat)) continue;
                bool ratio = percent || stat == BaseEnums.StatType.CritRate || stat == BaseEnums.StatType.CritDamage;
                target[stat] = ratio ? pair.Value / 100f : pair.Value;
            }
        }

        /// <summary>아군 한 명에게 비약을 건다. 시작 마나는 상태가 아니라 즉시 채운다.</summary>
        public static void Apply(Unit hero, ItemData item)
        {
            if (hero == null || item == null) return;
            var status = BuffStatus.Create(StatusId, StatusKey, item.name, hero, hero, new TonicEffect(item),
                duration: 0, stackPolicy: BaseEnums.StatusStackPolicy.Replace, isBeneficial: true,
                description: $"{item.name}의 효과가 이번 전투 동안 이어집니다.");
            hero.AddStatus(status);

            if (item.tonicStartManaPercent > 0f && hero.UltimateResourceType == BaseEnums.UltimateResourceType.Mana)
            {
                int target = Mathf.RoundToInt(hero.ManaMax * item.tonicStartManaPercent / 100f);
                if (hero.ManaCurr < target) hero.AddUltimateResource(target - hero.ManaCurr);
            }
        }

        public override bool IsBeneficial => true;

        public override float StatFlatModifier(Unit unit, BaseEnums.StatType stat)
            => unit == Target && _flat.TryGetValue(stat, out float value) ? value : 0f;

        public override float StatPercentModifier(Unit unit, BaseEnums.StatType stat)
            => unit == Target && _percent.TryGetValue(stat, out float value) ? value : 0f;

        public override float ReceivingDamageModifier(Unit unit, DamageContext context)
            => unit == Target ? _damageTakenMultiplier : 1f;

        /// <summary>정화의 성수 — 첫 해로운 상태 하나를 확정 저항하고 사라진다.</summary>
        public override float NegativeStatusResistanceChanceModifier(Unit unit, Entities.Status.UnitStatus status)
        {
            if (unit != Target || !_negateAvailable) return 0f;
            _negateAvailable = false;
            return 1f;
        }
    }
}
