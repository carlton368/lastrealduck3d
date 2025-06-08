using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    public float moveSpeed = 10f;
    public float mouseSensitivity = 2f;
    public float fastMoveMultiplier = 3f; // Shift로 빠른 이동
    
    [Header("충돌 감지 설정")]
    public LayerMask collisionLayers = -1; // 충돌을 감지할 레이어
    public float collisionRadius = 0.3f; // 카메라 충돌 반경
    public float collisionOffset = 0.1f; // 벽과의 최소 거리
    
    private Camera _camera;
    private float _rotationX = 0f;
    private Vector3 _lastValidPosition;
    
    void Start()
    {
        _camera = GetComponent<Camera>();
        _lastValidPosition = transform.position;
        
        // 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        
        // ESC로 커서 해제/잠금 토글
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }
    }
    
    private void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // 수평 회전 (Y축)
        transform.Rotate(Vector3.up * mouseX);
        
        // 수직 회전 (X축) - 제한
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, -90f, 90f);
        
        // 카메라의 수직 회전 적용
        Vector3 currentRotation = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(_rotationX, currentRotation.y, 0);
    }
    
    private void HandleMovement()
    {
        Vector3 moveVector = Vector3.zero;
        
        // 현재 속도 계산 (Shift로 빠른 이동)
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            currentSpeed *= fastMoveMultiplier;
        
        // WASD 이동
        if (Input.GetKey(KeyCode.W)) moveVector += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveVector -= transform.forward;
        if (Input.GetKey(KeyCode.A)) moveVector -= transform.right;
        if (Input.GetKey(KeyCode.D)) moveVector += transform.right;
        
        // 상하 이동
        if (Input.GetKey(KeyCode.E)) 
            moveVector += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) 
            moveVector -= Vector3.up;
        
        // 이동 적용
        if (moveVector.magnitude > 0)
        {
            moveVector.Normalize();
            Vector3 targetPosition = transform.position + moveVector * currentSpeed * Time.deltaTime;
            
            // 충돌 검사 후 이동
            MoveWithCollisionCheck(targetPosition);
        }
    }
    
    private void MoveWithCollisionCheck(Vector3 targetPosition)
    {
        Vector3 currentPosition = transform.position;
        Vector3 direction = (targetPosition - currentPosition).normalized;
        float distance = Vector3.Distance(currentPosition, targetPosition);
        
        // SphereCast로 충돌 검사
        if (Physics.SphereCast(currentPosition, collisionRadius, direction, out RaycastHit hit, distance + collisionOffset, collisionLayers))
        {
            // 충돌이 감지되면 안전한 위치까지만 이동
            float safeDistance = Mathf.Max(0, hit.distance - collisionOffset);
            Vector3 safePosition = currentPosition + direction * safeDistance;
            
            // 각 축별로 개별 이동 시도 (벽을 따라 미끄러지듯 이동)
            TrySlideMovement(targetPosition, safePosition);
        }
        else
        {
            // 충돌이 없으면 정상 이동
            transform.position = targetPosition;
            _lastValidPosition = targetPosition;
        }
    }
    
    private void TrySlideMovement(Vector3 targetPosition, Vector3 blockedPosition)
    {
        Vector3 currentPosition = transform.position;
        
        // X축 이동 시도
        Vector3 xMovement = new Vector3(targetPosition.x, currentPosition.y, currentPosition.z);
        if (IsPositionSafe(xMovement))
        {
            transform.position = xMovement;
            _lastValidPosition = xMovement;
            return;
        }
        
        // Z축 이동 시도
        Vector3 zMovement = new Vector3(currentPosition.x, currentPosition.y, targetPosition.z);
        if (IsPositionSafe(zMovement))
        {
            transform.position = zMovement;
            _lastValidPosition = zMovement;
            return;
        }
        
        // Y축 이동 시도
        Vector3 yMovement = new Vector3(currentPosition.x, targetPosition.y, currentPosition.z);
        if (IsPositionSafe(yMovement))
        {
            transform.position = yMovement;
            _lastValidPosition = yMovement;
            return;
        }
        
        // 모든 축에서 막혔으면 제자리에 유지
        transform.position = _lastValidPosition;
    }
    
    private bool IsPositionSafe(Vector3 position)
    {
        // 해당 위치에서 충돌이 있는지 확인
        return !Physics.CheckSphere(position, collisionRadius, collisionLayers);
    }
    
    private void ToggleCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    // 기즈모로 충돌 반경 시각화
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
        
        // 이동 방향 표시
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}