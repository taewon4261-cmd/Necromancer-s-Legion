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
    [SerializeField] private List<Pool> pools = new List<Pool>();
    
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    public void Init()
    {
        if (poolDictionary == null) poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolDictionary.Clear();

        // [STABILITY] 데이터 보호: 런타임 자동 할당(AutoPopulate) 제거. 
        // 모든 풀은 인스펙터에서 명시적으로 설정되어야 합니다.
        if (pools == null || pools.Count == 0)
        {
            Debug.LogError("<color=red>[PoolManager]</color> Pools list is empty! Please configure pools in the Inspector.");
            return;
        }

        foreach (Pool pool in pools)
        {
            if (pool.prefab == null) 
            {
                Debug.LogWarning($"<color=yellow>[PoolManager]</color> Prefab missing for tag: {pool.tag}");
                continue;
            }

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
