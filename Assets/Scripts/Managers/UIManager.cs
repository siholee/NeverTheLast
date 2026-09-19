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
    ///   <see cref="BattleHud"/>       — 상시 표시되는 전투 HUD(파티/로그/상단바/TAB 캐릭터 창)
    ///   <see cref="PauseMenuScreen"/> — ESC 메뉴. 설정과 자료실(<see cref="WikiScreen"/>)을 여기서 연다
    ///   <see cref="PreparationScreen"/> — 준비 페이즈 하단 바
    ///   나머지 ModalScreen 파생 화면들 — 보상/상점/스킬/육성/캐릭터 선택/사건
    ///
    /// 화면 객체는 전부 지연 생성이라, 실제로 열리기 전까지 GameObject를 만들지 않는다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private BattleHud _hud;
        private readonly PreparationScreen _preparation = new();
        private readonly RewardScreen _reward = new();
        private readonly ShopScreen _shop = new();
        private readonly SkillScreen _skill = new();
        private readonly TrainingScreen _training = new();
        private readonly TrainingResultScreen _trainingResult = new();
        private readonly CharacterSelectScreen _characterSelect = new();
        private readonly EventScreen _event = new();
        private readonly BattleResultScreen _battleResult = new();

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

        // ── 전투 결과 ────────────────────────────────────────────────

        /// <summary>전투 정산 화면. 확인을 누르면 <paramref name="onContinue"/>가 다음 단계를 이어 간다.</summary>
        public void ShowBattleResult(BattleResultData result, System.Action onContinue)
        {
            _battleResult.Show(result, onContinue);
        }

        public void HideBattleResult()
        {
            _battleResult.Hide();
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

        // ── 상점 ─────────────────────────────────────────────────────

        public void ShowShopPanel()
        {
            _shop.Show();
        }

        public void HideShopPanel()
        {
            _shop.Hide();
        }

        // ── 스킬 ─────────────────────────────────────────────────────

        public void ShowSkillPanel()
        {
            _skill.Show();
        }

        public void HideSkillPanel()
        {
            _skill.Hide();
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

        /// <summary>
        /// 사건 화면의 자동 진행 타이머와 캐릭터 선택의 키보드·패드 입력을 굴린다.
        /// 두 화면 모두 MonoBehaviour가 아니다.
        /// </summary>
        private void Update()
        {
            _event.Tick(Time.deltaTime);
            _characterSelect.Tick();
            _battleResult.Tick();
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

        /// <summary>자리가 없어 영입이 막혔을 때, 떠나보낼 사람을 고르는 화면.</summary>
        public void ShowEventRosterPrompt(string message, List<Unit> candidates)
        {
            _event.ShowRosterPrompt(message, candidates);
        }

        public void HideEventStagePanel()
        {
            _event.Hide();
        }

        // ── 설정 ─────────────────────────────────────────────────────

        public bool IsPauseMenuOpen => _pauseMenu != null && _pauseMenu.IsVisible;

        /// <summary>
        /// 아래 화면의 키보드·패드 입력을 가로채는 창이 떠 있는가 — ESC 메뉴(과 그 위의 자료실·설정) 또는
        /// TAB 캐릭터 창. 캐릭터 선택처럼 스스로 입력을 읽는 화면이 이걸 보고 멈춘다.
        /// </summary>
        public bool IsOverlayOpen => IsPauseMenuOpen || (_hud != null && _hud.IsCodexOpen);

        /// <summary>
        /// ESC와 우상단 ≡ 버튼의 단일 진입점. 메뉴 묶음 안에서는 가장 위의 것 하나만 닫고
        /// (설정 → 자료실 → 메뉴), 메뉴가 닫혀 있으면 연다. TAB 캐릭터 창은 건드리지 않는다 —
        /// 메뉴가 그 위 층에 뜨고, 메뉴를 닫으면 캐릭터 창이 그대로 남아 있다.
        /// 설정은 메인 메뉴와 같은 패널을 쓴다.
        /// </summary>
        public void HandleEscape()
        {
            if (_settingsUI != null && _settingsUI.IsOpen)
            {
                _settingsUI.Close();
                return;
            }

            // 떠 있는 창 중 가장 위의 것부터 닫는다(자료실 → 메뉴 → 확인 → 상점·스킬·훈련).
            // 캐릭터 창이 떠 있으면 그 아래 층 창은 건드리지 않고 예전처럼 메뉴를 그 위에 띄운다.
            int floor = _hud != null && _hud.IsCodexOpen ? UI.Theme.UITheme.LayerMenu : int.MinValue;
            if (ModalScreen.TryEscapeTop(floor)) return;

            // 덱 구성 모드는 창이 아니지만 입력을 바꾸는 모드다. ESC로 빠져나올 수 있어야 한다.
            if (floor == int.MinValue && _preparation.TryExitDeckMode()) return;

            PauseMenu.Toggle();
        }

        private PauseMenuScreen _pauseMenu;
        private UI.SettingsUI _settingsUI;

        /// <summary>자료실(게임 내 위키). ESC 메뉴에서 연다.</summary>
        private readonly WikiScreen _wiki = new();

        private PauseMenuScreen PauseMenu => _pauseMenu ??= new PauseMenuScreen(() =>
            _settingsUI != null ? _settingsUI : _settingsUI = gameObject.AddComponent<UI.SettingsUI>(), _wiki);

    }
}
