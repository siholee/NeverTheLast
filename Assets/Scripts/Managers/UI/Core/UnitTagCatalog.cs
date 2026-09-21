using System;
using System.Collections.Generic;
using System.Linq;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Core
{
    /// <summary>
    /// 캐릭터의 전투 성향을 짧게 알려 주는 UI 태그 사전.
    /// 데이터에는 안정적인 영문 키만 두고, 표시 이름·설명·아이콘 경로는 여기서 함께 관리한다.
    /// 아이콘 원본은 흰색 알파 마스크라 사용하는 화면이 <see cref="Image.color"/>로 색을 정한다.
    /// </summary>
    public static class UnitTagCatalog
    {
        public readonly struct Definition
        {
            public readonly string Key;
            public readonly string Name;
            public readonly string Description;
            public readonly string IconPath;

            public Definition(string key, string name, string description, string icon)
            {
                Key = key;
                Name = name;
                Description = description;
                IconPath = $"Sprite/UI/UnitTags/{icon}";
            }
        }

        private static readonly Definition[] All =
        {
            new("Burst", "방출", "해당 유닛은 강력한 궁극기 위주의 전투 방식을 사용합니다.", "TAG_BURST"),
            new("Precision", "정밀", "해당 유닛은 지속적인 피해를 입힙니다.", "TAG_PRECISION"),
            new("FollowUp", "연속", "해당 유닛은 추가 행동을 주로 사용합니다.", "TAG_FOLLOW_UP"),
            new("Support", "지원", "해당 유닛은 아군을 지원하는 데 특화되어 있습니다.", "TAG_SUPPORT"),
            new("Control", "제어", "해당 유닛은 적을 방해하고 약화하는 데 특화되어 있습니다.", "TAG_CONTROL"),
            new("Healing", "치유", "해당 유닛은 아군의 체력을 회복시킬 수 있습니다.", "TAG_HEALING"),
            new("Shielding", "방어", "해당 유닛은 아군에게 방어막을 부여할 수 있습니다.", "TAG_SHIELDING"),
            new("Infusion", "부여", "해당 유닛은 적에게 원소를 부여하는 데 특화되어 있습니다.", "TAG_INFUSION"),
            new("Sturdy", "견고", "해당 유닛은 매우 튼튼합니다.", "TAG_STURDY"),
        };

        private static readonly Dictionary<string, Definition> ByKey = All.ToDictionary(
            definition => definition.Key, definition => definition, StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> Icons = new(StringComparer.Ordinal);

        public static IReadOnlyList<Definition> Resolve(IEnumerable<string> keys)
        {
            if (keys == null) return Array.Empty<Definition>();

            var result = new List<Definition>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in keys)
            {
                if (string.IsNullOrWhiteSpace(raw) || !seen.Add(raw.Trim())) continue;
                if (ByKey.TryGetValue(raw.Trim(), out Definition definition)) result.Add(definition);
                else Debug.LogWarning($"[UnitTagCatalog] 알 수 없는 유닛 UI 태그: {raw}");
            }

            return result;
        }

        public static Sprite Icon(Definition definition)
        {
            if (Icons.TryGetValue(definition.IconPath, out Sprite cached) && cached != null) return cached;
            Sprite icon = Resources.Load<Sprite>(definition.IconPath);
            // 새 PNG의 .meta는 에디터가 만든다. 아직 Sprite 타입으로 바꾸지 않은 최초 임포트에서도
            // 바로 보이도록 Texture2D 폴백을 둔다.
            if (icon == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(definition.IconPath);
                if (texture != null)
                {
                    icon = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    icon.name = definition.Key;
                }
            }
            if (icon != null) Icons[definition.IconPath] = icon;
            return icon;
        }

        /// <summary>
        /// [아이콘 | 이름] 칩을 한 줄로 만든다. nestedTooltip이면 고정된 상위 툴팁 안에서
        /// 보조 툴팁을 띄우고, 아니면 보통 공용 툴팁을 쓴다.
        /// </summary>
        public static void BuildChips(Transform parent, IReadOnlyList<Definition> tags, Color tint,
            bool nestedTooltip = false)
        {
            UIBuild.Clear(parent);
            if (parent == null || tags == null || tags.Count == 0) return;

            var layout = parent.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            foreach (Definition tag in tags)
            {
                Image chip = UIBuild.Panel($"Tag_{tag.Key}", parent, UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, 6, UITheme.Outline, 1);
                chip.rectTransform.sizeDelta = new Vector2(72f, 30f);

                Image icon = UIBuild.Solid("Icon", chip.transform, tint);
                Sprite sprite = Icon(tag);
                if (sprite != null) icon.sprite = sprite;
                else icon.enabled = false;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UIBuild.Anchor(icon.rectTransform, new Vector2(0.08f, 0.14f), new Vector2(0.35f, 0.86f));

                TextMeshProUGUI label = UIBuild.Text("Name", chip.transform, tag.Name,
                    UITheme.FontMicro, tint, TextAlignmentOptions.Center);
                label.raycastTarget = false;
                UIBuild.Anchor(label.rectTransform, new Vector2(0.36f, 0f), new Vector2(0.96f, 1f));

                if (nestedTooltip)
                {
                    NestedTooltipTrigger.Attach(chip.gameObject, tag.Name,
                        () => new[] { UITooltip.Line.Note(tag.Description, UITheme.TextPrimary) }, tint);
                }
                else
                {
                    TooltipTrigger.Attach(chip.gameObject, () => tag.Name,
                        () => new[] { UITooltip.Line.Note(tag.Description, UITheme.TextPrimary) }, tint);
                }
            }
        }
    }
}
