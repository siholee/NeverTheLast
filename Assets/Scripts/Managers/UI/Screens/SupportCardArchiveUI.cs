using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>메인 메뉴에서 직접 완주해 만든 육성 카드만 모아 보는 열람실.</summary>
    public sealed class SupportCardArchiveUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _list;
        private TextMeshProUGUI _count;
        public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

        public void Open()
        {
            if (_canvas == null) Build();
            Rebuild();
            _canvas.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        private void Build()
        {
            _canvas = UIBuild.Canvas("SupportCardArchiveCanvas", 220);
            Image veil = UIBuild.Solid("Veil", _canvas.transform, new Color(0.86f, 0.90f, 0.89f, 0.98f));
            UIBuild.Stretch(veil.rectTransform);

            RectTransform safe = UIBuild.SafeArea(_canvas);
            Image sheet = UIBuild.Panel("ArchiveSheet", safe, Color.white,
                UIShapes.Corner.Diagonal, 10, UITheme.Outline, 1);
            UIBuild.Anchor(sheet.rectTransform, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));

            TextMeshProUGUI eyebrow = UIBuild.Text("Eyebrow", sheet.transform, "TRAINING RECORDS / SUPPORT CARDS",
                UITheme.FontMicro, UITheme.Accent, TextAlignmentOptions.Left);
            UIBuild.Anchor(eyebrow.rectTransform, new Vector2(0.035f, 0.91f), new Vector2(0.72f, 0.965f));

            TextMeshProUGUI title = UIBuild.Text("Title", sheet.transform, "육성 카드 열람실",
                UITheme.FontDisplay, UITheme.TextPrimary, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(title.rectTransform, new Vector2(0.035f, 0.82f), new Vector2(0.72f, 0.925f));

            _count = UIBuild.Text("Count", sheet.transform, "", UITheme.FontCaption,
                UITheme.TextSecondary, TextAlignmentOptions.Left);
            UIBuild.Anchor(_count.rectTransform, new Vector2(0.035f, 0.775f), new Vector2(0.78f, 0.83f));

            Button close = UIBuild.Button("Close", sheet.transform, "닫기  [ESC]", Close, fontSize: 20);
            UIBuild.Anchor(close.image.rectTransform, new Vector2(0.80f, 0.86f), new Vector2(0.965f, 0.95f));

            _list = UIBuild.ScrollArea("Cards", sheet.transform, out ScrollRect scroll);
            UIBuild.Stretch(scroll.viewport);
            scroll.viewport.offsetMin = new Vector2(28f, 28f);
            scroll.viewport.offsetMax = new Vector2(-28f, -154f);

            var layout = _list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(6, 18, 6, 6);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = _list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _canvas.gameObject.SetActive(false);
        }

        private void Rebuild()
        {
            UIBuild.Clear(_list);
            List<TrainedCharacterRecord> records = SaveSystem.LoadTrainedCharacters().records?
                .Where(record => record?.supportCard != null && record.supportCard.sourceUnitId > 0)
                .OrderByDescending(record => record.createdAtUnixSeconds)
                .ToList() ?? new List<TrainedCharacterRecord>();
            int characters = records.Select(record => record.unitId).Distinct().Count();
            _count.text = $"직접 완주해 만든 카드 {records.Count}장 · 캐릭터 {characters}명  /  기본 지원 카드는 표시하지 않습니다";

            if (records.Count == 0)
            {
                TextMeshProUGUI empty = UIBuild.Text("Empty", _list,
                    "아직 완주한 육성 기록이 없습니다.\n한 번의 여정을 끝내면 이곳에 카드가 남습니다.",
                    UITheme.FontHeading, UITheme.TextMuted, TextAlignmentOptions.Center, wrap: true);
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 260f;
                return;
            }

            Dictionary<int, List<TrainedCharacterRecord>> grouped = records
                .GroupBy(record => record.unitId)
                .ToDictionary(group => group.Key, group => group.OrderBy(record => record.createdAtUnixSeconds).ToList());
            UnitDataList units = DataManager.LoadUnitDataList();
            foreach (TrainedCharacterRecord record in records)
            {
                List<TrainedCharacterRecord> siblings = grouped[record.unitId];
                int variant = siblings.IndexOf(record) + 1;
                BuildCard(record, variant, siblings.Count, units?.units?.FirstOrDefault(unit => unit.id == record.unitId));
            }
        }

        private void BuildCard(TrainedCharacterRecord record, int variant, int total, UnitData unit)
        {
            Image card = UIBuild.Panel($"Card_{record.supportCard.supportId}", _list, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 196f;

            Image portrait = UIBuild.Solid("Portrait", card.transform, UITheme.SurfaceSunken);
            UIBuild.Anchor(portrait.rectTransform, new Vector2(0.015f, 0.08f), new Vector2(0.16f, 0.92f));
            portrait.preserveAspect = true;
            portrait.type = Image.Type.Simple;
            Sprite sprite = unit != null ? SpriteResource.LoadPortrait(unit.portrait) : null;
            portrait.enabled = sprite != null;
            if (sprite != null) portrait.sprite = sprite;

            string name = string.IsNullOrWhiteSpace(record.unitName) ? record.supportCard.sourceUnitName : record.unitName;
            TextMeshProUGUI heading = UIBuild.Text("Heading", card.transform,
                $"{name}  ·  육성 카드 #{variant}", UITheme.FontHeading, UITheme.TextPrimary,
                TextAlignmentOptions.Left);
            heading.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(heading.rectTransform, new Vector2(0.18f, 0.67f), new Vector2(0.70f, 0.92f));

            TextMeshProUGUI number = UIBuild.Text("Variant", card.transform, $"보유 {total}장",
                UITheme.FontCaption, UITheme.Accent, TextAlignmentOptions.Right);
            UIBuild.Anchor(number.rectTransform, new Vector2(0.73f, 0.70f), new Vector2(0.97f, 0.90f));

            PrimaryStatSaveData stats = record.finalPrimaryStats ?? new PrimaryStatSaveData();
            SupportCardSaveData support = record.supportCard;
            string statsText = $"STR {stats.str}   DEX {stats.dex}   CON {stats.con}   INT {stats.intStat}   LUK {stats.luk}";
            TextMeshProUGUI statsLabel = UIBuild.Text("Stats", card.transform, statsText,
                UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.Left);
            UIBuild.Anchor(statsLabel.rectTransform, new Vector2(0.18f, 0.43f), new Vector2(0.97f, 0.66f));

            string made = record.createdAtUnixSeconds > 0
                ? DateTimeOffset.FromUnixTimeSeconds(record.createdAtUnixSeconds).ToLocalTime().ToString("yyyy.MM.dd HH:mm")
                : "이전 버전 기록";
            string details = $"특기 {support.specialtyTraining} · 등장 {support.specialtyRate}% · 훈련 +{support.trainingBonus} · " +
                             $"특기 +{support.specialtyBonus} · 전수 {support.skillTransferRate}% · 초기 우정 {support.initialBond}\n" +
                             $"육성 Lv.{record.finalTrainingLevel} · 위력 {support.sourcePower} · 패시브 {record.ownedPassiveCodes?.Count ?? 0}개 · {made}";
            TextMeshProUGUI detail = UIBuild.Text("Detail", card.transform, details,
                UITheme.FontCaption, UITheme.TextSecondary, TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Anchor(detail.rectTransform, new Vector2(0.18f, 0.07f), new Vector2(0.97f, 0.44f));
        }
    }
}
