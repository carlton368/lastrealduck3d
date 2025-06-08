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

        [Header("강도 기반 타이밍 설정")]
        [Tooltip("볼을 이 시간(초) 이상 부풀려야 액션이 트리거됩니다.")]
        public float requiredPuffDuration = 1f;
        [Tooltip("최대 파워를 위한 부풀림 시간 (초)")]
        public float maxPowerDuration = 5f;
        [Tooltip("강도 계산 곡선")]
        public AnimationCurve intensityCalculationCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f);

        [Header("UI 피드백")]
        [Tooltip("파워 충전 UI (Slider 또는 Image)")]
        public Slider powerSlider;
        public Image powerFillImage;
        [Tooltip("파워 레벨에 따른 색상")]
        public Gradient powerColorGradient = new Gradient();

        [Header("Testing (개발용)")]
        [Tooltip("에디터에서 테스트 모드 활성화")]
        public bool enableTestMode = false;
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
            InitializeIntensityCurve();
            InitializePowerColorGradient();
        }

        private void InitializeComponents()
        {
            // UI 컴포넌트 찾기
            if (tmpText == null) tmpText = FindObjectOfType<TextMeshProUGUI>();
            if (uiText == null) uiText = FindObjectOfType<Text>();

            // 파워 UI 컴포넌트 찾기
            if (powerSlider == null) powerSlider = FindObjectOfType<Slider>();
            if (powerFillImage == null && powerSlider != null)
            {
                powerFillImage = powerSlider.fillRect?.GetComponent<Image>();
            }

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

        private void InitializeIntensityCurve()
        {
            // 기본 강도 곡선 설정 (인스펙터에서 설정되지 않은 경우)
            if (intensityCalculationCurve.keys.Length == 0)
            {
                intensityCalculationCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f);
            }
        }

        private void InitializePowerColorGradient()
        {
            // 기본 색상 그라디언트 설정 (인스펙터에서 설정되지 않은 경우)
            if (powerColorGradient.colorKeys.Length == 0)
            {
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(Color.red, 0f);        // 낮은 파워: 빨강
                colorKeys[1] = new GradientColorKey(Color.yellow, 0.5f);   // 중간 파워: 노랑
                colorKeys[2] = new GradientColorKey(Color.green, 1f);      // 최대 파워: 초록

                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                powerColorGradient.SetKeys(colorKeys, alphaKeys);
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
            UpdatePowerUI();
        }

        private void ShowInitialInstruction()
        {
            if (_faceSubsystem != null)
            {
                SetInstruction("볼을 부풀려 파워 충전!");
            }
        }

        private (float leftPuff, float rightPuff, bool isPuffed) GetCheekPuffValues()
        {
            float puffValue = 0f;
            bool hasValidData = false;

            // 테스트 모드 처리 (에디터에서만)
#if UNITY_EDITOR
            if (enableTestMode && Input.GetKey(testKey))
            {
                return (1f, 1f, true);
            }
#endif

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
                
                SetInstruction("파워 충전 중...");
            }
            else if (!isPuffed && _cheekPuffed)
            {
                // Puff 종료
                _cheekPuffed = false;
                float puffDuration = Time.time - _puffStartTime;

                if (puffDuration >= requiredPuffDuration)
                {
                    float intensity = CalculateIntensity(puffDuration);
                    TriggerJumpAction(intensity, puffDuration);
                }
                else
                {
                    SetInstruction($"너무 짧습니다!");
                }
            }
            else if (_cheekPuffed)
            {
                // Puff 지속 중 - 현재 파워 레벨 표시
                float currentDuration = Time.time - _puffStartTime;
                float currentIntensity = CalculateIntensity(currentDuration);
                int powerPercent = Mathf.RoundToInt(currentIntensity * 100f);
                
                string powerLevel = GetPowerLevelText(currentIntensity);
                SetInstruction($"{powerLevel}");
            }
        }

        private float CalculateIntensity(float puffDuration)
        {
            // 최소 시간 이하는 0으로 처리
            if (puffDuration < requiredPuffDuration)
                return 0f;

            // 시간을 0~1 범위로 정규화 (requiredPuffDuration ~ maxPowerDuration)
            float normalizedTime = (puffDuration - requiredPuffDuration) / (maxPowerDuration - requiredPuffDuration);
            normalizedTime = Mathf.Clamp01(normalizedTime);

            // 곡선 적용하여 최종 강도 계산
            return intensityCalculationCurve.Evaluate(normalizedTime);
        }

        private string GetPowerLevelText(float intensity)
        {
            if (intensity < 0.2f) return "조금 부푼다..";
            else if (intensity < 0.4f) return "조금 더 부푼다..";
            else if (intensity < 0.6f) return "더 더 부푼다..!";
            else if (intensity < 0.8f) return "더 더 더 부푼다..!";
            else return "풀 파워!";
        }

        private void TriggerJumpAction(float intensity, float duration)
        {
            if (_playerMovement != null)
            {
                Debug.Log($"[FaceLandmarkTracker] Calling RequestJump with intensity: {intensity:F2} (duration: {duration:F1}s)");
                _playerMovement.RequestJump(intensity);
                
                int powerPercent = Mathf.RoundToInt(intensity * 100f);
                string powerLevel = GetPowerLevelText(intensity);
                SetInstruction($"발사! {powerPercent}% 파워");
            }
            else
            {
                Debug.Log("[FaceLandmarkTracker] PlayerMovement not found on puff end");
                //SetInstruction("PlayerMovement를 찾을 수 없습니다.");
            }
        }

        private void UpdatePowerUI()
        {
            if (!_cheekPuffed)
            {
                // 파워 UI 초기화
                if (powerSlider != null) powerSlider.value = 0f;
                if (powerFillImage != null) powerFillImage.color = powerColorGradient.Evaluate(0f);
                return;
            }

            // 현재 파워 레벨 계산
            float currentDuration = Time.time - _puffStartTime;
            float currentIntensity = CalculateIntensity(currentDuration);
            
            // 시각적 진행도 (0~1)
            float visualProgress = Mathf.Clamp01((currentDuration - requiredPuffDuration) / (maxPowerDuration - requiredPuffDuration));

            // UI 업데이트
            if (powerSlider != null)
            {
                powerSlider.value = visualProgress;
            }

            if (powerFillImage != null)
            {
                powerFillImage.color = powerColorGradient.Evaluate(currentIntensity);
            }
        }

        private void LateUpdate()
        {
            if (!enableDebugInfo) return;

            var (leftPuff, rightPuff, isPuffed) = GetCheekPuffValues();
            float puffDuration = _cheekPuffed ? Time.time - _puffStartTime : 0f;
            float currentIntensity = _cheekPuffed ? CalculateIntensity(puffDuration) : 0f;
            
            string arkitStatus = _faceSubsystem != null ? "연결됨" : "없음";
            string trackingStatus = _arFace != null ? _arFace.trackingState.ToString() : "없음";
            
            string testModeInfo = "";
#if UNITY_EDITOR
            if (enableTestMode)
            {
                testModeInfo = $"\n테스트 모드: {testKey} 키 활성화";
            }
#endif

            // string msg = $"ARKit Subsystem: {arkitStatus}\n" +
            //             $"Tracking State: {trackingStatus}\n" +
            //             $"Puff Value: {leftPuff:F3}\n" +
            //             $"Threshold: {puffBlendThreshold:F3}\n" +
            //             $"Is Puffed: {isPuffed}\n" +
            //             $"Duration: {puffDuration:F1}s / {maxPowerDuration:F1}s\n" +
            //             $"Intensity: {currentIntensity:F2} ({currentIntensity * 100:F0}%)\n" +
            //             $"PlayerMovement: {(_playerMovement != null ? "Found" : "Missing")}" +
            //             testModeInfo;
            string msg = $"볼 빵빵 {currentIntensity * 100:F0}%)\n";
            if (debugTmpText != null)
                debugTmpText.text = msg;
            else if (debugUiText != null)
                debugUiText.text = msg;
            else if (tmpText != null && !_cheekPuffed) // 충전 중이 아닐 때만 디버그 정보 표시
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

            // 시간 값 검증
            if (requiredPuffDuration >= maxPowerDuration)
            {
                maxPowerDuration = requiredPuffDuration + 1f;
            }
            
            requiredPuffDuration = Mathf.Max(0.1f, requiredPuffDuration);
            maxPowerDuration = Mathf.Max(requiredPuffDuration + 0.1f, maxPowerDuration);
        }

        /// <summary>
        /// 외부에서 현재 파워 레벨을 확인할 수 있는 메서드
        /// </summary>
        public float GetCurrentPowerLevel()
        {
            if (!_cheekPuffed) return 0f;
            
            float currentDuration = Time.time - _puffStartTime;
            return CalculateIntensity(currentDuration);
        }

        /// <summary>
        /// 현재 충전 중인지 확인하는 메서드
        /// </summary>
        public bool IsCharging()
        {
            return _cheekPuffed;
        }
    }
}