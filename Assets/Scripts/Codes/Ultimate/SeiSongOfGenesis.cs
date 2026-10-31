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
    /// 시전 시 보호막·피해 증가를 부여하고, 별도의 집중 오라로 추가 고정 피해와 피해 감소를 제공한다.
    /// 집중 중단은 시전 시 부여한 보호막·피해 증가를 지우지 않는다.
    /// </summary>
    public sealed class SeiSongOfGenesis : BaseUltimateCode
    {
        public const string ConcentrationKey = "sei_song_of_genesis";
        public const string BuffKey = "sei_song_buff";
        public const string MarkerKey = "sei_song_marker";
        public const string CastBuffKey = "sei_song_blessing";
        public const int BuffStatusId = 7001;
        public const int MarkerStatusId = 7002;

        private const int DurationTurns = 3;
        private const float BonusDefenseRatio = 0.25f;
        private const float ShieldFlat = 1200f;
        private const float ShieldPerLevel = 48f;
        private const float ShieldDefenseRatio = 7.2f;
        public override int BaseShieldAmount => StatDamage(ShieldFlat + ShieldPerLevel * (Caster?.Level ?? 0), BaseEnums.StatType.Defense, ShieldDefenseRatio);

        private bool _listening;

        public SeiSongOfGenesis(UltimateCodeContext context) : base(context)
        {
            CodeName = "창세의 노래";
        }

        protected override IEnumerator Perform()
        {
            List<Unit> allies = AliveAllies();
            int shield = BaseShieldAmount;

            // 이전 집중의 정리로 새 버프가 지워지지 않도록 교체를 먼저 처리한다.
            Concentration.Begin(Caster, ConcentrationKey, DurationTurns, OnConcentrationEnd);

            Caster.StartCoroutine(CardMotion.Channel(Caster, 0.18f, 0.55f, 0.3f));
            yield return GeoFx.SongOfGenesis(Caster, allies, ally =>
            {
                if (!DirectAttack.CasterCanContinue(Caster, true) || !Concentration.IsActive(Caster, ConcentrationKey)) return;
                ApplyBuff(ally);
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

        private void ApplyBuff(Unit ally)
        {
            if (ally == null || !ally.isActive) return;
            var blessing = BuffStatus.Create(7004, CastBuffKey, "창세의 축복", Caster, ally,
                new OutgoingDamageMultiplierEffect(1.25f), duration: 3, isBeneficial: true,
                description: "가하는 피해 +25%. 자신의 일반행동 3회까지. 집중이 끊겨도 유지된다.");
            blessing.NormalActionDuration = true;
            blessing.AffectedVfxPartId = StatusAffectedVfx.SuppressPartId;
            ally.AddStatus(blessing);
            var status = BuffStatus.Create(BuffStatusId, BuffKey, "창세의 노래", Caster, ally,
                new SongOfGenesisEffect(), duration: 0, isBeneficial: true,
                description: $"받는 피해 -25%. 피해를 가할 때마다 세이 방어력의 {BonusDefenseRatio * 100f:0}% 고정 피해를 더한다. " +
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
            if (!attacker.GetAllStatuses().Any(s => s.Key == BuffKey && s.Caster == Caster)) return;
            if (source == null || source.HasTag(DamageTag.BonusDamage) || source.HasTag(DamageTag.ElementalReaction) ||
                source.IsDamageOverTime || source.IsSharedDamage || source.CodeType == BaseEnums.CodeType.Effect || resolved.DamageDealt <= 0) return;

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
                    foreach (string key in new[] { BuffKey, MarkerKey })
                        if (unit.GetAllStatuses().Any(s => s.Key == key && s.Caster == Caster)) unit.RemoveStatusByKey(key);
                }
            }

            if (reason == Concentration.EndReason.Interrupted) GeoFx.ConcentrationBroken(Caster);
        }

        /// <summary>집중 오라. 제거되거나 집중이 끝나면 경감도 즉시 사라진다.</summary>
        private sealed class SongOfGenesisEffect : BaseEffect, ICardAura
        {
            public SongOfGenesisEffect() : base(0)
            {
                EffectName = "창세의 노래";
                Category = BaseEnums.EffectCategory.Positive;
            }

            public override bool IsBeneficial => true;

            public override float ReceivingDamageModifier(Unit unit, DamageContext context)
                => unit == Target && Concentration.IsActive(Caster, ConcentrationKey) ? .75f : 1f;

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
