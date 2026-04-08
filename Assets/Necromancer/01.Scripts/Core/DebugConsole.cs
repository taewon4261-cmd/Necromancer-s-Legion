using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Necromancer.Core
{
    /// <summary>
    /// 개발 중 스테이지 강제 이동, 치트(무적, 골드 추가 등)를 수행하는 시스템입니다.
    /// ` (Backquote) 키로 열고 닫을 수 있습니다.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        public static DebugConsole Instance { get; private set; }

        [Header("UI References")]
        public GameObject consolePanel;
        public InputField commandInput;
        public Text logText;

        private bool isOpen = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (consolePanel != null) consolePanel.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                ToggleConsole();
            }

            if (isOpen && Input.GetKeyDown(KeyCode.Return))
            {
                OnSubmitCommand();
            }
        }

        public void ToggleConsole()
        {
            isOpen = !isOpen;
            if (consolePanel != null)
            {
                consolePanel.SetActive(isOpen);
                if (isOpen)
                {
                    commandInput.ActivateInputField();
                    GameManager.Instance.SetPause(Necromancer.PauseSource.Debug, true);
                }
                else
                {
                    GameManager.Instance.SetPause(Necromancer.PauseSource.Debug, false);
                }
            }
        }

        public void OnSubmitCommand()
        {
            string cmd = commandInput.text.Trim().ToLower();
            commandInput.text = "";
            commandInput.ActivateInputField();

            ProcessCommand(cmd);
        }

        private void ProcessCommand(string fullCommand)
        {
            string[] parts = fullCommand.Split(' ');
            if (parts.Length == 0) return;

            string cmd = parts[0];
            
            switch (cmd)
            {
                case "soul":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int goldAmount))
                    {
                        GameManager.Instance.Resources.AddSoul(goldAmount);
                        Log($"Harvested {goldAmount} Soul.");
                    }
                    break;

                case "exp":
                    if (parts.Length > 1 && float.TryParse(parts[1], out float expAmount))
                    {
                        GameManager.Instance.AddExp(expAmount);
                        Log($"Added {expAmount} Exp.");
                    }
                    break;

                case "win":
                    Log("Cheat: Stage Win! (Returning to Title)");
                    // TODO: 인게임 결과 팝업 연동 전에는 수동으로 씬 이동
                    UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
                    break;

                case "die":
                    Log("Cheat: Self Destruct.");
                    PlayerController player = FindObjectOfType<PlayerController>();
                    if (player != null) player.TakeDamage(99999);
                    break;

                case "help":
                    Log("Commands: soul [val], exp [val], win, die, help");
                    break;

                default:
                    Log($"Unknown command: {cmd}");
                    break;
            }
        }

        private void Log(string message)
        {
            if (logText != null)
            {
                logText.text += $"\n> {message}";
                // 간단한 줄 생성 제한 (마지막 10줄만 유지 등) 추가 가능
            }
            Debug.Log($"[DebugConsole] {message}");
        }
    }
}
