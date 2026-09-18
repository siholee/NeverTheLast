using System.Collections.Generic;
using System.Linq;
using System.Text;
using Codes.Base;
using Managers.UI.Theme;
using UnityEngine;

namespace Managers.UI.Core
{
    /// <summary>
    /// 코드 설명을 TMP 서식 문자열로 짜는 도우미. 자료실 본문과 캐릭터 선택의 기술 상세가
    /// <b>같은 코드를 같은 모양</b>으로 보여 주도록 한 곳에 둔다.
    ///
    /// 데이터 문장에는 <c>&lt;고유 패시브&gt;</c> 같은 꺾쇠가 섞여 있어 그대로 넣으면 태그로 읽힌다.
    /// 데이터에서 온 글은 전부 <see cref="Plain"/>으로 감싼다.
    /// </summary>
    public static class CodeText
    {
        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        public static string Plain(string text) =>
            string.IsNullOrEmpty(text) ? "" : $"<noparse>{text.Replace("</noparse>", "")}</noparse>";

        public static string Paint(string text, Color color) => $"<color=#{Hex(color)}>{text}</color>";

        public static string SlotName(CodeCatalog.Slot slot) => slot switch
        {
            CodeCatalog.Slot.Normal => "일반행동",
            CodeCatalog.Slot.Special => "특수행동",
            CodeCatalog.Slot.Ultimate => "궁극기",
            _ => "패시브",
        };

        /// <summary>
        /// 코드 한 덩어리 — 이름 · 종류, 설명, 꼬리표. 이름은 받는 쪽 글자 크기를 그대로 쓰고,
        /// 설명과 꼬리표만 <paramref name="bodySize"/> · <paramref name="microSize"/>로 줄인다.
        /// 표에 없는 ID는 빈칸 대신 어느 슬롯의 몇 번이 없는지 적는다.
        /// </summary>
        public static void AppendBlock(StringBuilder sb, CodeCatalog.Slot slot, int codeId, string kind,
            float bodySize = UITheme.FontCaption, float microSize = UITheme.FontMicro)
        {
            if (codeId <= 0) return;

            CodeCatalog.Entry entry = CodeCatalog.Find(slot, codeId);
            string name = entry?.verbalName ?? $"{SlotName(slot)} #{codeId}";
            List<string> markers = CodeCatalog.Markers(entry?.description);
            Color tint = markers.Any(marker => marker.StartsWith("고유")) ? UITheme.CodeUnique : UITheme.TextPrimary;

            sb.Append($"<b>{Paint(Plain(name), tint)}</b>");
            // 일반행동은 이름도 "일반행동"이라 종류를 한 번 더 적으면 같은 말이 두 번 나온다.
            if (kind != name) sb.Append($"  <size={microSize}>{Paint(Plain(kind), UITheme.TextMuted)}</size>");
            sb.Append('\n');

            string summary = CodeCatalog.Summary(entry?.description);
            sb.Append(summary.Length > 0
                ? $"<size={bodySize}>{Paint(Plain(summary), UITheme.TextSecondary)}</size>\n"
                : $"<size={bodySize}>{Paint(Plain($"설명 없음 — 20_codes.yaml {slot} #{codeId}"), UITheme.Danger)}</size>\n");

            if (markers.Count > 0)
                sb.Append($"<size={microSize}>{Paint(Plain(string.Join(" · ", markers)), UITheme.TextMuted)}</size>\n");

            sb.Append("<size=6>\n</size>");
        }
    }
}
