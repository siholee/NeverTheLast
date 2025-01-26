using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    private GameManager gameMgr;
    private GridManager gridMgr;
    private List<Unit> heroPool;
    public GameObject heroPrefab;
    public int heroMaxCount = 12;
    private List<Unit> enemyPool;
    public GameObject enemyPrefab;
    public int enemyMaxCount = 12;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameMgr = FindFirstObjectByType<GameManager>();
        gridMgr = FindFirstObjectByType<GridManager>();

        for (int i = 0; i < heroMaxCount; i++) {
            GameObject obj = Instantiate(heroPrefab);
            Unit instantiatedUnit = obj.GetComponent<Unit>();
            heroPool.Add(instantiatedUnit);
        }
        for (int i = 0; i < enemyMaxCount; i++) {
            GameObject obj = Instantiate(enemyPrefab);
            Unit instantiatedUnit = obj.GetComponent<Unit>();
            enemyPool.Add(instantiatedUnit);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
