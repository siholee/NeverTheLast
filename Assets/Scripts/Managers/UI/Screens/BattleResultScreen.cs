using System;
using System.Collections.Generic;
using Helpers;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Screens
{
    /// <summary>전투 한 판의 정산. <see cref="GameManager"/>가 채워 <see cref="BattleResultScreen"/>에 넘긴다.</summary>
    public sealed class BattleResultData
    {
        public bool Victory;

        /// <summary>목숨이 다해 런이 끝났다.</summary>
        public bool GameOver;

        /// <summary>승패의 이유 한 줄(적 전멸 · 턴 초과 · 아군 전멸).</summary>
        public string Reason;

        public string StageLabel;
        public string ThemeName;

        public int LifeBefore;
        public int LifeAfter;
        public int GoldGained;
        public int ExpGained;
        public int Kills;
        public int TokensGained;

        /// <summary>남은 적 수. 패배일 때 목숨이 이만큼 준다.</summary>
        public int EnemiesLeft;

        /// <summary>승리 뒤 이어지는 것(보상 선택)을 버튼에 적는다.</summary>
        public string NextStep;

        public readonly List<Member> Party = new();

        public readonly struct Member
        {
            public readonly string Name;
            public readonly string Portrait;
            public readonly bool Fallen;

            public Member(string name, string portrait, bool fallen)
            {
                Name = name;
                Portrait = portrait;
                Fallen = fallen;
            }
        }
    }

    /// <summary>
    /// 전투 결과 화면. 명일방주의 <b>작전 종료</b> 화면을 따른다 — 판정 한 줄이 가장 크고,
    /// 그 아래에 무엇을 잃고 얻었는지가 같은 크기의 칸으로 나란히 선다.
    ///
    /// 예전에는 전투가 끝나면 곧바로 보상 · 다음 준비 화면으로 넘어갔다. QA는 2-1을 "이겼다"고 적었는데
    /// 목숨이 18 → 14로 줄어 있었다 — 목숨은 패배(턴 초과 · 전멸)에서만 줄어든다. 테스터조차
    /// 승패를 알아차리지 못했다는 뜻이다. 이제 판정 · 이유 · 목숨 변화 · 쓰러진 아군이 한 장에 보이고,
    /// 확인을 눌러야 다음으로 넘어간다.
    ///
    /// 목숨이 0이 되면 같은 화면이 런 종료를 알린다. 예전에는 아무 화면 없이 멈췄다.
    /// </summary>
    public class BattleResultScreen : ModalScreen
    {
        protected override string CanvasName => "BattleResultCanvas";

        /// <summary>보상(70)보다 위 — 보상 화면이 열리기 직전에 떠 있다.</summary>
        protected override int SortingOrder => 75;

        protected override string Title => "작전 종료";
        protected override string Caption => "OPERATION REPORT";
        protected override Vector2 AnchorMin => new(0.22f, 0.12f);
        protected override Vector2 AnchorMax => new(0.78f, 0.88f);

        private const int MaxMembers = 9;
        private const float MemberSize = 92f;

        private Image _verdictBand;
        private Image _verdictAccent;
        private TextMeshProUGUI _verdictCaption;
        private TextMeshProUGUI _verdict;
        private TextMeshProUGUI _reason;
        private TextMeshProUGUI _stage;

        private Tile _lifeTile;
        private Tile _goldTile;
        private Tile _expTile;
        private Tile _killTile;

        private TextMeshProUGUI _partyCaption;
        private RectTransform _partyRow;
        private TextMeshProUGUI _note;
        private Button _continue;
        private TextMeshProUGUI _continueLabel;

        private Action _onContinue;

        protected override void Build()
        {
            BuildVerdict();
            BuildTiles();
            BuildParty();
            BuildFooter();
        }

        // ── 판정 ─────────────────────────────────────────────────────

        private void BuildVerdict()
        {
            _verdictBand = UIBuild.Panel("Verdict", Body, UITheme.AccentFaint, UIShapes.Corner.Diagonal, 12);
            UIBuild.Anchor(_verdictBand.rectTransform, new Vector2(0f, 0.74f), new Vector2(1f, 1f));
            _verdictBand.raycastTarget = false;

            _verdictAccent = UIBuild.Solid("Accent", _verdictBand.transform, UITheme.Accent);
            UIBuild.Anchor(_verdictAccent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f));
            _verdictAccent.rectTransform.sizeDelta = new Vector2(6f, 0f);
            _verdictAccent.rectTransform.pivot = new Vector2(0f, 0.5f);

            // 빗금 장식은 판정 띠 오른쪽에만 — 면을 채우지 않고 무게만 준다.
            Image stripes = UIBuild.Solid("Stripes", _verdictBand.transform, Color.white);
            stripes.sprite = UIShapes.DiagonalStripes(14, UITheme.SubtleFill, Color.clear);
            stripes.type = Image.Type.Tiled;
            stripes.raycastTarget = false;
            UIBuild.Anchor(stripes.rectTransform, new Vector2(0.68f, 0f), new Vector2(1f, 1f));

            _verdictCaption = UIBuild.Label("VerdictCaption", _verdictBand.transform, "", UITheme.FontCaption,
                UITheme.Accent);
            _verdictCaption.characterSpacing = 22f;
            UIBuild.Anchor(_verdictCaption.rectTransform, new Vector2(0f, 0.70f), new Vector2(0.7f, 0.92f), 30f, 0f);

            _verdict = UIBuild.Text("Verdict", _verdictBand.transform, "", UITheme.FontDisplay * 1.35f,
                UITheme.TextPrimary);
            _verdict.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(_verdict.rectTransform, new Vector2(0f, 0.28f), new Vector2(0.7f, 0.74f), 30f, 0f);

            _reason = UIBuild.Text("Reason", _verdictBand.transform, "", UITheme.FontBody, UITheme.TextSecondary);
            UIBuild.Anchor(_reason.rectTransform, new Vector2(0f, 0.06f), new Vector2(0.7f, 0.30f), 30f, 0f);

            _stage = UIBuild.Text("Stage", _verdictBand.transform, "", UITheme.FontTitle, UITheme.TextPrimary,
                TextAlignmentOptions.MidlineRight);
            UIBuild.Anchor(_stage.rectTransform, new Vector2(0.6f, 0.2f), new Vector2(1f, 0.8f), 28f, 0f);
        }

        // ── 얻고 잃은 것 ─────────────────────────────────────────────

        private void BuildTiles()
        {
            RectTransform row = UIBuild.Container("Tiles", Body);
            UIBuild.Anchor(row, new Vector2(0f, 0.50f), new Vector2(1f, 0.71f));

            _lifeTile = new Tile(row, 0, "목숨", "LIFE");
            _goldTile = new Tile(row, 1, "골드", "GOLD");
            _expTile = new Tile(row, 2, "경험치", "EXP");
            _killTile = new Tile(row, 3, "처치", "KILLS");
        }

        private void BuildParty()
        {
            _partyCaption = UIBuild.Label("PartyCaption", Body, "", UITheme.FontCaption, UITheme.TextSecondary);
            UIBuild.Anchor(_partyCaption.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.47f));

            _partyRow = UIBuild.Container("Party", Body);
            UIBuild.Anchor(_partyRow, new Vector2(0f, 0.20f), new Vector2(1f, 0.41f));
        }

        private void BuildFooter()
        {
            _note = UIBuild.Text("Note", Body, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Anchor(_note.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 0.17f));

            _continue = UIBuild.Button("Continue", Body, "계속", Continue, primary: true, UITheme.FontHeading);
            UIBuild.Anchor(_continue.image.rectTransform, new Vector2(0.66f, 0.01f), new Vector2(1f, 0.15f));
            _continueLabel = _continue.GetComponentInChildren<TextMeshProUGUI>();
            _continueLabel.richText = true;
        }

        // ── 열기 ─────────────────────────────────────────────────────

        public void Show(BattleResultData result, Action onContinue)
        {
            EnsureBuilt();
            _onContinue = onContinue;
            Bind(result ?? new BattleResultData());
            Show();
        }

        /// <summary>엔터 · 스페이스로도 넘어간다. UIManager가 매 프레임 부른다.</summary>
        public void Tick()
        {
            if (!IsVisible) return;
            if (GameManager.Instance?.uiManager?.IsPauseMenuOpen == true) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                Continue();
            }
        }

        private void Continue()
        {
            Action callback = _onContinue;
            _onContinue = null;
            Hide();
            callback?.Invoke();
        }

        private void Bind(BattleResultData result)
        {
            Color verdictColor = result.Victory ? UITheme.Accent : UITheme.Danger;

            _verdictBand.sprite = UIShapes.CutCorner(12,
                new Color(verdictColor.r, verdictColor.g, verdictColor.b, 0.12f), UIShapes.Corner.Diagonal);
            _verdictAccent.color = verdictColor;
            _verdictCaption.color = verdictColor;
            _verdictCaption.text = result.GameOver ? "RUN TERMINATED"
                : result.Victory ? "MISSION COMPLETE" : "MISSION FAILED";
            _verdict.text = result.GameOver ? "런 종료" : result.Victory ? "작전 성공" : "작전 실패";
            _verdict.color = result.Victory ? UITheme.TextPrimary : verdictColor;
            _reason.text = result.Reason ?? "";
            _stage.text = string.IsNullOrEmpty(result.ThemeName)
                ? result.StageLabel
                : $"{result.StageLabel}\n<size=55%><color=#{Hex(UITheme.TextMuted)}>{result.ThemeName}</color></size>";
            _stage.richText = true;

            // ── 목숨 ── 이 화면이 생긴 이유. 줄었으면 적색으로 크게, 차이를 따로 적는다.
            int lifeDelta = result.LifeAfter - result.LifeBefore;
            _lifeTile.Set(
                lifeDelta == 0 ? $"{result.LifeAfter}" : $"{result.LifeBefore} → {result.LifeAfter}",
                lifeDelta == 0 ? "변화 없음" : $"{lifeDelta}  (남은 적 {result.EnemiesLeft})",
                lifeDelta < 0 ? UITheme.Danger : UITheme.TextPrimary,
                lifeDelta < 0 ? UITheme.Danger : UITheme.TextMuted,
                emphasize: lifeDelta < 0);

            _goldTile.Set($"+{result.GoldGained:N0}",
                result.Victory ? "처치 + 승리 보수" : "처치 보수만",
                result.GoldGained > 0 ? UITheme.Accent : UITheme.TextPrimary, UITheme.TextMuted);

            _expTile.Set($"+{result.ExpGained:N0}", "출전한 파티 전원", UITheme.TextPrimary, UITheme.TextMuted);

            _killTile.Set($"{result.Kills}",
                result.TokensGained > 0 ? $"토큰 +{result.TokensGained}" : "적 처치 수",
                UITheme.TextPrimary, result.TokensGained > 0 ? UITheme.Accent : UITheme.TextMuted);

            BindParty(result);

            _note.text = result.GameOver
                ? "목숨이 모두 소진되어 이번 런이 끝났습니다. 저장본은 지워집니다."
                : result.Victory
                    ? "전투 불능이 된 아군은 전투가 끝나면 전투 시작 때의 체력으로 돌아옵니다."
                    : "패배해도 다음 스테이지로 넘어갑니다. 승리 보수와 보상 선택은 없고, 남은 적 1기마다 목숨이 1 줄어듭니다.";

            string action = result.GameOver ? "메인 메뉴로"
                : string.IsNullOrEmpty(result.NextStep) ? "계속" : result.NextStep;
            _continueLabel.text = $"{action}\n<size=60%>ENTER</size>";
        }

        private void BindParty(BattleResultData result)
        {
            UIBuild.Clear(_partyRow);

            int fallen = 0;
            foreach (BattleResultData.Member member in result.Party) if (member.Fallen) fallen++;

            _partyCaption.text = result.Party.Count == 0 ? ""
                : fallen == 0 ? "파티 · 전원 생존"
                : $"파티 · <color=#{Hex(UITheme.Danger)}>전투 불능 {fallen}명</color>";
            _partyCaption.richText = true;

            int count = Mathf.Min(MaxMembers, result.Party.Count);
            for (int i = 0; i < count; i++)
            {
                BuildMember(result.Party[i], i);
            }
        }

        private void BuildMember(BattleResultData.Member member, int index)
        {
            Color border = member.Fallen ? UITheme.Danger : UITheme.Outline;
            Image frame = UIBuild.Panel($"Member{index}", _partyRow, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 8, border, member.Fallen ? 2 : 1);
            frame.raycastTarget = false;
            UIBuild.Pin(frame.rectTransform, new Vector2(0f, 1f), new Vector2(MemberSize, MemberSize),
                new Vector2(index * (MemberSize + 12f), 0f));

            Sprite portrait = SpriteResource.LoadPortrait(member.Portrait);
            if (portrait != null)
            {
                var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(frame.transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = portrait;
                image.preserveAspect = true;
                image.raycastTarget = false;
                // 쓰러진 사람은 어둡게. 색을 빼는 셰이더 없이도 명도 차이로 충분히 갈린다.
                image.color = member.Fallen ? new Color(0.35f, 0.33f, 0.33f, 1f) : Color.white;
                UIBuild.Stretch(image.rectTransform, 5f, 5f);
            }

            Image nameBand = UIBuild.Solid("NameBand", frame.transform,
                new Color(UITheme.SurfaceRaised.r, UITheme.SurfaceRaised.g, UITheme.SurfaceRaised.b, 0.90f));
            nameBand.raycastTarget = false;
            UIBuild.Anchor(nameBand.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.26f), 1f, 1f);

            TextMeshProUGUI name = UIBuild.Text("Name", nameBand.transform, member.Name, UITheme.FontMicro,
                member.Fallen ? UITheme.TextMuted : UITheme.TextPrimary, TextAlignmentOptions.Center);
            UIBuild.Stretch(name.rectTransform, 3f, 0f);

            if (!member.Fallen) return;

            TextMeshProUGUI down = UIBuild.Text("Down", frame.transform, "전투 불능", UITheme.FontCaption,
                UITheme.Danger, TextAlignmentOptions.Center);
            down.fontStyle = FontStyles.Bold;
            UIBuild.Anchor(down.rectTransform, new Vector2(0f, 0.36f), new Vector2(1f, 0.66f));
        }

        /// <summary>ESC로는 넘기지 않는다. 결과를 못 보고 지나치는 일을 막으려는 화면이다.</summary>
        protected override bool OnEscape() => false;

        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        /// <summary>숫자 한 칸. 위에 라벨, 가운데 큰 숫자, 아래에 설명.</summary>
        private sealed class Tile
        {
            private readonly Image _panel;
            private readonly TextMeshProUGUI _value;
            private readonly TextMeshProUGUI _sub;

            public Tile(Transform parent, int index, string label, string caption)
            {
                _panel = UIBuild.Panel($"Tile{caption}", parent, UITheme.SurfaceSunken, UIShapes.Corner.Diagonal, 10,
                    UITheme.Outline, 1);
                _panel.raycastTarget = false;
                const float width = 0.25f;
                UIBuild.Anchor(_panel.rectTransform, new Vector2(index * width, 0f),
                    new Vector2((index + 1) * width, 1f), 6f, 0f);

                TextMeshProUGUI title = UIBuild.Text("Label", _panel.transform,
                    $"{label}  <size=70%><color=#{Hex(UITheme.TextMuted)}>{caption}</color></size>",
                    UITheme.FontCaption, UITheme.TextSecondary);
                title.richText = true;
                UIBuild.Anchor(title.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 0.95f), 16f, 0f);

                _value = UIBuild.Text("Value", _panel.transform, "", UITheme.FontDisplay, UITheme.TextPrimary);
                _value.fontStyle = FontStyles.Bold;
                UIBuild.Anchor(_value.rectTransform, new Vector2(0f, 0.28f), new Vector2(1f, 0.72f), 16f, 0f);

                _sub = UIBuild.Text("Sub", _panel.transform, "", UITheme.FontCaption, UITheme.TextMuted);
                UIBuild.Anchor(_sub.rectTransform, new Vector2(0f, 0.06f), new Vector2(1f, 0.28f), 16f, 0f);
            }

            public void Set(string value, string sub, Color valueColor, Color subColor, bool emphasize = false)
            {
                _value.text = value;
                _value.color = valueColor;
                _sub.text = sub;
                _sub.color = subColor;
                _panel.sprite = UIShapes.CutCorner(10,
                    emphasize ? new Color(UITheme.Danger.r, UITheme.Danger.g, UITheme.Danger.b, 0.12f) : UITheme.SurfaceSunken,
                    UIShapes.Corner.Diagonal, emphasize ? UITheme.Danger : UITheme.Outline, emphasize ? 2 : 1);
            }
        }
    }
}
