using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Effects.Vfx.Presets;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>구매 Handpaint 시트 + 매 프레임 갱신하는 48분할 검기/꽃잎. 판정은 정점에서 한 번만 연다.</summary>
    public static class NordFx
    {
        private static readonly Color Ember = new(1f, .58f, .42f, 1f);
        private static readonly Color Gold = new(1f, .91f, .71f, 1f);
        private static readonly Color Mint = new(.40f, .79f, .63f, 1f);
        private static readonly Color EmberEdge = new(.67f, .29f, .32f, 1f);
        private static readonly Color LeafEdge = new(.22f, .48f, .42f, 1f);
        private static readonly Color Spring = new(.95f, .96f, .75f, 1f);
        private static HandpaintSheet Slow(HandpaintSheet sheet, float duration)
            => new(sheet.Name, sheet.Columns, sheet.Rows, sheet.Columns * sheet.Rows / duration, sheet.Pivot);

        public static IEnumerator SurtrStrike(Unit caster, Unit target, Action impact)
        {
            if (caster == null || target == null) { impact?.Invoke(); yield break; }
            CardMotion.Throw(caster, target, .45f);
            Vector3 end = StatusMotifFx.Anchor(target);
            Blade(end, VfxSpace.SortingLayer(target), .82f, .36f, false);
            yield return HandpaintFx.PlayHit(target, Slow(HandpaintFx.Slash1RedSlow, .36f), new Vector2(1.05f, .98f), Ember, Gold, () =>
            {
                CardMotion.RakeReaction(target, VfxSpace.AimFrom(caster, target).Direction, .35f);
                impact?.Invoke();
            }, rotation: -22f, impactFrame: 4, glowAlpha: .17f, calmLight: true, peakHoldSeconds: .05f, desaturate: .75f, portraitAnchor: true, peakAlpha: .38f);
        }

        public static IEnumerator SurtrUltimate(Unit caster, IReadOnlyList<Unit> targets, bool empowered, Action impact)
        {
            if (caster == null || targets == null || targets.Count == 0) { impact?.Invoke(); yield break; }
            Vector3 source = StatusMotifFx.Anchor(caster);
            int layer = VfxSpace.SortingLayer(caster);
            // 축적 → 검기 → 불티 순서. 강화 상태에서만 두 번째 검기를 겹친다.
            // 얼굴을 채우는 평면 화염 대신 가슴·손 가장자리의 작은 불꽃과 얇은 검기 예고를 쓴다.
            Vector3 charge = StatusMotifFx.Anchor(caster) + Vector3.right*.22f*VfxSpace.CardSize;
            HandpaintFx.PlayAt(charge, layer, Slow(HandpaintFx.Fire1Red, .72f), new Vector2(.48f, .60f), Ember, Gold,
                peakAlpha: .50f, glowAlpha: .12f, calmLight: true)?.ClipPortraitHud(caster).ScaleEnvelope(.60f,1.35f,.40f);
            Ribbon.Arc(source, Gold, .60f, .46f, 230, 280, .045f, layer);
            yield return new WaitForSeconds(.28f);
            foreach (Unit target in targets)
            {
                Vector3 point = StatusMotifFx.Anchor(target);
                Blade(point, VfxSpace.SortingLayer(target), empowered ? 1.18f : .96f, .64f, empowered);
                HandpaintFx.PlayAt(point, VfxSpace.SortingLayer(target), Slow(HandpaintFx.FireAttack1Red, .60f), new Vector2(1.18f, 1.48f), Ember, Gold,
                    peakAlpha: .36f, glowAlpha: .18f, calmLight: true, orderOffset: 33, desaturate: .90f)?.ClipPortraitHud(target);
            }
            yield return new WaitForSeconds(.19f);
            AudioManager.PlayCue("surtr.ultimate.hit");
            impact?.Invoke();
            foreach (Unit target in targets)
            {
                if (target == null) continue;
                HandpaintFx.PlayAt(VfxSpace.PortraitPoint(target), VfxSpace.SortingLayer(target), Slow(HandpaintFx.Explosion2Red, .42f),
                    new Vector2(.90f, .98f), Ember, Gold, peakAlpha: .40f, glowAlpha: .12f, calmLight: true, desaturate: .85f)?.ClipPortraitHud(target);
                CardMotion.RakeReaction(target, VfxSpace.AimFrom(caster, target).Direction, .47f);
            }
            yield return new WaitForSeconds(.43f);
        }

        public static IEnumerator FreyaPrayer(Unit caster)
        {
            if (caster == null) yield break;
            int layer = VfxSpace.SortingLayer(caster);
            HandpaintFx.PlayAt(VfxSpace.PortraitPoint(caster), layer, Slow(HandpaintFx.Impact1White, .56f), new Vector2(.82f, .82f), Mint, Spring,
                peakAlpha: .68f, glowAlpha: .12f, calmLight: true);
            var grid = GridManager.Instance;
            yield return new WaitForSeconds(.36f);
            if (caster.isActive && !caster.isControlled && grid != null)
                foreach (Unit ally in grid.heroList.Concat(grid.enemyList).Where(x => x != null && x.isActive && x.IsOnField && x.IsEnemy == caster.IsEnemy))
                    Bloom(ally, .72f);
            yield return new WaitForSeconds(.16f);
        }
        public static void FreyaEmergency(Unit target) => Bloom(target, 1f);
        private static void Bloom(Unit target, float scale)
        {
            if (target == null) return;
            Vector3 point = StatusMotifFx.Anchor(target);
            int layer = VfxSpace.SortingLayer(target);
            // 여섯 꽃잎 궤적을 시간차로 연다. 체력/피해 숫자가 보이도록 중심은 비워 둔다.
            for (int i = 0; i < 6; i++)
            {
                Ribbon.Petal(point, Color.Lerp(LeafEdge,Mint,.25f), .52f * scale, .76f, i * 60, .040f * scale, layer, i * .024f);
                Ribbon.Petal(point, i % 2 == 0 ? Mint : Spring, .52f * scale, .76f, i * 60, .022f * scale, layer, i * .024f);
            }
            HandpaintFx.PlayAt(point, layer, Slow(HandpaintFx.Impact1White, .52f), new Vector2(.62f, .64f) * scale, Mint, Spring,
                peakAlpha: .44f, glowAlpha: .09f, calmLight: true, orderOffset: 26, desaturate: .80f)?.ClipPortraitHud(target);
        }

        private static void Blade(Vector3 point, int layer, float scale, float life, bool empowered)
        {
            Ribbon.Slash(point, EmberEdge, scale, life, .13f, layer, order: 0);
            Ribbon.Slash(point, Ember, scale, life, .083f, layer, order: 1);
            Ribbon.Slash(point, Gold, scale, life, .024f, layer, order: 2);
            // 첨부 검기처럼 가는 외곽 잔상이 늦게 따라온다. 밝은 중심과 몸체의 앞뒤 정렬을 고정한다.
            for (int i = 0; i < 3; i++)
                Ribbon.Slash(point + new Vector3(-.07f + i*.07f, .09f + i*.055f)*VfxSpace.CardSize,
                    i == 1 ? Ember : Gold, scale*(.83f+i*.045f), life*1.08f, .012f, layer, .035f+i*.027f, order: 3);
            if (empowered) Ribbon.Slash(point + Vector3.up * .12f * VfxSpace.CardSize, Gold, scale * .80f, life * 1.12f, .021f, layer, .045f, true);
            // 대상 위 공중의 원형 불씨는 제거하고, 몸통에 붙은 검기와 잔상만 남긴다.
        }

        /// <summary>시간/위치/형태를 전투 난수와 분리한다. 풀스크린 섬광 없이 작은 비가산 궤적을 이어 그린다.</summary>
        private sealed class Ribbon : MonoBehaviour
        {
            private const int Segments = 48;
            private static readonly List<Ribbon> Live = new();
            private const int MaxRibbons = 128;
            private Mesh _mesh;
            private Vector3[] _vertices;
            private Color[] _colors;
            private Vector3 _origin, _end;
            private Unit _from, _to;
            private Color _color;
            private float _age, _life, _delay, _radius, _start, _sweep, _width;
            private bool _bridge, _slash, _petal, _reverse;
            public static void Arc(Vector3 point, Color color, float radius, float life, float start, float sweep, float width, int layer, float delay = 0f)
            {
                Ribbon fx = Create(point, color, life, width, layer);
                fx._radius = radius * VfxSpace.CardSize;
                fx._start = start; fx._sweep = sweep; fx._delay = delay;
            }
            public static void Slash(Vector3 point, Color color, float scale, float life, float width, int layer, float delay = 0f, bool reverse = false, int order = 0)
            {
                Ribbon fx = Create(point, color, life, width, layer, order);
                fx._slash = true; fx._reverse = reverse; fx._radius = scale * VfxSpace.CardSize; fx._delay = delay;
            }
            public static void Petal(Vector3 point, Color color, float radius, float life, float angle, float width, int layer, float delay = 0f)
            {
                Ribbon fx = Create(point, color, life, width, layer);
                fx._petal = true; fx._radius = radius * VfxSpace.CardSize; fx._start = angle; fx._delay = delay;
            }
            public static void Bridge(Unit source, Unit target, Color color, float life)
            {
                Ribbon fx = Create(VfxSpace.PortraitPoint(source), color, life, .022f, VfxSpace.SortingLayer(source));
                fx._bridge = true; fx._from = source; fx._to = target; fx._end = VfxSpace.PortraitPoint(target);
            }
            private static Ribbon Create(Vector3 point, Color color, float life, float width, int layer, int order = 0)
            {
                Live.RemoveAll(x => x == null);
                if (Live.Count >= MaxRibbons) { Ribbon oldest = Live[0]; Live.RemoveAt(0); if (oldest != null) Destroy(oldest.gameObject); }
                var go = new GameObject("NordRibbon48");
                var fx = go.AddComponent<Ribbon>();
                Live.Add(fx);
                fx._origin = point; fx._color = color; fx._life = life; fx._width = width * VfxSpace.CardSize;
                fx._vertices = new Vector3[(Segments + 1) * 2]; fx._colors = new Color[fx._vertices.Length];
                var triangles = new int[Segments * 6];
                for (int i = 0; i < Segments; i++)
                {
                    int v = i * 2, t = i * 6;
                    triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                    triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
                }
                fx._mesh = new Mesh { name = "NordRibbon48" }; fx._mesh.MarkDynamic();
                fx._mesh.vertices = fx._vertices; fx._mesh.triangles = triangles;
                go.AddComponent<MeshFilter>().sharedMesh = fx._mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = VfxSpace.Alpha;
                renderer.sortingLayerID = VfxSpace.ResolveSortingLayer(layer);
                renderer.sortingOrder = VfxSpace.OverlaySortingOrder + 29 + order;
                fx.Draw(0f);
                return fx;
            }
            private void Update()
            {
                _age += Time.deltaTime;
                if (_age >= _life + _delay) { Destroy(gameObject); return; }
                Draw(Mathf.Clamp01((_age - _delay) / _life));
            }
            private Vector3 Point(float u)
            {
                if (_slash)
                {
                    float x = Mathf.Lerp(-.40f,.48f,u);
                    float y = Mathf.Lerp(.32f,-.30f,u) + Mathf.Sin(u*Mathf.PI)*.16f;
                    return _origin + new Vector3(_reverse ? -x : x, y) * _radius;
                }
                if (_petal)
                {
                    float a = (_start - 28f + 56f*u)*Mathf.Deg2Rad;
                    float r = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u*Mathf.PI)), .72f)*_radius;
                    return _origin + new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.86f)*r;
                }
                if (!_bridge)
                {
                    float a = (_start + _sweep * u) * Mathf.Deg2Rad;
                    return _origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * .68f) * _radius;
                }
                Vector3 a0 = _from != null ? VfxSpace.PortraitPoint(_from) : _origin;
                Vector3 b0 = _to != null ? VfxSpace.PortraitPoint(_to) : _end;
                Vector3 p = Vector3.Lerp(a0, b0, u);
                p.y += Mathf.Sin(u * Mathf.PI) * .24f * VfxSpace.CardSize;
                return p;
            }
            private void Draw(float t)
            {
                float head = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .56f));
                float tail = Mathf.Clamp01((t - .24f) / .76f);
                float opacity = Mathf.SmoothStep(0, 1, t / .10f) * (1f - Mathf.SmoothStep(0, 1, (t - .56f) / .44f));
                for (int i = 0; i <= Segments; i++)
                {
                    float f = i / (float)Segments, u = Mathf.Lerp(tail, head, f);
                    Vector3 p = Point(u), tangent = Point(Mathf.Min(1f, u + .002f)) - Point(Mathf.Max(0f, u - .002f));
                    Vector3 normal = new Vector3(-tangent.y, tangent.x).normalized;
                    float w = _width * Mathf.Sin(f * Mathf.PI) * (1f - .4f * t);
                    _vertices[i * 2] = p + normal * w; _vertices[i * 2 + 1] = p - normal * w;
                    Color c = _color; c.a = (_petal ? .84f : .76f) * opacity * Mathf.Sin(f * Mathf.PI);
                    _colors[i * 2] = _colors[i * 2 + 1] = c;
                }
                _mesh.vertices = _vertices; _mesh.colors = _colors; _mesh.RecalculateBounds();
            }
            private void OnDestroy() { Live.Remove(this); if (_mesh != null) Destroy(_mesh); }
        }
    }
}
