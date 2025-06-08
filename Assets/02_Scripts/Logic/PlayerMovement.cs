using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using CuteDuckGame;

/// <summary>
/// Fusion 네트워크 기반으로 플레이어 점프 이동을 처리하는 스크립트입니다.
/// 입력 권한(InputAuthority)을 가진 로컬 플레이어만 조작할 수 있으며,
/// 카메라 시점을 기준으로 물리 힘을 가해 이동을 수행합니다.
/// 강도 시스템을 통해 다양한 파워로 이동할 수 있습니다.
/// </summary>
public class PlayerMovement : NetworkBehaviour
{
    // Dead 레이어 인덱스 (Awake에서 초기화)
    private int _deadLayer; // 사망 처리용 Dead 레이어 인덱스
    private bool _isDead;

    // 사망 처리 RPC: 사망자에겐 패배 UI, 나머지에겐 승리 UI
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void OnDeathRpc()
    {
        if (_isDead)
            UIManager.Instance.ShowDefeatPanel();
        else
            UIManager.Instance.ShowWinPanel();
    }

    private Camera _playerCamera;
    private NetworkRigidbody3D _networkRigidbody;

    [Header("이동 설정")]
    public float MoveForce = 1000f;
    public float MaxSpeed = 10f;
    
    [Header("강도 기반 이동 설정")]
    [Tooltip("최소 이동 강도 (0~1)")]
    public float MinIntensity = 0.2f;
    [Tooltip("최대 이동 강도 (0~1)")]
    public float MaxIntensity = 1.0f;
    [Tooltip("강도에 따른 힘 배율 곡선")]
    public AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f);

    [Header("물리 설정")]
    public float Drag = 2f;
    public float AngularDrag = 5f;
    public float Mass = 1f;
    public float GroundCheckDistance = 1.1f;

    [Header("Audio")]
    public AudioSource[] audioSources;

    // 네트워크 동기화 변수들
    [Networked] public bool IsGrounded { get; set; }

    // 입력 처리용 (레이턴시 최적화)
    private bool _spacePressed;
    private float _lastInputTime;
    private float _currentIntensity = 1.0f; // 현재 이동 강도

    private void Awake()
    {
        // Dead 레이어 인덱스 캐싱
        _deadLayer = LayerMask.NameToLayer("dead");
        
        // 기본 강도 곡선 설정 (인스펙터에서 설정되지 않은 경우)
        if (intensityCurve.keys.Length == 0)
        {
            intensityCurve = AnimationCurve.Linear(0f, 0.2f, 1f, 1f);
        }
    }

    public override void Spawned()
    {
        // 자신의 캐릭터만 조작 가능
        if (!Object.HasInputAuthority)
        {
            enabled = false;
            return;
        }

        // NetworkRigidbody3D 컴포넌트 찾기
        _networkRigidbody = GetComponent<NetworkRigidbody3D>();
        
        if (_networkRigidbody == null)
        {
            Debug.LogError("NetworkRigidbody3D 컴포넌트가 필요합니다!");
            return;
        }

        // Rigidbody 설정
        _networkRigidbody.Rigidbody.linearDamping = Drag;
        _networkRigidbody.Rigidbody.angularDamping = AngularDrag;
        _networkRigidbody.Rigidbody.useGravity = true;
        _networkRigidbody.Rigidbody.mass = Mass;
        _networkRigidbody.Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        FindPlayerCamera();
    }

    private void FindPlayerCamera()
    {
        // LocalCamera 태그로 찾기
        GameObject localCameraObj = GameObject.FindWithTag("LocalCamera");
        if (localCameraObj != null)
        {
            _playerCamera = localCameraObj.GetComponent<Camera>();
            Debug.Log($"✅ LocalCamera 연결됨: {_playerCamera.name}");
        }
        else
        {
            Debug.LogWarning("LocalCamera를 찾을 수 없습니다!");
        }
    }

    private void Update()
    {
        // 입력 권한이 없는 클라이언트는 동작 무시
        if (!Object.HasInputAuthority) return;

        // LocalCamera가 없다면 다시 찾기
        if (_playerCamera == null)
        {
            FindPlayerCamera();
        }

        // 플랫폼별 입력 처리 (모바일 터치 / 데스크톱 키보드)
#if UNITY_ANDROID || UNITY_IOS
        // 화면 터치(첫번째 터치) 시작 시 점프 입력으로 간주
        // if (Input.touchCount > 0)
        // {
        //     Touch touch = Input.GetTouch(0);
        //     if (touch.phase == TouchPhase.Began && Time.time - _lastInputTime > 0.1f)
        //     {
        //         _spacePressed = true;
        //         _lastInputTime = Time.time;
        //     }
        // }
#else
        // 데스크톱: Space 키 입력
        if (Input.GetKeyDown(KeyCode.Space) && Time.time - _lastInputTime > 0.1f)
        {
            _spacePressed = true;
            _lastInputTime = Time.time;
        }
#endif
    }

    public override void FixedUpdateNetwork()
    {
        // 네트워크 틱마다 로컬 입력 처리 및 이동 적용
        if (!Object.HasInputAuthority) return;

        // 점프/이동 요청 처리
        if (_spacePressed)
        {
            // 요청된 점프/이동 실행
            AddCameraDirectionForce(_currentIntensity);
            _spacePressed = false;
        }

        // 바닥 감지
        CheckGrounded();
    }

    private void AddCameraDirectionForce(float intensity = 1.0f)
    {
        // 필요한 컴포넌트 체크
        if (_playerCamera == null || _networkRigidbody == null) return;

        // 강도 정규화 및 곡선 적용
        intensity = Mathf.Clamp(intensity, MinIntensity, MaxIntensity);
        float curveIntensity = intensityCurve.Evaluate(intensity);

        // 카메라 전방 벡터(Y축 제거) 계산
        Vector3 cameraForward = _playerCamera.transform.forward;
        Vector3 forceDirection = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;

        // 로컬 Rigidbody 직접 사용으로 레이턴시 제거
        Rigidbody localRb = _networkRigidbody.Rigidbody;
        Vector3 currentVelocity = localRb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);

        // 강도에 따른 힘 계산
        float finalForce = MoveForce * curveIntensity;

        // 최대 속도 제한 (강도에 따라 조정)
        float maxSpeedForThisMove = MaxSpeed * curveIntensity;
        
        if (horizontalVelocity.magnitude < maxSpeedForThisMove)
        {
            // 즉시 로컬 물리 적용 (네트워크 지연 없음)
            localRb.AddForce(forceDirection * finalForce, ForceMode.Force);
            
            Debug.Log($"이동 강도: {intensity:F2} -> 곡선 적용: {curveIntensity:F2} -> 최종 힘: {finalForce:F0}");
        }

        // Play random audio when grounded (강도에 따라 볼륨 조절)
        if (IsGrounded && audioSources != null && audioSources.Length > 0)
        {
            int randomIndex = Random.Range(0, audioSources.Length);
            AudioSource selectedAudio = audioSources[randomIndex];
            selectedAudio.volume = 0.5f + (curveIntensity * 0.5f); // 50% ~ 100% 볼륨
            selectedAudio.Play();
        }
    }

    private void CheckGrounded()
    {
        // NetworkRigidbody3D의 정확한 위치 사용
        Vector3 position = _networkRigidbody.Rigidbody.position;
        Vector3 rayOrigin = position + Vector3.up * 0.1f;
        
        // 바닥 감지 (QueryTriggerInteraction.Ignore로 성능 향상)
        IsGrounded = Physics.Raycast(rayOrigin, Vector3.down, GroundCheckDistance, 
            LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore);
    }

    // 외부에서 힘을 가할 수 있는 메서드
    public void AddForce(Vector3 force, ForceMode forceMode = ForceMode.Impulse)
    {
        if (Object.HasInputAuthority && _networkRigidbody != null)
        {
            _networkRigidbody.Rigidbody.AddForce(force, forceMode);
        }
    }

    // 폭발 효과
    public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius)
    {
        if (Object.HasInputAuthority && _networkRigidbody != null)
        {
            _networkRigidbody.Rigidbody.AddExplosionForce(explosionForce, explosionPosition, explosionRadius);
        }
    }

    // 텔레포트 메서드
    public void TeleportTo(Vector3 position)
    {
        if (Object.HasInputAuthority && _networkRigidbody != null)
        {
            _networkRigidbody.Rigidbody.position = position;
            _networkRigidbody.Rigidbody.linearVelocity = Vector3.zero;
            _networkRigidbody.Rigidbody.angularVelocity = Vector3.zero;
            transform.position = position;
        }
    }

    // 즉시 정지
    public void Stop()
    {
        if (Object.HasInputAuthority && _networkRigidbody != null)
        {
            _networkRigidbody.Rigidbody.linearVelocity = Vector3.zero;
            _networkRigidbody.Rigidbody.angularVelocity = Vector3.zero;
        }
    }

    // 컴포넌트 상태 확인
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (GetComponent<NetworkRigidbody3D>() == null)
        {
            Debug.LogWarning($"{name}: NetworkRigidbody3D 컴포넌트가 필요합니다!");
        }
        
        // 강도 값 검증
        MinIntensity = Mathf.Clamp01(MinIntensity);
        MaxIntensity = Mathf.Clamp01(MaxIntensity);
        if (MinIntensity > MaxIntensity)
        {
            MinIntensity = MaxIntensity;
        }
    }

    /// <summary>
    /// Dead 레이어에 닿으면 패배 UI를 활성화합니다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!Object.HasInputAuthority || _isDead) return;
        // 데드 레이어 충돌 처리
        if (other.gameObject.layer == _deadLayer)
        {
            _isDead = true;
            // 즉시 이동 및 물리 정지
            Stop();
            // 사망 RPC 호출
            OnDeathRpc();
        }
    }

    /// <summary>
    /// 외부 입력(예: 얼굴 AR 제스처)으로 점프/이동을 요청합니다.
    /// </summary>
    /// <param name="intensity">이동 강도 (0~1, 기본값 1.0)</param>
    public void RequestJump(float intensity = 1.0f)
    {
        // 쿨타임 검사 후 다음 FixedUpdateNetwork에서 처리되도록 플래그 설정
        if (Time.time - _lastInputTime > 0.1f)
        {
            _currentIntensity = intensity;
            _spacePressed = true;
            _lastInputTime = Time.time;
        }
    }

    /// <summary>
    /// 현재 이동 강도를 가져옵니다.
    /// </summary>
    public float GetCurrentIntensity()
    {
        return _currentIntensity;
    }

    /// <summary>
    /// 강도 곡선을 통해 실제 적용될 강도를 계산합니다.
    /// </summary>
    /// <param name="rawIntensity">원본 강도 (0~1)</param>
    /// <returns>곡선이 적용된 강도</returns>
    public float CalculateIntensity(float rawIntensity)
    {
        float clampedIntensity = Mathf.Clamp(rawIntensity, MinIntensity, MaxIntensity);
        return intensityCurve.Evaluate(clampedIntensity);
    }
}