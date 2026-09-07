using BaseClasses;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 소환수 한 마리의 설계도.
    ///
    /// 소환수는 <c>10_units.yaml</c>에 항목을 갖지 않는다. 소환자의 능력치를 비율로 물려받는
    /// 종속 개체이므로, 데이터 파일 대신 소환 시점의 소환자를 기준으로 세운다.
    /// 코드 ID는 <b>500번대 소환수 대역</b>을 쓴다.
    /// </summary>
    public sealed class SummonSpec
    {
        /// <summary>카드에 찍히는 이름.</summary>
        public string Name = "소환수";

        /// <summary>소환자 능력치를 물려받는 비율. 0.5면 절반이다.</summary>
        public float StatRatio = 0.5f;

        /// <summary>주스탯. 비면 소환자의 주스탯을 그대로 쓴다.</summary>
        public string MainStat;

        /// <summary>고유 원소. 비면 소환자의 원소를 물려받는다.</summary>
        public string Element;

        /// <summary>일반행동 코드 ID(500번대).</summary>
        public int NormalCodeId;

        /// <summary>궁극기 코드 ID(500번대). 0이면 궁극기를 갖지 않는다.</summary>
        public int UltimateCodeId;

        /// <summary>초상화 리소스 키. 비면 소환자의 초상화를 쓰지 않고 비워 둔다.</summary>
        public string Portrait;

        /// <summary>몇 턴 뒤 스스로 사라지는가. 0 이하면 소환자가 살아 있는 한 유지된다.</summary>
        public int LifetimeTurns;

        /// <summary>소환자 기준으로 실제 기본 스탯 다섯 개를 계산한다.</summary>
        public void ResolveStats(Unit owner, out int str, out int dex, out int con, out int intel, out int luk)
        {
            float ratio = Mathf.Max(0f, StatRatio);
            str = Scale(owner.GetBaseStr(), ratio);
            dex = Scale(owner.GetBaseDex(), ratio);
            con = Scale(owner.GetBaseCon(), ratio);
            intel = Scale(owner.GetBaseInt(), ratio);
            luk = Scale(owner.GetBaseLuk(), ratio);
        }

        // 스탯이 0이면 UnitStats가 데이터 오류로 보고 오류 로그를 뱉는다. 최소 1을 남긴다.
        private static int Scale(int value, float ratio)
            => Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0, value) * ratio));

        public string ResolveMainStat(Unit owner)
            => string.IsNullOrWhiteSpace(MainStat) ? owner.MainStat : MainStat;

        public string ResolveElement(Unit owner)
            => string.IsNullOrWhiteSpace(Element) ? owner.Element : Element;
    }

    /// <summary>소환수 코드 ID 대역(500~599)과 기성 소환수 설계도.</summary>
    public static class SummonCatalog
    {
        public const int FlyerNormalCodeId = 500;
        public const int FlyerUltimateCodeId = 500;
        public const int FenrirNormalCodeId = 501;

        /// <summary>라이트의 플라이어. 소환 시점 라이트 능력치의 50%를 물려받는다.</summary>
        public static SummonSpec Flyer(Unit owner) => new()
        {
            Name = "플라이어",
            StatRatio = 0.5f,
            MainStat = BaseEnums.PrimaryStat.INT.ToString(),
            Element = owner != null ? owner.Element : "Anemo",
            NormalCodeId = FlyerNormalCodeId,
            UltimateCodeId = FlyerUltimateCodeId,
            Portrait = "FLYER_PORTRAIT",
            LifetimeTurns = 3,
        };

        /// <summary>
        /// 로키의 펜리르. 궁극기가 없고 수명도 없다 — 로키가 살아 있는 한 함께 싸운다.
        ///
        /// 🟡 원안에 체력·능력치 비율이 없어 플라이어와 같은 50% 규격을 임시로 쓴다.
        /// </summary>
        public static SummonSpec Fenrir(Unit owner) => new()
        {
            Name = "펜리르",
            StatRatio = 0.5f,
            MainStat = BaseEnums.PrimaryStat.STR.ToString(),
            Element = owner != null ? owner.Element : "None",
            NormalCodeId = FenrirNormalCodeId,
            UltimateCodeId = 0,      // 궁극기 없음
            Portrait = "FENRIR_PORTRAIT",
            LifetimeTurns = 0,       // 소환자가 쓰러질 때까지
        };
    }
}
