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
        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            // 🚨 버그 수정 (널 레퍼런스 에러 방어)
            // 에디터 인스펙터에서 사용자가 프리팹을 실수로 집어넣지 않았을 때 에러로 터지는 것을 막습니다.
            if (pool.prefab == null)
            {
                Debug.LogError($"[PoolManager] 🔥 '{pool.tag}' 이름의 프리팹 칸이 비어있습니다! 00.Scenes의 [System] 오브젝트를 클릭해서 프리팹을 쏙 채워주세요!");
                continue; // 생성을 건너뛰어서 크러시(튕김)를 미연에 방지합니다.
            }

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, this.transform); 
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.tag, objectPool);
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
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"[PoolManager] Invalid pool tag: {tag}");
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(this.transform);
        poolDictionary[tag].Enqueue(obj);
    }
}
}
