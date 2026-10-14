#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using Managers.UI.Core;
using UnityEngine;

namespace Managers.UI.DevTools
{
    public sealed partial class VerifyHarnessRunner
    {
        private IEnumerator ScenarioCoreSealSafety(VerifyScenarioRequest config)
        {
            _ruiFailures.Clear();
            bool enabled = true;
            yield return YvPrepareScene(config, value => enabled = value);
            var game = GameManager.Instance;
            game.ActionScheduler.EndRound();
            GridManager.Instance.OnRoundEnd();
            game.gameState = BaseEnums.GameState.Preparation;
            var party = RewardCodeFlow.Party();
            foreach (var unit in party) unit.ApplyRewardCodeInventory(Enumerable.Repeat(0, 7).ToList(), new List<int>());
            Unit owner = party.First();
            owner.TryGrantRewardCode(540);
            var active = owner.ActiveRewardInstance(540);
            active.charge = 1;
            var copies = Enumerable.Range(0, 13).Select(index => new RewardCodeInstanceData
            {
                codeId = 540, instanceId = Guid.NewGuid().ToString("N"), acquiredStage = 3, charge = index
            }).ToList();
            owner.ApplyRewardCodeInventory(owner.RewardCodeSlots, copies.Select(x => x.codeId).ToList(),
                owner.RewardCodeSlotInstances, copies);
            string selected = copies.Last().instanceId;
            RuiCheck("인장 잘못된 개체 거절", "재고 불변", "ID 검증",
                !owner.TryReplaceRewardCode(540, 640, "missing") && !owner.TryReplaceRewardCode(540, 640, "") &&
                !owner.RemoveRewardCodeInstance(null) && !owner.RemoveRewardCodeInstance(""));
            game.inventoryManager.AddItem(5201);
            int stock = game.inventoryManager.CountItem(5201);
            RuiCheck("인장 잘못된 사다리 요청 거절", "540→601 거절·재고 유지", "API",
                !game.UseSeal(5201, owner, 540, 601, active.instanceId) && game.inventoryManager.CountItem(5201) == stock);
            game.uiManager.ShowInventoryPanel();
            yield return WaitUnscaled(.25f);
            var root = GameObject.Find("InventoryCanvas");
            RuiClick(RuiFind(root, "Item_5201"));
            yield return WaitUnscaled(.15f);
            RuiClick(RuiTargetCard(root, owner));
            yield return WaitUnscaled(.15f);
            RuiCheck("인장 전체 개체 표시", "활성1+보관13·2페이지", "다음 버튼",
                RuiFind(root, "NextPage") != null);
            RuiClick(RuiFind(root, "NextPage"));
            yield return WaitUnscaled(.15f);
            yield return CaptureStill("core_seal_stored_duplicate_page2");
            RuiClick(RuiFind(root, "Option5"));
            yield return WaitUnscaled(.15f);
            var upgraded = owner.StoredRewardCodeInstances.FirstOrDefault(x => x.instanceId == selected);
            RuiCheck("보관 개체 실제 인장 선택", "선택한 저장12만640·활성540저장1 보존", upgraded?.codeId.ToString(),
                upgraded?.codeId == 640 && upgraded.charge == 12 && upgraded.acquiredStage == 3 &&
                owner.ActiveRewardInstance(540)?.instanceId == active.instanceId && active.charge == 1 &&
                owner.StoredRewardCodeInstances.Count(x => x.codeId == 540) == 12 &&
                game.inventoryManager.CountItem(5201) == stock - 1);
            RuiCheck("소비 후 오래된 선택 거절", "인장 추가 소비 없음", "개체 변경",
                !game.UseSeal(5201, owner, 540, 640, selected) && game.inventoryManager.CountItem(5201) == stock - 1);
            owner.TryGrantRewardCode(512);
            string harmony = owner.ActiveRewardInstance(512).instanceId;
            game.inventoryManager.AddItem(5201);
            int harmonyStock = game.inventoryManager.CountItem(5201);
            RuiCheck("조화 인장 범위 밖 요청 거절", "512→619 거절·재고 유지", "API",
                !game.UseSeal(5201, owner, 512, 619, harmony) && game.inventoryManager.CountItem(5201) == harmonyStock);
            game.uiManager.HideInventoryPanel(); game.uiManager.ShowInventoryPanel();
            yield return WaitUnscaled(.15f);
            root = GameObject.Find("InventoryCanvas");
            RuiClick(RuiFind(root, "Item_5201")); yield return WaitUnscaled(.15f);
            RuiClick(RuiTargetCard(root, owner)); yield return WaitUnscaled(.15f);
            RuiClick(RuiFind(root, "Option0")); yield return WaitUnscaled(.15f);
            yield return CaptureStill("core_seal_harmony_choices");
            RuiClick(RuiFind(root, "Option0")); yield return WaitUnscaled(.15f);
            RuiCheck("조화 인장 실제 2단계 개체 유지", "512→601·같은GUID·1개소비", owner.ActiveRewardInstance(601)?.instanceId,
                owner.ActiveRewardInstance(601)?.instanceId == harmony && !owner.OwnedRewardCodeIds.Contains(512) &&
                game.inventoryManager.CountItem(5201) == harmonyStock - 1);
            game.uiManager.HideInventoryPanel();
            var saved = new UnitSaveData { currentHP = owner.HpCurr };
            owner.WriteRunState(saved);
            owner.RestoreRunState(JsonUtility.FromJson<UnitSaveData>(JsonUtility.ToJson(saved)));
            RuiCheck("인장 개체 저장 복원", "GUID·저장치·취득층 보존", selected,
                owner.StoredRewardCodeInstances.Any(x => x.instanceId == selected && x.codeId == 640 && x.charge == 12 && x.acquiredStage == 3));
            RuiFinish();
            OsFinish(enabled);
        }
    }
}
#endif
