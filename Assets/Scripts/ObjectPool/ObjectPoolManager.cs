using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    private GameManager gameMgr;
    private GridManager gridMgr;

    // 아군 유닛 관련
    private Transform heroPoolObj;
    private List<Hero> heroPool;
    private List<Hero> activeHeroes;
    public GameObject heroPrefab;
    public int heroMaxCountOnInspector = 12;
    private int heroMaxCount;

    // 적군 유닛 관련
    private Transform enemyPoolObj;
    private List<Enemy> enemyPool;
    private List<Enemy> activeEnemies;
    public GameObject enemyPrefab;
    public int enemyMaxCountOnInspector = 12;
    private int enemyMaxCount;

    private void Start()
    {
        gameMgr = GameManager.Instance;
        gridMgr = FindFirstObjectByType<GridManager>();

        heroPoolObj = transform.Find("UnitPool").Find("HeroPool");
        enemyPoolObj = transform.Find("UnitPool").Find("EnemyPool");

        heroMaxCount = heroMaxCountOnInspector;
        enemyMaxCount = enemyMaxCountOnInspector;

        heroPool = new List<Hero>();
        activeHeroes = new List<Hero>();

        enemyPool = new List<Enemy>();
        activeEnemies = new List<Enemy>();

        for (int i = 0; i < heroMaxCount; i++) {
            GameObject obj = Instantiate(heroPrefab);
            obj.name = $"Hero Unit {i + 1}";
            obj.transform.SetParent(heroPoolObj);
            Hero instantiatedUnit = obj.GetComponent<Hero>();
            instantiatedUnit.isActive = false;
            heroPool.Add(instantiatedUnit);
        }
        for (int i = 0; i < enemyMaxCount; i++) {
            GameObject obj = Instantiate(enemyPrefab);
            obj.name = $"Enemy Unit {i + 1}";
            obj.transform.SetParent(enemyPoolObj);
            Enemy instantiatedUnit = obj.GetComponent<Enemy>();
            instantiatedUnit.isActive = false;
            enemyPool.Add(instantiatedUnit);
        }
    }

    public GameObject ActivateUnit(bool isHero)
    {
        if (isHero)
        {
            foreach (Hero unit in heroPool)
            {
                if (!unit.isActive)
                {
                    unit.isActive = true;
                    activeHeroes.Add(unit);
                    return unit.gameObject;
                }
            }

            // 추가 유닛 생성
            GameObject newHero = Instantiate(heroPrefab);
            newHero.name = $"Hero Unit {heroPool.Count + 1}";
            newHero.transform.SetParent(heroPoolObj);
            Hero newHeroComponent = newHero.GetComponent<Hero>();
            newHeroComponent.isActive = true;
            heroPool.Add(newHeroComponent);
            activeHeroes.Add(newHeroComponent);
            return newHero;
        }
        else
        {
            foreach (Enemy unit in enemyPool)
            {
                if (!unit.isActive)
                {
                    unit.isActive = true;
                    activeEnemies.Add(unit);
                    return unit.gameObject;
                }
            }

            // 추가 유닛 생성
            GameObject newEnemy = Instantiate(enemyPrefab);
            newEnemy.name = $"Enemy Unit {enemyPool.Count + 1}";
            newEnemy.transform.SetParent(enemyPoolObj);
            Enemy newEnemyComponent = newEnemy.GetComponent<Enemy>();
            newEnemyComponent.isActive = true;
            enemyPool.Add(newEnemyComponent);
            activeEnemies.Add(newEnemyComponent);
            return newEnemy;
        }
    }

    public void DeactivateUnit(GameObject obj)
    {
        Hero heroComponent = obj.GetComponent<Hero>();
        if (heroComponent != null && activeHeroes.Contains(heroComponent))
        {
            activeHeroes.Remove(heroComponent);
            heroComponent.isActive = false;
            obj.transform.SetParent(heroPoolObj);
            return;
        }

        Enemy enemyComponent = obj.GetComponent<Enemy>();
        if (enemyComponent != null && activeEnemies.Contains(enemyComponent))
        {
            activeEnemies.Remove(enemyComponent);
            enemyComponent.isActive = false;
            obj.transform.SetParent(enemyPoolObj);
            return;
        }

        Debug.LogWarning("Object does not belong to any known pool.");
    }
}
