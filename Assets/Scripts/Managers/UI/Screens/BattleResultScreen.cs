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

        /// <summary>아군별 딜량. 많이 넣은 순서다(<see cref="Combat.DamageMeter"/>).</summary>
        public readonly List<DamageLine> Damage = new();

        public readonly struct DamageLine
        {
            public readonly string Name;
            public readonly string Portrait;
            public readonly long Damage;
            public readonly bool Fallen;

            public DamageLine(string name, string portrait, long damage, bool fallen)
            {
                Name = name;
                Portrait = portrait;
                Damage = damage;
                Fallen = fallen;
            }
        }

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

        private const int MaxMembers = 6;
        private const float DamageRowHeight = 34f;
        private const float DamageRowGap = 5f;

        /// <summary>결과 창을 열기 전에 전장 위에 판정 띠를 띄우는 시간(초, 실시간).</summary>
        private const float BannerSeconds = 1.4f;
        private const float BannerFade = 0.18f;

        private GameObject _bannerRoot;
        private CanvasGroup _bannerGroup;
        private Image _bannerBand;
        private TextMeshProUGUI _bannerTitle;
        private TextMeshProUGUI _bannerCaption;
        private float _bannerStartedAt = -1f;

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
        private Button _logButton;
        private readonly CombatLogScreen _log = new();
        private TextMeshProUGUI _continueLabel;

        private Action _onContinue;

        /// <summary>
        /// 판정 띠부터 결과 창의 확인까지, 다음 진행을 잠가 둔 구간이다.
        /// 이때 ESC나 우상단 메뉴가 일시정지 메뉴를 열면 결과 확인 흐름이 두 겹으로 겹친다.
        /// </summary>
        public bool IsAwaitingConfirmation => _bannerStartedAt >= 0f || IsVisible;

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
            UIBuild.Anchor(_verdictBand.rectTransform, new Vector2(0f, 0.79f), new Vector2(1f, 1f));
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
            UIBuild.Anchor(row, new Vector2(0f, 0.60f), new Vector2(1f, 0.765f));

            _lifeTile = new Tile(row, 0, "목숨", "LIFE");
            _goldTile = new Tile(row, 1, "골드", "GOLD");
            _expTile = new Tile(row, 2, "경험치", "EXP");
            _killTile = new Tile(row, 3, "처치", "KILLS");
        }

        /// <summary>
        /// 파티 칸은 이제 <b>딜 그래프</b>다. 초상화만 늘어놓던 예전 칸은 누가 쓰러졌는지만 알려 줬고,
        /// 누가 일을 했는지는 보이지 않았다. 한 줄에 초상화 · 이름 · 막대 · 딜량과 비중이 서고,
        /// 쓰러진 아군은 어둡게 깔리며 이름 옆에 적힌다.
        /// </summary>
        private void BuildParty()
        {
            _partyCaption = UIBuild.Label("PartyCaption", Body, "", UITheme.FontCaption, UITheme.TextSecondary);
            UIBuild.Anchor(_partyCaption.rectTransform, new Vector2(0f, 0.525f), new Vector2(1f, 0.575f));

            _partyRow = UIBuild.Container("Party", Body);
            UIBuild.Anchor(_partyRow, new Vector2(0f, 0.19f), new Vector2(1f, 0.515f));
        }

        private void BuildFooter()
        {
            _note = UIBuild.Text("Note", Body, "", UITheme.FontCaption, UITheme.TextMuted,
                TextAlignmentOptions.TopLeft, wrap: true);
            UIBuild.Anchor(_note.rectTransform, new Vector2(0f, 0f), new Vector2(0.43f, 0.17f));

            // 왜 이겼고 졌는지를 되짚는 창. 자동 전투라 원인을 모르면 편성을 무엇으로 고칠지도 모른다.
            _logButton = UIBuild.Button("CombatLog", Body, "전투 로그", OpenLog, false, UITheme.FontBody);
            UIBuild.Anchor(_logButton.image.rectTransform, new Vector2(0.45f, 0.01f), new Vector2(0.64f, 0.15f));
            TextMeshProUGUI logLabel = _logButton.GetComponentInChildren<TextMeshProUGUI>();
            logLabel.richText = true;
            logLabel.text = "전투 로그\n<size=60%>L</size>";

            _continue = UIBuild.Button("Continue", Body, "계속", Continue, primary: true, UITheme.FontHeading);
            UIBuild.Anchor(_continue.image.rectTransform, new Vector2(0.66f, 0.01f), new Vector2(1f, 0.15f));
            _continueLabel = _continue.GetComponentInChildren<TextMeshProUGUI>();
            _continueLabel.richText = true;
        }

        // ── 열기 ─────────────────────────────────────────────────────

        /// <summary>
        /// 먼저 전장 위에 "전투 승리 / 전투 패배" 띠를 잠깐 띄우고, 그다음 결과 창을 연다.
        /// 예전에는 마지막 적이 쓰러진 프레임에 곧바로 창이 덮여, 이겼다는 순간이 보이지 않았다.
        /// 띠는 누르거나 엔터를 치면 바로 넘어간다.
        /// </summary>
        public void Show(BattleResultData result, Action onContinue)
        {
            EnsureBuilt();
            _onContinue = onContinue;
            result ??= new BattleResultData();
            Bind(result);
            ShowBanner(result);
        }

        public override void Hide()
        {
            HideBanner();
            _log.Hide();
            base.Hide();
        }

        private void OpenLog()
        {
            if (!IsVisible) return;
            _log.Show();
        }

        /// <summary>엔터 · 스페이스로도 넘어간다. UIManager가 매 프레임 부른다.</summary>
        public void Tick()
        {
            if (TickBanner()) return;
            if (!IsVisible) return;
            // 로그 창이 떠 있는 동안 Enter가 결과 화면을 넘기면 읽던 로그가 사라진다.
            if (_log.IsVisible) return;
            if (Input.GetKeyDown(KeyCode.L))
            {
                OpenLog();
                return;
            }
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

            string next = result.GameOver ? "메인 메뉴로"
                : string.IsNullOrEmpty(result.NextStep) ? "다음 단계" : result.NextStep;
            _continueLabel.text = $"확인\n<size=60%>{next} · ENTER</size>";
        }

        private void BindParty(BattleResultData result)
        {
            UIBuild.Clear(_partyRow);

            // 딜 기록이 없는 경로(검증 도구 등)에서는 편성만 0으로 세운다.
            var lines = new List<BattleResultData.DamageLine>(result.Damage);
            if (lines.Count == 0)
            {
                foreach (BattleResultData.Member member in result.Party)
                    lines.Add(new BattleResultData.DamageLine(member.Name, member.Portrait, 0, member.Fallen));
            }

            int fallen = 0;
            long total = 0, max = 0;
            foreach (BattleResultData.DamageLine line in lines)
            {
                if (line.Fallen) fallen++;
                total += line.Damage;
                if (line.Damage > max) max = line.Damage;
            }

            string survival = fallen == 0 ? "전원 생존" : $"<color=#{Hex(UITheme.Danger)}>전투 불능 {fallen}명</color>";
            _partyCaption.text = lines.Count == 0 ? ""
                : total > 0 ? $"딜량 · 합계 {total:N0}   ·   {survival}"
                : $"파티 · {survival}";
            _partyCaption.richText = true;

            int count = Mathf.Min(MaxMembers, lines.Count);
            for (int i = 0; i < count; i++)
            {
                BuildDamageRow(lines[i], i, max, total);
            }
        }

        private void BuildDamageRow(BattleResultData.DamageLine line, int index, long max, long total)
        {
            Image frame = UIBuild.Panel($"Member{index}", _partyRow, UITheme.SurfaceRaised,
                UIShapes.Corner.Diagonal, 6, line.Fallen ? UITheme.Danger : UITheme.Outline, 1);
            frame.raycastTarget = false;
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, DamageRowHeight);
            rect.anchoredPosition = new Vector2(0f, -index * (DamageRowHeight + DamageRowGap));

            Sprite portrait = SpriteResource.LoadPortrait(line.Portrait);
            if (portrait != null)
            {
                var go = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(frame.transform, false);
                var image = go.GetComponent<Image>();
                image.sprite = portrait;
                image.preserveAspect = true;
                image.raycastTarget = false;
                // 쓰러진 사람은 어둡게. 색을 빼는 셰이더 없이도 명도 차이로 충분히 갈린다.
                image.color = line.Fallen ? new Color(0.35f, 0.33f, 0.33f, 1f) : Color.white;
                UIBuild.Pin(image.rectTransform, new Vector2(0f, 0.5f),
                    new Vector2(DamageRowHeight - 4f, DamageRowHeight - 4f), new Vector2(6f, 0f));
            }

            string name = line.Fallen
                ? $"{line.Name}  <size=80%><color=#{Hex(UITheme.Danger)}>전투 불능</color></size>"
                : line.Name;
            TextMeshProUGUI label = UIBuild.Text("Name", frame.transform, name, UITheme.FontCaption,
                line.Fallen ? UITheme.TextMuted : UITheme.TextPrimary);
            label.richText = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            UIBuild.Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f));
            label.rectTransform.offsetMin = new Vector2(DamageRowHeight + 12f, 0f);

            // 막대. 이번 판 1등이 가득 찬 길이이고, 1등만 금색으로 칠한다.
            Image track = UIBuild.Solid("Track", frame.transform, UITheme.Track);
            track.raycastTarget = false;
            UIBuild.Anchor(track.rectTransform, new Vector2(0.31f, 0.32f), new Vector2(0.78f, 0.68f));

            float ratio = max > 0 ? Mathf.Clamp01(line.Damage / (float)max) : 0f;
            Image fill = UIBuild.Solid("Fill", track.transform,
                index == 0 && ratio > 0f ? UITheme.CodeEnhanced : UITheme.Accent);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            fill.enabled = ratio > 0f;

            int share = total > 0 ? Mathf.RoundToInt(100f * line.Damage / total) : 0;
            TextMeshProUGUI value = UIBuild.Text("Value", frame.transform,
                $"{line.Damage:N0}  <size=78%><color=#{Hex(UITheme.TextMuted)}>{share}%</color></size>",
                UITheme.FontCaption, UITheme.TextPrimary, TextAlignmentOptions.MidlineRight);
            value.richText = true;
            UIBuild.Anchor(value.rectTransform, new Vector2(0.78f, 0f), new Vector2(1f, 1f), 10f, 0f);
        }

        // ── 판정 띠 ──────────────────────────────────────────────────

        private void BuildBanner()
        {
            if (_bannerRoot != null) return;

            Canvas canvas = UIBuild.Canvas("BattleVerdictBannerCanvas", SortingOrder - 1);
            _bannerRoot = new GameObject("BattleVerdictBanner", typeof(RectTransform));
            _bannerRoot.transform.SetParent(canvas.transform, false);
            UIBuild.Stretch(_bannerRoot.GetComponent<RectTransform>());
            _bannerGroup = UIBuild.Group(_bannerRoot);

            // 전체를 덮는 옅은 판 — 누르면 띠를 건너뛴다. 전장 클릭이 새어 들어가지도 않는다.
            Image catcher = UIBuild.Solid("Backdrop", _bannerRoot.transform, new Color(0f, 0f, 0f, 0.18f));
            UIBuild.Stretch(catcher.rectTransform);
            UIBuild.OnClick(catcher.gameObject, SkipBanner);

            _bannerBand = UIBuild.Glass("Band", _bannerRoot.transform, 4, 0.82f);
            _bannerBand.raycastTarget = false;
            UIBuild.Anchor(_bannerBand.rectTransform, new Vector2(-0.02f, 0.41f), new Vector2(1.02f, 0.59f));

            _bannerCaption = UIBuild.Label("Caption", _bannerBand.transform, "", UITheme.FontCaption,
                UITheme.Accent, TextAlignmentOptions.Center);
            _bannerCaption.characterSpacing = 30f;
            UIBuild.Anchor(_bannerCaption.rectTransform, new Vector2(0f, 0.66f), new Vector2(1f, 0.90f));

            _bannerTitle = UIBuild.Text("Title", _bannerBand.transform, "", UITheme.FontDisplay * 1.7f,
                UITheme.TextPrimary, TextAlignmentOptions.Center);
            _bannerTitle.fontStyle = FontStyles.Bold;
            _bannerTitle.characterSpacing = 16f;
            UIBuild.Anchor(_bannerTitle.rectTransform, new Vector2(0f, 0.10f), new Vector2(1f, 0.70f));

            _bannerRoot.SetActive(false);
        }

        private void ShowBanner(BattleResultData result)
        {
            BuildBanner();
            Color color = result.Victory ? UITheme.Accent : UITheme.Danger;
            _bannerCaption.text = result.GameOver ? "RUN TERMINATED" : result.Victory ? "VICTORY" : "DEFEAT";
            _bannerCaption.color = color;
            _bannerTitle.text = result.GameOver ? "런 종료" : result.Victory ? "전투 승리" : "전투 패배";
            _bannerTitle.color = result.Victory ? UITheme.TextPrimary : color;

            _bannerGroup.alpha = 0f;
            _bannerRoot.SetActive(true);
            _bannerStartedAt = Time.unscaledTime;
        }

        /// <summary>띠가 떠 있으면 시간을 굴리고 true. 다 지났으면 결과 창을 연다.</summary>
        private bool TickBanner()
        {
            if (_bannerStartedAt < 0f) return false;

            float t = Time.unscaledTime - _bannerStartedAt;
            bool skip = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                        Input.GetKeyDown(KeyCode.Space);
            if (t >= BannerSeconds || skip)
            {
                SkipBanner();
                return true;
            }

            float fadeIn = Mathf.Clamp01(t / BannerFade);
            float fadeOut = Mathf.Clamp01((BannerSeconds - t) / BannerFade);
            _bannerGroup.alpha = Mathf.Min(fadeIn, fadeOut);
            // 띠가 들어올 때 살짝 납작했다가 펴지며 자리를 잡는다.
            float grow = Mathf.SmoothStep(0.92f, 1f, fadeIn);
            _bannerBand.rectTransform.localScale = new Vector3(1f, grow, 1f);
            return true;
        }

        private void SkipBanner()
        {
            if (_bannerStartedAt < 0f) return;
            HideBanner();
            base.Show();
        }

        private void HideBanner()
        {
            _bannerStartedAt = -1f;
            if (_bannerRoot != null) _bannerRoot.SetActive(false);
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
