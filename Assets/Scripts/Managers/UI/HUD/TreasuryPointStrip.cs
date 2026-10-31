using System;
using Combat;
using Managers.Stage;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Managers.UI.HUD
{
    /// <summary>SP처럼 한 점이 재보 하나다. 예약은 잔고 이동이며, 실제 소비만 소멸 연출을 한다.</summary>
    public sealed class TreasuryPointStrip : IDisposable
    {
        private readonly RectTransform _root;
        private readonly Image[] _points = new Image[PartyTreasury.Capacity];
        private readonly Image[] _reservedDots = new Image[PartyTreasury.Capacity];
        private readonly Image[] _echoes = new Image[PartyTreasury.Capacity];
        private readonly float[] _ages = new float[PartyTreasury.Capacity];
        private readonly int[] _pulses = new int[PartyTreasury.Capacity];
        private readonly Color[] _echoColors = new Color[PartyTreasury.Capacity];
        private readonly Vector2[] _echoOrigins = new Vector2[PartyTreasury.Capacity];
        private readonly TextMeshProUGUI _title, _count, _capacity, _reservation;
        private readonly Sprite _filled, _outline;
        private int _lastTotal, _lastAvailable, _lastReserved;
        private int _displayAvailable = -1, _displayReserved = -1;
        private bool _shown;
        private bool _stageLayout;
        private readonly Image _surface;
        private readonly Sprite _cardSurface;
        private readonly TooltipTrigger _cardTooltip;
        private static readonly Color Ink = new(.13f, .21f, .36f, 1f);
        private static readonly Color Gold = new(.72f, .53f, .22f, 1f);

        public TreasuryPointStrip(Transform parent)
        {
            _root = UIBuild.Container("PartyTreasury", parent);
            UIBuild.Pin(_root, new Vector2(1, 0), new Vector2(452, 60), new Vector2(-UITheme.ScreenEdgeInset, UITheme.ScreenEdgeInset));
            Image surface = UIBuild.Panel("TreasurySurface", _root, new Color(.96f, .98f, 1f, .91f), cut: 10);
            _surface = surface;
            _cardSurface = surface.sprite;
            UIBuild.Stretch(surface.rectTransform);
            _filled = PointSprite(false); _outline = PointSprite(true);
            _title = UIBuild.Text("TreasuryTitle", _root, "재보", 17, Ink, TextAlignmentOptions.MidlineLeft);
            UIBuild.Pin(_title.rectTransform, new Vector2(0, .5f), new Vector2(50, 26), new Vector2(18, 0));
            for (int i = 0; i < _points.Length; i++)
            {
                float x = 80 + i * 23;
                _points[i] = MakePoint("TreasuryPoint" + i, x, 0, 19);
                _reservedDots[i] = MakePoint("TreasuryReserved" + i, x + 6, 0, 7);
                _echoes[i] = MakePoint("TreasuryEcho" + i, x, 0, 19);
                _echoes[i].enabled = false;
            }
            _count = UIBuild.Text("TreasuryCount", _root, "0", 23, Ink, TextAlignmentOptions.MidlineRight);
            UIBuild.Pin(_count.rectTransform, new Vector2(0, .5f), new Vector2(38, 30), new Vector2(368, 0));
            _capacity = UIBuild.Text("TreasuryCapacity", _root, "/12", 14, UITheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UIBuild.Pin(_capacity.rectTransform, new Vector2(0, .5f), new Vector2(28, 28), new Vector2(411, -3));
            _reservation = UIBuild.Text("TreasuryReservation", _root, "", 14, new Color(.43f, .32f, .16f), TextAlignmentOptions.Midline);
            UIBuild.Pin(_reservation.rectTransform, new Vector2(.5f, .5f), new Vector2(300, 18), new Vector2(2, -15));
            _cardTooltip=TooltipTrigger.Attach(surface.gameObject, () => "재보 · 아군 공용", () => new[] { UITooltip.Line.Note(
                $"사용 가능 {PartyTreasury.Available} · 사격 예약 {PartyTreasury.Reserved}\n채운 별은 사용 가능 · 점이 있는 빈 별은 사격 예약\n공유 상한 {PartyTreasury.Capacity} · 예약도 상한에 포함 · 전투 종료 시 소멸", UITheme.TextSecondary) }, UITheme.Accent);
            surface.gameObject.AddComponent<StageTreasuryTooltip>();
            _lastTotal = PartyTreasury.Total;
            _lastAvailable = PartyTreasury.Available; _lastReserved = PartyTreasury.Reserved;
            _root.gameObject.SetActive(false);
            PartyTreasury.BalanceChanged += OnBalanceChanged;
        }

        private Image MakePoint(string name, float x, float y, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var image = go.GetComponent<Image>(); image.sprite = _filled; image.raycastTarget = false;
            UIBuild.Pin(image.rectTransform, new Vector2(0, .5f), new Vector2(size, size), new Vector2(x, y));
            return image;
        }

        private void OnBalanceChanged()
        {
            int total = PartyTreasury.Total, available = PartyTreasury.Available, reserved = PartyTreasury.Reserved;
            if (_shown && PartyTreasury.Active && total != _lastTotal)
            {
                // 예약/반환은 합계가 그대로다. 혼합 잔고에서도 실제 소비한 가용 점의 위치와 색을 남긴다.
                bool availableSpent = total < _lastTotal && reserved == _lastReserved && available < _lastAvailable;
                int first = total > _lastTotal ? _lastAvailable : availableSpent ? available : total;
                int end = total > _lastTotal ? available : availableSpent ? _lastAvailable : _lastTotal;
                for (int i = first; i < end; i++)
                {
                    _ages[i] = 0; _pulses[i] = total > _lastTotal ? 1 : -1;
                    _echoColors[i] = i < _lastAvailable ? Ink : Gold;
                    _echoOrigins[i] = _points[i].rectTransform.anchoredPosition;
                }
            }
            _lastTotal = total; _lastAvailable = available; _lastReserved = reserved;
        }

        private static bool HasTreasuryAlly()
        {
            var heroes = GridManager.Instance?.heroList;
            if (heroes == null) return false;
            foreach (var u in heroes)
                if (u != null && !u.IsEnemy && !u.IsSummon && u.IsOnField && (u.HasUnitTag("Treasury") || u.HasArchetypeTag("Treasury"))) return true;
            return false;
        }

        public static bool ShouldShow => PartyTreasury.Active && HasTreasuryAlly();

        private static void SetRowY(RectTransform rect, float y) => rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);

        public void Tick()
        {
            bool stage = VisualModeState.Current == VisualMode.Stage;
            ApplyLayout(stage);
            bool show = ShouldShow && (!stage || StageBattleHud.Visible);
            if (_shown != show)
            {
                _shown = show; _root.gameObject.SetActive(show);
                Array.Clear(_pulses, 0, _pulses.Length);
                _lastTotal = PartyTreasury.Total;
                _lastAvailable = PartyTreasury.Available; _lastReserved = PartyTreasury.Reserved;
                _displayAvailable = _displayReserved = -1;
            }
            if (!show) return;
            if (_displayAvailable != PartyTreasury.Available)
            { _displayAvailable = PartyTreasury.Available; _count.text = _displayAvailable.ToString(); }
            if (_displayReserved != PartyTreasury.Reserved)
            {
                _displayReserved = PartyTreasury.Reserved;
                _reservation.text = _displayReserved > 0 ? $"사격 예약 {_displayReserved}" : "";
                if (!stage)
                {
                    float y = _displayReserved > 0 ? 7 : 0;
                    SetRowY(_title.rectTransform, y); SetRowY(_count.rectTransform, y); SetRowY(_capacity.rectTransform, y - 3);
                    for (int i = 0; i < _points.Length; i++)
                    { SetRowY(_points[i].rectTransform, y); SetRowY(_reservedDots[i].rectTransform, y); }
                }
            }
            float delta = Time.timeScale > 0 ? Effects.Vfx.VfxTiming.ReadableDeltaTime * Mathf.Sqrt(BattleHud.EffectSpeedMultiplier) : 0;
            for (int i = 0; i < _points.Length; i++)
            {
                bool available = i < PartyTreasury.Available, reserved = !available && i < PartyTreasury.Total;
                _points[i].sprite = stage ? UIShapes.RoundedRect(0,available?Color.white:Color.clear,Color.white,available?0:1) : available ? _filled : _outline;
                Color ink = stage ? new Color(.94f, .79f, .42f) : Ink;
                _points[i].color = available ? ink : reserved ? Gold : new Color(ink.r, ink.g, ink.b, .40f);
                _reservedDots[i].enabled = reserved; _reservedDots[i].color = Gold;
                float scale = 1;
                if (_pulses[i] != 0)
                {
                    _ages[i] += delta;
                    float t = Mathf.Clamp01(_ages[i] / .34f);
                    if (_pulses[i] > 0) scale += Mathf.Sin(t * Mathf.PI) * .18f;
                    else
                    {
                        _echoes[i].enabled = t < 1;
                        Color prior = _echoColors[i];
                        _echoes[i].color = new Color(prior.r, prior.g, prior.b, (1 - t) * .65f);
                        _echoes[i].rectTransform.localScale = Vector3.one * (1 - .25f * t);
                        _echoes[i].rectTransform.anchoredPosition = _echoOrigins[i] + Vector2.up * (10 * t);
                    }
                    if (t >= 1) _pulses[i] = 0;
                }
                if (_pulses[i] >= 0) _echoes[i].enabled = false;
                _points[i].rectTransform.localScale = Vector3.one * scale;
            }
        }

        private void ApplyLayout(bool stage)
        {
            if (stage)
            {
                float scale = Screen.height / 1080f / _root.GetComponentInParent<Canvas>().scaleFactor;
                float offset=StageBattleHud.Current?.CenterOffset ?? 0f;
                UIBuild.Pin(_root, new Vector2(.5f, 0), new Vector2(136, 150), new Vector2(offset * scale, 18f * scale));
                _root.localScale = Vector3.one * scale;
            }
            if (_stageLayout == stage) return;
            _stageLayout = stage;
            _cardTooltip.enabled=!stage;
            if(stage)UITooltip.HideAny();
            foreach(var text in new[]{_title,_count,_capacity,_reservation})text.enableAutoSizing=!stage;
            _displayReserved = -1;
            _surface.sprite = stage ? UIShapes.RoundedRect(12, Color.white) : _cardSurface;
            _surface.color = stage ? new Color(14f/255f,22f/255f,40f/255f,.7f) : Color.white;
            Array.Clear(_pulses,0,_pulses.Length);
            _title.color = _count.color = stage ? Color.white : Ink;
            _capacity.color = stage ? new Color(.8f,.85f,.92f) : UITheme.TextSecondary;
            if (stage)
            {
                UIBuild.Pin(_title.rectTransform, new Vector2(.5f,.5f), new Vector2(100,24), new Vector2(0,48));
                _title.alignment = TextAlignmentOptions.Center; _title.fontSize = 14;
                _count.fontSize=20;_capacity.fontSize=13;
                UIBuild.Pin(_count.rectTransform, new Vector2(.5f,.5f), new Vector2(40,26), new Vector2(-12,-48));
                UIBuild.Pin(_capacity.rectTransform, new Vector2(.5f,.5f), new Vector2(35,24), new Vector2(24,-50));
                UIBuild.Pin(_reservation.rectTransform, new Vector2(.5f,.5f), new Vector2(130,16), new Vector2(0,-66));
                _reservation.fontSize = 11;
                for (int i=0;i<_points.Length;i++)
                {
                    Vector2 p = new(-31.5f + i%4*21, 16-i/4*21);
                    foreach (Image image in new[]{_points[i],_echoes[i]}) UIBuild.Pin(image.rectTransform,new Vector2(.5f,.5f),new Vector2(12,12),p);
                    _points[i].rectTransform.localRotation=Quaternion.Euler(0,0,45);
                    _echoes[i].rectTransform.localRotation=Quaternion.Euler(0,0,45);
                    _echoes[i].sprite=UIShapes.RoundedRect(0,Color.white);
                    UIBuild.Pin(_reservedDots[i].rectTransform,new Vector2(.5f,.5f),new Vector2(5,5),p);
                }
            }
            else
            {
                UIBuild.Pin(_root,new Vector2(1,0),new Vector2(452,60),new Vector2(-UITheme.ScreenEdgeInset,UITheme.ScreenEdgeInset));
                _root.localScale = Vector3.one;
                UIBuild.Pin(_title.rectTransform,new Vector2(0,.5f),new Vector2(50,26),new Vector2(18,0));
                _title.alignment = TextAlignmentOptions.MidlineLeft; _title.fontSize = 17;
                _count.fontSize=23;_capacity.fontSize=14;
                UIBuild.Pin(_count.rectTransform,new Vector2(0,.5f),new Vector2(38,30),new Vector2(368,0));
                UIBuild.Pin(_capacity.rectTransform,new Vector2(0,.5f),new Vector2(28,28),new Vector2(411,-3));
                UIBuild.Pin(_reservation.rectTransform,new Vector2(.5f,.5f),new Vector2(300,18),new Vector2(2,-15));
                _reservation.fontSize = 14;
                for(int i=0;i<_points.Length;i++)
                {
                    foreach(Image image in new[]{_points[i],_echoes[i]}) UIBuild.Pin(image.rectTransform,new Vector2(0,.5f),new Vector2(19,19),new Vector2(80+i*23,0));
                    _points[i].rectTransform.localRotation=_echoes[i].rectTransform.localRotation=Quaternion.identity;
                    _echoes[i].sprite=_filled;
                    UIBuild.Pin(_reservedDots[i].rectTransform,new Vector2(0,.5f),new Vector2(7,7),new Vector2(86+i*23,0));
                }
            }
        }

        // 같은 흰색 마스크 두 장을 재사용한다. 외곽선은 빈 슬롯에만 쓰며 UI 판에는 윤곽선을 더하지 않는다.
        private static Sprite _solidSprite, _rimSprite;
        private static Sprite PointSprite(bool rim)
        {
            Sprite cached = rim ? _rimSprite : _solidSprite;
            if (cached != null) return cached;
            const int size = 96;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + .5f) / size - .5f), dy = Mathf.Abs((y + .5f) / size - .5f);
                float d = (.47f - Mathf.Max(dx, dy) - 2.6f * Mathf.Min(dx, dy)) * size / 2.786f;
                float alpha = rim ? Mathf.Clamp01(d + .5f) * Mathf.Clamp01(5.2f - d) : Mathf.Clamp01(d + .5f);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TreasuryPointMask", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100);
            sprite.name = rim ? "TreasuryPointOutline" : "TreasuryPointFilled"; sprite.hideFlags = HideFlags.HideAndDontSave;
            if (rim) _rimSprite = sprite; else _solidSprite = sprite;
            return sprite;
        }

        public void Dispose() => PartyTreasury.BalanceChanged -= OnBalanceChanged;
    }

    /// <summary>Stage 재보만의 비고정 호버. 카드 사이의 빈 폭 안에서 바로 위에 설명한다.</summary>
    internal sealed class StageTreasuryTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform _panel;
        private TMP_Text _value;
        private PointerEventData _pointer;
        public bool Showing=>_panel!=null&&_panel.gameObject.activeInHierarchy;
        public RectTransform Panel=>_panel;
        public void OnPointerEnter(PointerEventData data)
        {
            if(!StageBattleHud.Visible)return;
            _pointer=data;
            if(_panel==null)
            {
                var image=UIBuild.Solid("StageTreasuryHint",transform,StageBattleHud.Glass);
                image.sprite=UIShapes.RoundedRect(10,Color.white);image.type=Image.Type.Sliced;image.raycastTarget=false;
                _panel=image.rectTransform;
                UIBuild.Pin(_panel,new Vector2(.5f,1),new Vector2(136,70),new Vector2(0,8));_panel.pivot=new Vector2(.5f,0);
                var title=UIBuild.Text("Title",_panel,"재보 · 아군 공용",13,StageBattleHud.Ink,TextAlignmentOptions.Center);
                UIBuild.Pin(title.rectTransform,new Vector2(.5f,1),new Vector2(124,22),new Vector2(0,-5));title.enableAutoSizing=false;
                _value=UIBuild.Text("Balance",_panel,"",12,StageBattleHud.Muted,TextAlignmentOptions.Center);
                UIBuild.Pin(_value.rectTransform,new Vector2(.5f,0),new Vector2(124,36),new Vector2(0,5));_value.enableAutoSizing=false;
            }
            _panel.gameObject.SetActive(true);Refresh();
        }
        public void OnPointerExit(PointerEventData data)=>Hide();
        private void Refresh()=>_value.text=$"가용 {PartyTreasury.Available} · 예약 {PartyTreasury.Reserved}\n공유 상한 {PartyTreasury.Capacity}";
        public void Hide(){_pointer=null;if(_panel!=null)_panel.gameObject.SetActive(false);}
        private void Update()
        {
            if(!Showing)return;
            if(!StageBattleHud.Visible||_pointer==null||!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,_pointer.position,null))Hide();
            else Refresh();
        }
        private void OnDisable()=>Hide();
        public static void HideForCapture()
        {foreach(var tip in UnityEngine.Object.FindObjectsByType<StageTreasuryTooltip>(FindObjectsSortMode.None))tip.Hide();}
    }
}
