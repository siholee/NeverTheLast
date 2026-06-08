using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;

namespace Codes.Base
{
    /// <summary>
    /// 코드(스킬) 팩토리.<br/>
    /// codeId 0 = 코드 없음 (null 반환).
    /// </summary>
    public static class CodeFactory
    {
        // ── 패시브 코드 ────────────────────────────────────────────────────────
        public static PassiveCode CreatePassiveCode(int codeId, PassiveCodeContext context)
        {
            return codeId switch
            {
                1 => new SeiP1Passive(context),    // 지령의 일체 (세이 P1)
                _ => null,
            };
        }

        // ── 기본 공격 코드 (Basic 행동: SP 생성) ──────────────────────────────
        public static NormalCode CreateBasicCode(int codeId, NormalCodeContext context)
        {
            return codeId switch
            {
                1 => new MagicBullet(context),     // 마력탄 (주술사 클래스 기본 공격)
                2 => new FireArrow(context),        // 화염살 (완드 무기 스킬)
                _ => null,
            };
        }

        // ── 클래스 스킬 코드 (클래스 고유 행동) ──────────────────────────────
        public static NormalCode CreateClassCode(int codeId, NormalCodeContext context)
        {
            return codeId switch
            {
                1 => new Meditation(context),   // 명상 (주술사/캐스터 클래스)
                _ => null,
            };
        }

        // ── 스킬 코드 (Normal 행동: SP 소모) ─────────────────────────────────
        public static NormalCode CreateNormalCode(int codeId, NormalCodeContext context)
        {
            return codeId switch
            {
                1 => new RockCannon(context),      // 암석포  (세이 Lv.1,  SP-1)
                2 => new RockMissile(context),     // 바위 미사일 (세이 Lv.15, SP-1)
                3 => new StoneShower(context),     // 스톤 샤워   (세이 Lv.25, SP-2)
                4 => new StoneEdge(context),       // 스톤 에지   (세이 Lv.40, SP-2)
                _ => null,
            };
        }

        // ── 궁극기 코드 (Ultimate 행동: 마나 소모) ───────────────────────────
        public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
        {
            return codeId switch
            {
                1 => new Meteor(context),          // 메테오 (세이 Lv.75)
                _ => null,
            };
        }
    }
}
