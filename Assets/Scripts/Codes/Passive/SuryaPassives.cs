using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using UnityEngine;

namespace Codes.Passive
{
    public static class SuryaCodeIds
    {
        public const int Innate = 267;
        public const int Distinction = 110;
        public const int Overrun = 111;
        public const int WallMeditation = 112;
        public const int Mitra = 113;
    }

    internal static class SuryaStatusIds
    {
        public const int Distinction = 6550;
        public const int Overrun = 6551;
    }

    /// <summary>
    /// 수리야 고유 P — 사비타.
    ///
    /// <b>햇빛 아래에서만</b> 돈다. 필드가 햇빛일 때 아군이 행동할 때마다, 그 아군이 수리야와
    /// 얼마나 닮았는지를 세어 그만큼 중첩을 얹는다.
    ///
    /// 닮음은 네 축을 각각 센다 — <b>원소 · 무기 숙련 · 속성 태그(하나씩)</b>.
    /// 아그니라면 불(원소) + 보주(숙련) + 베다 + 로카팔라 = <b>4중첩</b>이다.
    /// 그래서 "비슷한 것들로 채운 편성"일수록 수리야가 빨리 찬다.
    ///
    /// 100중첩에 닿으면 그 자리에서 일반행동(라비)을 추가행동으로 터뜨린다.
    /// </summary>
    public sealed class SuryaSavitr : UniquePassiveCode
    {
        public const string ResourceId = "surya_savitr";
        public const int MaxStacks = 100;

        private readonly List<(Unit unit, Action<EventContext> handler)> _hooks = new();
        private Action<EventContext> _cleanupHandler;
        private bool _registered;
        private bool _bursting;
        private int _burstSequence;

        public SuryaSavitr(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "사비타";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            Caster.SetCombatResourceMaximum(ResourceId, MaxStacks, resetCurrent: true);

            // 아군 각자의 '행동'을 듣는다. 턴이 아니라 행동이라 궁극기·추가행동·특수행동도 센다.
            foreach (Unit ally in CombatTargets.AliveAlliesIncludingSelf(Caster))
            {
                Unit bound = ally;
                Action<EventContext> handler = _ => OnAllyActed(bound);
                foreach (BaseEnums.UnitEventType type in ActionEvents) bound.AddListener(type, handler);
                _hooks.Add((bound, handler));
            }

            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;

            foreach ((Unit unit, Action<EventContext> handler) in _hooks)
            {
                if (unit == null) continue;
                foreach (BaseEnums.UnitEventType type in ActionEvents) unit.RemoveListener(type, handler);
            }
            _hooks.Clear();
            Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _cleanupHandler = null;
            _registered = false;
        }

        private static readonly BaseEnums.UnitEventType[] ActionEvents =
        {
            BaseEnums.UnitEventType.OnNormalActivates,
            BaseEnums.UnitEventType.OnUltimateActivates,
            BaseEnums.UnitEventType.OnAdditionalActivates,
            BaseEnums.UnitEventType.OnSpecialActivates,
        };

        private void OnAllyActed(Unit actor)
        {
            if (!_registered || Caster == null || !Caster.isActive) return;
            if (!Battlefield.Is(FieldKind.Sunlight)) return;      // 햇빛이 아니면 아무 일도 없다

            int gain = Likeness(Caster, actor);
            if (gain <= 0) return;

            Caster.AddCombatResource(ResourceId, gain);
            if (Caster.GetCombatResource(ResourceId) < MaxStacks) return;

            TryBurst();
        }

        /// <summary>
        /// 만충이면 그 자리에서 라비를 추가행동으로 터뜨린다.
        /// 라비 자신도 '행동'이라 다시 이 훅을 타므로 재진입을 막는다.
        /// </summary>
        private void TryBurst()
        {
            if (_bursting) return;

            var scheduler = Managers.GameManager.Instance?.ActionScheduler;
            if (scheduler == null) return;

            _bursting = true;
            scheduler.EnqueueAdditional(Caster, $"surya_savitr_{_burstSequence++}", "라비", () =>
            {
                _bursting = false;
                if (Caster != null && Caster.isActive) Caster.CastNormalCode();
            });
        }

