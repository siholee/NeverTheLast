using System;
using System.Collections.Generic;
using System.Linq;
using Codes.Base;
using Entities;
using UnityEngine;

namespace Managers.UI.Core
{
    /// <summary>
    /// 보상 코드를 다루는 선택 흐름 네 가지를 한곳에 둔다(Detail_17 §2).
    ///   · 코드 받기: 받을 아군 → (칸이 차면) 덮어쓸 코드 → 확정
    ///   · 승급의 인장: 아군 → 올릴 코드 → (조화의 문장이면) 바꿀 T2 → 확정
    ///   · 전수의 두루마리: 내줄 아군 → 코드 → 받을 아군(빈 칸) → 확정
    /// 보상 화면 · 상점 · 인벤토리가 같은 규칙 · 같은 문구를 쓰도록 판 두 개(<see cref="UnitTargetPicker"/> ·
    /// <see cref="CodeChoicePicker"/>)를 이 흐름이 번갈아 연다.
    /// </summary>
    public sealed class RewardCodeFlow
    {
        private readonly UnitTargetPicker _units;
        private readonly CodeChoicePicker _codes;
        private readonly Action _onCancel;
        private readonly Action<string> _setTitle;
        private Action<Unit> _unitPicked;
        private Action<int> _codePicked;

        public RewardCodeFlow(Transform parent, Action onCancel, Action<string> setTitle, string backLabel)
        {
            _onCancel = onCancel;
            _setTitle = setTitle;
            _units = new UnitTargetPicker(parent, unit => { if (unit != null) _unitPicked?.Invoke(unit); }, Cancel, backLabel);
            _codes = new CodeChoicePicker(parent, Cancel, backLabel);
        }

        public bool IsActive => _units.IsActive || _codes.IsActive;

        /// <summary>
        /// 안내문 아래에 덧붙일 거래 요약(상점 — 가격 · 보유 골드 · 남은 구매). 선택 판이 매대와 지갑을 덮으므로
        /// 결제 직전에도 무엇을 얼마에 사는지 읽히게 한다. 보상 · 공용 보급은 비워 둔다.
        /// </summary>
        public string Note { get; set; }

        private string WithNote(string guide) => string.IsNullOrEmpty(Note) ? guide : $"{guide}\n{Note}";

        public void Hide()
        {
            _units.SetActive(false);
            _codes.SetActive(false);
        }

        private void Cancel()
        {
            Hide();
            _onCancel?.Invoke();
        }

        // ── 코드 받기 ───────────────────────────────────────────

        /// <summary>보상 · 상점의 코드 카드. 확정되면 받는 아군을 넘긴다. 두 번째 인자는 구형 호출 호환용 0이다.</summary>
        public bool StartReceive(int codeId, string displayName, Action<Unit, int> onConfirm)
        {
            List<Unit> candidates = Party().Where(unit => unit.CanReceiveRewardCode(codeId, out _, out _, out _)).ToList();
            if (candidates.Count == 0) return false;

            _setTitle?.Invoke(displayName + " — 받을 아군");
            _unitPicked = unit =>
            {
                if (!unit.CanReceiveRewardCode(codeId, out _, out _, out _)) return;
                Hide();
                onConfirm?.Invoke(unit, 0);
            };
            return _units.Show(candidates, WithNote("코드를 받을 아군을 고르세요."), unit => ReceivePreview(codeId, unit));
        }

        /// <summary>
        /// 매대 카드의 아군 한 줄 — "세이 · 7/7 · 보관함", "인드라 · 0/7 · 활성화", "쿠베라 · 상위 보유".
        /// 자세한 문구는 선택 판(<see cref="ReceivePreview"/>)과 툴팁이 맡는다.
        /// </summary>
        public static string ReceiveShort(int codeId, Unit unit)
        {
            if (!unit.CanReceiveRewardCode(codeId, out int replaces, out bool needsOverwrite, out string reason))
                return $"{unit.UnitName} · {(reason == "상위 코드 보유" ? "상위 보유" : reason)}";
            int used = unit.RewardCodeIds.Count;
            string slot = replaces > 0 ? "하위 대체" : unit.FreeRewardCodeSlots == 0 ? "보관함" : "활성화";
            return $"{unit.UnitName} · {used}/{Unit.RewardCodeSlotCount} · {slot}";
        }

