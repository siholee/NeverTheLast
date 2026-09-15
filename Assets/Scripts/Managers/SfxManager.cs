using System;
using System.Collections.Generic;
using CGT.Pooling;
using Effects;
using Entities;
using BaseClasses;
using UnityEngine;
using Effects.Projectiles;

namespace Managers
{
  [System.Serializable]
  public class ProjectilePrefabEntry
  {
    public string key;
    public HS_Poolable prefab;

    [Tooltip("이 투사체만 따로 조절할 배율. 0 이하이면 전역 배율만 적용한다.")]
    public float scaleOverride = 0f;
  }

  public class SfxManager : MonoBehaviour
  {
    // 파티클 크기는 스폰 시점의 루트 스케일로 맞춘다.
    // (모든 파티클의 scalingMode가 Hierarchy(0)라 루트 스케일이 그대로 반영된다.)
    //
    // 배율이 두 갈래인 이유: 프로젝트가 손본 프리팹(Prefabs/SFX)은 파티클 startSize가 6인데,
    // Hovl 원본(Resources/SFX/Projectile)은 0.6으로 **정확히 10배 작다.**
    // 같은 배율을 쓰면 원본 프리팹은 전장(가로 약 68유닛)에서 점처럼 보여 사실상 안 보인다.
    // 두 값은 화면상 크기가 비슷해지도록(주 파티클 약 4.2유닛) 맞춰 두었다.

    /// <summary>프로젝트가 손본 프리팹(startSize 6 기준)의 배율.</summary>
    private const float DefaultProjectileScale = 0.7f;

    /// <summary>Hovl 원본 프리팹(startSize 0.6 기준)의 배율.</summary>
    private const float ElementalProjectileScale = 6f;

    // 동시에 살아있는 투사체 상한. 전체 공격/광역 스킬에서 화면이 난잡해지는 것을 막는다.
    private const int MaxConcurrentProjectiles = 12;

    /// <summary>
    /// 투사체 배율. 카드 한 변의 1/3쯤이라 카드를 가리지 않으면서 또렷하게 보인다.
    /// </summary>
    private static float ProjectileScale => Cell.CardSize * 0.34f;

    [SerializeField] private HS_CustomPoolableManager poolableManager;
    [SerializeField] private List<ProjectilePrefabEntry> projectilePrefabEntries;

    [Tooltip("모든 투사체에 적용되는 전역 크기 배율. 0 이하이면 기본값을 사용한다.")]
    [SerializeField] private float projectileScale = DefaultProjectileScale;

    public Dictionary<string, HS_Poolable> ProjectilePrefabs;

    private readonly Dictionary<string, float> _scaleOverrides = new();
    private readonly List<HS_Poolable> _liveProjectiles = new();

    private void Awake()
    {
      poolableManager = HS_CustomPoolableManager.EnsureExists();
      ProjectilePrefabs = new Dictionary<string, HS_Poolable>();
      foreach (var entry in projectilePrefabEntries)
      {
        if (!ProjectilePrefabs.ContainsKey(entry.key))
          ProjectilePrefabs.Add(entry.key, entry.prefab);

        if (entry.prefab != null && entry.scaleOverride > 0f)
          _scaleOverrides[entry.prefab.name] = entry.scaleOverride;
      }
    }

    /// <summary>베기 선이 나타난 뒤 실제 피해가 들어가기까지의 짧은 타격 지연.</summary>
    public const float MeleeImpactDelay = SlashEffect.ImpactDelay;

    /// <summary>
    /// 이 공격이 투사체 대신 대상 위 근접 연출을 쓸지 판정한다.
    ///
    /// 판정은 <see cref="AttackVisuals"/> 한 곳이 한다 — 무기 분류 태그가 있으면 그것을,
    /// 없으면 손에 든 무기를 본다. 예전에는 "검 숙련이면 베기, 로마·메히코면 베기"라는
    /// 두 줄짜리 예외만 있어서 창병도 둔기병도 똑같은 빛덩이를 쏘았다.
    /// </summary>
    public static bool ShouldPlayMelee(Unit attacker, DamageContext context)
    {
      if (attacker == null || context?.DamageTags == null) return false;
      return AttackVisuals.IsMelee(attacker, context, AttackVisuals.Classify(attacker, context));
    }

