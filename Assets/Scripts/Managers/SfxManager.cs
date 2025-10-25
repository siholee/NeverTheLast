using System.Collections.Generic;
using CGT.Pooling;
using Effects;
using Entities;
using BaseClasses;
using UnityEngine;

namespace Managers
{
  [System.Serializable]
  public class ProjectilePrefabEntry
  {
    public string key;
    public HS_Poolable prefab;
  }
  public class SfxManager : MonoBehaviour
  {
    [SerializeField] private HS_CustomPoolableManager poolableManager;
    [SerializeField] private List<ProjectilePrefabEntry> projectilePrefabEntries;

    public Dictionary<string, HS_Poolable> ProjectilePrefabs;


    private void Awake()
    {
      poolableManager = HS_CustomPoolableManager.EnsureExists();
      ProjectilePrefabs = new Dictionary<string, HS_Poolable>();
      foreach (var entry in projectilePrefabEntries)
      {
        if (!ProjectilePrefabs.ContainsKey(entry.key))
          ProjectilePrefabs.Add(entry.key, entry.prefab);
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
      HS_Poolable projectile = poolableManager.GetInstanceOf(prefab);
      projectile.transform.position = unitFrom.transform.position;
      projectile.transform.rotation = Quaternion.LookRotation(unitTo.transform.position - unitFrom.transform.position);
      projectile.gameObject.SetActive(true);
      HS_ProjectileCustomMover mover = projectile.GetComponent<HS_ProjectileCustomMover>();
      
      if (pathData != null)
        mover.SetProjectileInfo(unitFrom, unitTo, duration, pathType, pathData);
      else
        mover.SetProjectileInfo(unitFrom, unitTo, duration, pathType, new ProjectilePathData());
    }
  }
}