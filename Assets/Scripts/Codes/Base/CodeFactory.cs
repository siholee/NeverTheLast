using BaseClasses;
using Codes.Normal;
using Codes.Passive;
using Codes.Special;
using Codes.Ultimate;

namespace Codes.Base
{
  /// <summary>
  /// 숫자 ID를 코드 클래스로 잇는다. 슬롯마다 네임스페이스가 따로라 특성 201과
  /// 궁극기 201은 무관하다 — 스위치도 슬롯마다 하나씩이다.
  ///
  /// 2026-09 전면 개편으로 옛 캐릭터 · 적의 코드는 전부 걷어냈다. 캐릭터를 하나씩
  /// 다시 만들면서 여기에 한 줄씩 되돌려 넣는다. ID 규칙은 Detail_11 §1.5를 따른다 —
  /// 아군 일반 · 궁극기는 유닛 ID와 같고, 고유 특성은 유닛 ID + 200, 적 공용은 1000번대다.
  /// </summary>
  public static class CodeFactory
  {
    /// <summary>
    /// 만든 코드에 요청한 ID를 새긴다. 코드 클래스는 제 ID를 모르므로
    /// <b>ID는 여기서만 정확히 알 수 있다.</b> 화면이 `20_codes.yaml`의 설명을 찾을 때 쓴다.
    /// </summary>
    private static T Tag<T>(T code, int codeId) where T : Code
    {
      if (code != null) code.CatalogId = codeId;
      return code;
    }

    public static PassiveCode CreatePassiveCode(int codeId, PassiveCodeContext context)
      => Tag(BuildPassive(codeId, context), codeId);

    private static PassiveCode BuildPassive(int codeId, PassiveCodeContext context)
    {
      return codeId switch
      {
        201 => new SeiBigBang(context),              // 세이 — 빅뱅
        202 => new ShiFinalVerse(context),            // 시 — 멸망의 시
        203 => new SabahDebuffHunter(context),         // 사바흐 — 빈틈 포착
        207 => new JeanIronSanction(context),          // 잔 — 철권제재
        220 => new PygmalionGalatea(context),         // 피그말리온 — 갈라테아
        244 => new HimikoBlaze(context),              // 히미코 — 맹화
        260 => new ChandraNishakara(context),         // 찬드라 — 니샤카라
        261 => new YamaDeathVerdict(context),         // 야마 — 죽음의 선고
        262 => new AgniBurnToTheEnd(context),         // 아그니 — 끝까지 태워 주지
        263 => new IndraDevaKing(context),            // 인드라 — 데바의 왕
        264 => new VayuTailwind(context),             // 바유 — 바람이 남긴 길
        265 => new KuberaPatronage(context),          // 쿠베라 — 재보천의 후원
        266 => new VarunaLawLevee(context),           // 바루나 — 법이 만든 제방
        267 => new SuryaBeforeSunset(context),        // 수리야 — 해가 지기 전에
        321 => new AnubisSoulHarvest(context),        // 아누비스 — 영혼 수확
        380 => new UchiTaoist(context),               // 우치 — 도사란 무엇인가
        322 => new BastetReliableGuard(context),      // 바스테트 — 든든한 경호대가 있다고!
        // 보상 코드 500~999는 표(20_codes.yaml) 기반 팩토리가 받는다(Detail_17).
        >= RewardCodeCatalog.MinId and <= RewardCodeCatalog.MaxId
          => Codes.Passive.Reward.RewardCodeFactory.Create(codeId, context),
        _ => null,
      };
    }

    public static NormalCode CreateNormalCode(int codeId, NormalCodeContext context)
      => Tag(BuildNormal(codeId, context), codeId);

