using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>카드 오라의 모양.</summary>
    public enum CardAuraStyle
    {
        /// <summary>카드 테두리 바깥으로 은은하게 번지는 빛. 받은 버프에 쓴다.</summary>
        Glow,
    }

    /// <summary>
    /// 상태 효과가 구현하면 그 상태가 붙어 있는 동안 <b>카드에 오라가 그려진다.</b>
    /// 상태 아이콘은 작아서 8배속에서 놓치기 쉽다 — 파티 전체 버프 · 집중처럼 판을 바꾸는 상태는
    /// 카드 자체가 빛나야 읽힌다. 카드는 가장 먼저 찾은 Glow 하나를 그린다.
    /// </summary>
    public interface ICardAura
    {
        CardAuraStyle AuraStyle { get; }
        Color AuraColor { get; }
    }
}
