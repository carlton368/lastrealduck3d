using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;

namespace CuteDuckGame
{
    public class UIManager : MonoBehaviour
    {
        // 현재 씬에서 사용될 EventSystem을 연결하기 위한 필드
        [SerializeField] private EventSystem eventSystem;

        // Play 버튼 GameObject를 연결하기 위한 필드
        [SerializeField] private GameObject playButton;

        // Leave 버튼 GameObject를 연결하기 위한 필드
        [SerializeField] private GameObject leaveButton;

        // Defeat 패널 GameObject를 연결하기 위한 필드
        [SerializeField] private GameObject defeatPanel;

        // Win 패널 GameObject를 연결하기 위한 필드
        [SerializeField] private GameObject winPanel;

        // 패배 후 자동 전환할 씬 인덱스 (빌드 설정 기준, 1 = 2번째 씬)
        [SerializeField] private int returnSceneBuildIndex = 0;

        // 씬 전환 전 대기 시간(초)
        [SerializeField] private float returnDelay = 3f;

        // 싱글톤(Singleton) 접근을 위한 정적 필드
        public static UIManager Instance;

        private void Awake()
        {
            // 싱글톤 초기화
            Instance = this;

            // Defeat 패널 초기 비활성화
            if (defeatPanel != null)
                defeatPanel.SetActive(false);

            // Win 패널 초기 비활성화
            if (winPanel != null)
                winPanel.SetActive(false);

            // 씬이 로드될 때마다 이벤트를 받아서 OnSceneLoaded를 호출하도록 연결
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 씬이 로드될 때마다 호출되는 메서드.
        /// 씬 이름에 따라 버튼 활성화 여부를 다르게 설정한다.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Title 씬일 경우 버튼 비활성화
            if (scene.name.Contains("Title"))
            {
                TogglePlayButton(true);
                ToggleLeaveButton(false);
                // Defeat 패널 비활성화
                defeatPanel?.SetActive(false);
                // Win 패널 비활성화
                winPanel?.SetActive(false);
                return;
            }

            // Game 씬일 경우 Leave 버튼 활성화
            if (scene.name.Contains("Game"))
            {
                TogglePlayButton(false);
                ToggleLeaveButton(true);
                // Defeat 패널 비활성화
                defeatPanel?.SetActive(false);
                // Win 패널 비활성화
                winPanel?.SetActive(false);
                return;
            }
        }

        /// <summary>
        /// UI 인터랙션 활성/비활성 전환
        /// </summary>
        public void ToggleInteraction(bool isOn)
        {
            eventSystem.enabled = isOn;
        }

        /// <summary>
        /// Play 버튼 활성/비활성 전환
        /// </summary>
        public void TogglePlayButton(bool isOn)
        {
            playButton.SetActive(isOn);
        }

        /// <summary>
        /// Leave 버튼 활성/비활성 전환
        /// </summary>
        public void ToggleLeaveButton(bool isOn)
        {
            leaveButton.SetActive(isOn);
        }

        /// <summary>
        /// Defeat 패널을 활성화하고 UI 인터랙션을 끕니다.
        /// </summary>
        public void ShowDefeatPanel()
        {
            if (defeatPanel != null)
                defeatPanel.SetActive(true);
            // 입력 비활성화
            ToggleInteraction(false);
            // 커서 잠금 해제 및 보이기
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // 패배 처리: 3초 대기 후 세션 종료 및 씬 전환, 재연결
            FusionSession.Instance.HandleDefeat(returnSceneBuildIndex, returnDelay);
        }

        /// <summary>
        /// Win 패널을 활성화하고 UI 인터랙션을 끕니다.
        /// </summary>
        public void ShowWinPanel()
        {
            if (winPanel != null)
                winPanel.SetActive(true);
            // 입력 비활성화
            ToggleInteraction(false);
            // 커서 잠금 해제 및 보이기
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // 승리 처리: 3초 대기 후 세션 종료 및 씬 전환, 재연결
            FusionSession.Instance.HandleDefeat(returnSceneBuildIndex, returnDelay);
        }
    }
}