// File: Assets/Necromancer/01.Scripts/System/InfiniteMap.cs
using UnityEngine;

namespace Necromancer.Systems
{
    /// <summary>
    /// 플레이어의 움직임에 맞춰 3x3 타일을 재배치하여 무한한 맵을 연출하는 클래스입니다.
    /// (뱀파이어 서바이버 방식의 무한 배경 시스템)
    /// </summary>
    public class InfiniteMap : MonoBehaviour
    {
        [Header("Grid Setup")]
        public Transform playerTransform;
        public Sprite tileSprite;
        public float tileSize = 10f; // PPU 100 기준 1000px 이미지일 때 10이 최적
        public int gridSize = 4; // 4x4로 설정
        public int sortingOrder = -100;

        [Header("Movement Boundary (Optional)")]
        [Tooltip("체크하면 플레이어가 지정된 범위를 못 나갑니다.")]
        public bool useLimit = false;
        [Tooltip("맵의 중심(0,0)으로부터의 거리를 설정하세요.")]
        public float mapLimit = 100f;

        private Transform[] tiles;

        private void Start()
        {
            if (playerTransform == null && GameManager.Instance != null)
                playerTransform = GameManager.Instance.playerTransform;

            // [수정] 시작할 때 플레이어의 위치를 기준으로 타일을 깔아둠 (켜지자마자 보이게)
            Vector3 startPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            
            // 플레이어 근처의 그리드 좌표 계산 (타일 사이즈 단위로 반올림)
            float startX = Mathf.Round(startPos.x / tileSize) * tileSize;
            float startY = Mathf.Round(startPos.y / tileSize) * tileSize;

            tiles = new Transform[gridSize * gridSize];
            int index = 0;
            float offset = (gridSize - 1) * 0.5f;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    GameObject tileObj = new GameObject($"Tile_{x}_{y}");
                    tileObj.transform.SetParent(this.transform);
                    SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
                    sr.sprite = tileSprite;
                    sr.sortingOrder = sortingOrder;

                    // [수정] 플레이어가 어디에 있든 그 주변 4x4로 즉시 배치
                    Vector3 pos = new Vector3(startX + (x - offset) * tileSize, startY + (y - offset) * tileSize, 0);
                    tileObj.transform.position = pos; 
                    tiles[index++] = tileObj.transform;
                }
            }
        }

        private void LateUpdate()
        {
            if (playerTransform == null) return;

            // 1. 캐릭터 경계 제한 로직
            if (useLimit)
            {
                Vector3 clampedPos = playerTransform.position;
                clampedPos.x = Mathf.Clamp(clampedPos.x, -mapLimit, mapLimit);
                clampedPos.y = Mathf.Clamp(clampedPos.y, -mapLimit, mapLimit);
                playerTransform.position = clampedPos;
            }

            // 2. 무한 타일 워프 로직 (그리드 크기에 맞춰 유연하게 계산)
            float totalWidth = gridSize * tileSize;
            foreach (Transform tile in tiles)
            {
                Vector3 diff = playerTransform.position - tile.position;

                // 타일 배치 간격의 절반보다 멀어지면 반대편으로 이동
                if (Mathf.Abs(diff.x) > totalWidth * 0.5f)
                {
                    float moveDir = diff.x > 0 ? 1 : -1;
                    tile.Translate(Vector3.right * moveDir * totalWidth);
                }

                if (Mathf.Abs(diff.y) > totalWidth * 0.5f)
                {
                    float moveDir = diff.y > 0 ? 1 : -1;
                    tile.Translate(Vector3.up * moveDir * totalWidth);
                }
            }
        }
    }
}
