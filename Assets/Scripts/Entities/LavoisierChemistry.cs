using System;
using System.Collections.Generic;
using BaseClasses;
using Entities.Status;
using Managers;
using UnityEngine;

namespace Entities
{
    public enum ReagentKind { Fuel, Stabilizer, Catalyst }

    /// <summary>시약은 1/4 단위의 정수로 보관한다. CON 변경은 한도만 바꾼다.</summary>
    public sealed class ReagentInventory
    {
        private readonly int[] quarters = new int[3];
        public int Capacity { get; private set; } = 20;
        public float this[ReagentKind kind] => quarters[(int)kind] / 4f;
        public float Batch => Math.Min(quarters[0], Math.Min(quarters[1], quarters[2])) / 4f;
        public int NormalActions { get; private set; }

        public void Resize(int constitution)
        {
            Capacity = 20 + Math.Max(0, constitution);
            for (int i = 0; i < quarters.Length; i++) quarters[i] = Math.Min(quarters[i], Capacity * 4);
        }

        public void Receive(ReagentKind kind) => Add(kind, Capacity * 2);

        public void ResolveNormal()
        {
            if (++NormalActions % 2 != 0) return;
            int lowest = 0;
            for (int i = 1; i < quarters.Length; i++)
                if (quarters[i] < quarters[lowest]) lowest = i;
            Add((ReagentKind)lowest, Capacity);
        }

        private void Add(ReagentKind kind, int amount)
            => quarters[(int)kind] = Math.Min(Capacity * 4, quarters[(int)kind] + amount);

        public float Consume()
        {
            int amount = Math.Min(quarters[0], Math.Min(quarters[1], quarters[2]));
            for (int i = 0; i < quarters.Length; i++) quarters[i] -= amount;
            return amount / 4f;
        }

        public void Clear()
        {
            Array.Clear(quarters, 0, quarters.Length);
            NormalActions = 0;
        }

        public static float CatalystMultiplier(int luck)
            => 1f + Math.Max(0, luck) / (100f + Math.Max(0, luck));

        public static float Damage(int intelligence, int luck, float batch)
            => Math.Max(0, intelligence) * (8f + 0.3f * batch) * CatalystMultiplier(luck);
    }

    /// <summary>유닛에 종속된 전투 자원. 개전 전 사전 초기화하여 아군 오라의 순서에 영향받지 않는다.</summary>
    public sealed class LavoisierChemistry
    {
        public const int UnitId = 8;
        public const int PassiveId = 208;
        private readonly Unit owner;
        private readonly HashSet<(Unit source, ReagentKind kind)> receivedThisTurn = new();
        private int receivedTurn = -1;
        private int resolvedTurn = -1;
        public ReagentInventory Reagents { get; } = new();
        public bool Active { get; private set; }
        public bool CanReact => Active && Reagents.Batch > 0 && owner.ManaCurr >= 3;
        public float PreviewDamage => Reagents.Batch > 0
            ? ReagentInventory.Damage(owner.GetBaseInt(), owner.GetBaseLuk(), Reagents.Batch) : 0;

        public LavoisierChemistry(Unit owner)
        {
            this.owner = owner;
            owner.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActionResolved, OnNormalResolved);
        }

        public void BeginRound()
        {
            EndRound();
            Active = true;
            Resize();
        }

        public void EndRound()
        {
            Active = false;
            Reagents.Clear();
            receivedThisTurn.Clear();
            receivedTurn = resolvedTurn = -1;
            owner.ClearUltimateResource();
        }

        public void Resize() => Reagents.Resize(owner.GetBaseCon());

        public void Receive(Unit source, ReagentKind kind)
        {
            if (!Active || !owner.isActive || owner.HpCurr <= 0 || owner.IsBench || source == null) return;
            while (source.IsSummon && source.SummonOwner != null) source = source.SummonOwner;
            if (source == owner || source.IsEnemy != owner.IsEnemy || source.IsBench) return;
            if (receivedTurn != owner.TurnCount)
            {
                receivedTurn = owner.TurnCount;
                receivedThisTurn.Clear();
            }
            if (!receivedThisTurn.Add((source, kind))) return;
            Resize();
            Reagents.Receive(kind);
            owner.RefreshView();
        }

        public void ReceiveBuff(UnitStatus status, Unit source = null)
        {
            if (status == null || (!status.IsBeneficial && status.Category != BaseEnums.StatusCategory.Positive)) return;
            // 회복 틱만 발생하는 상태/표식은 버프 시약으로 세지 않는다.
            if (!status.Effects.Exists(e => e.EffectObject != null && e.EffectObject.CountsAsReagentBuff)) return;
            Receive(source ?? status.Caster, ReagentKind.Catalyst);
        }

        private void OnNormalResolved(EventContext context)
        {
            if (!Active || !owner.isActive || owner.IsBench || resolvedTurn == owner.TurnCount) return;
            ActionScheduler scheduler = GameManager.Instance?.ActionScheduler;
            if (scheduler?.ExecutingUnit != null &&
                (scheduler.ExecutingUnit != owner || scheduler.ActingKind != ActionScheduler.ActionKind.Normal)) return;
            resolvedTurn = owner.TurnCount;
            Resize();
            Reagents.ResolveNormal();
            owner.AddUltimateResource(1);
            owner.RefreshView();
        }

        public string Summary => $"연료 {Reagents[ReagentKind.Fuel]:0.##}  안정제 {Reagents[ReagentKind.Stabilizer]:0.##}  촉매 {Reagents[ReagentKind.Catalyst]:0.##} / 각 {Reagents.Capacity}";
        public string WaitingReason => owner.ManaCurr < 3 ? $"반응 준비 {owner.ManaCurr}/3"
            : Reagents[ReagentKind.Fuel] <= 0 ? "연료 부족"
            : Reagents[ReagentKind.Stabilizer] <= 0 ? "안정제 부족"
            : Reagents[ReagentKind.Catalyst] <= 0 ? "촉매 부족" : "배합 준비 완료";
    }
}
