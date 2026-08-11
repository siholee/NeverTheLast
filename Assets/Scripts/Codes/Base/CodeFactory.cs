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
        1 => new HolyEnchant(context),        // (구) 홀리 인챈트 — 미사용, 참고용 보존
        50 => new SeiCreation(context),       // 세이 초기 패시브 — 창조
        51 => new SeiCitadel(context),        // 세이 Lv.6  성채
        52 => new SeiPreparation(context),    // 세이 Lv.50 사전준비
        53 => new SeiBigBang(context),        // 세이 Lv.92 빅뱅
        2 => new AtalanteWeaknessTracker(context), // 아탈란테 패시브
        140 => new AtalanteArcher(context),
        141 => new AtalanteAgility(context),
        142 => new AtalanteGlassCannon(context),
        143 => new AtalanteHunter(context),
        144 => new AtalanteVirulentPoison(context),
        145 => new AtalanteSniper(context),
        146 => new AtalanteHuntressBlessing(context),
        3 => new ChandraNishakara(context), // 찬드라 패시브
        4 => new GalateaPassive(context), // 피그말리온 패시브
        5 => new ShiFinalVerse(context), // 시 패시브
        54 => new ShiSwordMaster(context),
        55 => new ShiAllWeaponMastery(context),
        56 => new ShiSwiftness(context),
        58 => new HeavenlyKiller(context),
        59 => new ShiTenDaysNoFlower(context),
        60 => new ShiCertainDestiny(context),
        8 => new ChandraBastion(context),
        10 => new ArmorTrainingPassive(context),
        11 => new Block(context),
        12 => new ChandraSecondWind(context),
        20 => new ChandraBulwark(context),
        27 => new ChandraLastStandFormation(context),
        35 => new ChandraPurificationBath(context),
        38 => new ChandraIronWall(context),
        62 => new ChandraMoonlight(context),
        70 => new QuetzalcoatlBounty(context),
        71 => new QuetzalcoatlWisdom(context),
        72 => new QuetzalcoatlLucky(context),
        73 => new QuetzalcoatlOvercritScholar(context),
        74 => new QuetzalcoatlMeditation(context),
        75 => new QuetzalcoatlWingedSerpent(context),
        80 => new OrionGeoAffinity(context),
        81 => new OrionHeavyInfantry(context),
        82 => new OrionMarksman(context),
        83 => new OrionSentinel(context),
        84 => new TheseusCodeWeave(context),
        85 => new TheseusHydroAffinity(context),
        86 => new TheseusFighter(context),
        87 => new TheseusPhalanx(context),
        88 => new TheseusSelfHealing(context),
        89 => new TheseusOverheal(context),
        90 => new MacanaGrowth(context),
        91 => new AsclepiusNashorsTooth(context),
        92 => new AmaterasuSunRhythm(context),
        93 => new AmaterasuConcealment(context),
        94 => new AmaterasuFireworks(context),
        95 => new AmaterasuBreakthrough(context),
        96 => new AsclepiusCriticalTreatment(context),
        97 => new AsclepiusManaBreathing(context),
        98 => new AsclepiusDivineMedicine(context),
        220 => new PygmalionWeaklingContempt(context),
        221 => new PygmalionSlowAndSteady(context),
        222 => new PygmalionSharpThorns(context),
        223 => new PygmalionLoveGodBlessing(context),
        230 => new InheritedAtalanteWeaknessTracker(context),
        231 => new InheritedOrionGeoAffinity(context),
        232 => new InheritedAsclepiusNashorsTooth(context),
        233 => new InheritedAmaterasuSunRhythm(context),
        >= 101 and <= 112 => new EnemyCommonPassive(context, codeId),
        120 => new TsukuyomiMoonReckoning(context),
        121 => new TsukuyomiSpellShield(context),
        122 => new TsukuyomiFickle(context),
        123 => new TsukuyomiCurse(context),
        124 => new TsukuyomiWidenWound(context),
        125 => new TsukuyomiPainfulWound(context),
        126 => new TsukuyomiCycle(context),
        127 => new TsukuyomiChainLightning(context),
        128 => new TsukuyomiFullMoon(context),
        130 => new SurtrTwilight(context),
        133 => new SurtrGiant(context),
        134 => new SurtrFireMastery(context),
        135 => new SurtrCelestialBody(context),
        136 => new SurtrFighter(context),
        137 => new SurtrClutchPlayer(context),
        138 => new SurtrGodslayer(context),
        (>= 164 and <= 171) or (>= 176 and <= 179) => new ColosseumPassive(
          context,
          codeId,
          ColosseumCodeNames.Passive(codeId)),
        >= 204 and <= 211 => new AztecPassive(
          context,
          codeId,
          AztecCodeNames.Passive(codeId)),
        _ => null,
      };
    }

    public static NormalCode CreateNormalCode(int codeId, NormalCodeContext context)
    {
      return codeId switch
      {
        1 => new a005_NAtlanta(context), // 아탈란테 일반공격
        2 => new a004_NPygmalion(context), // 피그말리온 일반공격
        3 => new SeiRadiantBolt(context), // 세이 일반공격
        10 => new MagicBolt(context),
        11 => new Shoot(context),
        12 => new Sachi(context),
        13 => new ShiVoidSlash(context), // 시 일반공격
        70 => new QuetzalcoatlBall(context),
        80 => new OrionNormalAttack(context),
        84 => new TheseusNormalAttack(context),
        91 => new AsclepiusNormalAttack(context),
        92 => new AmaterasuNormalAttack(context),
        120 => new TsukuyomiLightningBolt(context),
        130 => new SurtrScorchingSlash(context),
        >= 101 and <= 108 => new EnemyArchetypeNormal(
          context,
          (EnemyArchetypeNormalStyle)(codeId - 101)),
        _ => new NormalAttack(context), // 기본 일반공격 (임시, 나중에 각 유닛별로 교체 예정)
      };
    }

    public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
    {
      return codeId switch
      {
        3 => new a005_U_Moonfall(context), // 아탈란테 궁극기
        5 => new a012_U_Soma(context), // 찬드라 궁극기
        6 => new a004_U_LovesPrize(context), // 피그말리온 궁극기
        7 => new a002_U_FinalBell(context), // 시 궁극기
        8 => new SeiSanctuary(context), // 세이 궁극기
        70 => new QuetzalcoatlYorisUltimate(context),
        80 => new OrionHeavyBlow(context),
        84 => new TheseusRecoveryStrike(context),
        91 => new AsclepiusFlask(context),
        92 => new AmaterasuPortalBarrage(context),
        120 => new TsukuyomiMoonThunder(context),
        130 => new SurtrRagnarok(context),
        (>= 140 and <= 147) or 149 => new ColosseumUltimate(
          context,
          (ColosseumUltimateStyle)(codeId - 140)),
        >= 150 and <= 157 => new AztecUltimate(
          context,
          (AztecUltimateStyle)(codeId - 150)),
        100 => new EnemyTestUltimate(context), // 적 전용 테스트 궁극기
        _ => null,
      };
    }
  }
}
