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

    // 네트워크 동기화 변수들
    [Networked] public bool IsGrounded { get; set; }

    // 입력 처리용
    private bool _spacePressed;

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
        
        Debug.Log($"NetworkRigidbody3D Player spawned - Gravity: {_networkRigidbody.Rigidbody.useGravity}");
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

        // Space키 입력 수집
        _spacePressed = _spacePressed || Input.GetKeyDown(KeyCode.Space);
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

        // 현재 수평 속도 체크
        Vector3 currentVelocity = _networkRigidbody.Rigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);

        // 최대 속도 제한
        if (horizontalVelocity.magnitude < MaxSpeed)
        {
            // 카메라 방향으로 힘 적용
            _networkRigidbody.Rigidbody.AddForce(forceDirection * MoveForce, ForceMode.Force);
            
            Debug.Log($"카메라 방향 이동: {forceDirection} x {MoveForce}");
        }
    }

    private void CheckGrounded()
    {
        // NetworkRigidbody3D의 정확한 위치 사용
        Vector3 position = _networkRigidbody.Rigidbody.position;
        Vector3 rayOrigin = position + Vector3.up * 0.1f;
        
        // 바닥 감지
        RaycastHit hit;
        IsGrounded = Physics.Raycast(rayOrigin, Vector3.down, out hit, GroundCheckDistance);
        
        // 디버그용 (Scene View에서만 보임)
        Debug.DrawRay(rayOrigin, Vector3.down * GroundCheckDistance, IsGrounded ? Color.green : Color.red);
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

    // 디버그용 Gizmo
    void OnDrawGizmosSelected()
    {
        if (_networkRigidbody == null) return;

        // 바닥 감지 Ray 표시
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Vector3 position = _networkRigidbody.Rigidbody.position;
        Vector3 rayOrigin = position + Vector3.up * 0.1f;
        Gizmos.DrawRay(rayOrigin, Vector3.down * GroundCheckDistance);
        
        // 속도 벡터 표시
        Gizmos.color = Color.blue;
        Vector3 velocity = _networkRigidbody.Rigidbody.linearVelocity;
        Gizmos.DrawRay(position, velocity);
    }

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