        /// <summary>
        /// 두 유닛이 얼마나 닮았는가. 원소 1 + 무기 숙련 1 + 겹치는 속성 태그 수.
        /// 방어구 숙련은 세지 않는다 — "무기 숙련"이 조건이다.
        /// </summary>
        public static int Likeness(Unit self, Unit other)
        {
            if (self == null || other == null) return 0;

            int score = 0;
            if (!string.IsNullOrWhiteSpace(self.Element) &&
                string.Equals(self.Element, other.Element, StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }

            if (WeaponProficiencies(self).Overlaps(WeaponProficiencies(other))) score++;

            var tags = new HashSet<string>(self.UnitTags, StringComparer.OrdinalIgnoreCase);
            foreach (string tag in other.UnitTags)
            {
                if (tags.Contains(tag)) score++;
            }

            return score;
        }

        private static HashSet<string> WeaponProficiencies(Unit unit)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in unit.StartingProficiencies)
            {
                if (Enum.TryParse(raw, true, out EquipmentProficiency proficiency) &&
                    proficiency.IsWeapon() && proficiency != EquipmentProficiency.Shield)
                {
                    set.Add(proficiency.ToString());
                }
            }
            return set;
        }
    }

    /// <summary>Lv.16 특별함 — 특수행동을 할 때마다 올스탯 +3%. 중첩된다.</summary>
    public sealed class SuryaDistinction : PassiveCode
    {
        private const float Bonus = 1.03f;
        private Action<EventContext> _handler;
        private bool _registered;

        public SuryaDistinction(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "특별함";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;
            _handler = _ => Grant();
            Caster.AddListener(BaseEnums.UnitEventType.OnSpecialActivates, _handler);
            _registered = true;
        }

        public override void StopCode()
        {
            if (!_registered || Caster == null) return;
            Caster.RemoveListener(BaseEnums.UnitEventType.OnSpecialActivates, _handler);
            _handler = null;
            _registered = false;
        }

        private void Grant()
        {
            foreach (BaseEnums.PrimaryStat stat in AllStats)
            {
                Caster.AddStatus(BuffStatus.Create(
                    SuryaStatusIds.Distinction + (int)stat, $"surya_distinction_{stat}", CodeName,
                    Caster, Caster, new PrimaryStatMultiplierEffect(Bonus, stat),
                    stackPolicy: BaseEnums.StatusStackPolicy.Stack,
                    isBeneficial: true,
                    description: "특수행동을 할 때마다 올스탯이 3% 증가합니다."));
            }
        }

        private static readonly BaseEnums.PrimaryStat[] AllStats =
        {
            BaseEnums.PrimaryStat.STR, BaseEnums.PrimaryStat.DEX, BaseEnums.PrimaryStat.CON,
            BaseEnums.PrimaryStat.INT, BaseEnums.PrimaryStat.LUK,
        };
    }

    /// <summary>Lv.30 폭주 — 과부하(불+전기) 반응으로 만드는 피해 +25%.</summary>
    public sealed class SuryaOverrun : PassiveCode
    {
        public const float Bonus = 0.25f;

        public SuryaOverrun(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "폭주";
            IgnoresActivationChance = true;
        }
    }

    /// <summary>Lv.41 면벽수련 — CON 집중 훈련 효율 +10%. <c>TrainingManager</c>가 읽는다.</summary>
    public sealed class SuryaWallMeditation : PassiveCode
    {
        public const float TrainingBonus = 0.10f;

        public SuryaWallMeditation(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "면벽수련";
            IgnoresActivationChance = true;
        }

        public override float SupportTrainingBonus(BaseEnums.PrimaryStat stat)
            => stat == BaseEnums.PrimaryStat.CON ? TrainingBonus : 0f;
    }

    /// <summary>
    /// Lv.70 미트라 — 봉우(108)의 금색 상위. 우정 훈련 보너스 +75%.
    /// 둘을 함께 들면 높은 쪽만 적용된다.
    /// </summary>
    public sealed class SuryaMitra : PassiveCode
    {
        public const float FriendshipBonus = 0.75f;

        public SuryaMitra(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "미트라";
            IgnoresActivationChance = true;
            Grade = BaseEnums.CodeGrade.Enhanced;
        }
    }
}
