using System.Collections.Generic;
using Entities;
using Managers.UI.HUD;
using Managers.UI.Screens;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    /// <summary>
    /// UI 진입점. 화면을 직접 그리지 않고, 각 화면 객체에 위임한다.
    ///
    /// 구성:
    ///   <see cref="BattleHud"/>       — 상시 표시되는 전투 HUD(파티/로그/상단바/TAB 코덱스)
    ///   <see cref="PreparationScreen"/> — 준비 페이즈 하단 바
    ///   나머지 ModalScreen 파생 화면들 — 보상/육성/캐릭터 선택/사건
    ///
    /// 화면 객체는 전부 지연 생성이라, 실제로 열리기 전까지 GameObject를 만들지 않는다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private BattleHud _hud;
        private readonly PreparationScreen _preparation = new();
        private readonly RewardScreen _reward = new();
        private readonly TrainingScreen _training = new();
        private readonly TrainingResultScreen _trainingResult = new();
        private readonly CharacterSelectScreen _characterSelect = new();
        private readonly EventScreen _event = new();

        private BattleHud Hud => _hud != null ? _hud : _hud = BattleHud.Create();

        private void Start()
        {
            // HUD를 미리 만들어 두어야 생명력/골드 초기값이 곧바로 보인다.
            UpdateLifeText();
            UpdateGoldText();
        }

        // ── 상단 상태 표시 ───────────────────────────────────────────

        public void UpdateGameStatus(GameState currentState, int remainingTime)
        {
            Hud.SetPhase(currentState, 0);
        }

        public void UpdateGameStatusWithEnemyCount(GameState currentState, int remainingTime, int enemyCount)
        {
            Hud.SetPhase(currentState, enemyCount);
        }

        public void UpdateLifeText()
        {
            if (GameManager.Instance == null) return;
            Hud.SetLife(GameManager.Instance.life);
        }

        public void UpdateGoldText()
        {
            InventoryManager inventory = GameManager.Instance?.inventoryManager;
            if (inventory == null) return;
            Hud.SetGold(inventory.Gold);
        }

        // ── 유닛 상세 / 코덱스 ───────────────────────────────────────

        /// <summary>유닛을 눌렀을 때 코덱스를 그 유닛 기준으로 연다.</summary>
        public void ShowUnitDetail(Unit unit)
        {
            if (unit == null) return;
            Hud.ShowUnitDetail(unit);
        }

        public void ToggleCodex()
        {
            Hud.ToggleCodex();
        }

        // 구 InfoTab 호출 경로 호환. 셀을 클릭하면 코덱스가 열린다.
        public void ShowInfoTab(Unit unit) => ShowUnitDetail(unit);

        public void HideInfoTab()
        {
            // 코덱스는 TAB 또는 닫기 버튼으로만 닫는다. 별도 동작 없음.
        }

        // ── 장비 ─────────────────────────────────────────────────────

        /// <summary>장비 화면은 코덱스의 장비 탭이 대신한다.</summary>
        public void ShowEquipmentPanel()
        {
            Unit main = TrainingManager.GetMainUnit();
            if (main != null) ShowUnitDetail(main);
            else ToggleCodex();
        }

        public void HideEquipmentPanel()
        {
            // 코덱스와 통합되어 별도 숨김 처리가 필요 없다.
        }

        // ── 준비 페이즈 ──────────────────────────────────────────────

        public void ShowPreparationPhasePanel(bool deckOnly, bool actionUsed, string message = null)
        {
            _preparation.Show(deckOnly, actionUsed, message);
        }

        public void HidePreparationPhasePanel()
        {
            _preparation.Hide();
        }

        // ── 보상 ─────────────────────────────────────────────────────

        public void ShowRewardPanel(List<RewardDef> rewards)
        {
            _reward.Show(rewards);
        }

        public void HideRewardPanel()
        {
            _reward.Hide();
        }

        // ── 육성 ─────────────────────────────────────────────────────

        public void ShowTrainingPhasePanel()
        {
            _training.Show();
        }

        public void HideTrainingPhasePanel()
        {
            _training.Hide();
        }

        /// <summary>훈련 결과 화면. 확인을 누르면 GameManager가 준비 페이즈로 넘긴다.</summary>
        public void ShowTrainingResultPanel(TrainingManager.TrainingResult result)
        {
            _training.Hide();
            _trainingResult.Show(result);
        }

        public void HideTrainingResultPanel()
        {
            _trainingResult.Hide();
        }

        // ── 캐릭터 선택 ──────────────────────────────────────────────

        public void ShowCharacterSelection()
        {
            _characterSelect.Show();
        }

        public void HideCharacterSelection()
        {
            _characterSelect.Hide();
        }

        // ── 사건 ─────────────────────────────────────────────────────

        /// <summary>사건 화면의 자동 진행 타이머를 굴린다. 화면 자체는 MonoBehaviour가 아니다.</summary>
        private void Update()
        {
            _event.Tick(Time.deltaTime);
        }

        public void ShowEventStagePanel(StageEventData eventData, int dialogueIndex)
        {
            _event.Show(eventData, dialogueIndex);
        }

        public void ShowEventMessage(string message)
        {
            _event.ShowMessage(message);
        }

        public void ShowEventResolution(string message)
        {
            _event.ShowResolution(message);
        }

        public void HideEventStagePanel()
        {
            _event.Hide();
        }

        // ── 설정 ─────────────────────────────────────────────────────

        public void ShowSettings()
        {
            // 인게임 설정은 아직 별도 화면이 없다. 메인 메뉴의 설정 화면을 재사용하기 전까지는
            // 배속/일시정지 같은 최소 동작만 HUD가 담당한다.
            Debug.Log("[UIManager] 인게임 설정 화면은 아직 준비되지 않았습니다.");
        }

    }
}
