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
        
        // ESC 키 처리 (GetKeyDown으로 최적화)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }
    }
    
    void FixedUpdate()
    {
        // 물리 기반 이동은 FixedUpdate에서 처리 (성능 향상)
        HandleMovement();
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
        
        // 거리가 너무 작으면 충돌 검사 생략 (성능 최적화)
        if (distance < 0.001f) return;
        
        // SphereCast로 충돌 검사 (최적화: QueryTriggerInteraction.Ignore)
        if (Physics.SphereCast(currentPosition, collisionRadius, direction, out RaycastHit hit, 
            distance + collisionOffset, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            // 충돌이 감지되면 안전한 위치까지만 이동
            float safeDistance = Mathf.Max(0, hit.distance - collisionOffset);
            Vector3 safePosition = currentPosition + direction * safeDistance;
            
            // 간단한 이동만 수행 (복잡한 슬라이드 제거)
            transform.position = safePosition;
            _lastValidPosition = safePosition;
        }
        else
        {
            // 충돌이 없으면 정상 이동
            transform.position = targetPosition;
            _lastValidPosition = targetPosition;
        }
    }
    
    // 복잡한 슬라이드 이동 시스템 제거 (성능 향상)
    
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
    
    // 기즈모 제거 (Release 빌드에서 성능 향상)
}