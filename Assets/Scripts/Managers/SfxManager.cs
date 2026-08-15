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
    // 파티클 prefab들은 원본이 전투 화면 기준으로 과하게 크다.
    // prefab을 직접 수정하면 서드파티(Hovl Studio) 에셋을 건드리게 되므로
    // 스폰 시점에 스케일을 적용해 조절한다.
    // (모든 파티클의 scalingMode가 Hierarchy(0)라 루트 스케일이 그대로 반영된다.)
    private const float DefaultProjectileScale = 0.45f;

    // 동시에 살아있는 투사체 상한. 전체 공격/광역 스킬에서 화면이 난잡해지는 것을 막는다.
    private const int MaxConcurrentProjectiles = 12;

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
      ProjectilePathType pathType, ProjectilePathData pathData)
    {
      TrimLiveProjectiles();

      HS_Poolable projectile = poolableManager.GetInstanceOf(prefab);
      projectile.transform.position = unitFrom.transform.position;
      projectile.transform.rotation = Quaternion.LookRotation(unitTo.transform.position - unitFrom.transform.position);
      // 풀에서 재사용되므로 매번 스케일을 다시 지정한다.
      projectile.transform.localScale = Vector3.one * GetProjectileScale(prefab);
      projectile.gameObject.SetActive(true);
      _liveProjectiles.Add(projectile);

      // 궤적을 굴리는 것은 이 컴포넌트다. Hovl 기본 프리팹에는 없으므로 붙어 있는지 확인한다.
      HS_ProjectileCustomMover mover = projectile.GetComponent<HS_ProjectileCustomMover>();
      if (mover == null)
      {
        Debug.LogWarning(
          $"[SfxManager] {prefab.name}에 HS_ProjectileCustomMover가 없어 투사체가 움직이지 않습니다.");
        return;
      }

      mover.SetProjectileInfo(unitFrom, unitTo, duration, pathType, pathData ?? new ProjectilePathData());
    }
  }
}