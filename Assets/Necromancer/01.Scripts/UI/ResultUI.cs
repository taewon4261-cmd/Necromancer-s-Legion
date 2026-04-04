using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Necromancer.Core;
using UnityEngine.SceneManagement;

namespace Necromancer.UI
{
    /// <summary>
    /// 스테이지 클리어 또는 패배 시 나타나는 결과창 전용 컨트롤러
    /// </summary>
    public class ResultUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI tmpTitle;
        [SerializeField] private TextMeshProUGUI tmpSoulCount;
        [SerializeField] private Button btnConfirm;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 결과창을 활성화하고 데이터를 바인딩합니다.
        /// </summary>
        /// <param name="isVictory">승리 여부</param>
        /// <param name="soulCount">획득한 총 소울 수</param>
        public void Open(bool isVictory, int soulCount)
        {
            gameObject.SetActive(true);

            if (tmpTitle != null)
            {
                tmpTitle.text = isVictory ? "승 리" : "패 배";
                tmpTitle.color = isVictory ? Color.green : Color.red;
            }

            if (tmpSoulCount != null)
            {
                tmpSoulCount.text = $"획득한 소울: {soulCount:N0}";
            }
        }

        /// <summary>
        /// 확인 버튼 클릭 시 타이틀 씬으로 이동 (시간 복구 포함)
        /// </summary>
        public void OnClick_Confirm()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("TitleScene");
        }
    }
}
