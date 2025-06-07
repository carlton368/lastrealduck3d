using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

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

        // 싱글톤(Singleton) 접근을 위한 정적 필드
        public static UIManager Instance;

        private void Awake()
        {
            // 싱글톤 초기화
            Instance = this;

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
                return;
            }

            // Game 씬일 경우 Leave 버튼 활성화
            if (scene.name.Contains("Game"))
            {
                TogglePlayButton(false);
                ToggleLeaveButton(true);
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
    }
}