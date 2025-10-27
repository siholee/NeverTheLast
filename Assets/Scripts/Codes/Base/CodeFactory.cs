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
        3 => new Soma(context), // 찬드라 패시브
        4 => new GenericPassive(context) { CodeName = "갈라테아" }, // 피그말리온 패시브
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
        5 => new Nishakara(context), // 찬드라 궁극기
        6 => new a004_U_LovesPrize(context), // 피그말리온 궁극기
        100 => new EnemyTestUltimate(context), // 적 전용 테스트 궁극기
        _ => null,
      };
    }
  }
}