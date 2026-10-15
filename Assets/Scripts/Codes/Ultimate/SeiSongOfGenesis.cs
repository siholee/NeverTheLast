using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Effects.Vfx;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Ultimate
{
    /// <summary>
    /// 세이 — 궁극기 「창세의 노래」 (마나 120, <b>집중</b>).
    ///
    /// 3턴간 아군 전체에게 <c>50 + 방어력의 15%</c>만큼 공격력을 부여하고,
    /// 버프를 받은 아군이 피해를 가할 때마다 <c>세이 방어력의 10%</c> 고정 피해를 덧붙인다.
    ///
    ///   · 지속은 <b>세이의 턴</b>으로 센다(집중). 세이가 행동불능이 되거나 쓰러지면 즉시 풀린다.
    ///   · 공격력 증가량은 <b>시전 순간</b>의 방어력으로 굳힌다 — 버프가 도중에 흔들리지 않는다.
    ///   · 덧붙는 고정 피해는 <b>그 순간</b>의 세이 방어력을 읽는다.
    ///   · 덧붙는 피해는 다시 덧붙지 않고(BonusDamage 태그), 지속피해 · 원소 반응에는 붙지 않는다.
    ///   · 아군 전체에게 <c>레벨 × 2 + 방어력의 25%</c> 보호막도 두른다. 보호막은 집중과 무관하게 남는다 —
    ///     세이가 기절해도 이미 깔린 방패까지 걷히면 "집중"이 아니라 "전부 취소"가 된다.
    /// </summary>
    public sealed class SeiSongOfGenesis : BaseUltimateCode
    {
        public const string ConcentrationKey = "sei_song_of_genesis";
        public const string BuffKey = "sei_song_buff";
        public const string MarkerKey = "sei_song_marker";
        public const int BuffStatusId = 7001;
        public const int MarkerStatusId = 7002;

        private const int DurationTurns = 3;
        private const float AttackFlat = 50f;
        private const float AttackDefenseRatio = 0.15f;
        private const float BonusDefenseRatio = 0.10f;
        private const float ShieldPerLevel = 2f;
        private const float ShieldDefenseRatio = 0.25f;

        private bool _listening;

        public SeiSongOfGenesis(UltimateCodeContext context) : base(context)
        {
            CodeName = "창세의 노래";
        }

        protected override IEnumerator Perform()
        {
            List<Unit> allies = AliveAllies();
            float attackBonus = AttackFlat + Caster.StatValue(BaseEnums.StatType.Defense) * AttackDefenseRatio;
            int shield = Mathf.RoundToInt(StatDamage(ShieldPerLevel * Caster.Level, BaseEnums.StatType.Defense, ShieldDefenseRatio));

            // 이전 집중의 정리로 새 버프가 지워지지 않도록 교체를 먼저 처리한다.
            Concentration.Begin(Caster, ConcentrationKey, DurationTurns, OnConcentrationEnd);

            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.18f, 0.55f, 0.3f));
            yield return GeoFx.SongOfGenesis(Caster, allies, ally =>
            {
                if (!DirectAttack.CasterCanContinue(Caster, true) || !Concentration.IsActive(Caster, ConcentrationKey)) return;
                ApplyBuff(ally, attackBonus);
                if (ally == null || !ally.isActive) return;
                ally.AddShield(shield, Caster);
                // 기본 화풍은 창세의 노래 affected가 버프와 보호막을 한 번에 보여 준다.
                // Legacy만 기존 결정 보호막을 별도로 유지한다.
                GeoFx.StoneShield(ally, 1.5f, null);
            });

            if (!DirectAttack.CasterCanContinue(Caster, true) || !Concentration.IsActive(Caster, ConcentrationKey)) yield break;
            var marker = BuffStatus.Create(MarkerStatusId, MarkerKey, "집중: 창세의 노래", Caster, Caster,
                new ConcentrationMarkerEffect(), duration: 0, isBeneficial: true,
                description: $"세이의 턴 {DurationTurns}번 동안 유지된다. 세이가 행동불능이 되면 풀린다.");
            marker.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            Caster.AddStatus(marker);

            if (!_listening)
            {
                Unit.AnyDamageDealt += OnAnyDamageDealt;
                _listening = true;
            }

            yield return new WaitForSeconds(0.2f);
        }

        private void ApplyBuff(Unit ally, float attackBonus)
        {
            if (ally == null || !ally.isActive) return;
            var status = BuffStatus.Create(BuffStatusId, BuffKey, "창세의 노래", Caster, ally,
                new SongOfGenesisEffect(attackBonus), duration: 0, isBeneficial: true,
                description: $"공격력 +{attackBonus:0}. 피해를 가할 때마다 세이 방어력의 {BonusDefenseRatio * 100f:0}% 고정 피해를 더한다. " +
                             "(집중 — 세이가 행동불능이 되면 풀린다)");
            status.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            ally.AddStatus(status);
        }

        /// <summary>버프를 받은 아군이 피해를 가했다 — 고정 피해를 덧붙인다.</summary>
        private void OnAnyDamageDealt(DamageResolvedContext resolved)
        {
            if (resolved == null || Caster == null || !Concentration.IsActive(Caster, ConcentrationKey)) return;

            Unit attacker = resolved.Attacker;
            Unit target = resolved.Target;
            DamageContext source = resolved.DamageContext;
            if (attacker == null || target == null || !target.isActive) return;
            if (!attacker.HasStatusKey(BuffKey)) return;
            if (source == null || source.HasTag(DamageTag.BonusDamage) || source.CodeType == BaseEnums.CodeType.Effect) return;

            int bonus = Mathf.RoundToInt(Caster.StatValue(BaseEnums.StatType.Defense) * BonusDefenseRatio);
            if (bonus <= 0) return;

            ImpactFx.TrueSpark(VfxSpace.WorldPoint(target));
            target.TakeDamage(new DamageContext(attacker, bonus, source.CodeType, new List<int>
            {
                DamageTag.SingleTarget, DamageTag.TrueDamage, DamageTag.BonusDamage,
            }));
        }

        private void OnConcentrationEnd(Concentration.EndReason reason)
        {
            if (_listening)
            {
                Unit.AnyDamageDealt -= OnAnyDamageDealt;
                _listening = false;
            }

            GridManager grid = GridManager.Instance;
            if (grid != null)
            {
                foreach (Unit unit in grid.heroList.Concat(grid.enemyList).Where(unit => unit != null))
                {
                    unit.RemoveStatusByKey(BuffKey);
                    unit.RemoveStatusByKey(MarkerKey);
                }
            }

            if (reason == Concentration.EndReason.Interrupted) GeoFx.ConcentrationBroken(Caster);
        }

        /// <summary>창세의 노래를 받은 아군 — 공격력 고정 가산과 금빛 오라.</summary>
        private sealed class SongOfGenesisEffect : BaseEffect, ICardAura
        {
            private readonly float _attack;

            public SongOfGenesisEffect(float attack) : base(0, attack)
            {
                _attack = attack;
                EffectName = "창세의 노래";
                Category = BaseEnums.EffectCategory.Positive;
            }

            public override bool IsBeneficial => true;

            public override float StatFlatModifier(Unit unit, BaseEnums.StatType stat)
                => unit == Target && stat == BaseEnums.StatType.Attack ? _attack : 0f;

            public CardAuraStyle AuraStyle => CardAuraStyle.Glow;
            public Color AuraColor => new(1f, 0.82f, 0.36f, 0.75f);
        }

        /// <summary>세이가 집중 중임을 알리는 표식 — 카드 가장자리에 호박빛이 낮게 맥동한다.</summary>
        private sealed class ConcentrationMarkerEffect : BaseEffect, ICardAura
        {
            public ConcentrationMarkerEffect() : base(0) { EffectName = "집중"; }

            public override bool IsBeneficial => true;

            public CardAuraStyle AuraStyle => CardAuraStyle.Glow;
            public Color AuraColor => new(1f, 0.74f, 0.28f, 0.58f);
        }
    }
}
