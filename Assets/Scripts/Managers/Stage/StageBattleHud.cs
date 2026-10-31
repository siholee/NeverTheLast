using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Entities;
using Managers.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.Stage
{
    /// <summary>전투 시작의 카드 자리를 고정한다. 사망은 같은 자리의 표시 사본, 증원은 위쪽 줄이다.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class StageBattleHud : MonoBehaviour
    {
        // Stage 크롬과 월드 카드 모두 높이 1080 기준의 같은 픽셀 배율을 사용한다.
        public static float UiScale(RectTransform rect) => Screen.height / 1080f / rect.GetComponentInParent<Canvas>().scaleFactor;
        public static Color Glass => new(247f / 255f, 250f / 255f, 253f / 255f, .86f);
        public static Color Ink => new(18f / 255f, 33f / 255f, 51f / 255f);
        public static Color Muted => new(58f / 255f, 77f / 255f, 102f / 255f);
        public static void TextSize(TMP_Text text,float size)
        {text.enableAutoSizing=false;text.fontSize=size;}
        private sealed class Slot
        {
            public Unit Unit;
            public bool Enemy;
            public int Column, Index, Row;
            public Canvas Frozen;
            public Canvas Live;
            public float Rise;
        }
        private readonly List<Slot> _slots = new();
        private readonly List<Object> _frozenArt = new();
        private bool _started;
        private int _syncFrame = -1, _syncCount;
        public static bool Visible => VisualModeState.Current == VisualMode.Stage &&
            GameManager.Instance != null && GameManager.Instance.gameState == BaseEnums.GameState.RoundInProgress;
        public static StageBattleHud Current
        {
            get
            {
                var grid = GridManager.Instance;
                if (grid == null) return null;
                return grid.GetComponent<StageBattleHud>() ?? grid.gameObject.AddComponent<StageBattleHud>();
            }
        }
        private void Sync()
        {
            if (!Visible) return;
            var grid = GridManager.Instance;
            int countNow=grid.heroList.Count+grid.enemyList.Count;
            if(_syncFrame==Time.frameCount&&_syncCount==countNow)return;
            _syncFrame=Time.frameCount;_syncCount=countNow;
            foreach (var slot in _slots.Where(s => s.Frozen != null && s.Unit != null && s.Unit.isActive && s.Unit.HpCurr > 0))
            { Destroy(slot.Frozen.gameObject); slot.Frozen = null; }
            var units = grid.heroList.Concat(grid.enemyList).Where(u => u != null && u.isActive && u.IsOnField && !u.IsSummon);
            bool initial = !_started;
            foreach (bool enemy in new[] { false, true })
            {
                var newcomers = units.Where(u => u.IsEnemy == enemy && !_slots.Any(s => ReferenceEquals(s.Unit, u)))
                    .OrderBy(u => enemy ? Mathf.Abs(u.currentCell.xPos) : -Mathf.Abs(u.currentCell.xPos))
                    .ThenBy(u => u.currentCell.yPos).ToArray();
                int count = _slots.Count(s => s.Enemy == enemy);
                foreach (var unit in newcomers)
                {
                    int index = initial ? count++ : _slots.Count(s => s.Enemy == enemy && s.Row > 0);
                    _slots.Add(new Slot { Unit = unit, Enemy = enemy, Column = Mathf.Abs(unit.currentCell.xPos)-1,
                        Index = index % 5, Row = initial ? index / 5 : 1 + index / 5 });
                }
            }
            _started = true;
        }
        public Vector2 Position(Unit unit)
        {
            Sync();
            var slot = _slots.FirstOrDefault(s => ReferenceEquals(s.Unit, unit));
            if (slot == null) return new Vector2(960, 912);
            int count = slot.Row > 0 ? 5 : _slots.Count(s => s.Enemy == slot.Enemy && s.Row == slot.Row);
            float width = count * 118f + Mathf.Max(0, count - 1) * 8f;
            float center = 960f + CenterOffset;
            float halfGap = TreasuryPointStrip.ShouldShow ? 82f : 7f;
            float x = slot.Enemy ? center + halfGap : center - halfGap - width;
            return new Vector2(x + slot.Index * 126f + 59f, 912f - slot.Row * 109f);
        }
        public float CenterOffset
        {
            get
            {
                Sync();
                float Width(bool enemy)
                {
                    int count=_slots.Count(s=>s.Enemy==enemy&&s.Row==0);
                    return count*118f+Mathf.Max(0,count-1)*8f;
                }
                return (Width(false)-Width(true))*.5f;
            }
        }
        public int Column(Unit unit) { Sync(); return _slots.FirstOrDefault(s => ReferenceEquals(s.Unit, unit))?.Column ?? 0; }
        public float HeightScale(Unit unit) { Sync(); return _slots.FirstOrDefault(s => ReferenceEquals(s.Unit, unit))?.Row > 0 ? .7f : 1f; }
        public void Track(Unit unit, Canvas canvas, float rise)
        {
            var slot=_slots.FirstOrDefault(s=>ReferenceEquals(s.Unit,unit));
            if(slot==null)return;
            slot.Live=canvas;slot.Rise=rise;
        }
        public void Freeze(Unit unit, Canvas source)
        {
            if (!Visible || unit == null || unit.isActive && unit.HpCurr > 0) return;
            Sync();
            var slot = _slots.FirstOrDefault(s => ReferenceEquals(s.Unit, unit));
            if (slot == null || slot.Frozen != null) return;
            slot.Frozen = Instantiate(source, transform);
            slot.Frozen.name = "StageFallenCard";
            slot.Frozen.transform.localScale = Vector3.one / 30f;
            slot.Frozen.gameObject.SetActive(true);
            slot.Frozen.GetComponent<CanvasGroup>().alpha = .65f;
            foreach (var image in slot.Frozen.GetComponentsInChildren<Image>(true))
            {
                float gray = image.color.grayscale;
                image.color = new Color(gray, gray, gray, image.color.a);
                if (image.name is "HpFill" or "HpDamageTrail" or "ShieldFill" or "StageHudBorder" or "StageSummonBadgeBack" or "StageReadyGlow") image.enabled = false;
                if (image.name == "StagePortrait" && image.sprite != null)
                {
                    var texture=Instantiate(image.sprite.texture);
                    var pixels=texture.GetPixels();
                    for(int i=0;i<pixels.Length;i++) { float g=pixels[i].grayscale; pixels[i]=new Color(g,g,g,pixels[i].a); }
                    texture.SetPixels(pixels);texture.Apply();
                    var sprite=Sprite.Create(texture,image.sprite.rect,new Vector2(.5f,.5f),image.sprite.pixelsPerUnit);
                    image.sprite=sprite;_frozenArt.Add(texture);_frozenArt.Add(sprite);
                }
            }
            foreach (var label in slot.Frozen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "HpValue") label.text = "0";
                if (label.name == "StageSummonBadge") label.text = "";
            }
        }
        private void LateUpdate()
        {
            if (!Visible)
            {
                foreach (var slot in _slots) if (slot.Frozen != null) Destroy(slot.Frozen.gameObject);
                foreach (var art in _frozenArt) if (art != null) Destroy(art);
                _frozenArt.Clear();
                _slots.Clear(); _started = false; _syncFrame = -1;
                return;
            }
            Sync();
            // 카메라 흔들림의 LateUpdate까지 끝난 뒤 화면 좌표를 확정한다. 전장 모션은 HUD로 전파하지 않는다.
            foreach (var slot in _slots)
            {
                Canvas canvas=slot.Frozen!=null?slot.Frozen:slot.Live;
                if(canvas==null||!canvas.gameObject.activeInHierarchy)continue;
                Vector2 pixel=Position(slot.Unit);pixel.y-=slot.Rise;
                canvas.transform.position=StageLayout.PixelToWorld(pixel,Camera.main);
                canvas.transform.rotation=Quaternion.identity;
                Vector3 scale=canvas.transform.parent.lossyScale;
                canvas.transform.localScale=new Vector3(1f/(30f*scale.x),HeightScale(slot.Unit)/(30f*scale.y),1f/(30f*scale.z));
            }
        }
        private void OnDestroy()
        {
            foreach (var art in _frozenArt) if (art != null) Destroy(art);
        }
    }
}
