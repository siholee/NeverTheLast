#if UNITY_EDITOR
using System.IO;
using Effects;
using Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 실제 런타임 VFX 컴포넌트를 Unity Editor 프리뷰 씬에 배치해 PNG로 렌더링한다.
/// 메뉴: Tools/Combat VFX/Export Preview (Ctrl+Alt+V)
/// </summary>
public static class CombatVfxPreviewExporter
{
    private const int Width = 1800;
    private const int Height = 700;

    [MenuItem("Tools/Combat VFX/Export Preview %&v")]
    public static void ExportPreview()
    {
        // PreviewScene은 일반 Camera.Render에서 컬링될 수 있어, 열린 작업 씬을 건드리지 않는
        // 임시 Additive Scene을 사용한다. 렌더 직후 바로 닫는다.
        // EditorSceneManager.NewScene은 Play Mode에서 예외를 던진다. 전투를 보다가 곧바로
        // 프리뷰를 뽑는 것이 이 도구의 주 사용법이므로 런타임에는 일반 additive Scene을 쓴다.
        bool playing = EditorApplication.isPlaying;
        Scene previewScene = playing
            ? SceneManager.CreateScene("Combat VFX Preview")
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        Camera camera = null;
        Sprite cardSprite = null;
        RenderTexture renderTexture = null;
        Texture2D capture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            camera = CreateCamera(previewScene);
            cardSprite = CreateCardSprite();

            Unit attacker = CreateBareUnit(previewScene, "Slash Attacker", new Vector3(-20f, 0f));
            Unit slashTarget = CreateUnit(previewScene, "Slash Target", new Vector3(-15f, 0f), cardSprite,
                new Color(0.31f, 0.16f, 0.18f));
            SlashEffect slash = SlashEffect.Play(attacker, slashTarget, new Color(1f, 0.38f, 0.12f));
            SceneManager.MoveGameObjectToScene(slash.gameObject, previewScene);
            slash.SetPreviewTime(0.13f);

            // 찌르기 — 공격 방향을 따라 들어왔다 빠진다.
            Unit thrustAttacker = CreateBareUnit(previewScene, "Thrust Attacker", new Vector3(-10f, 0f));
            Unit thrustTarget = CreateUnit(previewScene, "Thrust Target", new Vector3(-5f, 0f), cardSprite,
                new Color(0.18f, 0.22f, 0.30f));
            ThrustEffect thrust = ThrustEffect.Play(
                thrustAttacker, thrustTarget, new Color(0.60f, 0.90f, 0.97f));
            SceneManager.MoveGameObjectToScene(thrust.gameObject, previewScene);
            thrust.SetPreviewTime(0.12f);

            // 타격 — 방향 없이 퍼지는 충격 고리.
            Unit impactAttacker = CreateBareUnit(previewScene, "Impact Attacker", new Vector3(0f, 0f));
            Unit impactTarget = CreateUnit(previewScene, "Impact Target", new Vector3(5f, 0f), cardSprite,
                new Color(0.26f, 0.22f, 0.14f));
            ImpactEffect impact = ImpactEffect.Play(
                impactAttacker, impactTarget, new Color(1f, 0.78f, 0.21f));
            SceneManager.MoveGameObjectToScene(impact.gameObject, previewScene);
            impact.SetPreviewTime(0.11f);

            Unit airborneTarget = CreateUnit(previewScene, "Airborne Target", new Vector3(15f, 0f), cardSprite,
                new Color(0.12f, 0.24f, 0.34f));
            // 프리뷰에서는 UnitCardView 대신 카드 스프라이트만 실제 상승 높이만큼 올린다.
            airborneTarget.transform.GetChild(0).localPosition = Vector3.up * (Cell.CardSize * 0.13f);
            AirborneEffect airborne = AirborneEffect.Attach(airborneTarget);
            SceneManager.MoveGameObjectToScene(airborne.gameObject, previewScene);
            airborne.SetPreviewTime(0.42f);

            renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            capture.Apply();
            RenderTexture.active = previousActive;
            camera.targetTexture = null;

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CombatVfxPreview.png"));
            File.WriteAllBytes(output, capture.EncodeToPNG());
            Debug.Log($"[CombatVfxPreview] Exported: {output}");
        }
        finally
        {
            // RenderTexture는 Camera와 전역 active 슬롯에서 모두 떼어낸 뒤 해제해야 한다.
            // 순서가 반대면 "Releasing render texture that is set as Camera.targetTexture"가 발생한다.
            if (camera != null) camera.targetTexture = null;
            if (RenderTexture.active == renderTexture) RenderTexture.active = previousActive;
            if (playing)
            {
                SceneManager.UnloadSceneAsync(previewScene);
            }
            else
            {
                EditorSceneManager.CloseScene(previewScene, true);
            }

            if (capture != null) DestroyTemporary(capture, playing);
            if (cardSprite != null)
            {
                Texture2D cardTexture = cardSprite.texture;
                DestroyTemporary(cardSprite, playing);
                if (cardTexture != null) DestroyTemporary(cardTexture, playing);
            }
            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyTemporary(renderTexture, playing);
            }
        }
    }

    private static void DestroyTemporary(Object target, bool playing)
    {
        if (target == null) return;
        if (playing) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    private static Camera CreateCamera(Scene scene)
    {
        var go = new GameObject("Preview Camera");
        SceneManager.MoveGameObjectToScene(go, scene);
        Camera camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7.6f;
        camera.transform.position = new Vector3(0f, 0.4f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.031f, 0.045f, 1f);
        camera.allowHDR = true;
        return camera;
    }

    private static Unit CreateUnit(
        Scene scene, string name, Vector3 position, Sprite cardSprite, Color color)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = position;
        Unit unit = root.AddComponent<Unit>();

        var card = new GameObject("Card");
        card.transform.SetParent(root.transform, false);
        var renderer = card.AddComponent<SpriteRenderer>();
        renderer.sprite = cardSprite;
        renderer.color = color;
        renderer.sortingOrder = 1;
        float scale = Cell.CardSize / cardSprite.bounds.size.x;
        card.transform.localScale = new Vector3(scale, scale, 1f);
        return unit;
    }

    private static Unit CreateBareUnit(Scene scene, string name, Vector3 position)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = position;
        return root.AddComponent<Unit>();
    }

    private static Sprite CreateCardSprite()
    {
        const int width = 420;
        const int height = 420;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool border = x < 12 || x >= width - 12 || y < 12 || y >= height - 12;
                pixels[y * width + x] = border
                    ? new Color(0.72f, 0.76f, 0.84f, 1f)
                    : new Color(1f, 1f, 1f, 1f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
#endif
