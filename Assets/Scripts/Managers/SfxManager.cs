using System.Collections.Generic;
using CGT.Pooling;
using Entities;
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

    public void FireSingleProjectile(HS_Poolable prefab, Unit unitFrom, Unit unitTo, float duration)
    {
      HS_Poolable projectile = poolableManager.GetInstanceOf(prefab);
      projectile.transform.position = unitFrom.transform.position;
      projectile.transform.rotation = Quaternion.LookRotation(unitTo.transform.position - unitFrom.transform.position);
      projectile.gameObject.SetActive(true);
      HS_ProjectileCustomMover mover = projectile.GetComponent<HS_ProjectileCustomMover>();
      mover.SetProjectileInfo(unitFrom, unitTo, duration);
    }
  }
}