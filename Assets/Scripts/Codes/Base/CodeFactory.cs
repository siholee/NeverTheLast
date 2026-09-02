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
        180 => new HolyEnchant(context),        // (구) 홀리 인챈트 — 미사용, 참고용 보존
        // ── 베다 진영 (야마·아그니·인드라·바유) ──
        261 => new YamaDeathContract(context),
        56 => new VedicWisdom(context),
        57 => new ElectroMastery(context),
        58 => new PyroMastery(context),
        59 => new YamaHighVoltage(context),
        60 => new YamaCharge(context),
        61 => new AllOrNothing(context),
        262 => new AgniEternalFlame(context),
        62 => new AgniArchmage(context),
        263 => new IndraThunderMark(context),
        63 => new IndraFrontrunner(context),
        64 => new IndraGenius(context),
        65 => new IndraDragonSlayer(context),
        66 => new IndraOverconfidence(context),
        67 => new IndraGuide(context),
        264 => new VayuPurifyingWind(context),
        68 => new VayuSecondWind(context),
        69 => new VayuAmbush(context),
        201 => new SeiBigBang(context),        // 세이 초기 패시브 — 빅뱅
        7 => new SeiPreparation(context),    // 세이 Lv.50 사전준비
        8 => new SeiFirstSong(context),      // 세이 Lv.92 최초의 노래
        221 => new AtalanteWeaknessTracker(context), // 아탈란테 패시브
        44 => new AtalanteArcher(context),
        45 => new AtalanteAgility(context),
        46 => new AtalanteGlassCannon(context),
        47 => new AtalanteHunter(context),
        48 => new AtalanteVirulentPoison(context),
        49 => new AtalanteSniper(context),
        50 => new AtalanteHuntressBlessing(context),
        260 => new ChandraNishakara(context), // 찬드라 패시브
        220 => new GalateaPassive(context), // 피그말리온 패시브
        202 => new ShiFinalVerse(context), // 시 패시브
        9 => new ShiSwordMaster(context),
        10 => new ShiSwiftness(context),
        11 => new HeavenlyKiller(context),
        12 => new ShiTenDaysNoFlower(context),
        13 => new ShiCertainDestiny(context),
        1 => new ChandraBastion(context),
        181 => new ArmorTrainingPassive(context),
        182 => new Block(context),
        2 => new ChandraSecondWind(context),
        3 => new ChandraBulwark(context),
        4 => new ChandraLastStandFormation(context),
        5 => new ChandraPurificationBath(context),
        6 => new ChandraIronWall(context),
        14 => new ChandraMoonlight(context),
        340 => new QuetzalcoatlBounty(context),
        15 => new QuetzalcoatlWisdom(context),
        16 => new QuetzalcoatlLucky(context),
        17 => new QuetzalcoatlOvercritScholar(context),
        18 => new QuetzalcoatlMeditation(context),
        19 => new QuetzalcoatlWingedSerpent(context),
        222 => new OrionGeoAffinity(context),
        20 => new OrionHeavyInfantry(context),
        21 => new OrionMarksman(context),
        22 => new OrionSentinel(context),
        223 => new TheseusCodeWeave(context),
        23 => new TheseusHydroAffinity(context),
        24 => new TheseusFighter(context),
        183 => new GreekPhalanx(context),
        25 => new TheseusSelfHealing(context),
        26 => new TheseusOverheal(context),
        224 => new AsclepiusNashorsTooth(context),
        240 => new AmaterasuSunRhythm(context),
        27 => new AmaterasuConcealment(context),
        28 => new AmaterasuFireworks(context),
        29 => new AmaterasuBreakthrough(context),
        30 => new AsclepiusCriticalTreatment(context),
        31 => new AsclepiusManaBreathing(context),
        32 => new AsclepiusDivineMedicine(context),
        51 => new PygmalionWeaklingContempt(context),
        52 => new PygmalionSlowAndSteady(context),
        53 => new PygmalionSharpThorns(context),
        54 => new PygmalionLoveGodBlessing(context),
        // ── 노르드 신규 (프레이아·로키·스카디) ──
        281 => new FreyaPhytoncide(context),
        70 => new FreyaMedicine(context),
        71 => new FreyaLeadership(context),
        72 => new FreyaWarChief(context),
        282 => new LokiFenrir(context),
        73 => new LokiSummoner(context),
        283 => new SkadiFrostWarrior(context),
        74 => new CryoMastery(context),
        75 => new SkadiCryoAffinity(context),
        76 => new SkadiElementalist(context),
        // ── 쿠베라·바루나·오르페우스·스사노오 ──
        265 => new KuberaEarthLaw(context),
        77 => new KuberaGeoAffinity(context),
        78 => new KuberaRooting(context),
        79 => new KuberaMerchant(context),
        266 => new VarunaOceanVerdict(context),
        80 => new VarunaWaterHeaven(context),
        225 => new OrpheusCheatedDeath(context),
        81 => new OrpheusBard(context),
        242 => new SusanooRaijin(context),
        82 => new SusanooWaveCut(context),
        55 => new InheritedAsclepiusNashorsTooth(context),
        (>= 1000 and <= 1003) or (>= 1008 and <= 1015) => new EnemyCommonPassive(context, codeId),
        241 => new TsukuyomiMoonReckoning(context),
        33 => new TsukuyomiSpellShield(context),
        34 => new TsukuyomiFickle(context),
        1016 => new TsukuyomiCurse(context),
        35 => new TsukuyomiWidenWound(context),
        36 => new TsukuyomiPainfulWound(context),
        37 => new TsukuyomiCycle(context),
        184 => new TsukuyomiChainLightning(context),
        38 => new TsukuyomiFullMoon(context),
        280 => new SurtrTwilight(context),
        39 => new SurtrGiant(context),
        185 => new SurtrFireMastery(context),
        40 => new SurtrCelestialBody(context),
        41 => new SurtrFighter(context),
        42 => new SurtrClutchPlayer(context),
        43 => new SurtrGodslayer(context),
        (>= 1100 and <= 1107) or (>= 1112 and <= 1115) => new ColosseumPassive(
          context,
          codeId,
          ColosseumCodeNames.Passive(codeId)),
        >= 1300 and <= 1308 => new AztecPassive(
          context,
          codeId,
          AztecCodeNames.Passive(codeId)),
        // ── 이집트 (호루스·아누비스·바스테트·세트·토트·이시스) ──
        320 => new HorusSkyFalcon(context),
        321 => new AnubisSoulHarvest(context),
        322 => new BastetAirborneHunter(context),
        86 => new EgyptianPiercingShot(context),
        87 => new AnubisSpearGuard(context),
        88 => new AnubisReaper(context),
        89 => new BastetMasterThief(context),
        90 => new BastetLightArmament(context),
        91 => new BastetSelfish(context),
        323 => new SetBloodOfTheDesert(context),
        324 => new ThothToughnessScholar(context),
        92 => new ThothIllusionist(context),
        93 => new ThothHiddenTruth(context),
        325 => new IsisDesertRadiance(context),
        94 => new IsisElement(context),
        // ── 아스완 (종말의 사도 · 사령 · 오시리스 · 아문·라) ──
        1400 => new AswanPyreJudgment(context, 0.10f),
        1401 => new AswanPyreJudgment(context, 0.20f),
        1402 => new AswanPyreSentence(context),
        1403 => new AswanNetherReturn(context),
        1404 => new OsirisImperfectResurrection(context),
        1405 => new AmunRaAscension(context),
        1406 => new AswanHolyBlade(context),
        1407 => new AswanCoarseSkin(context),
        1408 => new AmunRaTranscendence(context),
        1409 => new AmunRaSoulDrain(context),
        1410 => new AmunRaHellfire(context),
        1411 => new AswanDeathChant(context),
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
        203 => new SabahDebuffHunter(context),
        95 => new SabahDexterity(context),
        226 => new IcariaRecklessChallenge(context),
        96 => new IcariaMentalStrength(context),
        97 => new IcariaPyroAffinity(context),
        98 => new SabahAssassin(context),
        // ── 마리 ──
        360 => new MarieCriticalCommand(context),
        99 => new MarieNobleBloodline(context),
        100 => new MarieOfficer(context),
        101 => new MarieArcDeTriomphe(context),
        // ── 로마 (레기온) ──
        1218 => new NightRaid(context),
        83 => new Charisma(context),
        84 => new Intuition(context),
        85 => new Tactician(context),
        1219 => new MindsEye(context),
        // ── 로마 3인 — 레기온 패시브 구현을 공유한다 ──
        300 or 301 or 302 => new LegionPassive(
          context,
          codeId,
          LegionCodeNames.Passive(codeId)),
        >= 1200 and <= 1217 => new LegionPassive(
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
        21 => new a005_NAtlanta(context), // 아탈란테 일반공격
        20 => new a004_NPygmalion(context), // 피그말리온 일반공격
        1 => new SeiRadiantBolt(context), // 세이 일반공격
        900 => new MagicBolt(context),
        901 => new Shoot(context),
        60 => new Sachi(context),
        2 => new ShiVoidSlash(context), // 시 일반공격
        61 => new YamaNormalAttack(context),
        62 => new AgniNormalAttack(context),
        63 => new IndraNormalAttack(context),
        64 => new VayuNormalAttack(context),
        81 => new FreyaNormalAttack(context),
        82 => new LokiNormalAttack(context),
        83 => new SkadiNormalAttack(context),
        65 => new KuberaNormalAttack(context),
        66 => new VarunaNormalAttack(context),
        42 => new SusanooNormalAttack(context),
        25 => new OrpheusNormalAttack(context),
        140 => new QuetzalcoatlBall(context),
        22 => new OrionNormalAttack(context),
        23 => new TheseusNormalAttack(context),
        24 => new AsclepiusNormalAttack(context),
        40 => new AmaterasuNormalAttack(context),
        41 => new TsukuyomiLightningBolt(context),
        80 => new SurtrScorchingSlash(context),
        >= 1000 and <= 1007 => new EnemyArchetypeNormal(
          context,
          (EnemyArchetypeNormalStyle)(codeId - 1000)),
        // ── 로마 3인 — 레기온 구현을 공유하되 제 분기를 갖는다 ──
        100 => new LegionNormal(context, LegionNormalStyle.Agrippa),
        101 => new LegionNormal(context, LegionNormalStyle.Octavia),
        102 => new LegionNormal(context, LegionNormalStyle.Caesar),
        >= 1200 and <= 1211 => new LegionNormal(
          context,
          (LegionNormalStyle)(codeId - 1200)),
        >= 1300 and <= 1306 => new AztecNormal(
          context,
          (AztecNormalStyle)(codeId - 1300)),
        120 => new HorusNormalAttack(context),
        121 => new AnubisNormalAttack(context),
        122 => new BastetNormalAttack(context),
        123 => new SetNormalAttack(context),
        124 => new ThothNormalAttack(context),
        125 => new IsisNormalAttack(context),
        // ── 아스완 ──
        1400 => new AswanPaladinSlash(context, 80),    // 파멸의 성기사
        1401 => new AswanPaladinSlash(context, 100),   // 종말의 성기사
        1402 => new AswanInquisitorPrayer(context, 60),
        1403 => new AswanInquisitorPrayer(context, 80),
        1404 => new AswanWraithClaw(context),
        1405 => new AswanFallenPriestRite(context),
        1406 => new OsirisEarthRequiem(context),
        1407 => new AmunRaBladeVolley(context),
        902 => new AmunRaBladeVolley(context, alwaysEmpowered: true),
        3 => new SabahLightningSlash(context),
        26 => new IcariaRecklessThrust(context),
        160 => new MariePistolShot(context),
        _ => new NormalAttack(context), // 기본 일반공격 (임시, 나중에 각 유닛별로 교체 예정)
      };
    }

    public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
    {
      return codeId switch
      {
        21 => new a005_U_Moonfall(context), // 아탈란테 궁극기
        60 => new a012_U_Soma(context), // 찬드라 궁극기
        20 => new a004_U_LovesPrize(context), // 피그말리온 궁극기
        2 => new a002_U_FinalBell(context), // 시 궁극기
        1 => new SeiSanctuary(context), // 세이 궁극기
        61 => new YamaFinalArrival(context),
        62 => new AgniWhiteFlame(context),
        63 => new IndraThunderbolt(context),
        64 => new VayuSouthWind(context),
        81 => new FreyaHarvest(context),
        82 => new LokiBaldrSlayer(context),
        83 => new SkadiIcicleSpike(context),
        65 => new KuberaGoldenQuake(context),
        66 => new VarunaMakara(context),
        42 => new SusanooAmenoMurakumo(context),
        25 => new OrpheusLament(context),
        140 => new QuetzalcoatlYorisUltimate(context),
        22 => new OrionHeavyBlow(context),
        23 => new TheseusRecoveryStrike(context),
        24 => new AsclepiusFlask(context),
        40 => new AmaterasuPortalBarrage(context),
        41 => new TsukuyomiMoonThunder(context),
        80 => new SurtrRagnarok(context),
        (>= 1100 and <= 1107) or 1109 => new ColosseumUltimate(
          context,
          (ColosseumUltimateStyle)(codeId - 1100)),
        >= 1300 and <= 1306 => new AztecUltimate(
          context,
          (AztecUltimateStyle)(codeId - 1300)),
        // ── 로마 3인 — 레기온 구현을 공유하되 제 분기를 갖는다 ──
        100 => new LegionUltimate(context, LegionUltimateStyle.Agrippa),
        101 => new LegionUltimate(context, LegionUltimateStyle.Octavia),
        102 => new LegionUltimate(context, LegionUltimateStyle.Caesar),
        >= 1200 and <= 1211 => new LegionUltimate(
          context,
          (LegionUltimateStyle)(codeId - 1200)),
        120 => new HorusWadjet(context),
        121 => new AnubisStoneSpear(context),
        122 => new BastetApexExecution(context),
        123 => new SetBurningSlash(context),
        124 => new ThothEyeOfWisdom(context),
        125 => new IsisDesertDeluge(context),
        // ── 아스완 ──
        1400 => new AswanPaladinJudgment(context),     // 파멸·종말의 성기사 공용
        1401 => new AswanInquisitorPyre(context, 60),
        1402 => new AswanInquisitorPyre(context, 80),
        1403 => new AswanWraithSummon(context),
        1404 => new AswanFallenPriestWard(context),
        1405 => new OsirisRebirthFlood(context),
        1406 => new AmunRaSolarJudgment(context),
        3 => new SabahAzrael(context),
        26 => new IcariaChallengeTheSky(context),
        160 => new MarieSongOfRevolution(context),
        900 => new EnemyTestUltimate(context), // 적 전용 테스트 궁극기
        _ => null,
      };
    }
  }
}
