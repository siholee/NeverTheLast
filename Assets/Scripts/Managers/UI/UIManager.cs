using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Managers.UI
{
    public class UIManager : MonoBehaviour
    {
        // 싱글턴 패턴 구현
        public static UIManager Instance { get; private set; }

        [Header("기본 설정")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private RectTransform topPanelArea;     // 12시 위치 (중앙 상단)
        [SerializeField] private RectTransform bottomPanelArea;  // 6시 위치 (중앙 하단)

        [Header("UI 매니저들")]
        [SerializeField] private TopPanelManager topPanelManager;
        [SerializeField] private BottomPanelManager bottomPanelManager;

        [Header("디버깅")]
        [SerializeField] private bool showDebugAreas = false;

        private void Awake()
        {
            // 싱글턴 초기화
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            // 추가 초기화
            EnsureUIComponents();
        }

        private void Start()
        {
            // 캔버스가 지정되지 않았다면, 현재 오브젝트의 Canvas 컴포넌트 사용
            if (mainCanvas == null)
                mainCanvas = GetComponent<Canvas>();

            // UI 영역 설정
            SetupUIAreas();
            
            // UI 매니저들 초기화
            InitializeUIManagers();
        }

        // UI 구성요소 확인 및 생성
        private void EnsureUIComponents()
        {
            // 메인 캔버스 확인
            if (mainCanvas == null)
                mainCanvas = GetComponent<Canvas>();
                
            // 상단 패널 영역 생성
            if (topPanelArea == null)
            {
                GameObject topAreaObj = new GameObject("TopPanelArea");
                topAreaObj.transform.SetParent(mainCanvas.transform, false);
                topPanelArea = topAreaObj.AddComponent<RectTransform>();
                
                if (showDebugAreas)
                {
                    Image topAreaBg = topAreaObj.AddComponent<Image>();
                    topAreaBg.color = new Color(1f, 0f, 0f, 0.2f); // 디버깅용 빨간색 배경
                }
            }
            
            // 하단 패널 영역 생성
            if (bottomPanelArea == null)
            {
                GameObject bottomAreaObj = new GameObject("BottomPanelArea");
                bottomAreaObj.transform.SetParent(mainCanvas.transform, false);
                bottomPanelArea = bottomAreaObj.AddComponent<RectTransform>();
                
                if (showDebugAreas)
                {
                    Image bottomAreaBg = bottomAreaObj.AddComponent<Image>();
                    bottomAreaBg.color = new Color(0f, 0f, 1f, 0.2f); // 디버깅용 파란색 배경
                }
            }
        }

        // UI 영역 설정
        private void SetupUIAreas()
        {
            // 화면 비율에 따른 크기 조정을 위한 기준값 (1920x1080 기준)
            float referenceWidth = 1920f;
            float referenceHeight = 1080f;
            float scaleX = Screen.width / referenceWidth;
            float scaleY = Screen.height / referenceHeight;

            // 상단 패널 영역 설정 (중앙 상단)
            topPanelArea.anchorMin = new Vector2(0.5f, 1f);
            topPanelArea.anchorMax = new Vector2(0.5f, 1f);
            topPanelArea.pivot = new Vector2(0.5f, 1f);
            topPanelArea.anchoredPosition = new Vector2(0f, -20f * scaleY);
            topPanelArea.sizeDelta = new Vector2(800f * scaleX, 80f * scaleY);

            // 하단 패널 영역 설정 (중앙 하단)
            bottomPanelArea.anchorMin = new Vector2(0.5f, 0f);
            bottomPanelArea.anchorMax = new Vector2(0.5f, 0f);
            bottomPanelArea.pivot = new Vector2(0.5f, 0f);
            bottomPanelArea.anchoredPosition = new Vector2(0f, 20f * scaleY);
            bottomPanelArea.sizeDelta = new Vector2(950f * scaleX, 150f * scaleY);
        }

        // UI 매니저들 초기화
        private void InitializeUIManagers()
        {
            // TopPanelManager 초기화
            if (topPanelManager == null)
            {
                GameObject topManagerObj = new GameObject("TopPanelManager");
                topManagerObj.transform.SetParent(transform, false);
                topPanelManager = topManagerObj.AddComponent<TopPanelManager>();
                topPanelManager.Initialize(topPanelArea);
            }
            else
            {
                topPanelManager.Initialize(topPanelArea);
            }

            // BottomPanelManager 초기화
            if (bottomPanelManager == null)
            {
                GameObject bottomManagerObj = new GameObject("BottomPanelManager");
                bottomManagerObj.transform.SetParent(transform, false);
                bottomPanelManager = bottomManagerObj.AddComponent<BottomPanelManager>();
                bottomPanelManager.Initialize(bottomPanelArea);
            }
            else
            {
                bottomPanelManager.Initialize(bottomPanelArea);
            }

            Debug.Log("UI 매니저들 초기화 완료");
        }

        // 상단 패널 매니저 반환
        public TopPanelManager GetTopPanelManager()
        {
            return topPanelManager;
        }

        // 하단 패널 매니저 반환
        public BottomPanelManager GetBottomPanelManager()
        {
            return bottomPanelManager;
        }

        // 화면 크기 변경 시 호출 (필요 시 Scene의 이벤트 시스템에 연결)
        public void OnScreenSizeChanged()
        {
            SetupUIAreas();
        }

        // 게임 상태 변경 시 UI 업데이트
        public void UpdateByGameState(BaseClasses.BaseEnums.GameState state)
        {
            switch (state)
            {
                case BaseClasses.BaseEnums.GameState.Preparation:
                    bottomPanelArea.gameObject.SetActive(true);
                    break;
                case BaseClasses.BaseEnums.GameState.RoundInProgress:
                    bottomPanelArea.gameObject.SetActive(false);
                    break;
                case BaseClasses.BaseEnums.GameState.RoundEnd:
                    bottomPanelArea.gameObject.SetActive(true);
                    break;
                case BaseClasses.BaseEnums.GameState.GameOver:
                    // 게임오버 UI 표시 (필요시 구현)
                    break;
            }
            
            // 각 매니저에게 상태 변경 알림
            topPanelManager.OnGameStateChanged(state);
            bottomPanelManager.OnGameStateChanged(state);
        }
    }
}