    /// <summary>
    /// 근접 공격이면 무기 분류에 맞는 연출을 재생하고 true를 반환한다. 공격 코드에서는
    /// true일 때 투사체를 만들지 않고 <see cref="MeleeImpactDelay"/> 뒤 피해를 적용하면 된다.
    /// </summary>
    public bool TryPlayMeleeAttack(Unit attacker, Unit target, DamageContext context)
    {
      if (target == null || !ShouldPlayMelee(attacker, context)) return false;

      if (context.TryMarkImpactVfx(target))
      {
        AttackVisualForm form = AttackVisuals.Classify(attacker, context);
        Color accent = AttackVisuals.AccentFor(attacker, context, form);

        switch (form)
        {
          case AttackVisualForm.Thrust:
            ThrustEffect.Play(attacker, target, accent);
            break;
          case AttackVisualForm.Impact:
            ImpactEffect.Play(attacker, target, accent);
            break;
          default:
            SlashEffect.Play(attacker, target, accent);
            break;
        }

        // 때리는 쪽도 움직인다. 맞는 카드만 흔들리면 누가 쳤는지가 화면에서 사라진다.
        attacker.currentCell?.PlayAttackReaction(1.15f);
        if (attacker.currentCell == null) attacker.SummonView?.PlayAttackReaction(1.15f);
      }
      return true;
    }

    /// <summary>즉시 피해형 스킬이 별도 발사 루틴을 거치지 않아도 근접 연출을 얻는다.</summary>
    public void PlayDamageImpact(Unit target, DamageContext context)
    {
      if (context?.Attacker == null || target == null) return;
      TryPlayMeleeAttack(context.Attacker, target, context);
    }

    /// <summary>에어본 상태가 유지되는 동안 대상 아래에 상승 파티클을 붙인다.</summary>
    public void PlayAirborne(Unit target) => AirborneEffect.Attach(target);

    /// <summary>투사체에 적용할 최종 스케일.</summary>
    private float GetProjectileScale(HS_Poolable prefab)
    {
      if (prefab != null && _scaleOverrides.TryGetValue(prefab.name, out float overrideScale))
        return overrideScale;

      return projectileScale > 0f ? projectileScale : DefaultProjectileScale;
    }

    /// <summary>
    /// 동시 투사체 수를 상한 이하로 유지한다. 이미 비활성화된(재생이 끝난) 것은 목록에서 정리하고,
    /// 그래도 넘치면 가장 오래된 것부터 회수한다.
    ///
    /// 회수는 반드시 RejoinPool()로 해야 한다. SetActive(false)만 하면 ObjectPool이
    /// 인스턴스를 되돌려받지 못해(Release 미호출) 풀이 계속 새 인스턴스를 만들게 된다.
    /// </summary>
    private void TrimLiveProjectiles()
    {
      _liveProjectiles.RemoveAll(p => p == null || !p.gameObject.activeInHierarchy);

      while (_liveProjectiles.Count >= MaxConcurrentProjectiles)
      {
        HS_Poolable oldest = _liveProjectiles[0];
        _liveProjectiles.RemoveAt(0);
        if (oldest != null && oldest.gameObject.activeInHierarchy)
          oldest.RejoinPool();
      }
    }

    /// <summary>
    /// 단일 투사체 발사 (기본 - 직선 경로)
    /// </summary>
    public void FireSingleProjectile(HS_Poolable prefab, Unit unitFrom, Unit unitTo, float duration)
    {
      FireSingleProjectile(prefab, unitFrom, unitTo, duration, ProjectilePathType.Linear, null);
    }
    
    /// <summary>
    /// 단일 투사체 발사 (경로 타입 지정)
    /// </summary>
    public void FireSingleProjectile(HS_Poolable prefab, Unit unitFrom, Unit unitTo, float duration, 
      ProjectilePathType pathType)
    {
      FireSingleProjectile(prefab, unitFrom, unitTo, duration, pathType, null);
    }
    
    /// <summary>
    /// 단일 투사체 발사 (경로 타입 및 파라미터 지정)
    /// </summary>
    public void FireSingleProjectile(HS_Poolable prefab, Unit unitFrom, Unit unitTo, float duration,
      ProjectilePathType pathType, ProjectilePathData pathData, Action onImpact = null,
      bool pastel = false)
    {
      // prefab은 더 이상 쓰지 않는다. 투사체는 CardProjectile이 코드로 그린다.
      // (호출부를 전부 고치지 않으려고 시그니처만 남겨 두었다.)
      if (unitFrom == null || unitTo == null) return;

      BaseEnums.UnitElement element = ElementalProjectiles.Parse(unitFrom.Element);
      Color color = pastel
        ? ElementalProjectiles.PastelColorFor(element)
        : ElementalProjectiles.ColorFor(element);
      CardProjectile.Fire(unitFrom, unitTo, color, duration,
        pathType, pathData ?? new ProjectilePathData(), ProjectileScale, onImpact);
    }