        public static string ReceivePreview(int codeId, Unit unit)
        {
            if (!unit.CanReceiveRewardCode(codeId, out int replaces, out bool needsOverwrite, out string reason))
                return reason;
            if (replaces > 0) return $"▲ 활성화 · {NameOf(replaces)}은 보관";
            int used = unit.RewardCodeIds.Count;
            return unit.FreeRewardCodeSlots == 0 ? $"활성 {used}/7 — 보관함으로 받기" : $"활성 {used}/7 — 빈 칸에 활성화";
        }

        public bool StartNectar(ItemData item,Action<Unit> onConfirm)
        {
            if(item?.IsNectar!=true)return false;
            _setTitle?.Invoke(item.name+" — 받을 아군");
            _unitPicked=unit=>{Hide();onConfirm?.Invoke(unit);};
            return _units.Show(Party(),ItemTooltip.EffectLine(item),unit=>"현재 체력 비율 유지 · 런이 끝날 때까지 지속");
        }

        // ── 승급의 인장 ─────────────────────────────────────────

        /// <summary>인장이 이 코드를 올릴 대상(한 단계, 최대 티어 이하). 없으면 0. 조화의 문장은 -1(고르게 한다).</summary>
        public static int SealTarget(int codeId, int maxTier)
        {
            if (codeId == 512) return maxTier >= 2 ? -1 : 0;
            int target = RewardCodeCatalog.SealTargetOf(codeId);
            return target > 0 && RewardCodeCatalog.TierOf(target) <= maxTier ? target : 0;
        }

        private static bool CanSeal(Unit unit, int codeId, int maxTier)
        {
            int target = SealTarget(codeId, maxTier);
            if (target == -1) return RewardCodeCatalog.HarmonySealChoices.Any(choice => !unit.OwnedRewardCodeIds.Contains(choice));
            return target > 0 && !unit.OwnedRewardCodeIds.Contains(target);
        }

        public bool StartSeal(ItemData seal, Action<Unit, int, int, string> onConfirm)
        {
            int maxTier = seal?.sealMaxTier ?? 0;
            List<Unit> candidates = Party().Where(unit => unit.OwnedRewardCodeIds.Any(id => CanSeal(unit, id, maxTier))).ToList();
            if (candidates.Count == 0) return false;

            _setTitle?.Invoke(seal.name + " — 아군");
            _unitPicked = unit =>
            {
                _units.SetActive(false);
                _setTitle?.Invoke(seal.name + " — 올릴 코드");
                var options = new List<CodeChoicePicker.Option>();
                foreach (var instance in unit.OwnedRewardCodeInstances.Where(x => CanSeal(unit, x.codeId, maxTier)))
                {
                    int codeId = instance.codeId;
                    string instanceId = instance.instanceId;
                    int target = SealTarget(codeId, maxTier);
                    bool active = unit.RewardCodeSlotInstances.Any(x => x?.instanceId == instanceId);
                    string location = (active ? "활성" : "보관") + $" · 취득 {instance.acquiredStage}층";
                    string charge = RewardCodeCatalog.Find(codeId)?.capacitorCap > 0 ? $" · 저장 {instance.charge:0.##}" : "";
                    string destination = target == -1 ? "퍼센트 스탯 코드 중 선택" : $"{NameOf(target)} (T{RewardCodeCatalog.TierOf(target)})";
                    options.Add(new CodeChoicePicker.Option(codeId, NameOf(codeId),
                        $"{location}{charge} → {destination}", RewardCodeCatalog.TierOf(codeId), () =>
                        {
                            if (target != -1)
                            {
                                Hide();
                                onConfirm?.Invoke(unit, codeId, target, instanceId);
                                return;
                            }
                            _setTitle?.Invoke(seal.name + " — 바꿀 코드");
                            _codePicked = choice =>
                            {
                                Hide();
                                onConfirm?.Invoke(unit, codeId, choice, instanceId);
                            };
                            _codes.Show(RewardCodeCatalog.HarmonySealChoices
                                .Where(choice => !unit.OwnedRewardCodeIds.Contains(choice))
                                .Select(choice => Option(choice, Describe(choice))).ToList(),
                                "조화의 문장을 바꿀 퍼센트 스탯 코드를 고르세요.", _codePicked);
                        }));
                }
                _codes.Show(options, $"{seal.name}은(는) 선택한 한 장을 최대 T{maxTier}까지 올립니다.", null);
            };
            return _units.Show(candidates, "인장을 쓸 아군을 고르세요.",
                unit => $"올릴 수 있는 코드 {unit.OwnedRewardCodeIds.Count(id => CanSeal(unit, id, maxTier))}개");
        }

