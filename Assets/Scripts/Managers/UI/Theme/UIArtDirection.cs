using System;
using Managers.UI.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Managers.UI.Theme
{
    /// <summary>
    /// 전 화면이 함께 쓰는 게임 창 문법.
    ///
    /// 흰 판 위에 글자만 늘어놓으면 사무용 웹 화면처럼 읽혔다(2026-10-08 1920 검수). 원신 계열 메뉴처럼
    /// <b>틀(남색 제목 띠) · 본문(밝은 종이) · 주 행동(남색 행동 면)</b>이 서로 다른 재질로 갈리게 하고,
    /// 선택은 어디서나 같은 표지 하나로 읽게 한다.
    ///   · 선택 — 옅은 군청 면과 군청 글자. 가로 탭 · 타일만 아래 표지를 쓰며 좌측 장식선은 두지 않는다.
    ///   · 호버 — <see cref="UIButtonStyle"/> · <see cref="UIHoverRing"/>의 옅은 군청 막
    ///   · 주 행동 — 남색으로 채운 작은 반경의 면. 보조 행동 — 흰 면 + 헤어라인
    ///   · 비활성 — 형태를 남기고 글자만 흐리게
    /// </summary>
    public static class UIArtDirection
    {
        /// <summary>남색 제목 띠의 기준 높이. 제목 · 아이콘 · 닫기 · 머리 탭이 모두 이 띠의 중심선(32px)에 선다.</summary>
        public const float HeaderHeight = 64f;

        /// <summary>제목 띠 안 요소의 세로 중심(띠 위 끝에서 잰 px).</summary>
        public const float HeaderMidline = HeaderHeight * 0.5f;

        /// <summary>선택 막대의 두께.</summary>
        public const float SelectionBar = 3f;

        /// <summary>남색 띠 위 글자의 보조 명도. 흰 제목과 같은 흰색을 낮춰 쓰고 다른 색을 섞지 않는다.</summary>
        public static readonly Color HeaderInkSoft = new(1f, 1f, 1f, 0.72f);

        /// <summary>선택 면. 군청 12% — 흰 종이 위에서 회색 상자와 다르게 푸른 기가 돈다.</summary>
        public static readonly Color SelectedFill = new(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.10f);

        public static UIIcon CodeIcon(int id)
        {
            string chargedStat=Codes.Base.RewardCodeCatalog.Find(id)?.capacitorStat;
            if(!string.IsNullOrEmpty(chargedStat)) return chargedStat switch
            {
                "Attack"=>UIIcon.Burst, "MaxHp"=>UIIcon.Heart, "Defense"=>UIIcon.Armor,
                "SpecialAttack"=>UIIcon.Infusion, "SpecialDefense"=>UIIcon.Shield,
                "Speed"=>UIIcon.Swift, _=>UIIcon.Precision
            };
            if(id is 501 or 601 or 509) return UIIcon.Burst;
            if(id is 502 or 602 or 701 or 703 or 801) return UIIcon.Heart;
            if(id is 503 or 603 or 510) return UIIcon.Armor;
            if(id is 504 or 604 or 702) return UIIcon.Shield;
            if(id is 505 or 605) return UIIcon.Infusion;
            if(id is 506 or 606) return UIIcon.Swift;
            if(id is 507 or 508 or 607 or 608 or 511) return UIIcon.Precision;
            var tags=Codes.Base.RewardCodeCatalog.TagsOf(id);
            foreach(string tag in tags)
            {
                switch(tag)
                {
                    case "Healing":return UIIcon.Heart;
                    case "Sturdy":return UIIcon.Armor;
                    case "Counter":return UIIcon.Counter;
                    case "Infusion":return UIIcon.Infusion;
                    case "Dot":return UIIcon.Dot;
                    case "Precision":return UIIcon.Precision;
                    case "Support":return UIIcon.Support;
                    case "Economy":return UIIcon.Coin;
                }
            }
            return UIIcon.Support;
        }

        public static UIIcon ScreenIcon(string canvas) => canvas switch
        {
            "ShopCanvas" => UIIcon.Shop, "InventoryCanvas" => UIIcon.Supply,
            "RestCanvas" => UIIcon.Rest, "RewardCanvas" => UIIcon.Growth,
            "WikiCanvas" => UIIcon.Guide, "CharacterSelectCanvas" => UIIcon.Party,
            "BattleResultCanvas" => UIIcon.Check, "CombatLogCanvas" => UIIcon.Lexicon,
            "PauseMenuCanvas" => UIIcon.Menu, _ => UIIcon.Route
        };

        /// <summary>제목 띠 왼쪽의 기능 기호. 띠 위라 흰색이며 24px 상자의 중심이 띠 중심선에 온다.</summary>
        public static void TitleGlyph(Transform panel, UIIcon icon, float inset = 28)
        {
            Image glyph = UIBuild.Icon("ScreenGlyph", panel, icon, UITheme.TextOnAccent);
            glyph.raycastTarget = false;
            UIBuild.Pin(glyph.rectTransform, new Vector2(0, 1), new Vector2(24, 24), new Vector2(inset, -(HeaderMidline - 12f)));
            glyph.rectTransform.pivot = new Vector2(0, 1);
        }

        /// <summary>
        /// 남색 제목 띠. 창의 틀과 본문을 재질로 가른다 — 흰 종이 위 흰 유리 띠는 경계가 흐려 창이 한 장의 사각판으로 보였다.
        /// 위 모서리만 창과 같이 둥글고 아래는 직선이며, 띠 아래에 2px 하늘빛 선 하나로 본문이 시작된다.
        /// 오브젝트 이름 "NavigationInk"는 검증이 찾는 계약이다.
        /// </summary>
        public static void Header(Transform parent, float height = HeaderHeight, int radius = 16)
        {
            Image band = UIBuild.Solid("NavigationInk", parent, Color.white);
            band.sprite = HeaderPlate(radius);
            band.type = Image.Type.Sliced;
            band.raycastTarget = false;
            UIBuild.Anchor(band.rectTransform, new Vector2(0f, 1f), Vector2.one);
            band.rectTransform.offsetMin = new Vector2(0f, -height);
            band.rectTransform.offsetMax = Vector2.zero;
            band.transform.SetAsFirstSibling();

            // 띠 아래 끝선 — 남색과 종이 사이의 경계를 또렷하게 한다. 장식 무늬는 두지 않는다.
            Image line = UIBuild.Solid("NavigationCyan", band.transform, new Color(0.56f, 0.76f, 0.93f, 0.85f));
            line.raycastTarget = false;
            UIBuild.Anchor(line.rectTransform, Vector2.zero, new Vector2(1f, 0f));
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            line.rectTransform.anchoredPosition = new Vector2(0f, 1f);
        }

        /// <summary>위 두 모서리만 둥근 남색 판. 위→아래로 아주 약하게 짙어진다.</summary>
        private static Sprite HeaderPlate(int radius)
        {
            radius = Mathf.Clamp(radius, 4, 24);
            int size = radius * 2 + 6;
            string key = $"header_{radius}";
            if (HeaderCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

            Color top = new(0.165f, 0.259f, 0.427f, 1f);    // #2A426D
            Color bottom = new(0.118f, 0.192f, 0.333f, 1f); // #1E3155
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float t = y / (float)(size - 1);
                Color c = Color.Lerp(bottom, top, t);
                // 위쪽 절반에서만 둥글게 깎는다. 아래 모서리는 본문과 직선으로 맞닿는다.
                float cx = x + 0.5f, cy = y + 0.5f;
                float alpha = 1f;
                if (cy > size - radius)
                {
                    float dx = cx < radius ? radius - cx : cx > size - radius ? cx - (size - radius) : 0f;
                    float dy = cy - (size - radius);
                    if (dx > 0f) alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                }
                c.a = alpha;
                pixels[y * size + x] = c;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = key,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply();
            int slice = radius + 1;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            HeaderCache[key] = sprite;
            return sprite;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> HeaderCache = new();

        /// <summary>
        /// 제목 띠의 제목. 흰 굵은 24px이며 46px 상자의 중심이 띠 중심선에 온다(TMP Midline은 실제 글자 경계로 가운데를 잡는다).
        /// </summary>
        public static TextMeshProUGUI HeaderTitle(Transform panel, string name, string text, float left, float width = 620f)
        {
            TextMeshProUGUI title = UIBuild.Text(name, panel, text, 24f, UITheme.TextOnAccent, TextAlignmentOptions.MidlineLeft);
            title.characterSpacing = 0f;
            title.fontStyle = FontStyles.Bold;
            UIBuild.Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(width, 46f), new Vector2(left, -(HeaderMidline - 23f)));
            return title;
        }

        /// <summary>
        /// 제목 띠 오른쪽의 원형 닫기. 40px 원의 중심이 띠 중심선에 오고, 오른쪽 끝선은 본문 여백과 같다.
        /// 띠 위에서 흰 X가 읽히도록 남색 위 반투명 흰 원을 쓴다.
        /// </summary>
        public static Button HeaderClose(string name, Transform panel, Action onClick, float rightInset = 24f)
        {
            Button close = UIBuild.SymbolButton(name, panel, UIIcon.Close, onClick);
            close.image.sprite = UIShapes.RoundedRect(20, new Color(1f, 1f, 1f, 0.12f));
            Image icon = close.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.color = UITheme.TextOnAccent;
                UIBuild.Stretch(icon.rectTransform, 12f, 12f);
            }
            UIBuild.Pin(close.image.rectTransform, new Vector2(1f, 1f), new Vector2(40f, 40f),
                new Vector2(-rightInset, -(HeaderMidline - 20f)));
            UIButtonStyle.Attach(close);
            close.GetComponent<UIButtonStyle>().LightFeedback = true;
            return close;
        }

        /// <summary>
        /// 제목 띠 안의 보조 버튼(파티 창의 [공용 보급 열기] 등). 흰 헤어라인 알약에 흰 글자.
        /// </summary>
        public static void HeaderButton(Button button)
        {
            if (button == null) return;
            button.image.sprite = UIShapes.RoundedRect(20, new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.42f), 1);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.color = UITheme.TextOnAccent;
            UIButtonStyle.Attach(button);
            button.GetComponent<UIButtonStyle>().LightFeedback = true;
        }

        public static void Atmosphere(Transform parent, Color tint)
        {
            Image glow = UIBuild.Solid("PastelLight", parent, Color.white);
            glow.sprite = UIShapes.Glow(128, tint); glow.type = Image.Type.Simple; glow.raycastTarget = false;
            UIBuild.Anchor(glow.rectTransform, new Vector2(.45f,.35f), new Vector2(1f,1f));
            glow.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// 본문 탭. 선택 탭은 옅은 군청 면 + 아래 3px 막대 + 군청 굵은 글자, 나머지는 면 없이 보조 글자.
        /// 목록 행 선택(<see cref="SelectRow"/>)과 같은 면 · 막대 문법이라 화면마다 선택 표지가 달라지지 않는다.
        /// </summary>
        public static void Tab(Button button, bool selected)
        {
            if (button == null) return;
            button.image.sprite = UIShapes.RoundedRect(8, selected ? SelectedFill : Color.clear);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) { label.color = selected ? UITheme.AccentDim : UITheme.TextSecondary;
                label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal; }
            Transform old = button.transform.Find("ActiveIndicator");
            Image mark = old != null ? old.GetComponent<Image>() : UIBuild.Solid("ActiveIndicator", button.transform, UITheme.Accent);
            mark.raycastTarget = false;
            mark.sprite = UIShapes.RoundedRect(2, Color.white);
            mark.color = UITheme.Accent;
            UIBuild.Anchor(mark.rectTransform, new Vector2(.5f,0), new Vector2(.5f,0));
            mark.rectTransform.pivot = new Vector2(.5f, 0f);
            // 막대는 탭 폭 전체가 아니라 글자 폭 근처만 덮는다. 넓은 탭에서도 표지가 한 덩어리로 읽힌다.
            float width = label != null ? Mathf.Clamp(label.preferredWidth + 16f, 36f, 220f) : 48f;
            mark.rectTransform.sizeDelta = new Vector2(width, SelectionBar);
            mark.rectTransform.anchoredPosition = Vector2.zero;
            mark.gameObject.SetActive(selected);
        }

        /// <summary>
        /// 남색 제목 띠 안의 탭. 선택 탭은 흰 알약 위 남색 굵은 글자, 나머지는 면 없이 흰 72% 글자.
        /// </summary>
        public static void HeaderTab(Button button, bool selected)
        {
            if (button == null) return;
            button.image.sprite = UIShapes.RoundedRect(20, selected ? new Color(1f, 1f, 1f, 0.96f) : Color.clear);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = selected ? UITheme.AccentDim : HeaderInkSoft;
                label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
            Transform old = button.transform.Find("ActiveIndicator");
            if (old != null) old.gameObject.SetActive(false);
            UIButtonStyle style = button.GetComponent<UIButtonStyle>();
            if (style != null) style.LightFeedback = !selected;
        }

        /// <summary>
        /// 목록 행은 옅은 군청 면으로 선택을 구분한다. 가로 타일의 아래 표지만 유지한다.
        /// 좌측 막대는 윤곽선과 겹치고 글자 높이와 어긋나므로 생성하지 않는다.
        /// 행의 스프라이트를 바꾸므로 드롭 대상의 평상 모양을 잡기 전에 부른다.
        /// </summary>
        public static void SelectRow(Image row, bool selected, bool barAtBottom = false, int radius = 8)
        {
            if (row == null) return;
            row.sprite = UIShapes.RoundedRect(radius, selected ? SelectedFill : Color.clear);
            row.type = Image.Type.Sliced;
            row.color = Color.white;
            Transform old = row.transform.Find("SelectionBar");
            if (!barAtBottom)
            {
                // 재사용된 행에 이전 표지가 남아 있어도 선택 갱신 때 숨긴다.
                if (old != null) old.gameObject.SetActive(false);
                return;
            }
            Image bar = old != null ? old.GetComponent<Image>() : UIBuild.Solid("SelectionBar", row.transform, UITheme.Accent);
            bar.raycastTarget = false;
            bar.sprite = UIShapes.RoundedRect(2, Color.white);
            bar.color = UITheme.Accent;
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(0.2f, 0f);
            rect.anchorMax = new Vector2(0.8f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, SelectionBar);
            rect.anchoredPosition = Vector2.zero;
            bar.gameObject.SetActive(selected);
        }

        /// <summary>기존 선 아이콘에 신화의 각인을 조합한다. 색 대신 형태로 코드 역할을 읽는다.</summary>
        public static Image Emblem(string name, Transform parent, UIIcon icon, int tier = 1)
        {
            Image plate = UIBuild.Solid(name, parent, Color.white);
            plate.sprite = Seal(tier);
            plate.type = Image.Type.Simple;
            plate.preserveAspect = true;
            plate.raycastTarget = false;
            Image glyph = UIBuild.Icon("EmblemGlyph", plate.transform, icon, UITheme.Accent);
            glyph.raycastTarget = false;
            UIBuild.Stretch(glyph.rectTransform, 0f, 0f);
            glyph.rectTransform.anchorMin = new Vector2(.30f, .30f);
            glyph.rectTransform.anchorMax = new Vector2(.70f, .70f);
            return plate;
        }

        private static readonly System.Collections.Generic.Dictionary<int, Sprite> SealCache = new();
        /// <summary>256px 안티앨리어싱 각인을 공유한다. 읽는 동안 점멸하거나 회전하지 않는다.</summary>
        public static Sprite Seal(int tier)
        {
            tier = Mathf.Clamp(tier, 1, 5);
            if (SealCache.TryGetValue(tier, out Sprite cached) && cached != null) return cached;
            const int n = 256;
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new(x + .5f - n * .5f, y + .5f - n * .5f);
                float r = p.magnitude;
                float a = Mathf.Atan2(p.y, p.x);
                float edge = Mathf.Clamp01(1.6f - Mathf.Abs(r - 99f));
                float inner = Mathf.Clamp01(1.25f - Mathf.Abs(r - 81f)) * .35f;
                // 네 대각 모서리에서 외환을 열고 작은 마름모를 배치한다.
                float angle = Mathf.Abs(Mathf.Cos(a * 2f));
                edge *= Mathf.SmoothStep(0f, .28f, angle);
                float diamond = Mathf.Clamp01(1.4f - Mathf.Abs(Mathf.Abs(p.x) + Mathf.Abs(p.y) - 127f)) * .38f;
                float fill = Mathf.Clamp01((80f - r) / 15f) * .055f;
                float alpha = Mathf.Max(edge * .55f, Mathf.Max(inner, Mathf.Max(diamond, fill)));
                for (int i = 0; i < tier; i++)
                {
                    float theta = (68f + i * 11f) * Mathf.Deg2Rad;
                    Vector2 dot = new(Mathf.Cos(theta) * 99f, Mathf.Sin(theta) * 99f);
                    Vector2 q = p - dot;
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(4.8f - Mathf.Abs(q.x) - Mathf.Abs(q.y)));
                }
                Color c = UITheme.Accent; c.a = alpha; pixels[y * n + x] = c;
            }
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                  name = "CodeSeal_" + tier, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(pixels); texture.Apply(false, true);
            Sprite seal = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100);
            seal.hideFlags = HideFlags.HideAndDontSave;
            SealCache[tier] = seal;
            return seal;
        }

        /// <summary>큰 읽기 면의 빈 공간에만 흐릿한 각인을 둔다. 모든 장식은 클릭을 통과한다.</summary>
        public static void ReadingSurface(Transform parent, UIIcon icon, bool ornament = true)
        {
            Image light = UIBuild.Solid("SurfaceLight", parent, Color.white);
            light.sprite = UIShapes.Glow(128, new Color(.54f, .68f, .84f, .09f));
            light.raycastTarget = false;
            UIBuild.Anchor(light.rectTransform, new Vector2(.25f, .0f), Vector2.one, 8, 8);
            if (!ornament) return;
            Image seal = Emblem("SurfaceSignature", parent, icon);
            UIBuild.Pin(seal.rectTransform, new Vector2(1f, 0f), new Vector2(180f, 180f), new Vector2(-24f, 20f));
            seal.color = new Color(1f, 1f, 1f, .09f);
            foreach (Image child in seal.GetComponentsInChildren<Image>())
                if (child != seal) { Color c = child.color; c.a = .045f; child.color = c; }
        }

        /// <summary>알약의 과한 둥근 면 대신 작은 모서리·상단 빛·군청 그라데이션으로 주 행동을 구분한다.</summary>
        private static Sprite _primaryAction;
        public static Sprite ActionPlate(bool primary)
        {
            if (!primary) return UIShapes.RoundedRect(10, Color.white,
                new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, .18f), 1);
            if (_primaryAction != null) return _primaryAction;
            const int n = 64;
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 q = new(Mathf.Abs(x + .5f - 32f) - 22f, Mathf.Abs(y + .5f - 32f) - 22f);
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 10f;
                Color c = Color.Lerp(new Color(.105f, .17f, .30f), new Color(.17f, .26f, .43f), y / 63f);
                c = Color.Lerp(c, new Color(.58f, .68f, .80f), Mathf.Clamp01(1.5f + d) * .18f);
                c.a = Mathf.Clamp01(.5f - d);
                pixels[y * n + x] = c;
            }
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                  name = "PrimaryAction", hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(pixels); texture.Apply(false, true);
            _primaryAction = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
            _primaryAction.hideFlags = HideFlags.HideAndDontSave;
            return _primaryAction;
        }

        /// <summary>목록의 얕은 흰 면. 전체 윤곽을 강조하지 않는 헤어라인만 남긴다.</summary>
        public static Sprite Sheet(int radius = 10)
            => UIShapes.RoundedRect(radius, Color.white, new Color(0.34f, 0.49f, 0.62f, 0.08f), 1);
    }
}
