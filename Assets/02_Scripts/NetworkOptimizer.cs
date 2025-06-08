using UnityEngine;
using Fusion;

/// <summary>
/// MCP와 함께 사용하는 런타임 네트워크 최적화 매니저
/// 레이턴시 감소와 물리 처리 성능 향상에 집중
/// </summary>
public class NetworkOptimizer : MonoBehaviour
{
    [Header("네트워크 최적화 설정")]
    [SerializeField] private bool enableClientPrediction = true;
    [SerializeField] private bool enablePhysicsOptimization = true;
    [SerializeField] private float networkCullDistance = 50f;
    
    [Header("물리 최적화")]
    [SerializeField] private int maxPhysicsUpdatesPerFrame = 10; // 극한 최적화: 10회/프레임
    [SerializeField] private float physicsTimeStep = 0.005f; // 극한 최적화: 200Hz
    
    // 최적화 상태 추적
    private int currentPhysicsUpdates = 0;
    private float lastPhysicsUpdate = 0f;
    
    private void Start()
    {
        InitializeNetworkOptimizations();
    }
    
    private void Update()
    {
        if (enablePhysicsOptimization)
        {
            OptimizePhysicsUpdates();
        }
        
        // 프레임마다 카운터 리셋
        currentPhysicsUpdates = 0;
    }
    
    private void InitializeNetworkOptimizations()
    {
        // Unity Physics 극한 최적화 설정
        if (enablePhysicsOptimization)
        {
            Physics.defaultMaxDepenetrationVelocity = 50f; // 극한: 50f
            Physics.defaultMaxAngularSpeed = 50f; // 극한: 50f  
            Physics.bounceThreshold = 0.1f; // 극한: 0.1f (매우 민감)
            Physics.sleepThreshold = 0.001f; // 극한: 0.001f (거의 안 잠들기)
            Physics.defaultSolverIterations = 8; // 기본 6→8로 정확도 향상
            Physics.defaultSolverVelocityIterations = 2; // 기본 1→2로 속도 정확도 향상
        }
        
        // 네트워크 컬링 설정
        SetupNetworkCulling();
        
        // 네트워크 최적화 완료 (로깅 제거)
    }
    
    private void OptimizePhysicsUpdates()
    {
        // 물리 업데이트 빈도 제한
        if (Time.time - lastPhysicsUpdate < physicsTimeStep) return;
        if (currentPhysicsUpdates >= maxPhysicsUpdatesPerFrame) return;
        
        lastPhysicsUpdate = Time.time;
        currentPhysicsUpdates++;
    }
    
    private void SetupNetworkCulling()
    {
        // 거리 기반 네트워크 오브젝트 컬링
        var networkObjects = FindObjectsOfType<NetworkObject>();
        foreach (var netObj in networkObjects)
        {
            if (netObj.GetComponent<NetworkCulling>() == null)
            {
                var culling = netObj.gameObject.AddComponent<NetworkCulling>();
                culling.maxDistance = networkCullDistance;
            }
        }
    }
    
    /// <summary>
    /// 물리 충돌 최적화 (간결화)
    /// </summary>
    public bool ShouldProcessPhysics() => currentPhysicsUpdates < maxPhysicsUpdatesPerFrame;
    
    // MCP 연동용 정적 메소드들 (간결화)
    public static void OptimizeNetworkSettings() => 
        FindObjectOfType<NetworkOptimizer>()?.InitializeNetworkOptimizations();
    
    public static void TogglePhysicsOptimization(bool enable) {
        var optimizer = FindObjectOfType<NetworkOptimizer>();
        if (optimizer != null) optimizer.enablePhysicsOptimization = enable;
    }
    
    // OnGUI 제거로 런타임 성능 향상
}

/// <summary>
/// 개별 네트워크 오브젝트용 컬링 컴포넌트
/// </summary>
public class NetworkCulling : MonoBehaviour
{
    public float maxDistance = 50f;
    private NetworkObject networkObject;
    private Camera playerCamera;
    
    private void Start()
    {
        networkObject = GetComponent<NetworkObject>();
        playerCamera = Camera.main;
    }
    
    private void Update()
    {
        if (networkObject == null || playerCamera == null) return;
        
        // 새로 접속한 플레이어는 항상 기존 오브젝트들을 볼 수 있도록
        if (!networkObject.HasInputAuthority)
        {
            // 다른 플레이어의 오브젝트는 항상 활성화 (물리 연산 보장)
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            return;
        }
        
        // 자신의 오브젝트만 거리 기반 컬링 적용
        float distance = Vector3.Distance(transform.position, playerCamera.transform.position);
        bool shouldBeActive = distance <= maxDistance;
        if (gameObject.activeSelf != shouldBeActive)
        {
            gameObject.SetActive(shouldBeActive);
        }
    }
} 