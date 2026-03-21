using UnityEngine;
using Necromancer;

namespace Necromancer.Systems
{
    public class PoolObjectAutoRelease : MonoBehaviour
    {
        public float delay = 1f;
        public string poolTag = "HitEffect";

        private void OnEnable()
        {
            Invoke(nameof(ReturnToPool), delay);
        }

        private void ReturnToPool()
        {
            if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
            {
                GameManager.Instance.poolManager.Release(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
