using System.Collections.Generic;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>
    /// 사건 스테이지 화면. 대사를 순서대로 읽고 마지막에 선택지를 고른다.
    ///
    /// 연출 없이 텍스트와 선택지만 다루는 화면이다. 화자 초상화는 있으면 쓰고 없으면 생략한다.
    /// </summary>
    public class EventScreen : ModalScreen
    {
        protected override string CanvasName => "EventCanvas";
        protected override int SortingOrder => 75;
        protected override string Title => "사건";
        protected override Vector2 AnchorMin => new(0.14f, 0.16f);
        protected override Vector2 AnchorMax => new(0.86f, 0.84f);

        private Image _portraitFrame;
        private Image _portrait;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _body;
        private RectTransform _choiceArea;
        private Button _advance;

        protected override void Build()
        {
            _portraitFrame = UIBuild.Panel("PortraitFrame", Body, UITheme.SurfaceSunken,
                UIShapes.Corner.Diagonal, 8, UITheme.Outline, 1);
            UIBuild.Pin(_portraitFrame.rectTransform, new Vector2(0f, 1f), new Vector2(120f, 120f),
                Vector2.zero);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(_portraitFrame.transform, false);
            _portrait = portraitGo.GetComponent<Image>();
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            UIBuild.Stretch(_portrait.rectTransform, 4f, 4f);

            _speaker = UIBuild.Text("Speaker", Body, "", UITheme.FontHeading, UITheme.Accent);
            UIBuild.Anchor(_speaker.rectTransform, new Vector2(0f, 0.80f), new Vector2(1f, 0.92f), 0f, 0f);
            _speaker.rectTransform.offsetMin = new Vector2(136f, _speaker.rectTransform.offsetMin.y);

            _body = UIBuild.Text("Body", Body, "", UITheme.FontBody, UITheme.TextPrimary,
                TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Anchor(_body.rectTransform, new Vector2(0f, 0.46f), new Vector2(1f, 0.80f), 0f, 0f);
            _body.rectTransform.offsetMin = new Vector2(136f, _body.rectTransform.offsetMin.y);

            _choiceArea = UIBuild.Container("Choices", Body);
            UIBuild.Anchor(_choiceArea, new Vector2(0f, 0f), new Vector2(1f, 0.44f));

            // 대사가 남아 있을 때만 보이는 "계속" 버튼.
            _advance = UIBuild.Button("Advance", Body, "계속 ▸",
                () => GameManager.Instance?.AdvanceEventDialogue());
            UIBuild.Pin(_advance.image.rectTransform, new Vector2(1f, 0f), new Vector2(160f, 44f),
                Vector2.zero);
        }

        /// <summary>대사 인덱스에 맞춰 화면을 갱신한다. 대사가 끝나면 선택지를 띄운다.</summary>
        public void Show(StageEventData stageEvent, int dialogueIndex)
        {
            EnsureBuilt();
            base.Show();

            SetTitle(string.IsNullOrWhiteSpace(stageEvent?.title) ? "사건" : stageEvent.title);
            UIBuild.Clear(_choiceArea);

            List<StageEventDialogueData> dialogue = stageEvent?.dialogue;
            bool hasDialogueLeft = dialogue != null && dialogueIndex < dialogue.Count;

            if (hasDialogueLeft)
            {
                StageEventDialogueData line = dialogue[dialogueIndex];
                _speaker.text = line.speaker ?? "";
                _body.text = line.text ?? "";
                SetPortrait(ResolvePortrait(line));
            }
            else
            {
                // 대사를 다 읽었으면 마지막 대사를 남겨둔 채 선택지를 연다.
                _speaker.text = "";
                if (dialogue == null || dialogue.Count == 0) _body.text = "";
                SetPortrait(null);
            }

            bool showChoices = !hasDialogueLeft && stageEvent?.choices != null && stageEvent.choices.Count > 0;
            _advance.gameObject.SetActive(hasDialogueLeft);

            if (showChoices) BuildChoices(stageEvent.choices);
        }

        private void BuildChoices(List<StageEventChoiceData> choices)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                StageEventChoiceData choice = choices[i];
                string choiceId = choice.id;

                Button button = UIBuild.Button($"Choice{i}", _choiceArea, choice.text,
                    () => GameManager.Instance?.SelectEventChoice(choiceId));
                button.image.rectTransform.anchorMin = new Vector2(0f, 1f);
                button.image.rectTransform.anchorMax = new Vector2(1f, 1f);
                button.image.rectTransform.pivot = new Vector2(0.5f, 1f);
                button.image.rectTransform.sizeDelta = new Vector2(0f, 48f);
                button.image.rectTransform.anchoredPosition = new Vector2(0f, -i * 54f);

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        /// <summary>선택 실패 등 중간 안내. 화면은 그대로 두고 본문만 바꾼다.</summary>
        public void ShowMessage(string message)
        {
            EnsureBuilt();
            base.Show();
            _body.text = message ?? "";
            _advance.gameObject.SetActive(false);
        }

        /// <summary>사건 종료 안내. 확인을 누르면 다음 진행으로 넘어간다.</summary>
        public void ShowResolution(string message)
        {
            EnsureBuilt();
            base.Show();

            _speaker.text = "";
            _body.text = message ?? "";
            SetPortrait(null);
            _advance.gameObject.SetActive(false);

            UIBuild.Clear(_choiceArea);
            Button confirm = UIBuild.Button("Confirm", _choiceArea, "확인",
                () => GameManager.Instance?.CompleteEventStage(), primary: true);
            UIBuild.Pin(confirm.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(220f, 48f),
                Vector2.zero);
        }

        private void SetPortrait(Sprite sprite)
        {
            _portrait.sprite = sprite;
            _portrait.enabled = sprite != null;
            _portraitFrame.gameObject.SetActive(sprite != null);

            // 초상화가 없으면 본문이 왼쪽 여백까지 넓게 쓰도록 들여쓰기를 없앤다.
            float indent = sprite != null ? 136f : 0f;
            _speaker.rectTransform.offsetMin = new Vector2(indent, _speaker.rectTransform.offsetMin.y);
            _body.rectTransform.offsetMin = new Vector2(indent, _body.rectTransform.offsetMin.y);
        }

        /// <summary>대사에 지정된 초상화를 찾는다. 지정이 없으면 화자 이름으로 유추한다.</summary>
        private static Sprite ResolvePortrait(StageEventDialogueData line)
        {
            if (line == null) return null;

            if (!string.IsNullOrWhiteSpace(line.portrait))
            {
                return Resources.Load<Sprite>($"Sprite/Portraits/{line.portrait}");
            }

            if (string.IsNullOrWhiteSpace(line.speaker)) return null;
            return Resources.Load<Sprite>($"Sprite/Portraits/{line.speaker}");
        }
    }
}
