using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    private Camera _playerCamera;
    private NetworkRigidbody3D _networkRigidbody;

    [Header("이동 설정")]
    public float MoveForce = 500f;
    public float MaxSpeed = 10f;

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
        
        // Debug.Log 제거 (성능 향상)
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
        if (!Object.HasInputAuthority) return;

        // LocalCamera가 없다면 다시 찾기
        if (_playerCamera == null)
        {
            FindPlayerCamera();
        }

        // Space키 입력 수집 (중복 입력 방지로 성능 향상)
        if (Input.GetKeyDown(KeyCode.Space) && Time.time - _lastInputTime > 0.1f)
        {
            _spacePressed = true;
            _lastInputTime = Time.time;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority) return;

        // Space키 처리
        if (_spacePressed)
        {
            AddCameraDirectionForce();
            _spacePressed = false;
        }

        // 바닥 감지
        CheckGrounded();
    }

    private void AddCameraDirectionForce()
    {
        if (_playerCamera == null || _networkRigidbody == null) return;

        // 카메라가 바라보는 방향 (Y축 제외한 수평 방향만)
        Vector3 cameraForward = _playerCamera.transform.forward;
        Vector3 forceDirection = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;

        // 로컬 Rigidbody 직접 사용으로 레이턴시 제거
        Rigidbody localRb = _networkRigidbody.Rigidbody;
        Vector3 currentVelocity = localRb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);

        // 최대 속도 제한
        if (horizontalVelocity.magnitude < MaxSpeed)
        {
            // 즉시 로컬 물리 적용 (네트워크 지연 없음)
            localRb.AddForce(forceDirection * MoveForce, ForceMode.Force);
        }

        // Play random audio when grounded
        if (IsGrounded && audioSources != null && audioSources.Length > 0)
        {
            int randomIndex = Random.Range(0, audioSources.Length);
            audioSources[randomIndex].Play();
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

    // 디버그 기즈모 제거 (성능 향상)

    // 컴포넌트 상태 확인
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (GetComponent<NetworkRigidbody3D>() == null)
        {
            Debug.LogWarning($"{name}: NetworkRigidbody3D 컴포넌트가 필요합니다!");
        }
    }
}