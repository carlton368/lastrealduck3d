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
        
        // 입력 플랫폼에 따른 커서 토글
#if !UNITY_ANDROID && !UNITY_IOS
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }
#else
        // 모바일: 두 손가락 탭으로 커서 토글 (디버그용)
        if (Input.touchCount == 2 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            ToggleCursor();
        }
#endif
    }
    
    void FixedUpdate()
    {
        // 물리 기반 이동은 FixedUpdate에서 처리 (성능 향상)
        HandleMovement();
    }
    
    private void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        
        float mouseX = 0f;
        float mouseY = 0f;

#if UNITY_ANDROID || UNITY_IOS
        // 모바일: 터치 드래그로 시선 회전 (첫번째 터치)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Moved)
            {
                // deltaPosition은 프레임 사이 픽셀 이동량이므로 감도 보정 필요
                mouseX = touch.deltaPosition.x * mouseSensitivity * 0.02f;
                mouseY = touch.deltaPosition.y * mouseSensitivity * 0.02f;
            }
        }
#else
        // 데스크톱: 마우스 이동
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif
        
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
        // 데스크톱 전용 이동 처리
#if !UNITY_ANDROID && !UNITY_IOS
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
#endif

        // 모바일 전용 이동 처리 (두 번째 손가락 드래그)
#if UNITY_ANDROID || UNITY_IOS
        Vector3 moveVector = Vector3.zero;

        // 두 번째 손가락의 이동량을 카메라 이동으로 사용
        if (Input.touchCount > 1)
        {
            Touch moveTouch = Input.GetTouch(1);

            if (moveTouch.phase == TouchPhase.Moved || moveTouch.phase == TouchPhase.Stationary)
            {
                // deltaPosition은 한 프레임 동안의 픽셀 이동량이므로 감도 보정을 위해 스케일 계수 적용
                Vector2 moveDelta = moveTouch.deltaPosition * 0.02f;

                // 전후(Delta Y), 좌우(Delta X) 이동 반영
                moveVector += transform.forward * moveDelta.y;
                moveVector += transform.right * moveDelta.x;
            }
        }

        if (moveVector.sqrMagnitude > 0.0001f)
        {
            float currentSpeed = moveSpeed;
            Vector3 targetPosition = transform.position + moveVector.normalized * currentSpeed * Time.deltaTime;

            // 충돌 검사 후 이동
            MoveWithCollisionCheck(targetPosition);
        }
#endif
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