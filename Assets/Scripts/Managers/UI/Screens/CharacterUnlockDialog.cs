using Core;
using Entities;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;

namespace Managers.UI.Screens
{
    /// <summary>최초 완주 보상. 확인 전까지 저장해 두어 종료 후에도 안내를 놓치지 않는다.</summary>
    public static class CharacterUnlockDialog
    {
        public static void ShowPending()
        {
            if (!SaveSystem.LoadTrainedCharacters().pendingCharacterUnlockIds.Contains(LavoisierChemistry.UnitId)) return;
            var canvas = UIBuild.Canvas("CharacterUnlock", 100);
            UIBuild.EnsureEventSystem();
            UIBuild.Backdrop("Backdrop", canvas.transform);
            var panel = UIBuild.Panel("Panel", canvas.transform, UITheme.Surface);
            UIBuild.Pin(panel.rectTransform, new Vector2(.5f, .5f), new Vector2(850, 470), Vector2.zero);
            var portrait = UIBuild.Solid("Portrait", panel.transform, Color.white);
            portrait.sprite = SpriteResource.LoadPortrait("LAVOISIER_PORTRAIT");
            portrait.preserveAspect = true;
            UIBuild.Anchor(portrait.rectTransform, new Vector2(.035f, .22f), new Vector2(.40f, .88f));
            var title = UIBuild.Text("Title", panel.transform, "새로운 동료 · 라부아지에", 30, UITheme.Accent);
            UIBuild.Anchor(title.rectTransform, new Vector2(.44f, .75f), new Vector2(.97f, .90f));
            var body = UIBuild.Text("Body", panel.transform,
                "육성 모드 첫 클리어 보상\n\n불 · 아카샤 / 갈리아 / 인간\n치유·보호막·버프로 시약을 모으는 고급 딜러\n\n이제 메인과 서포터로 편성할 수 있습니다.\n무한 모드는 라부아지에의 육성을 완료하면 열립니다.",
                19, UITheme.TextPrimary, TextAlignmentOptions.TopLeft, true);
            UIBuild.Anchor(body.rectTransform, new Vector2(.44f, .25f), new Vector2(.96f, .72f));
            var quote = UIBuild.Text("Quote", panel.transform, "“제 실험이 도움이 됐다고요? 헤헤… 한 번만 더 말해주시면 안 돼요?”",
                17, UITheme.TextSecondary, TextAlignmentOptions.Center, true);
            UIBuild.Anchor(quote.rectTransform, new Vector2(.035f, .13f), new Vector2(.965f, .24f));
            var button = UIBuild.Button("Confirm", panel.transform, "함께 실험하자", () =>
            {
                SaveSystem.AcknowledgeCharacterUnlock(LavoisierChemistry.UnitId);
                Object.Destroy(canvas.gameObject);
            }, true);
            UIBuild.Pin((RectTransform)button.transform, new Vector2(.5f, 0), new Vector2(230, 44), new Vector2(0, 16));
        }
    }
}
