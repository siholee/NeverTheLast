using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>시의 휘어진 칼날 한 획. 타격 순간 전체 실루엣을 열고 꼬리부터 깎아 내며 소멸한다.</summary>
    public sealed class ShiSlashStroke : MonoBehaviour
    {
        private const int Segments = 48;
        private const int Rows = 5;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector2 _start, _control, _end;
        private float _width, _life, _age;
        private Color _ink;
        private bool _draw;

        public static void Play(Vector3 origin, float size, int layer, int order,
            Vector2 start, Vector2 control, Vector2 end, float width, float life, Color ink, bool draw = false)
        {
            var go = new GameObject(draw ? "Shi_IaiBlade" : "Shi_ClawStroke");
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * size;
            var fx = go.AddComponent<ShiSlashStroke>();
            fx._start = start;
            fx._control = control;
            fx._end = end;
            fx._width = width;
            fx._life = VfxTiming.AtLeastFrames(life, 5);
            fx._ink = ink;
            fx._draw = draw;
            fx._vertices = new Vector3[(Segments + 1) * Rows];
            fx._colors = new Color[fx._vertices.Length];
            fx._mesh = new Mesh { name = "Shi_CurvedBlade", hideFlags = HideFlags.HideAndDontSave };
            fx._mesh.MarkDynamic();
            var triangles = new int[Segments * (Rows - 1) * 6];
            int ti = 0;
            for (int i = 0; i < Segments; i++)
                for (int j = 0; j < Rows - 1; j++)
                {
                    int v = i * Rows + j;
                    triangles[ti++] = v; triangles[ti++] = v + Rows; triangles[ti++] = v + 1;
                    triangles[ti++] = v + 1; triangles[ti++] = v + Rows; triangles[ti++] = v + Rows + 1;
                }
            fx.Apply(0f);
            fx._mesh.triangles = triangles;
            fx._mesh.uv = new Vector2[fx._vertices.Length];
            go.AddComponent<MeshFilter>().sharedMesh = fx._mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = VfxSpace.Alpha;
            renderer.sortingLayerID = VfxSpace.ResolveSortingLayer(layer);
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Destroy(go, Mathf.Max(3f, Time.timeScale * 2f));
        }

        private void Update()
        {
            // 고배속·캡처·낮은 FPS에서도 한 Update가 정점을 건너뛰지 않게 표시만 제한한다.
            _age += Mathf.Min(Time.deltaTime, _life / 6f);
            if (_age >= _life) { Destroy(gameObject); return; }
            Apply(_age / _life);
        }

        private void Apply(float phase)
        {
            // 피격은 첫 프레임부터 90% 길이. 발도만 짧게 뽑힌 뒤 정점에 도달한다.
            float reveal = _draw ? Mathf.Lerp(0.16f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(phase / 0.30f), 3f))
                : Mathf.Lerp(0.90f, 1f, Mathf.Clamp01(phase / 0.12f));
            float erase = Mathf.SmoothStep(0f, 0.96f, Mathf.Clamp01((phase - 0.34f) / 0.66f));
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 0.48f) / 0.52f));
            for (int i = 0; i <= Segments; i++)
            {
                float t = Mathf.Lerp(erase, reveal, i / (float)Segments);
                float u = 1f - t;
                Vector2 p = u * u * _start + 2f * u * t * _control + t * t * _end;
                Vector2 tangent = (2f * u * (_control - _start) + 2f * t * (_end - _control)).normalized;
                Vector2 normal = new(-tangent.y, tangent.x);
                // 얇은 꼬리 → 굵은 몸체 → 날카로운 끝. 폭 변화는 작은 붓결만 남긴다.
                float profile = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.72f);
                profile *= Mathf.Lerp(0.48f, 1.1f, t) * (1f + 0.07f * Mathf.Sin(t * 53f));
                float half = _width * profile * (1f - 0.46f * erase) * 0.5f;
                for (int row = 0; row < Rows; row++)
                {
                    float across = row switch { 0 => -1f, 1 => -0.88f, 2 => 0.28f, 3 => 0.88f, _ => 1f };
                    int index = i * Rows + row;
                    _vertices[index] = p + normal * (half * across);
                    Color color = row == 2 ? Color.Lerp(_ink, new Color(0.12f, 0.12f, 0.13f, _ink.a), 0.35f) : _ink;
                    color.a *= opacity * (row == 0 || row == Rows - 1 ? 0f : 1f);
                    _colors[index] = color;
                }
            }
            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
