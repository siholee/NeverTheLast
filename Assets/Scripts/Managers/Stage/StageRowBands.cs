using System.Collections.Generic;
using BaseClasses;
using Managers.UI.Core;
using TMPro;
using UnityEngine;

namespace Managers.Stage
{
    /// <summary>시안 rowBandsHTML의 바닥 띠·점선·준비 열 이름. 화면 입력을 받지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class StageRowBands : MonoBehaviour
    {
        private GameObject _root;
        private Mesh _mesh;
        private Material _material;
        private MeshRenderer _bands;
        private readonly List<TextMeshPro> _labels = new();

        public MeshRenderer Bands => _bands;
        public IReadOnlyList<TextMeshPro> Labels => _labels;

        private void Build()
        {
            _root = new GameObject("StageRowBands");
            _root.transform.SetParent(transform, false);
            // 카메라와 함께 생성·정리하되 기하 좌표는 높이 1080 기준 월드 원점에 둔다.
            _root.transform.position = Vector3.zero;
            _root.transform.rotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            Vector3 World(Vector2 p) => new((p.x - StageLayout.CenterX) / 30f, (540f - p.y) / 30f, 0f);
            void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] {World(a), World(b), World(c), World(d)});
                for (int j = 0; j < 4; j++) colors.Add(color);
                triangles.AddRange(new[] {i, i + 1, i + 2, i, i + 2, i + 3});
            }
            for (int side = -1; side <= 1; side += 2)
            for (int column = 0; column < 2; column++)
            {
                Vector2 a = StageLayout.ForPreparation(side, column, 0).Pixel;
                Vector2 b = StageLayout.ForPreparation(side, column, 3).Pixel;
                Vector2 delta = b - a;
                Vector2 normal = new Vector2(-delta.y, delta.x).normalized * 34f;
                Vector2 start = a + Vector2.down * 36f, end = b + Vector2.up * 30f;
                Color color = side < 0 ? new Color32(33, 54, 92, 255) : new Color32(200, 65, 79, 255);
                color.a = .10f;
                Quad(start - normal, start + normal, end + normal, end - normal, color);
                // SVG stroke-width 2, stroke-dasharray 10 10.
                Vector2 direction = (end - start).normalized;
                Vector2 lineNormal = new(-direction.y, direction.x);
                float length = Vector2.Distance(start, end);
                color.a = .35f;
                for (float offset = 0; offset < length; offset += 20f)
                {
                    Vector2 p = start + direction * offset;
                    Vector2 q = start + direction * Mathf.Min(offset + 10f, length);
                    Quad(p - lineNormal, p + lineNormal, q + lineNormal, q - lineNormal, color);
                }
                var labelObject = new GameObject($"StageRowLabel_{side}_{column}", typeof(TextMeshPro));
                labelObject.transform.SetParent(_root.transform, false);
                var label = labelObject.GetComponent<TextMeshPro>();
                UIBuild.ApplyFont(label);
                label.text = column == 0 ? "전열" : "후열";
                label.fontSize = 22f / 3f; // World TMP의 em은 0.1 월드 단위다.
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Bottom;
                label.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
                label.rectTransform.pivot = new Vector2(.5f, 0f);
                label.transform.localPosition = World(b + Vector2.up * 36f);
                color.a = .9f;
                label.color = color;
                label.renderer.sortingLayerID = SortingLayer.layers[0].id;
                label.renderer.sortingOrder = -897;
                _labels.Add(label);
            }
            _mesh = new Mesh { name = "StageRowBands", hideFlags = HideFlags.HideAndDontSave };
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetTriangles(triangles, 0);
            _mesh.RecalculateBounds();
            _material = new Material(Shader.Find("Sprites/Default"))
                { name = "StageRowBands", hideFlags = HideFlags.HideAndDontSave };
            _root.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _bands = _root.AddComponent<MeshRenderer>();
            _bands.sharedMaterial = _material;
            _bands.sortingLayerID = SortingLayer.layers[0].id;
            _bands.sortingOrder = -898;
        }

        private void LateUpdate()
        {
            bool stage = VisualModeState.Current == VisualMode.Stage;
            if (_root == null)
            {
                if (!stage) return;
                Build();
            }
            _root.SetActive(stage);
            if (!stage) return;
            bool preparation = GameManager.Instance != null &&
                               GameManager.Instance.gameState == BaseEnums.GameState.Preparation;
            _material.color = new Color(1f, 1f, 1f, preparation ? 2f : 1f);
            foreach (var label in _labels) label.gameObject.SetActive(preparation);
        }

        private void OnDisable()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root);
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
