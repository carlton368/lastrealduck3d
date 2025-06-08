using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARKit;
using Fusion;

namespace CuteDuckGame.AR
{
    [RequireComponent(typeof(ARFace))]
    public class FaceLandmarkTracker : MonoBehaviour
    {
        [Header("Instruction / Debug UI")]
        public TextMeshProUGUI tmpText;
        public Text uiText;

        [Header("ARKit BlendShape 설정")]
        [Tooltip("ARKit 'CheekPuff' 블렌드쉐입 임계값 (0~1)")]
        public float puffBlendThreshold = 0.5f;

        [Header("Debug")]
        public bool enableDebugInfo = true;
        public TextMeshProUGUI debugTmpText;
        public Text debugUiText;

        [Header("Puff Timing")]
        [Tooltip("볼을 이 시간(초) 이상 부풀려야 액션이 트리거됩니다.")]
        public float requiredPuffDuration = 1f;

        [Header("Testing (개발용)")]
        [Tooltip("에디터에서 테스트 모드 활성화")]
        public bool enableTestMode = true;
        [Tooltip("테스트용 키")]
        public KeyCode testKey = KeyCode.P;

        // Runtime Components
        private ARFace _arFace;
        private ARFaceManager _arFaceManager;
        private ARKitFaceSubsystem _faceSubsystem;
        private PlayerMovement _playerMovement;

        // State tracking
        private bool _cheekPuffed = false;
        private bool _baseSetAnnounced = false;
        private float _puffStartTime = 0f;
        private float _lastPlayerMovementSearchTime = 0f;
        private const float PLAYER_MOVEMENT_SEARCH_INTERVAL = 1f;

        private void Awake()
        {
            _arFace = GetComponent<ARFace>();
            InitializeComponents();
            FindPlayerMovement();
        }

        private void InitializeComponents()
        {
            // UI 컴포넌트 찾기
            if (tmpText == null) tmpText = FindObjectOfType<TextMeshProUGUI>();
            if (uiText == null) uiText = FindObjectOfType<Text>();

            // ARFaceManager 찾기
            _arFaceManager = FindObjectOfType<ARFaceManager>();
            if (_arFaceManager == null)
            {
                Debug.LogError("[FaceLandmarkTracker] ARFaceManager를 찾을 수 없습니다!");
                return;
            }

            // ARKitFaceSubsystem 가져오기
            if (_arFaceManager.subsystem is ARKitFaceSubsystem arkitSubsystem)
            {
                _faceSubsystem = arkitSubsystem;
                Debug.Log("[FaceLandmarkTracker] ARKitFaceSubsystem 초기화 완료");
            }
            else
            {
                Debug.LogWarning("[FaceLandmarkTracker] ARKitFaceSubsystem를 찾을 수 없습니다. 테스트 모드로 전환됩니다.");
            }
        }

        private void FindPlayerMovement()
        {
            foreach (var pm in FindObjectsOfType<PlayerMovement>())
            {
                if (pm.Object != null && pm.Object.HasInputAuthority)
                {
                    _playerMovement = pm;
                    Debug.Log($"[FaceLandmarkTracker] PlayerMovement found: {pm.name}");
                    return;
                }
            }
            Debug.Log("[FaceLandmarkTracker] PlayerMovement not found");
        }

        private void Update()
        {
            // PlayerMovement 재검색 (성능을 위해 간격 제한)
            if (_playerMovement == null && Time.time - _lastPlayerMovementSearchTime > PLAYER_MOVEMENT_SEARCH_INTERVAL)
            {
                FindPlayerMovement();
                _lastPlayerMovementSearchTime = Time.time;
            }

            // ARKitFaceSubsystem 재초기화 시도
            if (_faceSubsystem == null && _arFaceManager != null && _arFaceManager.subsystem is ARKitFaceSubsystem arkitSubsystem)
            {
                _faceSubsystem = arkitSubsystem;
                Debug.Log("[FaceLandmarkTracker] ARKitFaceSubsystem 재연결됨");
            }

            // 볼 부풀림 감지
            var (leftPuff, rightPuff, isPuffed) = GetCheekPuffValues();
            
            if (!_baseSetAnnounced)
            {
                ShowInitialInstruction();
                _baseSetAnnounced = true;
            }

            ProcessPuffGesture(isPuffed);
        }

        private void ShowInitialInstruction()
        {
            if (_faceSubsystem != null)
            {
                SetInstruction("ARKit 얼굴 추적 준비됨. 볼을 부풀려 이동하세요.");
            }
        }

