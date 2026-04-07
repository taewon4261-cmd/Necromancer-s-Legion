using UnityEngine;

namespace Necromancer.UI
{
    public class SettingKeyManager : MonoBehaviour
    {
        [Header("연결할 설정창 오브젝트")]
        public GameObject settingPanel;

        private float lastTime = 0f;

        void Update()
        {
            // [STABILITY] 오직 'GameScene'일 때만 ESC로 설정창을 띄웁니다!
            // if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene") return;

            // ESC 또는 안드로이드 뒤로가기 감지
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // [STABILITY] 연결이 끊어졌거나 없는 경우 현재 씬에서 자동으로 찾아옵니다! 
                // 인스펙터 연결이 안 되어 있어도 작동하게 됩니다.
                if (settingPanel == null || !settingPanel.scene.IsValid())
                {
                    var foundUI = UnityEngine.Object.FindFirstObjectByType<SettingUI>(FindObjectsInactive.Include);
                    if (foundUI != null) settingPanel = foundUI.gameObject;
                }

                // [DEBUG] 로그가 콘솔창에 무조건 보여야 합니다!
                Debug.Log("<color=orange>[KeyManager] ESC/Back Pressed!</color>");

                if (settingPanel == null)
                {
                    Debug.LogWarning("[KeyManager] 설정창(SettingUI)을 씬에서 찾을 수 없습니다!");
                    return;
                }

                // 쿨다운 (연타 방지)
                if (Time.unscaledTime < lastTime + 0.3f) return;
                lastTime = Time.unscaledTime;

                // [SOUND] 토글 시 버튼 효과음
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
                }

                // 토글 처리
                bool nextState = !settingPanel.activeSelf;
                settingPanel.SetActive(nextState);

                // 일시정지 연동
                if (nextState) 
                {
                    Time.timeScale = 0f;
                    Debug.Log("[KeyManager] Panel ON (Paused)");
                }
                else 
                {
                    // 닫을 때는 세팅UI 내부의 저장 로직 호출 시도
                    var ui = settingPanel.GetComponent<SettingUI>();
                    if (ui != null) ui.CloseAndSave();
                    else Time.timeScale = 1f;
                    
                    Debug.Log("[KeyManager] Panel OFF (Resumed)");
                }
            }
        }
    }
}
