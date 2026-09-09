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
        186 => new HydroMastery(context),
        187 => new AnemoMastery(context),
        188 => new DendroMastery(context),
        189 => new GeoMastery(context),
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
        204 => new GaudiSagradaFamilia(context), // 가우디 초기 패시브 — 사그리다 파밀리아
        205 => new LightGreatFlight(context), // 라이트 초기 패시브 — 위대한 비행
        206 => new NicoleElectricField(context), // 니콜 초기 패시브 — 일렉트릭 필드
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
        6 => new IronWallPassive(context),      // 공용 — 일반행동마다 CON +2
        14 => new ChandraMoonlight(context),
        340 => new QuetzalcoatlBounty(context),
        15 => new ScholarshipPassive(context),
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
        283 => new SkadiNorthernGuardian(context),
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
        1016 => new CursePassive(context),   // 공용 — 부여 지속피해 +20%
        35 => new TsukuyomiWidenWound(context),
        36 => new TsukuyomiPainfulWound(context),
        37 => new TsukuyomiCycle(context),
        184 => new TsukuyomiChainLightning(context),
        38 => new TsukuyomiFullMoon(context),
        280 => new SurtrTwilight(context),
        39 => new GiantPassive(context),        // 공용 — STR +5%
        185 => new SurtrFireMastery(context),
        40 => new SurtrCelestialBody(context),
        41 => new LifestealFighter(context, 0.08f, "투사", VoidBeastCodeIds.AllDayLong,
            BaseEnums.CodeGrade.Normal),
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
        102 => new AnubisRaiseTheDead(context),
        103 => new PerfectRecovery(context),   // 공용 — 기사회생의 금색 상위
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
        // ── 범용 거인 ──
        1420 => new GenericGiantBulwark(context, 0.10f),
        1421 => new GenericGiantBulwark(context, 0.20f),
        1422 => new FrostGiantCore(context),
        1423 => new GiantShatter(context),
        1424 => new GiantStunningBlow(context),
        1425 => new GiantCoupDeGrace(context),
        1426 => new GiantAdaptability(context),
        // ── 범용 씨앗 ──
        1430 => new GenericSeedFormation(context, 1),
        1431 => new GenericSeedFormation(context, 2),
        1432 => new FrostSeedCore(context),
        1433 => new SeedSelfDestruct(context, enhanced: false),
        1434 => new SeedSelfDestruct(context, enhanced: true),
        1435 => new SeedOverload(context),
        1436 => new SeedFrostRay(context),
        // ── 공허의 프리즘 ──
        1500 => new VoidPrismCore(context),
        1501 => new VoidPrismResonance(context),
        1502 => new VoidPrismSplit(context),
        1503 => new VoidPrismDeathEcho(context),
        1504 => new VoidPrismDestructionRay(context),
        // ── 공허의 괴조 ──
        1510 => new VoidMonstrousBirdRebirth(context),
        1511 => new VoidMonstrousBirdAcceleration(context),
        1512 => new VoidMonstrousBirdSharpBeak(context),

        // ── 공허의 멧돼지(야수) ──
        1520 => new VoidBeastBruteForce(context),
        1521 => new VoidBeastHardSkin(context, VoidBeastStatusIds.StoneSkin, "void_beast_stone_skin",
            "바위피부", 10, VoidBeastCodeIds.SteelSkin, BaseEnums.CodeGrade.Normal),
        1522 => new VoidBeastArrogance(context),
        1523 => new VoidBeastHardSkin(context, VoidBeastStatusIds.SteelSkin, "void_beast_steel_skin",
            "강철피부", 20, 0, BaseEnums.CodeGrade.Enhanced),
        1524 => new LifestealFighter(context, 0.12f, "하루종일도 할 수 있어", 0,
            BaseEnums.CodeGrade.Enhanced),

        // ── 공허의 늑대 ──
        1530 => new VoidWolfBloodScent(context),
        1531 => new VoidWolfBloodthirst(context, VoidWolfStatusIds.Vampire, "void_wolf_vampire",
            "흡혈귀", 0.25f, VoidWolfCodeIds.DarkLord, BaseEnums.CodeGrade.Normal),
        1532 => new VoidWolfBloodthirst(context, VoidWolfStatusIds.DarkLord, "void_wolf_dark_lord",
            "어둠의 군주", 0.50f, 0, BaseEnums.CodeGrade.Enhanced),

        // ── 공허의 사슴 ──
        1540 => new VoidDeerResonance(context),
        1541 => new VoidAlphaSpecimen(context, VoidSharedStatusIds.AlphaSpecimen, "void_alpha_specimen",
            "알파 개체", 2, VoidSharedCodeIds.Perfection, BaseEnums.CodeGrade.Normal),
        1542 => new VoidStorm(context),
        1543 => new VoidUltimateBarrier(context),
        1544 => new VoidVanguardAura(context),
        1545 => new VoidIntimidation(context),
        1546 => new VoidAlphaSpecimen(context, VoidSharedStatusIds.Perfection, "void_perfection",
            "완전함", 3, 0, BaseEnums.CodeGrade.Enhanced),

        // ── 공허의 기사 ──
        1550 => new VoidKnightArmor(context),

        // ── 공허의 사수 ──
        1560 => new VoidMarksmanSightLine(context),

        // ── 공허의 선봉대 ──
        1570 => new VoidVanguardOath(context),
        1571 => new VoidPhalanxFormation(context),

        // ── 공허의 분쇄자 ──
        1580 => new VoidCrusherMass(context),
        1581 => new VoidCrusherBody(context, VoidCrusherStatusIds.UltimateBody, "void_ultimate_body",
            "궁극의 신체", 2.5f, VoidCrusherCodeIds.TranscendentBody, BaseEnums.CodeGrade.Normal),
        1582 => new VoidCrusherBody(context, VoidCrusherStatusIds.TranscendentBody, "void_transcendent_body",
            "천무지체", 5f, 0, BaseEnums.CodeGrade.Enhanced),
        1583 => new VoidAdaptiveArmor(context),

        // ── 공허의 용 ──
        1590 => new VoidDragonOrigin(context),
        1591 => new VoidAether(context),
        1592 => new VoidPerfectResurrection(context),
        1593 => new VoidElementalMastery(context),
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
        21 => new a005_NAtlanta(context), // 아탈란테 일반행동
        20 => new a004_NPygmalion(context), // 피그말리온 일반행동
        1 => new SeiRadiantBolt(context), // 세이 일반행동
        4 => new GaudiNormalAttack(context), // 가우디 일반/강화 일반행동
        5 => new LightNormalAttack(context), // 라이트 일반행동/플라이어 소환
        6 => new NicoleArcShot(context), // 니콜 일반행동
        500 => new FlyerNormalAttack(context), // 소환수 — 플라이어 일반행동
        501 => new FenrirBite(context), // 소환수 — 펜리르 일반행동
        900 => new MagicBolt(context),
        901 => new Shoot(context),
        60 => new Sachi(context),
        2 => new ShiVoidSlash(context), // 시 일반행동
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
        // ── 범용 거인 ──
        1420 => new GenericGiantNormalAttack(context, 80),
        1421 => new GenericGiantNormalAttack(context, 100),
        // ── 범용 씨앗 ──
        1430 => new GenericSeedVolley(context),
        // ── 공허의 프리즘 ──
        1500 => new VoidPrismNormal(context),
        1510 => new VoidMonstrousBirdNormal(context),
        1520 => new VoidBeastNormal(context),
        1530 => new VoidWolfNormal(context),
        1550 => new VoidKnightNormal(context),
        1560 => new VoidMarksmanNormal(context),
        1570 => new VoidVanguardNormal(context),
        1580 => new VoidCrusherNormal(context),
        1540 => new VoidDeerNormal(context, BaseEnums.UnitElement.None),
        1541 => new VoidDeerNormal(context, BaseEnums.UnitElement.Pyro),
        1542 => new VoidDeerNormal(context, BaseEnums.UnitElement.Hydro),
        1543 => new VoidDeerNormal(context, BaseEnums.UnitElement.Anemo),
        1544 => new VoidDeerNormal(context, BaseEnums.UnitElement.Electro),
        1545 => new VoidDeerNormal(context, BaseEnums.UnitElement.Dendro),
        1546 => new VoidDeerNormal(context, BaseEnums.UnitElement.Cryo),
        1547 => new VoidDeerNormal(context, BaseEnums.UnitElement.Geo),
        1590 => new VoidDragonNormal(context, BaseEnums.UnitElement.None),
        1591 => new VoidDragonNormal(context, BaseEnums.UnitElement.Pyro),
        1592 => new VoidDragonNormal(context, BaseEnums.UnitElement.Hydro),
        1593 => new VoidDragonNormal(context, BaseEnums.UnitElement.Anemo),
        1594 => new VoidDragonNormal(context, BaseEnums.UnitElement.Electro),
        1595 => new VoidDragonNormal(context, BaseEnums.UnitElement.Dendro),
        1596 => new VoidDragonNormal(context, BaseEnums.UnitElement.Cryo),
        1597 => new VoidDragonNormal(context, BaseEnums.UnitElement.Geo),
        902 => new AmunRaBladeVolley(context, alwaysEmpowered: true),
        3 => new SabahLightningSlash(context),
        26 => new IcariaRecklessThrust(context),
        160 => new MariePistolShot(context),
        _ => new NormalAttack(context), // 기본 일반행동 (임시, 나중에 각 유닛별로 교체 예정)
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
        4 => new GaudiImmortalLegacy(context), // 가우디 궁극기
        5 => new LightFlyingDream(context), // 라이트 궁극기
        6 => new NicoleOvercharge(context), // 니콜 궁극기
        500 => new FlyerSkyfall(context), // 소환수 — 플라이어 궁극기
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
        // ── 범용 거인 ──
        1420 => new GenericGiantHammerfall(context, 80, 130),
        1421 => new GenericGiantHammerfall(context, 100, 150),
        // ── 범용 씨앗 ──
        1430 => new GenericSeedOverdrive(context),
        1431 => new FrostSeedBeam(context),
        // ── 공허의 프리즘. 기본형은 부착 없음, 팔레트 변형만 해당 원소를 부착한다. ──
        1500 => new VoidPrismUltimate(context, BaseEnums.UnitElement.None),
        1501 => new VoidPrismUltimate(context, BaseEnums.UnitElement.Pyro),
        1502 => new VoidPrismUltimate(context, BaseEnums.UnitElement.Hydro),
        1503 => new VoidPrismUltimate(context, BaseEnums.UnitElement.Anemo),
        1504 => new VoidPrismUltimate(context, BaseEnums.UnitElement.Electro),
        1505 => new VoidPrismUltimate(context, BaseEnums.UnitElement.Dendro),
        1506 => new VoidPrismUltimate(context, BaseEnums.UnitElement.Cryo),
        1507 => new VoidPrismUltimate(context, BaseEnums.UnitElement.Geo),
        1510 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.None),
        1511 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.Pyro),
        1512 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.Hydro),
        1513 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.Anemo),
        1514 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.Electro),
        1515 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.Dendro),
        1516 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.Cryo),
        1517 => new VoidMonstrousBirdUltimate(context, BaseEnums.UnitElement.Geo),
        1520 => new VoidBeastUltimate(context, BaseEnums.UnitElement.None),
        1521 => new VoidBeastUltimate(context, BaseEnums.UnitElement.Pyro),
        1522 => new VoidBeastUltimate(context, BaseEnums.UnitElement.Hydro),
        1523 => new VoidBeastUltimate(context, BaseEnums.UnitElement.Anemo),
        1524 => new VoidBeastUltimate(context, BaseEnums.UnitElement.Electro),
        1525 => new VoidBeastUltimate(context, BaseEnums.UnitElement.Dendro),
        1526 => new VoidBeastUltimate(context, BaseEnums.UnitElement.Cryo),
        1527 => new VoidBeastUltimate(context, BaseEnums.UnitElement.Geo),
        1530 => new VoidWolfUltimate(context, BaseEnums.UnitElement.None),
        1531 => new VoidWolfUltimate(context, BaseEnums.UnitElement.Pyro),
        1532 => new VoidWolfUltimate(context, BaseEnums.UnitElement.Hydro),
        1533 => new VoidWolfUltimate(context, BaseEnums.UnitElement.Anemo),
        1534 => new VoidWolfUltimate(context, BaseEnums.UnitElement.Electro),
        1535 => new VoidWolfUltimate(context, BaseEnums.UnitElement.Dendro),
        1536 => new VoidWolfUltimate(context, BaseEnums.UnitElement.Cryo),
        1537 => new VoidWolfUltimate(context, BaseEnums.UnitElement.Geo),
        1540 => new VoidDeerUltimate(context, 0),
        1541 => new VoidDeerUltimate(context, 1),
        1542 => new VoidDeerUltimate(context, 2),
        1543 => new VoidDeerUltimate(context, 3),
        1544 => new VoidDeerUltimate(context, 4),
        1545 => new VoidDeerUltimate(context, 5),
        1546 => new VoidDeerUltimate(context, 6),
        1547 => new VoidDeerUltimate(context, 7),
        1550 => new VoidKnightUltimate(context, BaseEnums.UnitElement.None),
        1551 => new VoidKnightUltimate(context, BaseEnums.UnitElement.Pyro),
        1552 => new VoidKnightUltimate(context, BaseEnums.UnitElement.Hydro),
        1553 => new VoidKnightUltimate(context, BaseEnums.UnitElement.Anemo),
        1554 => new VoidKnightUltimate(context, BaseEnums.UnitElement.Electro),
        1555 => new VoidKnightUltimate(context, BaseEnums.UnitElement.Dendro),
        1556 => new VoidKnightUltimate(context, BaseEnums.UnitElement.Cryo),
        1557 => new VoidKnightUltimate(context, BaseEnums.UnitElement.Geo),
        1560 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.None),
        1561 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.Pyro),
        1562 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.Hydro),
        1563 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.Anemo),
        1564 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.Electro),
        1565 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.Dendro),
        1566 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.Cryo),
        1567 => new VoidMarksmanUltimate(context, BaseEnums.UnitElement.Geo),
        1570 => new VoidVanguardUltimate(context),
        1580 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.None),
        1581 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.Pyro),
        1582 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.Hydro),
        1583 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.Anemo),
        1584 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.Electro),
        1585 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.Dendro),
        1586 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.Cryo),
        1587 => new VoidCrusherUltimate(context, BaseEnums.UnitElement.Geo),
        1590 => new VoidDragonUltimate(context, BaseEnums.UnitElement.None),
        1591 => new VoidDragonUltimate(context, BaseEnums.UnitElement.Pyro),
        1592 => new VoidDragonUltimate(context, BaseEnums.UnitElement.Hydro),
        1593 => new VoidDragonUltimate(context, BaseEnums.UnitElement.Anemo),
        1594 => new VoidDragonUltimate(context, BaseEnums.UnitElement.Electro),
        1595 => new VoidDragonUltimate(context, BaseEnums.UnitElement.Dendro),
        1596 => new VoidDragonUltimate(context, BaseEnums.UnitElement.Cryo),
        1597 => new VoidDragonUltimate(context, BaseEnums.UnitElement.Geo),
        3 => new SabahAzrael(context),
        26 => new IcariaChallengeTheSky(context),
        160 => new MarieSongOfRevolution(context),
        900 => new EnemyTestUltimate(context), // 적 전용 테스트 궁극기
        _ => null,
      };
    }
  }
}