    /// <summary>
    /// 시전자의 원소에 맞는 투사체를 쏜다.
    ///
    /// 원소별로 프리팹이 다르고 원신 원소 색이 입혀진다.
    /// 해당 원소의 프리팹이 없으면 <paramref name="fallback"/>으로 물러난다.
    /// </summary>
    public void FireElementalProjectile(Unit unitFrom, Unit unitTo, float duration,
      ProjectilePathType pathType, ProjectilePathData pathData, HS_Poolable fallback = null,
      Action onImpact = null, DamageContext context = null)
    {
      if (unitFrom == null || unitTo == null) return;

      AttackVisualForm form = AttackVisuals.Classify(unitFrom, context);
      Color color = AttackVisuals.AccentFor(unitFrom, context, form);
      ProjectileVisualStyle style = AttackVisuals.StyleFor(form, context);
      CardProjectile.Fire(unitFrom, unitTo, color, duration,
        pathType, pathData ?? new ProjectilePathData(), ProjectileScale, onImpact, style);

      // 물리 원거리는 쏘는 반동이 있다. 활을 놓는 순간 카드가 조금 물러나야 던진 티가 난다.
      if (AttackVisuals.IsPhysical(form)) unitFrom.currentCell?.PlayAttackReaction(0.8f);
    }

    /// <summary>
    /// 허공의 한 점에서 대상에게 투사체를 쏜다.
    ///
    /// 시전자가 직접 쏘지 않는 연출(허공에 열린 차원문 등)에 쓴다.
    /// <paramref name="unitFrom"/>은 원소 색을 고르는 데만 쓰고, 궤적의 출발점은 <paramref name="origin"/>이다.
    /// </summary>
    public void FireProjectileFromPoint(Vector3 origin, Unit unitFrom, Unit unitTo, float duration,
      ProjectilePathType pathType, ProjectilePathData pathData, Action onImpact = null,
      DamageContext context = null)
    {
      if (unitFrom == null || unitTo == null) return;

      AttackVisualForm form = AttackVisuals.Classify(unitFrom, context);
      Color color = AttackVisuals.AccentFor(unitFrom, context, form);
      ProjectileVisualStyle style = AttackVisuals.StyleFor(form, context);
      CardProjectile.FireFrom(origin, unitFrom, unitTo, color, duration,
        pathType, pathData ?? new ProjectilePathData(), ProjectileScale, onImpact, style);
    }

    /// <summary>
    /// 지정한 Hovl 투사체 외형에 별도의 원소 색을 입혀 발사한다.
    /// 캐릭터 전용 연출이 원소 기본 프리팹과 다른 실루엣을 필요로 할 때 사용한다.
    /// </summary>
    public void FireTintedProjectile(HS_Poolable prefab, BaseEnums.UnitElement tintElement,
      Unit unitFrom, Unit unitTo, float duration, ProjectilePathType pathType, ProjectilePathData pathData,
      Action onImpact = null)
    {
      if (unitFrom == null || unitTo == null) return;

      // 외형은 하나로 통일하고 색만 캐릭터별 원소를 따른다.
      CardProjectile.Fire(unitFrom, unitTo, ElementalProjectiles.ColorFor(tintElement), duration,
        pathType, pathData ?? new ProjectilePathData(), ProjectileScale, onImpact);
    }

    /// <summary>
    /// 궤적을 굴릴 컴포넌트를 준비한다.
    ///
    /// Hovl 원본 프리팹에는 <see cref="HS_ProjectileCustomMover"/>가 없고 스톡 무버만 있다.
    /// 없으면 붙이고, 스톡 무버는 꺼 둔다 — 둘 다 살아 있으면 서로 위치를 밀어 궤적이 망가진다.
    /// </summary>
    private static HS_ProjectileCustomMover PrepareMover(HS_Poolable projectile)
    {
      var stock = projectile.GetComponent<HS_ProjectileMover>();
      if (stock != null) stock.enabled = false;

      var mover = projectile.GetComponent<HS_ProjectileCustomMover>();
      if (mover == null) mover = projectile.gameObject.AddComponent<HS_ProjectileCustomMover>();
      mover.ResolveMissingReferences();
      return mover;
    }
  }
}
