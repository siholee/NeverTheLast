using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Codes.Base;
using Combat;
using Effects.Base;
using Effects.Buffs;
using Entities;
using Managers;
using UnityEngine;

namespace Codes.Passive
{
    /// <summary>스킬라 전용 상수. 브레이크 단계가 이 보스의 축이다.</summary>
    public static class ScyllaCombat
    {
        /// <summary>체력 바를 몇 칸으로 끊는가. 칸이 깨질 때마다 새 권능이 열린다.</summary>
        public const int Segments = 3;

        /// <summary>공허의 파도가 들고 있는 고정 능력치. 스킬라가 아무리 커져도 변하지 않는다.</summary>
        public const int WaveCon = 20;
        public const int WaveDex = 20;

        /// <summary>여왕의 진노가 불러내는 세이렌 수.</summary>
        public const int WrathSirenCount = 2;

        /// <summary>세이렌의 적 ID. 여왕의 진노가 이 개체를 불러낸다.</summary>
        public const int SirenEnemyId = 1145;

        public const int WrathStatusId = 6580;
        public const int AttunementStatusId = 6581;

        /// <summary>체력 구간을 표시하는 상태. 미지의 공포가 전투 내내 물고 있다.</summary>
        public const int SegmentStatusId = 6582;
        public const string WrathStatusKey = "scylla_queens_wrath";
        public const string AttunementStatusKey = "scylla_outer_attunement";

        /// <summary>혼돈의 소용돌이가 전기까지 부착하는가. 여왕의 진노가 열어 준다.</summary>
        public static bool HasQueensWrath(Unit unit)
            => unit != null && unit.HasStatusKey(WrathStatusKey);

        /// <summary>일반행동 뒤에 '모든 것을 먼지로'가 따라붙는가. 외신 동조가 열어 준다.</summary>
        public static bool HasOuterAttunement(Unit unit)
            => unit != null && unit.HasStatusKey(AttunementStatusKey);
    }

    /// <summary>
    /// 스킬라 P(1920) — 미지의 공포.
    ///
    /// 체력 바를 세 칸으로 끊고, 칸이 깨질 때마다 새 권능을 연다.
    ///   · 1브레이크 — <b>여왕의 진노</b>: 세이렌 둘을 불러내고 혼돈의 소용돌이에 전기 부착이 붙는다.
    ///   · 2브레이크 — <b>외신 동조</b>: 올스탯 +10%, 일반행동 뒤에 '모든 것을 먼지로'가 따라붙는다.
    ///
    /// 전투가 시작되면 <b>공허의 파도</b>를 필드에 세운다. 고정 CON 20 · DEX 20을 들고
    /// 제 차례마다 필드 전체에 물을 끼얹는다 — 스킬라가 제어당해도 파도는 멈추지 않는다.
    ///
    /// 🔸 원안의 두 SP에는 두 번째 발동 시점이 적혀 있지 않았다. 1브레이크가 여왕의 진노라
    /// 적혀 있으므로 외신 동조를 2브레이크에 두었다 — 확정되면 상수만 고치면 된다.
    /// </summary>
    public sealed class ScyllaUnknownDread : UniquePassiveCode
    {
        private const string StatusKey = "scylla_unknown_dread";

        private Action<EventContext> _damageHandler;
        private Action<EventContext> _cleanupHandler;
        private int _segmentsBroken;
        private bool _waveSummoned;
        private bool _registered;

        public ScyllaUnknownDread(PassiveCodeContext context) : base(context)
        {
            CodeType = BaseEnums.CodeType.Passive;
            CodeName = "미지의 공포";
            IgnoresActivationChance = true;
        }

        public override void CastCode()
        {
            if (Caster == null || _registered) return;

            _segmentsBroken = 0;
            Caster.AddStatus(BuffStatus.Create(
                ScyllaCombat.SegmentStatusId, StatusKey, CodeName, Caster, Caster,
                new ScyllaSegmentEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: $"체력 바가 {ScyllaCombat.Segments}칸으로 나뉘며, 칸이 깨질 때마다 새 권능이 열립니다."));

            _damageHandler = _ => CheckBreak();
            _cleanupHandler = _ => StopCode();
            Caster.AddListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
            Caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
            _registered = true;

            SummonWave();
        }

        public override void StopCode()
        {
            if (!_registered) return;

            if (Caster != null)
            {
                Caster.RemoveListener(BaseEnums.UnitEventType.OnAfterDamageTaken, _damageHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, _cleanupHandler);
                Caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, _cleanupHandler);
                Caster.RemoveStatusByKey(StatusKey);
            }
            _damageHandler = null;
            _cleanupHandler = null;
            _waveSummoned = false;
            _registered = false;
        }

