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
        // ── 베다 진영 (야마·아그니·인드라·바유) ──
        240 => new YamaDeathContract(context),
        241 => new VedicWisdom(context),
        242 => new ElectroMastery(context),
        243 => new PyroMastery(context),
        244 => new YamaHighVoltage(context),
        245 => new YamaCharge(context),
        246 => new AllOrNothing(context),
        247 => new AgniEternalFlame(context),
        248 => new AgniArchmage(context),
        249 => new IndraThunderMark(context),
        250 => new IndraFrontrunner(context),
        251 => new IndraGenius(context),
        252 => new IndraDragonSlayer(context),
        253 => new IndraOverconfidence(context),
        254 => new IndraGuide(context),
        255 => new VayuPurifyingWind(context),
        256 => new VayuSecondWind(context),
        257 => new VayuAmbush(context),
        50 => new SeiBigBang(context),        // 세이 초기 패시브 — 빅뱅
        52 => new SeiPreparation(context),    // 세이 Lv.50 사전준비
        53 => new SeiFirstSong(context),      // 세이 Lv.92 최초의 노래
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
        87 => new GreekPhalanx(context),
        88 => new TheseusSelfHealing(context),
        89 => new TheseusOverheal(context),
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
        // ── 노르드 신규 (프레이아·로키·스카디) ──
        260 => new FreyaPhytoncide(context),
        261 => new FreyaMedicine(context),
        262 => new FreyaLeadership(context),
        263 => new FreyaWarChief(context),
        264 => new LokiFenrir(context),
        265 => new LokiSummoner(context),
        266 => new SkadiFrostWarrior(context),
        267 => new CryoMastery(context),
        268 => new SkadiCryoAffinity(context),
        269 => new SkadiElementalist(context),
        // ── 쿠베라·바루나·오르페우스·스사노오 ──
        270 => new KuberaEarthLaw(context),
        271 => new KuberaGeoAffinity(context),
        272 => new KuberaRooting(context),
        273 => new KuberaMerchant(context),
        274 => new VarunaOceanVerdict(context),
        275 => new VarunaWaterHeaven(context),
        276 => new OrpheusCheatedDeath(context),
        277 => new OrpheusBard(context),
        278 => new SusanooRaijin(context),
        279 => new SusanooWaveCut(context),
        232 => new InheritedAsclepiusNashorsTooth(context),
        (>= 101 and <= 104) or (>= 109 and <= 116) => new EnemyCommonPassive(context, codeId),
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
        >= 320 and <= 328 => new AztecPassive(
          context,
          codeId,
          AztecCodeNames.Passive(codeId)),
        // ── 이집트 (호루스·아누비스·바스테트·세트·토트·이시스) ──
        340 => new HorusSkyFalcon(context),
        341 => new AnubisSoulHarvest(context),
        342 => new BastetAirborneHunter(context),
        343 => new EgyptianPiercingShot(context),
        344 => new AnubisSpearGuard(context),
        345 => new AnubisReaper(context),
        346 => new BastetMasterThief(context),
        347 => new BastetLightArmament(context),
        348 => new BastetSelfish(context),
        349 => new SetBloodOfTheDesert(context),
        350 => new ThothToughnessScholar(context),
        351 => new ThothIllusionist(context),
        352 => new ThothHiddenTruth(context),
        353 => new IsisDesertRadiance(context),
        354 => new IsisElement(context),
        // ── 아스완 (종말의 사도 · 사령 · 오시리스 · 아문·라) ──
        360 => new AswanPyreJudgment(context, 0.10f),
        361 => new AswanPyreJudgment(context, 0.20f),
        362 => new AswanPyreSentence(context),
        363 => new AswanNetherReturn(context),
        364 => new OsirisImperfectResurrection(context),
        365 => new AmunRaAscension(context),
        366 => new AswanHolyBlade(context),
        367 => new AswanCoarseSkin(context),
        368 => new AmunRaTranscendence(context),
        369 => new AmunRaSoulDrain(context),
        370 => new AmunRaHellfire(context),
        371 => new AswanDeathChant(context),
        // ── 장비 전용 패시브 ──
        400 => new KhopeshItemPassive(context),
        401 => new YasakaniMagatamaItemPassive(context),
        402 => new YataMirrorItemPassive(context),
        403 => new MinotaurHornItemPassive(context),
        404 => new GoldenAppleItemPassive(context),
        405 => new GoldenBeastShieldItemPassive(context),
        406 => new AriadneThreadItemPassive(context),
        407 => new OuroborosBranchItemPassive(context),
        408 => new AegeusSwordItemPassive(context),
        409 => new PeriphetesClubItemPassive(context),
        410 => new JambiyaItemPassive(context),
        411 => new AlamutFortressItemPassive(context),
        412 => new AugusteWatchItemPassive(context),
        // ── 사바흐·이카리아 ──
        420 => new SabahDebuffHunter(context),
        421 => new SabahDexterity(context),
        422 => new IcariaRecklessChallenge(context),
        423 => new IcariaMentalStrength(context),
        424 => new IcariaPyroAffinity(context),
        425 => new SabahAssassin(context),
        // ── 마리 ──
        426 => new MarieCriticalCommand(context),
        427 => new MarieNobleBloodline(context),
        428 => new MarieOfficer(context),
        429 => new MarieArcDeTriomphe(context),
        // ── 로마 (레기온) ──
        280 => new NightRaid(context),
        281 => new Charisma(context),
        282 => new Intuition(context),
        283 => new Tactician(context),
        284 => new MindsEye(context),
        >= 300 and <= 317 => new LegionPassive(
          context,
          codeId,
          LegionCodeNames.Passive(codeId)),
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
        240 => new YamaNormalAttack(context),
        241 => new AgniNormalAttack(context),
        242 => new IndraNormalAttack(context),
        243 => new VayuNormalAttack(context),
        244 => new FreyaNormalAttack(context),
        245 => new LokiNormalAttack(context),
        246 => new SkadiNormalAttack(context),
        247 => new KuberaNormalAttack(context),
        248 => new VarunaNormalAttack(context),
        249 => new SusanooNormalAttack(context),
        250 => new OrpheusNormalAttack(context),
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
        >= 260 and <= 271 => new LegionNormal(
          context,
          (LegionNormalStyle)(codeId - 260)),
        >= 280 and <= 286 => new AztecNormal(
          context,
          (AztecNormalStyle)(codeId - 280)),
        290 => new HorusNormalAttack(context),
        291 => new AnubisNormalAttack(context),
        292 => new BastetNormalAttack(context),
        293 => new SetNormalAttack(context),
        294 => new ThothNormalAttack(context),
        295 => new IsisNormalAttack(context),
        // ── 아스완 ──
        300 => new AswanPaladinSlash(context, 80),    // 파멸의 성기사
        301 => new AswanPaladinSlash(context, 100),   // 종말의 성기사
        302 => new AswanInquisitorPrayer(context, 60),
        303 => new AswanInquisitorPrayer(context, 80),
        304 => new AswanWraithClaw(context),
        305 => new AswanFallenPriestRite(context),
        306 => new OsirisEarthRequiem(context),
        307 => new AmunRaBladeVolley(context),
        308 => new AmunRaBladeVolley(context, alwaysEmpowered: true),
        310 => new SabahLightningSlash(context),
        311 => new IcariaRecklessThrust(context),
        312 => new MariePistolShot(context),
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
        240 => new YamaFinalArrival(context),
        241 => new AgniWhiteFlame(context),
        242 => new IndraThunderbolt(context),
        243 => new VayuSouthWind(context),
        244 => new FreyaHarvest(context),
        245 => new LokiBaldrSlayer(context),
        246 => new SkadiIcicleSpike(context),
        247 => new KuberaGoldenQuake(context),
        248 => new VarunaMakara(context),
        249 => new SusanooAmenoMurakumo(context),
        250 => new OrpheusLament(context),
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
        >= 180 and <= 186 => new AztecUltimate(
          context,
          (AztecUltimateStyle)(codeId - 180)),
        >= 160 and <= 171 => new LegionUltimate(
          context,
          (LegionUltimateStyle)(codeId - 160)),
        290 => new HorusWadjet(context),
        291 => new AnubisStoneSpear(context),
        292 => new BastetApexExecution(context),
        293 => new SetBurningSlash(context),
        294 => new ThothEyeOfWisdom(context),
        295 => new IsisDesertDeluge(context),
        // ── 아스완 ──
        300 => new AswanPaladinJudgment(context),     // 파멸·종말의 성기사 공용
        302 => new AswanInquisitorPyre(context, 60),
        303 => new AswanInquisitorPyre(context, 80),
        304 => new AswanWraithSummon(context),
        305 => new AswanFallenPriestWard(context),
        306 => new OsirisRebirthFlood(context),
        307 => new AmunRaSolarJudgment(context),
        310 => new SabahAzrael(context),
        311 => new IcariaChallengeTheSky(context),
        312 => new MarieSongOfRevolution(context),
        100 => new EnemyTestUltimate(context), // 적 전용 테스트 궁극기
        _ => null,
      };
    }
  }
}
