using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    public float moveSpeed = 10f;
    public float mouseSensitivity = 2f;
    
    private Camera _camera;
    private float _rotationX = 0f;
    
    void Start()
    {
        _camera = GetComponent<Camera>();
        
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
        
        // WASD 이동
        if (Input.GetKey(KeyCode.W)) moveVector += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveVector -= transform.forward;
        if (Input.GetKey(KeyCode.A)) moveVector -= transform.right;
        if (Input.GetKey(KeyCode.D)) moveVector += transform.right;
        
        // 상하 이동
        if (Input.GetKey(KeyCode.E)) 
            moveVector += Vector3.up;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftShift)) 
            moveVector -= Vector3.up;
        
        // 이동 적용
        if (moveVector.magnitude > 0)
        {
            moveVector.Normalize();
            transform.position += moveVector * moveSpeed * Time.deltaTime;
        }
    }
}