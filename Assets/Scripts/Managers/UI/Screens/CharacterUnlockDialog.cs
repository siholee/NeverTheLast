using System.Collections.Generic;
using System.Linq;
using Core;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 해금 안내. 확인 전까지 저장해 두어 종료 후에도 안내를 놓치지 않는다 — <b>모르고 지나간 해금은 열리지 않은 것과 같다</b>.
    /// 대기열에 여럿이면 하나씩 뜬다. 문구는 유닛 데이터에서 읽는다.
    /// </summary>
    public static class CharacterUnlockDialog
    {
        public static void ShowPending()
        {
            List<int> pending = SaveSystem.LoadTrainedCharacters().pendingCharacterUnlockIds;
            if (pending == null || pending.Count == 0) return;

            int unitId = pending[0];
            UnitData data = GameManager.Instance?.unitDataList?.units?
                .FirstOrDefault(unit => unit != null && unit.id == unitId);
            // 데이터가 없으면 알릴 것도 없다. 대기열에 남겨 두면 매번 빈 창이 뜨므로 조용히 턴다.
            if (data == null)
            {
                SaveSystem.AcknowledgeCharacterUnlock(unitId);
                return;
            }

            var canvas = UIBuild.Canvas("CharacterUnlock", 100);
            UIBuild.EnsureEventSystem();
            UIBuild.Backdrop("Backdrop", canvas.transform);
            var panel = UIBuild.Panel("Panel", canvas.transform, UITheme.Surface);
            UIBuild.Pin(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(850, 470), Vector2.zero);
            var portrait = UIBuild.Solid("Portrait", panel.transform, Color.white);
            portrait.sprite = SpriteResource.LoadPortrait(data.portrait);
            portrait.preserveAspect = true;
            UIBuild.Anchor(portrait.rectTransform, new Vector2(.035f, .22f), new Vector2(.40f, .88f));
            var title = UIBuild.Text("Title", panel.transform, $"새로운 동료 · {data.name}", 30, UITheme.Accent);
            UIBuild.Anchor(title.rectTransform, new Vector2(.44f, .75f), new Vector2(.97f, .90f));
            var body = UIBuild.Text("Body", panel.transform, BuildBody(data),
                19, UITheme.TextPrimary, TextAlignmentOptions.TopLeft, true);
            UIBuild.Anchor(body.rectTransform, new Vector2(.44f, .25f), new Vector2(.96f, .72f));
            var quote = UIBuild.Text("Quote", panel.transform, Quote(),
                17, UITheme.TextSecondary, TextAlignmentOptions.Center, true);
            UIBuild.Anchor(quote.rectTransform, new Vector2(.035f, .13f), new Vector2(.965f, .24f));
            var button = UIBuild.Button("Confirm", panel.transform, "함께 가자", () =>
            {
                SaveSystem.AcknowledgeCharacterUnlock(unitId);
                Object.Destroy(canvas.gameObject);
            }, true);
            UIBuild.Pin((RectTransform)button.transform, new Vector2(.5f, 0), new Vector2(230, 44), new Vector2(0, 16));
        }

        private static string BuildBody(UnitData data)
        {
            string tags = data.tags == null || data.tags.Count == 0 ? "" : " / " + string.Join(" · ", data.tags);
            return $"새로 해금한 동료\n\n{ElementName(data.element)}{tags}\n{data.tagline}\n\n" +
                   "이제 다음 여정부터 편성할 수 있습니다.";
        }

        private static string Quote() => "“끝까지 함께 걸었으니, 이번에는 제가 앞에 서 볼게요.”";

        private static string ElementName(string element) => element switch
        {
            "Pyro" => "불",
            "Hydro" => "물",
            "Cryo" => "얼음",
            "Electro" => "번개",
            "Anemo" => "바람",
            "Geo" => "바위",
            "Dendro" => "풀",
            _ => element ?? "",
        };
    }
}
