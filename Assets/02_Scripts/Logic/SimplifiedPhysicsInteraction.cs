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
    
    [Header("로컬 즉시 이펙트")]
    [SerializeField] private GameObject localEffectPrefab; // 로컬 전용 약한 이펙트
    [SerializeField] private float localEffectIntensity = 0.6f; // 로컬 이펙트 강도 (60%)
    [SerializeField] private float localEffectDuration = 1.5f; // 로컬 이펙트 지속 시간
    
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
        
        // 💥 1단계: 로컬 즉시 이펙트 재생 (네트워크 지연 없음)
        Vector3 collisionPoint = collision.contacts[0].point;
        PlayLocalImmediateEffect(collisionPoint, impulse);
        
        // 즉시 충돌 처리 (분리된 메서드 호출 제거로 성능 향상)
        Vector3 force = -collision.contacts[0].normal * impulse * pushbackMultiplier;
        rb?.AddForce(force, ForceMode.Impulse); // 즉시 100% 적용
        
        // 네트워크 동기화 (쿨다운 없이 즉시)
        if (currentTime - lastGlobalRpcTime >= GLOBAL_RPC_COOLDOWN)
        {
            lastGlobalRpcTime = currentTime;
            SimpleCollisionRpc(otherId, force, collisionPoint, impulse);
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
    
    /// <summary>
    /// 🚀 로컬 즉시 이펙트 - 네트워크 지연 없이 즉시 재생
    /// </summary>
    private void PlayLocalImmediateEffect(Vector3 position, float intensity)
    {
        // 효과 임계값 체크
        if (intensity < minCollisionForce) return;
        
        // 로컬 파티클 이펙트 재생
        GameObject effectToUse = localEffectPrefab != null ? localEffectPrefab : collisionEffectPrefab;
        if (effectToUse != null)
        {
            var localEffect = Instantiate(effectToUse, position, Quaternion.identity);
            
            // 로컬 이펙트는 약간 작게 (예측 이펙트임을 시각적으로 표현)
            float localScale = Mathf.Clamp(intensity / 10f * localEffectIntensity, 0.3f, 1.2f);
            localEffect.transform.localScale = Vector3.one * localScale;
            
            // 로컬 이펙트 태그 설정 (나중에 구분 가능)
            TrySetTag(localEffect, "LocalEffect");
            
            // 로컬 이펙트는 짧게 지속
            Destroy(localEffect, localEffectDuration);
        }
        
        // 로컬 사운드 재생 (약간 작은 볼륨)
        if (audioSource != null && collisionSounds.Length > 0)
        {
            int soundIndex = Mathf.FloorToInt(intensity / 5f);
            soundIndex = Mathf.Clamp(soundIndex, 0, collisionSounds.Length - 1);
            
            // 로컬 이펙트는 60% 볼륨으로 재생
            float originalVolume = audioSource.volume;
            audioSource.volume = originalVolume * localEffectIntensity;
            audioSource.PlayOneShot(collisionSounds[soundIndex]);
            audioSource.volume = originalVolume; // 볼륨 복원
        }
        else
        {
            // 📢 사운드 재생 실패 원인 진단 로그
            if (audioSource == null)
            {
                Debug.LogWarning($"[Sound Check] AudioSource is NULL on {gameObject.name}. Cannot play sound.");
            }
            if (collisionSounds.Length == 0)
            {
                Debug.LogWarning($"[Sound Check] CollisionSounds array is EMPTY on {gameObject.name}. No sounds to play.");
            }
        }
    }
    
    /// <summary>
    /// 서버 검증 후 확정 이펙트 (기존 메서드)
    /// </summary>
    private void PlayCollisionEffect(Vector3 position, float intensity)
    {
        // 기존 효과 로직 (확정 이펙트)
        if (collisionEffectPrefab != null)
        {
            var effect = Instantiate(collisionEffectPrefab, position, Quaternion.identity);
            float scale = Mathf.Clamp(intensity / 10f, 0.5f, 2f);
            effect.transform.localScale = Vector3.one * scale;
            
            // 확정 이펙트 태그 설정
            TrySetTag(effect, "ConfirmedEffect");
            
            Destroy(effect, 2f);
        }
        
        if (audioSource != null && collisionSounds.Length > 0)
        {
            int soundIndex = Mathf.FloorToInt(intensity / 5f);
            soundIndex = Mathf.Clamp(soundIndex, 0, collisionSounds.Length - 1);
            audioSource.PlayOneShot(collisionSounds[soundIndex]);
        }
    }
    
    /// <summary>
    /// 안전하게 태그를 설정하는 헬퍼 메서드
    /// </summary>
    private void TrySetTag(GameObject obj, string tagName)
    {
        try
        {
            obj.tag = tagName;
        }
        catch (UnityException)
        {
            // 태그가 없으면 무시 (기능에는 영향 없음)
            Debug.LogWarning($"Tag '{tagName}' not found. Effect will work without tag.");
        }
    }
    
    #endregion

    
    // OnGUI 제거로 성능 향상
}