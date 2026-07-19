using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Ultimate;
using Codes.Test;

namespace Codes.Base
{
  public static class CodeFactory
  {
    public static PassiveCode CreatePassiveCode(int codeId, PassiveCodeContext context)
    {
      return codeId switch
      {
        1 => new HolyEnchant(context),
        2 => new GenericPassive(context) { CodeName = "사냥꾼의 독" }, // 아탈란테 패시브
        3 => new ChandraNishakara(context), // 찬드라 패시브
        4 => new GenericPassive(context) { CodeName = "갈라테아" }, // 피그말리온 패시브
        5 => new ShiFinalVerse(context), // 시 패시브
        8 => new ChandraBastion(context),
        11 => new Block(context),
        12 => new ChandraSecondWind(context),
        20 => new ChandraBulwark(context),
        27 => new ChandraLastStandFormation(context),
        35 => new ChandraPurificationBath(context),
        38 => new ChandraIronWall(context),
        43 => new ChandraLokapala(context),
        62 => new ChandraMoonlight(context),
        70 => new QuetzalcoatlYorisGuard(context),
        71 => new QuetzalcoatlRegeneration(context),
        72 => new QuetzalcoatlLifesteal(context),
        73 => new QuetzalcoatlForestGrace(context),
        74 => new QuetzalcoatlSpellSniper(context),
        75 => new QuetzalcoatlRooting(context),
        76 => new QuetzalcoatlGiver(context),
        77 => new QuetzalcoatlScholar(context),
        78 => new QuetzalcoatlTranscendence(context),
        79 => new QuetzalcoatlPioneer(context),
        80 => new QuetzalcoatlCall(context),
        81 => new QuetzalcoatlGuardianWill(context),
        82 => new QuetzalcoatlCipactliSlayer(context),
        90 => new MacanaGrowth(context),
        120 => new TsukuyomiMoonReckoning(context),
        121 => new TsukuyomiSpellShield(context),
        122 => new TsukuyomiFickle(context),
        123 => new TsukuyomiCurse(context),
        124 => new TsukuyomiWidenWound(context),
        125 => new TsukuyomiPainfulWound(context),
        126 => new TsukuyomiCycle(context),
        127 => new TsukuyomiChainLightning(context),
        128 => new TsukuyomiFullMoon(context),
        129 => new MoonGodBlessing(context),
        100 => new EnemyTestPassive(context), // 적 전용 테스트 패시브
        _ => null,
      };
    }

    public static NormalCode CreateNormalCode(int codeId, NormalCodeContext context)
    {
      return codeId switch
      {
        1 => new a005_NAtlanta(context), // 아탈란테 일반공격
        2 => new a004_NPygmalion(context), // 피그말리온 일반공격
        10 => new MagicBolt(context),
        11 => new Shoot(context),
        12 => new Sachi(context),
        70 => new QuetzalcoatlBall(context),
        120 => new TsukuyomiLightningBolt(context),
        100 => new EnemyTestNormal(context), // 적 전용 테스트 일반 공격
        _ => new NormalAttack(context), // 기본 일반공격 (임시, 나중에 각 유닛별로 교체 예정)
      };
    }

    public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
    {
      return codeId switch
      {
        1 => new Laevateinn(context),
        2 => new ApplyPoison(context),
        3 => new a005_U_Moonfall(context), // 아탈란테 궁극기
        4 => new ApplyBurn(context), // 화상 부여
        5 => new a012_U_Soma(context), // 찬드라 궁극기
        6 => new a004_U_LovesPrize(context), // 피그말리온 궁극기
        7 => new a002_U_FinalBell(context), // 시 궁극기
        70 => new QuetzalcoatlYorisUltimate(context),
        120 => new TsukuyomiMoonThunder(context),
        100 => new EnemyTestUltimate(context), // 적 전용 테스트 궁극기
        _ => null,
      };
    }
  }
}