    private static NormalCode BuildNormal(int codeId, NormalCodeContext context)
    {
      return codeId switch
      {
        1 => new SeiGroundLeveling(context),         // 세이 — 땅고르기
        2 => new ShiGhostSlash(context),              // 시 — 귀참
        3 => new SabahLightningSlash(context),        // 사바흐 — 검격 / 번개 베기
        7 => new JeanHolyStrike(context),              // 잔 — 신성한 일격
        20 => new PygmalionEmber(context),            // 피그말리온 — 불씨
        44 => new HimikoEmber(context),               // 히미코 — 불씨
        60 => new ChandraSachi(context),              // 찬드라 — 샤친
        61 => new YamaRollOfDeathLightning(context),  // 야마 — 명부의 낙뢰
        62 => new AgniEmberFist(context),             // 아그니 — 불씨를 다지는 주먹
        63 => new IndraThunderbolt(context),          // 인드라 — 뇌정
        64 => new VayuGust(context),                  // 바유 — 돌풍
        65 => new KuberaCoinToss(context),            // 쿠베라 — 금화 투척
        66 => new VarunaSacredRope(context),          // 바루나 — 금줄
        67 => new SuryaNameBullet(context),           // 수리야 — 이름을 부르는 탄환
        121 => new AnubisHeartScale(context),         // 아누비스 — 심장의 저울
        180 => new UchiCloneArt(context),             // 우치 — 분신술
        UchiCloneStrike.CodeId => new UchiCloneStrike(context), // 소환수 — 분신 일격(500번대)
        122 => new BastetCatPunch(context),            // 바스테트 — 냥냥펀치
        EnemyBasicNormalIds.Charge => new EnemyCharge(context),
        EnemyBasicNormalIds.Shot => new EnemyShot(context),
        _ => null,
      };
    }

    public static SpecialCode CreateSpecialCode(int codeId, SpecialCodeContext context)
      => Tag(BuildSpecial(codeId, context), codeId);

    private static SpecialCode BuildSpecial(int codeId, SpecialCodeContext context)
    {
      return codeId switch
      {
        60 => new ChandraChangya(context),            // 찬드라 — 창야
        61 => new YamaDeathRound(context),            // 야마 — 죽음의 순회
        62 => new AgniFirstToBurn(context),           // 아그니 — 제일 먼저 태울 곳
        63 => new IndraVajra(context),                // 인드라 — 바즈라
        64 => new VayuEyeOfTyphoon(context),          // 바유 — 태풍의 눈
        65 => new KuberaGoldRush(context),            // 쿠베라 — 골드 러쉬
        66 => new VarunaOverflowingJudgment(context), // 바루나 — 넘치는 판결
        _ => null,
      };
    }

    public static UltimateCode CreateUltimateCode(int codeId, UltimateCodeContext context)
      => Tag(BuildUltimate(codeId, context), codeId);

    private static UltimateCode BuildUltimate(int codeId, UltimateCodeContext context)
    {
      return codeId switch
      {
        1 => new SeiSongOfGenesis(context),          // 세이 — 창세의 노래
        2 => new ShiFinalBell(context),               // 시 — 만종
        3 => new SabahAzrael(context),                // 사바흐 — 아즈라엘
        7 => new JeanTensionRising(context),          // 잔 — 텐션 오르는데!
        20 => new PygmalionSharpThorns(context),      // 피그말리온 — 날카로운 가시
        44 => new HimikoBlazeFocus(context),          // 히미코 — 맹화 집중
        60 => new ChandraSoma(context),               // 찬드라 — 소마
        61 => new YamaLastArrival(context),           // 야마 — 언젠가 다다를 마지막
        62 => new AgniRaiseTheStakes(context),        // 아그니 — 내가 판을 키워 주지
        63 => new IndraGathering(context),            // 인드라 — 팔방의 소집
        64 => new VayuStorm(context),                 // 바유 — 만류의 회랑
        65 => new KuberaTreasury(context),            // 쿠베라 — 보고 개방
        66 => new VarunaUnderSameLaw(context),        // 바루나 — 동일한 법 아래
        67 => new SuryaNoEscape(context),             // 수리야 — 도망칠 곳은 없어
        121 => new AnubisBookOfTheDead(context),      // 아누비스 — 사자의 서
        180 => new UchiManchunhwau(context),          // 우치 — 만천화우
        122 => new BastetOneMoreHit(context),          // 바스테트 — 미운 놈 한 대 더 때리기
        EnemyBasicUltimateIds.Smash => new EnemySmash(context),
        EnemyBasicUltimateIds.Volley => new EnemyVolley(context),
        EnemyBasicUltimateIds.GroundBreaker => new EnemyGroundBreaker(context),
        EnemyBasicUltimateIds.VoidBreath => new EnemyVoidBreath(context),
        _ => null,
      };
    }
  }
}
