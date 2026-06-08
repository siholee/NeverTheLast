// !! 반드시 "Editor" 폴더에 있어야 합니다 — Unity가 빌드에서 자동으로 제외합니다.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Managers.UI;
using Core;

namespace NeverTheLast.Editor
{
    public static class SceneBuilder
    {
        // ════════════════════════════════════════════════════════════════════════
        // 진입점
        // ════════════════════════════════════════════════════════════════════════

        [MenuItem("NeverTheLast/Setup Scenes (한 번만 실행)", false, 0)]
        public static void SetupAll()
        {
            EnsureDirectories();
            string menuPath = BuildMainMenuScene();
            string gamePath = BuildBattleScene();
            RegisterScenes(menuPath, gamePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(menuPath);
            Debug.Log("[NeverTheLast] 씬 셋업 완료! MainMenu(0), Game(1) 등록됨.");
        }

        [MenuItem("NeverTheLast/Rebuild Battle Scene", false, 1)]
        public static void RebuildBattleScene()
        {
            EnsureDirectories();
            string gamePath = BuildBattleScene();

            // Build Settings에 두 씬 등록 (MainMenu=0, Game=1)
            const string menuPath = "Assets/Scenes/MainMenu.unity";
            RegisterScenes(menuPath, gamePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 초기 씬(MainMenu)으로 복귀 — 없으면 Game으로 폴백
            if (System.IO.File.Exists(menuPath))
                EditorSceneManager.OpenScene(menuPath);
            else
                EditorSceneManager.OpenScene(gamePath);

            Debug.Log("[NeverTheLast] Battle Scene 재구성 완료 — MainMenu.unity 열림 (초기 씬).");
        }

        // ════════════════════════════════════════════════════════════════════════
        // BATTLE (GAME) 씬
        // ════════════════════════════════════════════════════════════════════════

        static string BuildBattleScene()
        {
            const string path = "Assets/Scenes/Game.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 카메라 ──────────────────────────────────────────────────────────
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor    = new Color(0.08f, 0.08f, 0.12f);
            cam.clearFlags         = CameraClearFlags.SolidColor;
            cam.nearClipPlane      = 0.1f;
            cam.farClipPlane       = 20f;
            camGo.AddComponent<AudioListener>();

            // ── Directional Light ────────────────────────────────────────────────
            var lightGo = new GameObject("Directional Light");
            var lit = lightGo.AddComponent<Light>();
            lit.type      = LightType.Directional;
            lit.intensity = 1f;
            lit.color     = new Color(1f, 0.96f, 0.84f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ── EventSystem ──────────────────────────────────────────────────────
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // ── 진영 구분 배경 (월드 공간 SpriteRenderer) ────────────────────────
            CreateSideBackground("HeroBackground",
                new Color(0.10f, 0.14f, 0.28f, 0.55f),
                new Vector3(-5f, 0f, 1f), new Vector3(9f, 4.5f, 1f));
            CreateSideBackground("EnemyBackground",
                new Color(0.28f, 0.10f, 0.10f, 0.55f),
                new Vector3( 5f, 0f, 1f), new Vector3(9f, 4.5f, 1f));

            // ── 구분선 ──────────────────────────────────────────────────────────
            CreateWorldQuad("HorizontalSep", new Vector3(0f, 0f, 0.5f),
                new Vector3(20f, 0.04f, 1f), new Color(0.5f, 0.5f, 0.5f, 0.3f));
            CreateWorldQuad("VerticalSep", new Vector3(0f, 0f, 0.5f),
                new Vector3(0.04f, 4.5f, 1f), new Color(0.5f, 0.5f, 0.5f, 0.4f));

            // ── Managers 부모 오브젝트 ───────────────────────────────────────────
            var managersGo = new GameObject("Managers");

            // GameManager + DataManager + SfxManager (반드시 같은 GO)
            // GameManager.Start()가 GetComponent<DataManager/SfxManager>() 호출
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(managersGo.transform);
            var gameManagerComp = gmGo.AddComponent<Managers.GameManager>();
            gmGo.AddComponent<Managers.DataManager>();
            gmGo.AddComponent<Managers.SfxManager>();

            // GridManager
            var gridGo = new GameObject("GridManager");
            gridGo.transform.SetParent(managersGo.transform);
            var gridManagerComp = gridGo.AddComponent<Managers.GridManager>();

            // 그리드 프리팹 연결
            var cellPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cell.prefab");
            var heroPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HeroPrefab.prefab");
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyPrefab.prefab");
            if (cellPrefab != null && heroPrefab != null && enemyPrefab != null)
            {
                var gridSO = new SerializedObject(gridManagerComp);
                gridSO.FindProperty("cellPrefab") .objectReferenceValue = cellPrefab;
                gridSO.FindProperty("heroPrefab") .objectReferenceValue = heroPrefab;
                gridSO.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
                gridSO.ApplyModifiedProperties();
                Debug.Log("[SceneBuilder] GridManager 프리팹 연결 완료");
            }
            else
            {
                Debug.LogWarning("[SceneBuilder] 프리팹 로드 실패. Inspector에서 직접 연결하세요.");
                Debug.LogWarning($"  Cell={cellPrefab}, Hero={heroPrefab}, Enemy={enemyPrefab}");
            }

            // BattleManager + BattleUI (자가 생성 전투 UI — Awake에서 Canvas 빌드)
            var bmGo = new GameObject("BattleManager");
            bmGo.transform.SetParent(managersGo.transform);
            var battleManagerComp = bmGo.AddComponent<Managers.BattleManager>();
            var battleUIComp      = bmGo.AddComponent<Managers.UI.BattleUI>();

            // BattleUI 한글 폰트 연결
            var koreanFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                "Assets/Fonts/NotoSansKR-VariableFont_wght SDF.asset");
            if (koreanFont != null)
            {
                var buiSO = new SerializedObject(battleUIComp);
                buiSO.FindProperty("koreanFont").objectReferenceValue = koreanFont;
                buiSO.ApplyModifiedProperties();
                Debug.Log("[SceneBuilder] BattleUI 한글 폰트 연결 완료");
            }
            else
            {
                Debug.LogWarning("[SceneBuilder] NotoSansKR SDF.asset 로드 실패. " +
                    "텍스트가 □□□로 보일 수 있습니다.");
            }

            // ── Canvas + UIManager (상단 바만 포함 — 나머지는 BattleUI가 런타임 빌드) ──
            var (canvasGo, uiManagerComp) = BuildBattleCanvas();

            // ── GameManager 참조 연결 (gridManager / uiManager / battleManager) ──
            {
                var so = new SerializedObject(gameManagerComp);
                so.FindProperty("gridManager")  .objectReferenceValue = gridManagerComp;
                so.FindProperty("uiManager")    .objectReferenceValue = uiManagerComp;
                so.FindProperty("battleManager").objectReferenceValue = battleManagerComp;
                so.ApplyModifiedProperties();
                Debug.Log("[SceneBuilder] GameManager 참조 연결 완료");
            }

            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Battle Canvas
        // ════════════════════════════════════════════════════════════════════════

        static (GameObject canvasGo, Managers.UIManager uiManager) BuildBattleCanvas()
        {
            // ─── 이 Canvas에는 상단 바만 포함 ─────────────────────────────────────
            // 행동 패널 / SP 표시 / 보상 패널은 BattleUI (sortingOrder=5) 가
            // Awake()에서 자가 생성하고, Start()에서 UIManager 필드를 런타임에 채운다.
            // BattleCanvas (sortingOrder=10)는 상단 HUD만 책임진다.
            var canvasGo = new GameObject("BattleCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();

            // ── Top Bar (y: 0.935 ~ 1.0) — Life + Stage만 표시 (Gold/Speed 제거) ──
            MakeImg(root, "TopBarBG", new Color(0.06f, 0.06f, 0.12f, 0.96f),
                0f, 0.935f, 1f, 1f);

            var lifeText  = MakeLbl(root, "LifeText",  "LIFE 3",      20, TextAnchor.MiddleLeft,
                new Color(1f, 0.4f, 0.4f), 0.01f, 0.935f, 0.22f, 1f);
            var stageText = MakeLbl(root, "StageText", "STAGE 1 — 1", 22, TextAnchor.MiddleCenter,
                new Color(0.80f, 0.88f, 1.0f), 0.22f, 0.935f, 0.78f, 1f);

            // ── UIManager — Life/Stage만 연결; 나머지는 BattleUI.Start()가 채운다 ──
            var uiManager = canvasGo.AddComponent<Managers.UIManager>();
            var so = new SerializedObject(uiManager);

            so.FindProperty("gameLifeText") .objectReferenceValue = lifeText;
            so.FindProperty("gameStageText").objectReferenceValue = stageText;

            so.ApplyModifiedProperties();
            Debug.Log("[SceneBuilder] UIManager 상단 바 연결 완료 (SP/버튼/보상은 BattleUI 런타임 바인딩)");

            return (canvasGo, uiManager);
        }

        // ════════════════════════════════════════════════════════════════════════
        // 보상 패널
        // ════════════════════════════════════════════════════════════════════════

        static GameObject BuildRewardPanel(
            RectTransform parent,
            out TMPro.TextMeshProUGUI[] textComps,
            out Button[] btnComps)
        {
            textComps = new TMPro.TextMeshProUGUI[3];
            btnComps  = new Button[3];

            var panel = new GameObject("RewardPanel");
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 어두운 오버레이
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(panel.transform, false);
            var oRt = overlay.AddComponent<RectTransform>();
            oRt.anchorMin = Vector2.zero;
            oRt.anchorMax = Vector2.one;
            oRt.offsetMin = oRt.offsetMax = Vector2.zero;
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            // 타이틀
            MakeLblChild(panel.transform, "RewardTitle", "보상 선택", 40,
                TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f),
                0.25f, 0.72f, 0.75f, 0.86f);

            // 카드 3장 — 각각 배경 + 설명 텍스트 + 선택 버튼
            float[] xs = { 0.05f, 0.37f, 0.69f };
            for (int i = 0; i < 3; i++)
            {
                float x0 = xs[i];
                float x1 = x0 + 0.26f;

                // 카드 배경
                MakeImgChild(panel.transform, $"RewardCard_{i}",
                    new Color(0.10f, 0.12f, 0.22f), x0, 0.20f, x1, 0.70f);

                // 보상 설명 텍스트 → UIManager.rewardTexts[i]
                textComps[i] = MakeLblChild(panel.transform, $"RewardText_{i}",
                    $"보상 {i + 1}", 20, TextAnchor.MiddleCenter, Color.white,
                    x0 + 0.01f, 0.42f, x1 - 0.01f, 0.68f);

                // 선택 버튼 → UIManager.rewardButtons[i]
                btnComps[i] = MakeBtnChild(panel.transform, $"RewardBtn_{i}", "선택", 22,
                    new Color(0.14f, 0.22f, 0.50f), new Color(0.20f, 0.32f, 0.70f),
                    x0 + 0.04f, 0.22f, x1 - 0.04f, 0.36f);
            }

            return panel;
        }

        // ════════════════════════════════════════════════════════════════════════
        // MAIN MENU 씬
        // ════════════════════════════════════════════════════════════════════════

        static string BuildMainMenuScene()
        {
            const string path = "Assets/Scenes/MainMenu.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 카메라
            var cam = new GameObject("Main Camera");
            cam.tag = "MainCamera";
            var c = cam.AddComponent<Camera>();
            c.orthographic    = true;
            c.backgroundColor = new Color(0.05f, 0.05f, 0.10f);
            c.clearFlags      = CameraClearFlags.SolidColor;
            cam.AddComponent<AudioListener>();

            // EventSystem
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // SettingsManager (DontDestroyOnLoad 싱글톤 — 씬 전환 후에도 유지)
            var smGo = new GameObject("SettingsManager");
            smGo.AddComponent<SettingsManager>();

            BuildMainMenuCanvas();

            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static void BuildMainMenuCanvas()
        {
            var canvasGo = new GameObject("MainMenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();

            // 배경 & 장식
            MakeImg(root, "BG", new Color(0.05f, 0.05f, 0.10f), 0f, 0f, 1f, 1f);
            MakeImg(root, "GradientBar", new Color(0.08f, 0.10f, 0.22f, 0.4f), 0f, 0.44f, 1f, 0.62f);

            // 타이틀
            MakeLbl(root, "Title",    "NEVER THE LAST", 64, TextAnchor.MiddleCenter,
                new Color(0.80f, 0.90f, 1.0f), 0.10f, 0.72f, 0.90f, 0.94f);
            MakeLbl(root, "Subtitle", "네버 더 라스트",  22, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.70f, 0.88f, 0.8f), 0.25f, 0.66f, 0.75f, 0.74f);

            // 메뉴 버튼
            MakeBtn(root, "NewGameBtn",  "새로운 여정", 28,
                new Color(0.12f, 0.18f, 0.42f), new Color(0.18f, 0.26f, 0.58f),
                0.30f, 0.54f, 0.70f, 0.66f);
            MakeBtn(root, "ContinueBtn", "이어하기",   26,
                new Color(0.10f, 0.20f, 0.18f), new Color(0.14f, 0.28f, 0.24f),
                0.30f, 0.40f, 0.70f, 0.52f);
            MakeBtn(root, "SettingsBtn", "설정",       24,
                new Color(0.18f, 0.14f, 0.24f), new Color(0.26f, 0.20f, 0.36f),
                0.30f, 0.26f, 0.70f, 0.38f);
            MakeBtn(root, "QuitBtn",     "종료",       20,
                new Color(0.22f, 0.10f, 0.10f), new Color(0.34f, 0.14f, 0.14f),
                0.38f, 0.04f, 0.62f, 0.13f);

            var settingsPanel = BuildSettingsPanel(root);
            settingsPanel.SetActive(false);

            canvasGo.AddComponent<MainMenuUI>();

            // ── 한글 폰트 일괄 적용 (빈 □□□ 방지) ──────────────────────────────
            ApplyKoreanFont(canvasGo);
        }

        static GameObject BuildSettingsPanel(RectTransform parent)
        {
            var panel = new GameObject("SettingsPanel");
            panel.transform.SetParent(parent, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

            // 전체 어두운 오버레이
            var bg = new GameObject("SettingsBG");
            bg.transform.SetParent(panel.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.06f, 0.92f);

            // 설정 패널 내용
            MakeImgChild(panel.transform, "SettingsFrame", new Color(0.10f, 0.12f, 0.22f),
                0.28f, 0.10f, 0.72f, 0.90f);
            MakeLblChild(panel.transform, "SettingsTitle", "설정", 40,
                TextAnchor.MiddleCenter, new Color(0.80f, 0.90f, 1.0f),
                0.30f, 0.76f, 0.70f, 0.88f);

            MakeLblChild(panel.transform, "MusicLabel", "음악 볼륨", 22,
                TextAnchor.MiddleLeft, Color.white, 0.33f, 0.62f, 0.62f, 0.70f);
            MakeSlider(panel.transform, "MusicSlider",  0.33f, 0.52f, 0.68f, 0.62f, 1f);

            MakeLblChild(panel.transform, "SFXLabel", "효과음 볼륨", 22,
                TextAnchor.MiddleLeft, Color.white, 0.33f, 0.40f, 0.62f, 0.48f);
            MakeSlider(panel.transform, "SFXSlider",    0.33f, 0.30f, 0.68f, 0.40f, 1f);

            MakeBtnChild(panel.transform, "SettingsBackBtn", "뒤로", 24,
                new Color(0.18f, 0.12f, 0.22f), new Color(0.28f, 0.18f, 0.36f),
                0.37f, 0.12f, 0.63f, 0.22f);

            panel.AddComponent<SettingsUI>();
            return panel;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Build Settings 등록
        // ════════════════════════════════════════════════════════════════════════

        static void RegisterScenes(string menuPath, string gamePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(menuPath, true),
                new EditorBuildSettingsScene(gamePath, true)
            };
            Debug.Log("[NeverTheLast] Build Settings: MainMenu(0), Game(1)");
        }

        // ════════════════════════════════════════════════════════════════════════
        // 월드 공간 헬퍼
        // ════════════════════════════════════════════════════════════════════════

        static void CreateSideBackground(string name, Color color, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.position   = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.color        = color;
            sr.sortingOrder = -1;
        }

        static void CreateWorldQuad(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.position   = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.color        = color;
            sr.sortingOrder = 0;
        }

        // ════════════════════════════════════════════════════════════════════════
        // UI 팩토리 헬퍼
        // ════════════════════════════════════════════════════════════════════════

        // ════════════════════════════════════════════════════════════════════════
        // 폰트 유틸리티
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GameObject 하위 모든 TextMeshProUGUI에 NotoSansKR 폰트를 일괄 적용.
        /// 런타임이 아닌 에디터 씬 빌드 시 호출 — 한글 □□□ 방지.
        /// </summary>
        static void ApplyKoreanFont(GameObject root)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                "Assets/Fonts/NotoSansKR-VariableFont_wght SDF.asset");
            if (font == null)
            {
                Debug.LogWarning("[SceneBuilder] NotoSansKR SDF 로드 실패 — 폰트 미적용.");
                return;
            }
            var tmps = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var tmp in tmps)
                tmp.font = font;
            Debug.Log($"[SceneBuilder] 한글 폰트 적용: {tmps.Length}개 TMP → {root.name}");
        }

        static void EnsureDirectories()
        {
            foreach (var dir in new[] { "Assets/Scenes", "Assets/Scripts/Editor" })
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        // RectTransform 부모를 받는 버전

        static Image MakeImg(RectTransform parent, string name, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static TMPro.TextMeshProUGUI MakeLbl(
            RectTransform parent, string name, string text,
            int size, TextAnchor anchor, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text               = text;
            tmp.fontSize           = size;
            tmp.color              = color;
            tmp.alignment          = ToTmpAlignment(anchor);
            tmp.fontStyle          = TMPro.FontStyles.Bold;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        static Button MakeBtn(RectTransform parent, string name, string label, int fontSize,
            Color normal, Color highlight,
            float xMin, float yMin, float xMax, float yMax)
        {
            var img = MakeImg(parent, name, normal, xMin, yMin, xMax, yMax);
            return WireButton(img.gameObject, img, label, fontSize, highlight);
        }

        // Transform 부모를 받는 버전

        static Image MakeImgChild(Transform parent, string name, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static TMPro.TextMeshProUGUI MakeLblChild(
            Transform parent, string name, string text,
            int size, TextAnchor anchor, Color color,
            float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text               = text;
            tmp.fontSize           = size;
            tmp.color              = color;
            tmp.alignment          = ToTmpAlignment(anchor);
            tmp.fontStyle          = TMPro.FontStyles.Bold;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        static Button MakeBtnChild(Transform parent, string name, string label, int fontSize,
            Color normal, Color highlight,
            float xMin, float yMin, float xMax, float yMax)
        {
            var img = MakeImgChild(parent, name, normal, xMin, yMin, xMax, yMax);
            return WireButton(img.gameObject, img, label, fontSize, highlight);
        }

        static Button WireButton(GameObject go, Image img, string label, int fontSize, Color highlight)
        {
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = highlight;
            cb.pressedColor     = highlight * 0.7f;
            cb.disabledColor    = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            btn.colors = cb;

            var lgo = new GameObject("Label");
            lgo.transform.SetParent(img.rectTransform, false);
            var lrt = lgo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = lgo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text          = label;
            tmp.fontSize      = fontSize;
            tmp.color         = Color.white;
            tmp.alignment     = TMPro.TextAlignmentOptions.Center;
            tmp.fontStyle     = TMPro.FontStyles.Bold;
            tmp.raycastTarget = false;
            return btn;
        }

        static Slider MakeSlider(Transform parent, string name,
            float xMin, float yMin, float xMax, float yMax, float initialValue = 1f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(xMin, yMin);
            rootRt.anchorMax = new Vector2(xMax, yMax);
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            root.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f);

            var slider = root.AddComponent<Slider>();
            slider.minValue     = 0f;
            slider.maxValue     = 1f;
            slider.wholeNumbers = false;
            slider.direction    = Slider.Direction.LeftToRight;

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(root.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.20f);
            faRt.anchorMax = new Vector2(1f, 0.80f);
            faRt.offsetMin = new Vector2(6f, 0f);
            faRt.offsetMax = new Vector2(-18f, 0f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(initialValue, 1f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = new Color(0.28f, 0.52f, 0.90f);
            slider.fillRect = fillRt;

            // Handle Slide Area
            var hArea = new GameObject("Handle Slide Area");
            hArea.transform.SetParent(root.transform, false);
            var haRt = hArea.AddComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10f, 0f);
            haRt.offsetMax = new Vector2(-10f, 0f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(hArea.transform, false);
            var handleRt = handle.AddComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(22f, 0f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color      = Color.white;
            slider.handleRect    = handleRt;
            slider.targetGraphic = handleImg;
            slider.value         = initialValue;
            return slider;
        }

        static TMPro.TextAlignmentOptions ToTmpAlignment(TextAnchor anchor) => anchor switch
        {
            TextAnchor.UpperLeft   => TMPro.TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TMPro.TextAlignmentOptions.Top,
            TextAnchor.UpperRight  => TMPro.TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft  => TMPro.TextAlignmentOptions.Left,
            TextAnchor.MiddleRight => TMPro.TextAlignmentOptions.Right,
            TextAnchor.LowerLeft   => TMPro.TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TMPro.TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight  => TMPro.TextAlignmentOptions.BottomRight,
            _                      => TMPro.TextAlignmentOptions.Center,
        };
    }
}
