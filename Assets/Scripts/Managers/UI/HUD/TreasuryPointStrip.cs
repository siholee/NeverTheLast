using System;
using Combat;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private static readonly Color Ink = new(.13f, .21f, .36f, 1f);
        private static readonly Color Gold = new(.72f, .53f, .22f, 1f);

        public TreasuryPointStrip(Transform parent)
        {
            _root = UIBuild.Container("PartyTreasury", parent);
            UIBuild.Pin(_root, new Vector2(1, 0), new Vector2(452, 60), new Vector2(-UITheme.ScreenEdgeInset, UITheme.ScreenEdgeInset));
            Image surface = UIBuild.Panel("TreasurySurface", _root, new Color(.96f, .98f, 1f, .91f), cut: 10);
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
            TooltipTrigger.Attach(surface.gameObject, () => "재보 · 아군 공용", () => new[] { UITooltip.Line.Note(
                $"사용 가능 {PartyTreasury.Available} · 사격 예약 {PartyTreasury.Reserved}\n채운 별은 사용 가능 · 점이 있는 빈 별은 사격 예약\n공유 상한 {PartyTreasury.Capacity} · 예약도 상한에 포함 · 전투 종료 시 소멸", UITheme.TextSecondary) }, UITheme.Accent);
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

        private static void SetRowY(RectTransform rect, float y) => rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);

        public void Tick()
        {
            bool show = PartyTreasury.Active && HasTreasuryAlly();
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
                float y = _displayReserved > 0 ? 7 : 0;
                SetRowY(_title.rectTransform, y); SetRowY(_count.rectTransform, y); SetRowY(_capacity.rectTransform, y - 3);
                for (int i = 0; i < _points.Length; i++)
                { SetRowY(_points[i].rectTransform, y); SetRowY(_reservedDots[i].rectTransform, y); }
            }
            float delta = Time.timeScale > 0 ? Effects.Vfx.VfxTiming.ReadableDeltaTime * Mathf.Sqrt(BattleHud.CurrentSpeedMultiplier) : 0;
            for (int i = 0; i < _points.Length; i++)
            {
                bool available = i < PartyTreasury.Available, reserved = !available && i < PartyTreasury.Total;
                _points[i].sprite = available ? _filled : _outline;
                _points[i].color = available ? Ink : reserved ? Gold : new Color(Ink.r, Ink.g, Ink.b, .40f);
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
}
