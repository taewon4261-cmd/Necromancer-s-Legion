
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 프리팹 재사용을 위한 범용 오브젝트 풀
/// Dictionary 캐싱, 배치 확장, 성능 경고 시스템 적용
/// </summary>
public class PoolManager : MonoBehaviour
{
    [global::System.Serializable]
    public class Pool
    {
        public string tag;           
        public GameObject prefab;    
        public int size;
        [Tooltip("재고 부족 시 한 번에 생성할 개수")]
        public int batchSize = 10;
    }

    [Header("Pool Settings")]
    [SerializeField] private List<Pool> pools = new List<Pool>();
    
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, Pool> poolSettings; // O(1) 접근을 위한 설정 캐시

    public void Init()
    {
        if (poolDictionary == null) poolDictionary = new Dictionary<string, Queue<GameObject>>();
        if (poolSettings == null) poolSettings = new Dictionary<string, Pool>();
        
        poolDictionary.Clear();
        poolSettings.Clear();

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

            // 설정 캐싱
            poolSettings[pool.tag] = pool;

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = CreateNewObject(pool.prefab);
                objectPool.Enqueue(obj);
            }
            
            if (!poolDictionary.ContainsKey(pool.tag))
                poolDictionary.Add(pool.tag, objectPool);
        }
        Debug.Log($"<color=green>[PoolManager]</color> Initialized with {poolDictionary.Count} pools.");
    }

    private GameObject CreateNewObject(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, this.transform); 
        obj.SetActive(false);
        return obj;
    }

    /// <summary>
    /// 풀에서 오브젝트 Get
    /// </summary>
    public GameObject Get(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.TryGetValue(tag, out Queue<GameObject> objectPool))
        {
            Debug.LogWarning($"[PoolManager] Invalid pool tag: {tag}");
            return null;
        }

        // 재고 부족 시 배치(Batch)로 대량 생성
        if (objectPool.Count == 0)
        {
            if (poolSettings.TryGetValue(tag, out Pool settings))
            {
                Debug.LogWarning($"<color=orange>[Performance Warning]</color> Pool Exhausted for tag: {tag}. Batch creating {settings.batchSize} items.");
                
                int countToCreate = Mathf.Max(1, settings.batchSize);
                for (int i = 0; i < countToCreate; i++)
                {
                    objectPool.Enqueue(CreateNewObject(settings.prefab));
                }
            }
            else
            {
                return null;
            }
        }

        GameObject objectToSpawn = objectPool.Dequeue();
        
        // 부모 재설정은 이미 생성 시 수행되었으므로 활성화 및 위치만 세팅
        objectToSpawn.transform.SetPositionAndRotation(position, rotation);
        objectToSpawn.SetActive(true);

        return objectToSpawn;
    }

    /// <summary>
    /// 사용 완료된 오브젝트를 풀로 반환
    /// </summary>
    public void Release(string tag, GameObject obj)
    {
        if (poolDictionary == null || !poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Invalid pool tag: {tag}");
            if (obj != null) obj.SetActive(false);
            return;
        }

        obj.SetActive(false);
        
        // 이미 PoolManager 자식이라면 SetParent 호출 생략하여 연산 절감
        if (obj.transform.parent != this.transform)
        {
            obj.transform.SetParent(this.transform);
        }
        
        poolDictionary[tag].Enqueue(obj);
    }
}
}