        private (float leftPuff, float rightPuff, bool isPuffed) GetCheekPuffValues()
        {
            float puffValue = 0f;
            bool hasValidData = false;

            // ARKit 6.1 API 사용
            if (_faceSubsystem != null && _arFace != null && 
                _arFace.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                try
                {
                    // ARKit 6.1의 정확한 API 사용
                    using (var blendShapes = _faceSubsystem.GetBlendShapeCoefficients(_arFace.trackableId, Unity.Collections.Allocator.Temp))
                    {
                        foreach (var coefficient in blendShapes)
                        {
                            if (coefficient.blendShapeLocation == ARKitBlendShapeLocation.CheekPuff)
                            {
                                puffValue = coefficient.coefficient;
                                hasValidData = true;
                                break;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[FaceLandmarkTracker] ARKit BlendShape 접근 오류: {e.Message}");
                }
            }

            bool isPuffed = puffValue > puffBlendThreshold;
            return (puffValue, puffValue, isPuffed);
        }

        private void ProcessPuffGesture(bool isPuffed)
        {
            if (isPuffed && !_cheekPuffed)
            {
                // Puff 시작
                _cheekPuffed = true;
                _puffStartTime = Time.time;
                
                if (_faceSubsystem != null)
                {
                    SetInstruction("볼 부풀림 감지됨 - 유지하세요…");
                }
                else
                {
#if UNITY_EDITOR
                    SetInstruction($"{testKey} 키 감지됨 - 계속 누르세요…");
#else
                    SetInstruction("터치 감지됨 - 계속 터치하세요…");
#endif
                }
            }
            else if (!isPuffed && _cheekPuffed)
            {
                // Puff 종료
                _cheekPuffed = false;
                float puffDuration = Time.time - _puffStartTime;

                if (puffDuration >= requiredPuffDuration)
                {
                    TriggerJumpAction();
                }
                else
                {
                    SetInstruction($"더 오래 유지하세요! ({puffDuration:F1}초 < {requiredPuffDuration:F1}초 필요)");
                }
            }
        }

        private void TriggerJumpAction()
        {
            if (_playerMovement != null)
            {
                Debug.Log($"[FaceLandmarkTracker] Calling RequestJump on {_playerMovement.name}");
                _playerMovement.RequestJump();
                SetInstruction("성공! 캐릭터가 이동합니다.");
            }
            else
            {
                Debug.Log("[FaceLandmarkTracker] PlayerMovement not found on puff end");
                SetInstruction("PlayerMovement를 찾을 수 없습니다.");
            }
        }

        private void LateUpdate()
        {
            if (!enableDebugInfo) return;

            var (leftPuff, rightPuff, isPuffed) = GetCheekPuffValues();
            float puffDuration = _cheekPuffed ? Time.time - _puffStartTime : 0f;
            
            string arkitStatus = _faceSubsystem != null ? "연결됨" : "없음";
            string trackingStatus = _arFace != null ? _arFace.trackingState.ToString() : "없음";
            
            string testModeInfo = "";

            string msg = $"ARKit Subsystem: {arkitStatus}\n" +
                        $"Tracking State: {trackingStatus}\n" +
                        $"Puff Value: {leftPuff:F3}\n" +
                        $"Threshold: {puffBlendThreshold:F3}\n" +
                        $"Is Puffed: {isPuffed}\n" +
                        $"Duration: {puffDuration:F1}s\n" +
                        $"PlayerMovement: {(_playerMovement != null ? "Found" : "Missing")}" +
                        testModeInfo;

            if (debugTmpText != null)
                debugTmpText.text = msg;
            else if (debugUiText != null)
                debugUiText.text = msg;
            else if (tmpText != null)
                tmpText.text = msg;
        }

        private void SetInstruction(string msg)
        {
            if (tmpText != null) tmpText.text = msg;
            if (uiText != null) uiText.text = msg;
        }

        private void OnEnable()
        {
            // ARFace 이벤트 구독 (필요시)
            if (_arFace != null)
            {
                // 얼굴 추적 상태 변화 모니터링 가능
            }
        }

        private void OnDisable()
        {
            // 정리 작업
        }

        // Inspector에서 설정 확인
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            if (GetComponent<ARFace>() == null)
            {
                Debug.LogWarning($"{name}: ARFace 컴포넌트가 필요합니다!");
            }

            if (FindObjectOfType<ARFaceManager>() == null)
            {
                Debug.LogWarning("씬에 ARFaceManager가 필요합니다!");
            }
        }
    }
}