using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using System.Collections.Generic;

// 기존 PlayerMovement와 완벽하게 호환되는 충돌 전용 물리 상호작용
public class SimplifiedPhysicsInteraction : NetworkBehaviour
{
    [Header("충돌 설정")]
    [SerializeField] private float minCollisionForce = 2f;
    [SerializeField] private float pushbackMultiplier = 1.5f;
    [SerializeField] private bool enablePlayerPushback = true;
    [SerializeField] private bool enableObjectInteraction = true;
    [SerializeField] private float collisionCooldown = 0.01f; // 극한 최적화: 0.01초
    
    [Header("상호작용 효과")]
    [SerializeField] private GameObject collisionEffectPrefab;
    [SerializeField] private AudioClip[] collisionSounds;
    [SerializeField] private float effectThreshold = 5f;
    
    // 컴포넌트 참조
    private NetworkRigidbody3D networkRigidbody;
    private PlayerMovement playerMovement;
    private AudioSource audioSource;
    private Rigidbody rb;
    
    // 네트워크 변수 (최소화)
    [Networked] public float LastCollisionTime { get; set; }
    
    // 네트워크 최적화 연동
    private NetworkOptimizer networkOptimizer;
    
    // 충돌 추적
    private Dictionary<NetworkId, float> lastCollisionTimes = new Dictionary<NetworkId, float>();
    
    // 최적화: 간단한 쿨다운 시스템으로 변경
    private float lastGlobalRpcTime = 0f;
    private const float GLOBAL_RPC_COOLDOWN = 0.01f; // 극한 최적화: 0.01초 (100Hz)
    
    public override void Spawned()
    {
        // 컴포넌트 초기화
        networkRigidbody = GetComponent<NetworkRigidbody3D>();
        playerMovement = GetComponent<PlayerMovement>();
        audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();
        
        // 자신의 캐릭터가 아니면 비활성화
        if (!Object.HasInputAuthority)
        {
            enabled = false;
            return;
        }
        
        // 네트워크 최적화 연동
        networkOptimizer = FindObjectOfType<NetworkOptimizer>();
        
        // 초기화 완료 (로깅 제거)
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority) return;
        
