
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
        
        // [STABILITY] 씬 전환 시 아직 필드에 남아있는(활성화된) 모든 오브젝트를 강제 회수
        // (PoolManager가 DontDestroyOnLoad이므로 이전 게임의 자식들이 남아있을 수 있음)
        ClearAllActiveObjects();

        poolDictionary.Clear();
        poolSettings.Clear();

        if (pools == null || pools.Count == 0)
        {
            Debug.LogError("<color=red>[PoolManager]</color> Pools list is empty!");
            return;
        }

        foreach (Pool pool in pools)
        {
            if (pool.prefab == null) continue;

            poolSettings[pool.tag] = pool;
            Queue<GameObject> objectPool = new Queue<GameObject>();
            
            // 기존 오브젝트를 재사용하거나 부족분을 채움
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = CreateNewObject(pool.prefab);
                objectPool.Enqueue(obj);
            }
            
            poolDictionary[pool.tag] = objectPool;
        }
    }

    /// <summary>
    /// [CLEANUP] 현재 활성화된 모든 오브젝트를 강제로 끄고 풀링 시스템을 초기화 대기 상태로 만듭니다.
    /// </summary>
    public void ClearAllActiveObjects()
    {
        // 1. 자식들을 직접 순회하여 활성화된 것들을 모두 끔
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
        }
        
        Debug.Log("<color=orange>[PoolManager]</color> All active objects cleared from the field.");
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
            if (obj != null) obj.SetActive(false);
            return;
        }

        // [STABILITY] 중복 반납 방지: 이미 비활성화 상태면 풀에 있거나 이미 처리된 것이므로 즉시 반환
        // Queue.Contains()는 O(n)이므로 activeSelf 단순 체크만으로 충분 (활성→비활성 순서 보장)
        if (!obj.activeSelf) return;

        obj.SetActive(false);
        
        if (obj.transform.parent != this.transform)
        {
            obj.transform.SetParent(this.transform);
        }
        
        poolDictionary[tag].Enqueue(obj);
    }
}
}
