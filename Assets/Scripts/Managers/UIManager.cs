using System;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using Managers.UI;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    public class UIManager : MonoBehaviour
    {
        // 상단 (골드/배속 제거 — 턴제에 불필요)
        public TMPro.TextMeshProUGUI gameLifeText;
        public TMPro.TextMeshProUGUI gameStageText;

        // Phase 4: 인벤토리 UI
        [Header("인벤토리")]
        public InventoryUI inventoryUI;

        public void TestButtonClick()
        {
            // Start Round 함수 여기에 구현하면 됨
            Debug.Log("Test Button Clicked");
        }

        // 턴 행동 선택 패널
        public GameObject actionPanel;
        public UnityEngine.UI.Button basicButton;    // Basic 공격 (SP +1 생성)
        public UnityEngine.UI.Button classButton;    // 클래스 스킬 (class_skill 슬롯)
        public UnityEngine.UI.Button normalButton;   // Skill 공격 (SP 소모)
        public UnityEngine.UI.Button ultimateButton;

        // Phase 3: SP 표시
        [Header("SP 표시")]
        public TMPro.TextMeshProUGUI spText;         // "SP: 3/5" 형식 표시
        public UnityEngine.UI.Image[] spPips;        // 도트 방식 선택적 사용

        private void Start()
        {
            // SP 변화 이벤트 구독
            if (GameManager.Instance?.spManager != null)
                GameManager.Instance.spManager.OnSPChanged += OnSPChanged;

            // 버튼 onClick 자동 연결 (SceneBuilder 생성 씬에서 Inspector 와이어링 없이 동작)
            basicButton?   .onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectBasic());
            classButton?   .onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectClass());
            normalButton?  .onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectNormal());
            ultimateButton?.onClick.AddListener(() => BattleManager.Instance?.OnPlayerSelectUltimate());
        }

        private void OnDestroy()
        {
            if (GameManager.Instance?.spManager != null)
                GameManager.Instance.spManager.OnSPChanged -= OnSPChanged;
        }

        private void OnSPChanged(int current)
        {
            RefreshSPDisplay(current);
        }

        private void RefreshSPDisplay(int current)
        {
            int max = SPManager.Max;
            if (spText != null)
                spText.text = $"SP {current}/{max}";

            // 도트 방식: 채워진 도트는 밝게, 빈 도트는 어둡게
            if (spPips != null)
            {
                for (int i = 0; i < spPips.Length; i++)
                {
                    if (spPips[i] == null) continue;
                    spPips[i].color = i < current
                        ? new Color(1f, 0.85f, 0f)   // 채움: 황금색
                        : new Color(0.3f, 0.3f, 0.3f); // 빔: 회색
                }
            }
        }

        // Phase 6: 행동 핍 표시
        [Header("행동 예산 핍")]
        public TMPro.TextMeshProUGUI actionsLeftText;   // "행동: 1" 형식
        public UnityEngine.UI.Image[] actionPips;       // 행동 수 도트 표시

        /// <summary>
        /// 히어로 턴에 행동 선택 패널 표시.
        /// Phase 6: actionsLeft / bonusActionsLeft로 핍 업데이트.
        /// </summary>
        public void ShowActionPanel(Unit unit, int actionsLeft = 1, int bonusActionsLeft = 0)
        {
            if (actionPanel != null)
            {
                actionPanel.SetActive(true);

                // 스킬 이름 + 궁극기 표시 여부를 BattleUI에 위임 (동적 라벨 업데이트)
                var battleUI = UnityEngine.Object.FindFirstObjectByType<Managers.UI.BattleUI>();
                battleUI?.UpdateSkillLabels(unit);

                // Basic 버튼: BasicCode(마력탄 등) 있고 타겟이 있을 때 활성
                if (basicButton != null)
                {
                    var basicCode = unit.BasicCode ?? unit.NormalCode;
                    basicButton.interactable = basicCode != null && basicCode.HasValidTarget();
                }

                // 클래스 스킬 버튼: ClassCode 있고 타겟 유효할 때 활성
                if (classButton != null && classButton.gameObject.activeSelf)
                {
                    classButton.interactable = unit.ClassCode != null && unit.ClassCode.HasValidTarget();
                }

                // Normal(Skill) 버튼: NormalCode 있고, SP 충분하고, 타겟 있을 때 활성
                if (normalButton != null && unit.NormalCode != null)
                {
                    int cost = unit.NormalCode.SpCost;
                    normalButton.interactable = GameManager.Instance.spManager.CanAfford(cost)
                                               && unit.NormalCode.HasValidTarget();
                }

                // 궁극기 버튼: UltimateCode 있고 마나 충분할 때만 활성 (없으면 숨김 처리됨)
                if (ultimateButton != null && ultimateButton.gameObject.activeSelf)
                {
                    ultimateButton.interactable = unit.UltimateCode != null
                        && unit.ManaCurr >= unit.ManaMax
                        && unit.UltimateCode.HasValidTarget();
                }

                // SP 표시 즉시 갱신
                RefreshSPDisplay(GameManager.Instance.spManager.Current);

                // Phase 6: 행동 수 핍 갱신
                RefreshActionPips(actionsLeft);
            }
            Debug.Log($"[UI] {unit.UnitName} 행동 선택 패널 표시 (행동: {actionsLeft}, SP: {GameManager.Instance.spManager.Current}/{SPManager.Max})");
        }

        private void RefreshActionPips(int actionsLeft)
        {
            if (actionsLeftText != null)
                actionsLeftText.text = $"행동 {actionsLeft}";

            if (actionPips != null)
            {
                for (int i = 0; i < actionPips.Length; i++)
                {
                    if (actionPips[i] == null) continue;
                    actionPips[i].color = i < actionsLeft
                        ? new Color(0.2f, 0.8f, 1f)   // 남은 행동: 청록
                        : new Color(0.3f, 0.3f, 0.3f); // 소모됨: 회색
                }
            }
        }

        /// <summary>
        /// 행동 선택 패널 숨기기
        /// </summary>
        public void HideActionPanel()
        {
            if (actionPanel != null)
            {
                actionPanel.SetActive(false);
            }
        }

        // Phase 7: 보상 선택 패널
        [Header("보상 패널")]
        public GameObject rewardPanel;
        public TMPro.TextMeshProUGUI[] rewardTexts;    // 보상 이름 (3개)
        public UnityEngine.UI.Button[] rewardButtons;  // 보상 선택 버튼 (3개)

        private System.Collections.Generic.List<RewardDef> _currentRewards;

        /// <summary>보상 패널 표시 및 버튼 초기화</summary>
        public void ShowRewardPanel(System.Collections.Generic.List<RewardDef> rewards)
        {
            if (rewardPanel == null) return;
            _currentRewards = rewards;
            rewardPanel.SetActive(true);

            for (int i = 0; i < rewardButtons.Length; i++)
            {
                if (rewardButtons[i] == null) continue;
                int captured = i;

                // 텍스트 갱신
                if (rewardTexts != null && i < rewardTexts.Length && rewardTexts[i] != null)
                {
                    rewardTexts[i].text = i < rewards.Count
                        ? $"{rewards[i].displayName}\n{rewards[i].description}"
                        : "";
                }

                // 버튼 활성화 여부
                rewardButtons[i].gameObject.SetActive(i < rewards.Count);

                // 리스너 교체
                rewardButtons[i].onClick.RemoveAllListeners();
                rewardButtons[i].onClick.AddListener(() =>
                {
                    if (captured < _currentRewards.Count)
                    {
                        // 단순화: 첫 번째 활성 히어로에게 적용
                        var hero = GridManager.Instance.heroList.Find(h => h.isActive);
                        GameManager.Instance.rewardManager?.ApplyReward(_currentRewards[captured], hero);
                    }
                });
            }
        }

        /// <summary>보상 패널 숨기기</summary>
        public void HideRewardPanel()
        {
            if (rewardPanel != null)
                rewardPanel.SetActive(false);
        }

        // ── 타겟 선택 UI (단일 타겟 동률 시) ─────────────────────────────────
        // TODO: SceneBuilder로 실제 UI 요소 연결 후 구현

        /// <summary>
        /// 단일 타겟 동률 시 플레이어 선택 UI 표시.<br/>
        /// 각 후보 유닛의 Cell을 하이라이트하고 클릭 시 BattleManager.OnPlayerSelectTarget() 호출.
        /// </summary>
        public void ShowTargetSelection(System.Collections.Generic.List<Entities.Unit> candidates)
        {
            Debug.Log($"[UI] 타겟 선택 — 후보: {string.Join(", ", candidates.ConvertAll(u => u.UnitName))}");
            // 임시: 0.5초 후 자동으로 첫 번째 후보 선택 (UI 미구현 상태 대비)
            StartCoroutine(AutoSelectTarget(candidates));
        }

        private System.Collections.IEnumerator AutoSelectTarget(System.Collections.Generic.List<Entities.Unit> candidates)
        {
            yield return new UnityEngine.WaitForSeconds(0.5f);
            if (candidates.Count > 0)
                Managers.BattleManager.Instance?.OnPlayerSelectTarget(candidates[0]);
        }

        public void HideTargetSelection()
        {
            Debug.Log("[UI] 타겟 선택 UI 숨김");
            // TODO: 하이라이트 해제
        }

        // ── 캐릭터 선택 UI ────────────────────────────────────────────────────
        // TODO: SceneBuilder로 CharacterSelection 패널 연결

        /// <summary>
        /// 캐릭터 선택 패널 표시.
        /// BattleUI가 있으면 캐릭터 선택 화면을 보여주고,
        /// 없으면 기본 팀으로 자동 시작.
        /// </summary>
        public void ShowCharacterSelection()
        {
            if (CharacterSelectionManager.Instance == null)
            {
                var go = new GameObject("CharacterSelectionManager");
                go.AddComponent<CharacterSelectionManager>();
            }
            // BattleUI 패널 사용 (같은 씬에 있음)
            var battleUI = UnityEngine.Object.FindFirstObjectByType<Managers.UI.BattleUI>();
            if (battleUI != null)
                battleUI.ShowCharacterSelectionPanel();
            else
            {
                // 폴백: BattleUI 없으면 기본 팀으로 즉시 시작
                CharacterSelectionManager.Instance.UseDefaultLineup();
                CharacterSelectionManager.Instance.ConfirmSelection();
            }
        }

    }
}