        // ── 전수의 두루마리 ─────────────────────────────────────

        public bool StartTransfer(ItemData scroll, Action<Unit, int, Unit> onConfirm)
        {
            List<Unit> party = Party();
            List<Unit> givers = party.Where(unit => unit.RewardCodeIds.Any(id =>
                party.Any(other => other != unit && other.FreeRewardCodeSlots > 0 &&
                                   other.CanReceiveRewardCode(id, out _, out _, out _)))).ToList();
            if (givers.Count == 0) return false;

            _setTitle?.Invoke(scroll.name + " — 내줄 아군");
            _unitPicked = giver =>
            {
                _units.SetActive(false);
                _setTitle?.Invoke(scroll.name + " — 옮길 코드");
                _codePicked = codeId =>
                {
                    List<Unit> receivers = party.Where(other => other != giver && other.FreeRewardCodeSlots > 0 &&
                                                                other.CanReceiveRewardCode(codeId, out _, out _, out _)).ToList();
                    if (receivers.Count == 0) return;
                    _codes.SetActive(false);
                    _setTitle?.Invoke(scroll.name + " — 받을 아군");
                    _unitPicked = receiver =>
                    {
                        Hide();
                        onConfirm?.Invoke(giver, codeId, receiver);
                    };
                    _units.Show(receivers, "코드를 받을 아군을 고르세요(빈 칸이 있어야 합니다).",
                        unit => $"칸 {unit.RewardCodeIds.Count}/{Unit.RewardCodeSlotCount}");
                };
                _codes.Show(OwnedOptions(giver, id => party.Any(other => other != giver && other.FreeRewardCodeSlots > 0 &&
                                                                        other.CanReceiveRewardCode(id, out _, out _, out _)),
                        id => $"T{RewardCodeCatalog.TierOf(id)} · {Describe(id)}"),
                    "옮길 코드를 고르세요.", _codePicked);
            };
            return _units.Show(givers, "코드를 내줄 아군을 고르세요.",
                unit => $"코드 {unit.RewardCodeIds.Count}개");
        }

        // ── 공용 ────────────────────────────────────────────────

        /// <summary>코드를 주고받을 수 있는 파티(벤치 · 쓰러진 아군 포함, 소환수 제외).</summary>
        public static List<Unit> Party() => Combat.AttackForms.PartyUnits().ToList();

        private static List<CodeChoicePicker.Option> OwnedOptions(Unit unit, Func<int, bool> filter, Func<int, string> subtitle)
            => unit.OwnedRewardCodeIds.Distinct().Where(filter).Select(id => Option(id, subtitle(id))).ToList();

        private static CodeChoicePicker.Option Option(int id, string subtitle)
            => new(id, NameOf(id), subtitle, RewardCodeCatalog.TierOf(id));

        public static string NameOf(int codeId) => RewardCodeCatalog.Find(codeId)?.verbalName ?? $"코드 {codeId}";

        private static string Describe(int codeId) => CodeCatalog.Summary(RewardCodeCatalog.Find(codeId)?.description);

        /// <summary>보상 코드 툴팁 — 티어 · 효과 전문 · 태그 · 상위 코드. 파티 창 · 상점 · 코드 선택 판이 같이 쓴다.</summary>
        public static List<Managers.UI.Core.UITooltip.Line> TooltipLines(int codeId)
        {
            var lines = new List<UITooltip.Line>
            {
                UITooltip.Line.Note($"TIER {RewardCodeCatalog.TierOf(codeId)} · 보상 코드", Theme.UITheme.TextMuted),
            };
            string summary = Describe(codeId);
            if (summary.Length > 0) lines.Add(UITooltip.Line.Note(summary, Theme.UITheme.TextPrimary));
            string tags = string.Join(" · ", UnitTagCatalog.Resolve(RewardCodeCatalog.TagsOf(codeId)).Select(tag => tag.Name));
            if (tags.Length > 0) lines.Add(new UITooltip.Line("태그", tags, Theme.UITheme.Accent));
            int upper = RewardCodeCatalog.UpperOf(codeId);
            if (upper > 0)
                lines.Add(new UITooltip.Line("상위 코드", $"{NameOf(upper)} (T{RewardCodeCatalog.TierOf(upper)})", Theme.UITheme.TextMuted));
            return lines;
        }
    }
}