        // 충돌 정리 빈도 감소 (성능 최적화: 2초마다)
        if (Runner.Tick % 120 == 0) CleanupCollisions();
    }
    
    #region Collision Detection
    
    // 최적화: OnCollisionEnter만 사용 (OnCollisionStay 제거로 성능 향상)
    private void OnCollisionEnter(Collision collision)
    {
        if (!Object.HasInputAuthority) return;
        
        HandlePhysicsCollision(collision);
    }
    
        private void HandlePhysicsCollision(Collision collision)
    {
        var otherNetworkObject = collision.gameObject.GetComponent<NetworkObject>();
        if (otherNetworkObject == null) return;

        // 극한 최적화: 조건문 최소화
        NetworkId otherId = otherNetworkObject.Id;
        float currentTime = Runner.SimulationTime;
        
        // 쿨다운 체크 간소화
        if (lastCollisionTimes.TryGetValue(otherId, out float lastTime) && 
            currentTime - lastTime < collisionCooldown) return;
        
        // 즉시 계산 및 적용 (중간 변수 제거)
        float impulse = collision.relativeVelocity.magnitude * (rb?.mass ?? 1f);
        if (impulse < minCollisionForce) return;
        
        lastCollisionTimes[otherId] = currentTime;
        LastCollisionTime = currentTime;
        
        // 즉시 충돌 처리 (분리된 메서드 호출 제거로 성능 향상)
        Vector3 force = -collision.contacts[0].normal * impulse * pushbackMultiplier;
        rb?.AddForce(force, ForceMode.Impulse); // 즉시 100% 적용
        
        // 네트워크 동기화 (쿨다운 없이 즉시)
        if (currentTime - lastGlobalRpcTime >= GLOBAL_RPC_COOLDOWN)
        {
            lastGlobalRpcTime = currentTime;
            SimpleCollisionRpc(otherId, force, collision.contacts[0].point, impulse);
        }
    }
    
    private float CalculateCollisionImpulse(Collision collision)
    {
        if (rb != null && collision.rigidbody != null)
        {
            Vector3 relativeVelocity = rb.linearVelocity - collision.rigidbody.linearVelocity;
            float combinedMass = rb.mass + collision.rigidbody.mass;
            return relativeVelocity.magnitude * combinedMass;
        }
        
        return collision.relativeVelocity.magnitude * (rb?.mass ?? 1f);
    }
    
    #endregion
    

    
    #region Utility Methods
    
    private void CleanupCollisions()
    {
        // 로컬 충돌 시간 정리만 수행 (성능 향상)
        if (lastCollisionTimes.Count > 10) // 10개 초과시만 정리
        {
            var keysToRemove = new List<NetworkId>();
            foreach (var kvp in lastCollisionTimes)
            {
                if (Runner.SimulationTime - kvp.Value > 3f) // 3초 타임아웃
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                lastCollisionTimes.Remove(key);
            }
        }
    }
    

    
    
    // 새로운 최적화 메서드들
    private void ApplyLocalCollisionEffect(Collision collision, float impulse)
    {
        // 즉시 100% 로컬 적용 (레이턴시 최소화)
        Vector3 force = CalculateCollisionForce(collision, impulse);
        if (rb != null)
        {
            rb.AddForce(force, ForceMode.Impulse); // 100% 즉시 적용
        }
    }
    
    private void ProcessCollisionNetwork(NetworkObject other, Collision collision, float impulse)
    {
        // NetworkOptimizer 연동 확인
        if (networkOptimizer != null && !networkOptimizer.ShouldProcessPhysics()) return;
        
        // 글로벌 RPC 쿨다운 확인 (성능 최적화)
        if (Runner.SimulationTime - lastGlobalRpcTime < GLOBAL_RPC_COOLDOWN) return;
        
        lastGlobalRpcTime = Runner.SimulationTime;
        
        Vector3 force = CalculateCollisionForce(collision, impulse);
        Vector3 position = collision.contacts[0].point;
        
        // 간단한 개별 RPC 호출
        SimpleCollisionRpc(other.Id, force, position, impulse);
    }
    
    private Vector3 CalculateCollisionForce(Collision collision, float impulse)
    {
        Vector3 normal = collision.contacts[0].normal;
        return -normal * impulse * pushbackMultiplier;
    }
    
    // ShowLocalEffect 메서드 제거로 호출 오버헤드 감소
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SimpleCollisionRpc(NetworkId otherId, Vector3 force, Vector3 position, float intensity)
    {
        var otherObject = Runner.FindObject(otherId);
        if (otherObject == null) return;
        
        // 극한 최적화: 즉시 강력한 힘 적용 + 강제 업데이트
        var otherRb = otherObject.GetComponent<Rigidbody>();
        if (otherRb != null)
        {
            otherRb.AddForce(force * 2f, ForceMode.Impulse); // 200% 극강화
            otherRb.WakeUp(); // 강제 깨우기
        }
        
        // 효과 재생 (간소화)
        if (intensity > effectThreshold)
        {
            PlayCollisionEffect(position, intensity);
        }
    }
    
    private void PlayCollisionEffect(Vector3 position, float intensity)
    {
        // 기존 효과 로직
        if (collisionEffectPrefab != null)
        {
            var effect = Instantiate(collisionEffectPrefab, position, Quaternion.identity);
            float scale = Mathf.Clamp(intensity / 10f, 0.5f, 2f);
            effect.transform.localScale = Vector3.one * scale;
            Destroy(effect, 2f);
        }
        
        if (audioSource != null && collisionSounds.Length > 0)
        {
            int soundIndex = Mathf.FloorToInt(intensity / 5f);
            soundIndex = Mathf.Clamp(soundIndex, 0, collisionSounds.Length - 1);
            audioSource.PlayOneShot(collisionSounds[soundIndex]);
        }
    }
    
    #endregion

    
    // OnGUI 제거로 성능 향상
}