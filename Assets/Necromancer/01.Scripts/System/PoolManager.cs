// File: Assets/Necromancer/01.Scripts/System/PoolManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 프리팹 재사용을 위한 범용 오브젝트 풀
/// GameManager에 의해 초기화(Init) 권한이 통제됨
/// </summary>
public class PoolManager : MonoBehaviour
{
    [global::System.Serializable]
    public class Pool
    {
        public string tag;           
        public GameObject prefab;    
        public int size;             
    }

    [Header("Pool Settings")]
    public List<Pool> pools;
    
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    public void Init()
    {
        if (poolDictionary == null) poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolDictionary.Clear();

        // [자가 치유] 인스펙터 리스트가 비어있다면 프로젝트 폴더에서 프리팹을 자동으로 로드
        if (pools == null || pools.Count == 0)
        {
            AutoPopulatePools();
        }

        foreach (Pool pool in pools)
        {
            if (pool.prefab == null) continue;

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, this.transform); 
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }
            if (!poolDictionary.ContainsKey(pool.tag))
                poolDictionary.Add(pool.tag, objectPool);
        }
        Debug.Log($"<color=green>[PoolManager]</color> Initialized with {poolDictionary.Count} pools.");
    }

    private void AutoPopulatePools()
    {
        pools = new List<Pool>();
        // 주요 프리팹들을 리소스로부터 혹은 경로로부터 자동 로드 (여기서는 주요 태그 기반)
        string[] corePrefabs = { "Enemy", "ExpGem", "Minion", "HitEffect" };
        foreach (var pName in corePrefabs)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{pName}");
            if (prefab == null) prefab = Resources.Load<GameObject>(pName); // 루트 시도
            
            if (prefab != null)
            {
                pools.Add(new Pool { tag = pName, prefab = prefab, size = 20 });
            }
        }
        
        if (pools.Count == 0)
        {
            Debug.LogWarning("[PoolManager] AutoPopulate failed. Please check if prefabs are in 'Resources' folder or assigned in Inspector.");
        }
    }

    /// <summary>
    /// 풀에서 오브젝트 Get (Instantiate 대체)
    /// 재고 부족 시 임시 확장 처리 포함
    /// </summary>
    public GameObject Get(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Invalid pool tag: {tag}");
            return null;
        }

        Queue<GameObject> objectPool = poolDictionary[tag];

        if (objectPool.Count == 0)
        {
            Pool poolDef = pools.Find(p => p.tag == tag);
            if (poolDef != null)
            {
                GameObject newObj = Instantiate(poolDef.prefab, this.transform);
                newObj.SetActive(false);
                objectPool.Enqueue(newObj);
                // Debug.Log($"[PoolManager] Extanded pool capacity. tag: {tag}");
            }
            else
            {
                return null;
            }
        }

        GameObject objectToSpawn = objectPool.Dequeue();
        objectToSpawn.SetActive(true);
        objectToSpawn.transform.SetPositionAndRotation(position, rotation);

        return objectToSpawn;
    }

    /// <summary>
    /// 사용 완료된 오브젝트를 풀로 반환 (Destroy 대체)
    /// </summary>
    public void Release(string tag, GameObject obj)
    {
        if (poolDictionary == null || !poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Invalid pool tag or dictionary not initialized: {tag}");
            if (obj != null) obj.SetActive(false);
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(this.transform);
        poolDictionary[tag].Enqueue(obj);
    }
}
}
