using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    private Rigidbody _rigidbody;
    private Camera _mainCamera;

    [Header("이동 설정")]
    public float CameraForce = 15f; // 스페이스바로 가하는 힘
    public float MaxSpeed = 8f;     // 최대 속도 제한

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _mainCamera = Camera.main;
        
        if (_rigidbody != null)
        {
            _rigidbody.freezeRotation = true; // 회전 고정
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
    }

    private void Update()
    {
        // 스페이스바로 카메라 방향 AddForce
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddCameraDirectionForce();
        }
    }

    private void AddCameraDirectionForce()
    {
        if (_rigidbody == null || _mainCamera == null) return;
        
        // 카메라가 보는 방향 (Y축 제외한 수평 방향만)
        Vector3 cameraForward = _mainCamera.transform.forward;
        Vector3 forceDirection = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;
        
        // 힘 적용
        _rigidbody.AddForce(forceDirection * CameraForce, ForceMode.Impulse);
        
        Debug.Log($"카메라 방향으로 힘 적용: {forceDirection}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority || _rigidbody == null) return;

        // 속도 제한
        Vector3 horizontalVelocity = new Vector3(_rigidbody.linearVelocity.x, 0, _rigidbody.linearVelocity.z);
        if (horizontalVelocity.magnitude > MaxSpeed)
        {
            Vector3 limitedVelocity = horizontalVelocity.normalized * MaxSpeed;
            _rigidbody.linearVelocity = new Vector3(limitedVelocity.x, _rigidbody.linearVelocity.y, limitedVelocity.z);
        }
    }

    // 외부에서 힘을 가할 수 있는 메서드 (오리와 상호작용 등에 사용)
    public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
    {
        if (_rigidbody != null)
        {
            _rigidbody.AddForce(force, mode);
        }
    }
}