        /// <summary>공허의 파도를 세운다. 스킬라의 능력치를 물려받지 않는 고정 개체다.</summary>
        private void SummonWave()
        {
            if (_waveSummoned || Caster == null || !Caster.IsOnField) return;
            _waveSummoned = GridManager.Instance?.SpawnSummon(Caster, SummonCatalog.VoidWave(Caster)) != null;
        }

        private void CheckBreak()
        {
            if (Caster == null || !Caster.isActive || Caster.HpMax <= 0) return;

            float segment = Caster.HpMax / (float)ScyllaCombat.Segments;
            if (segment <= 0f) return;

            int broken = Mathf.Clamp(
                Mathf.FloorToInt((Caster.HpMax - Caster.HpCurr) / segment), 0, ScyllaCombat.Segments);
            if (broken <= _segmentsBroken) return;

            int before = _segmentsBroken;
            _segmentsBroken = broken;

            if (before < 1 && broken >= 1) OpenQueensWrath();
            if (before < 2 && broken >= 2) OpenOuterAttunement();
        }

        /// <summary>1브레이크 — 세이렌 둘을 부르고 혼돈의 소용돌이에 전기를 얹는다.</summary>
        private void OpenQueensWrath()
        {
            if (Caster == null) return;

            Caster.AddStatus(BuffStatus.Create(
                ScyllaCombat.WrathStatusId, ScyllaCombat.WrathStatusKey, "여왕의 진노",
                Caster, Caster, new MarkerBuffEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "혼돈의 소용돌이가 적중 대상에게 전기 원소를 함께 부여합니다."));

            SpawnSirens();
            Debug.Log("[스킬라] 1브레이크 — 여왕의 진노");
        }

        /// <summary>2브레이크 — 올스탯이 오르고 일반행동 뒤에 추가행동이 붙는다.</summary>
        private void OpenOuterAttunement()
        {
            if (Caster == null) return;

            foreach (BaseEnums.PrimaryStat stat in new[]
                     {
                         BaseEnums.PrimaryStat.STR, BaseEnums.PrimaryStat.DEX, BaseEnums.PrimaryStat.CON,
                         BaseEnums.PrimaryStat.INT, BaseEnums.PrimaryStat.LUK,
                     })
            {
                Caster.AddStatus(BuffStatus.Create(
                    ScyllaCombat.AttunementStatusId, $"{ScyllaCombat.AttunementStatusKey}_{stat}", "외신 동조",
                    Caster, Caster, new PrimaryStatMultiplierEffect(1.1f, stat),
                    stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                    isBeneficial: true,
                    description: "올스탯 +10%."));
            }

            // 일반행동 뒤 추가행동을 여는 표식. 일반행동 코드가 이 키를 본다.
            Caster.AddStatus(BuffStatus.Create(
                ScyllaCombat.AttunementStatusId, ScyllaCombat.AttunementStatusKey, "외신 동조",
                Caster, Caster, new MarkerBuffEffect(),
                stackPolicy: BaseEnums.StatusStackPolicy.Ignore,
                isBeneficial: true,
                description: "일반행동 뒤에 '모든 것을 먼지로'가 따라붙습니다."));

            Debug.Log("[스킬라] 2브레이크 — 외신 동조");
        }

        private void SpawnSirens()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            int spawned = 0;
            // 적 진영의 빈 칸을 앞열부터 훑어 채운다. 자리가 없으면 부르지 못한다.
            foreach (int x in new[] { 1, 2 })
            {
                for (int y = 1; y <= 4 && spawned < ScyllaCombat.WrathSirenCount; y++)
                {
                    // 빈 칸인지 먼저 묻는다. SpawnUnit에 바로 던지면 점유된 칸마다 경고가 쌓인다.
                    if (!grid.IsCellAvailable(x, y)) continue;
                    if (grid.SpawnUnit(x, y, true, ScyllaCombat.SirenEnemyId) != null) spawned++;
                }
                if (spawned >= ScyllaCombat.WrathSirenCount) break;
            }
            Debug.Log($"[스킬라] 여왕의 진노 — 세이렌 {spawned}기 소환");
        }
    }

    /// <summary>체력 바를 여러 칸으로 끊어 브레이크를 눈에 보이게 한다.</summary>
    internal sealed class ScyllaSegmentEffect : BaseEffect
    {
        public ScyllaSegmentEffect() : base(0) { }

        public override bool IsBeneficial => true;

        public override int HpSegmentCount(Unit unit)
            => unit == Target ? ScyllaCombat.Segments : 0;
    }
}
