using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemiesSpawn : MonoBehaviour
{
    public GameObject[] enemiesPreefabs;
    public List<GameObject> enemies {get; private set;}
    
    void Start()
    {
        enemies = new List<GameObject>();

        StartCoroutine(Spawn());
    }

    IEnumerator Spawn()
    {
        int intEnemyIndex = 0;

        while(true)
        {
            intEnemyIndex = Random.Range(0, enemiesPreefabs.Length);

            GameObject enemy = Instantiate(enemiesPreefabs[intEnemyIndex], transform.position, Quaternion.identity);
            enemy.transform.position = new Vector3(enemy.transform.position.x, enemy.transform.position.y, -1);

            enemies.Add(enemy);

            yield return new WaitForSeconds(Random.Range(1, 4));

            Destroy(enemy, 6f);
        }
    }

    void Update()
    {
        
    }
}