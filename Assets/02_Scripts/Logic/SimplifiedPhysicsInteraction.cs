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
    [SerializeField] private float collisionCooldown = 0.1f;
    
    [Header("상호작용 효과")]
    [SerializeField] private GameObject collisionEffectPrefab;
    [SerializeField] private AudioClip[] collisionSounds;
    [SerializeField] private float effectThreshold = 5f;
    
    // 컴포넌트 참조
    private NetworkRigidbody3D networkRigidbody;
    private PlayerMovement playerMovement;
    private AudioSource audioSource;
    private Rigidbody rb;
    
    // 네트워크 변수
    [Networked] public NetworkDictionary<NetworkId, float> ActiveCollisions => default;
    [Networked] public float LastCollisionTime { get; set; }
    
    // 충돌 추적
    private Dictionary<NetworkId, float> lastCollisionTimes = new Dictionary<NetworkId, float>();
    
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
        
        Debug.Log("SimplifiedPhysicsInteraction 초기화 완료");
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority) return;
        
        // 충돌 정리 (0.5초마다)
        if (Runner.Tick % 30 == 0)
        {
            CleanupCollisions();
        }
    }
    
    #region Fusion v2 Collision Detection
    
    // Fusion v2에서 권장: OnTriggerStay 사용
    private void OnTriggerStay(Collider other)
    {
        if (!Object.HasInputAuthority) return;
        
        HandleTriggerInteraction(other, true);
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!Object.HasInputAuthority) return;
        
        HandleTriggerInteraction(other, false);
    }
    
    // Fusion v2에서 권장: OnCollisionStay 사용
    private void OnCollisionStay(Collision collision)
    {
        if (!Object.HasInputAuthority) return;
        
        HandlePhysicsCollision(collision);
    }
    
    private void HandleTriggerInteraction(Collider other, bool isInside)
    {
        var otherNetworkObject = other.GetComponent<NetworkObject>();
        if (otherNetworkObject == null) return;
        
        NetworkId otherId = otherNetworkObject.Id;
        
        if (isInside)
        {
            // 트리거 영역 내부
            if (!ActiveCollisions.ContainsKey(otherId))
            {
                // 새로운 상호작용 시작
                ActiveCollisions.Add(otherId, Runner.SimulationTime);
                OnInteractionStart(otherNetworkObject);
            }
            else
            {
                // 지속적인 상호작용
                ActiveCollisions.Set(otherId, Runner.SimulationTime);
            }
        }
        else
        {
            // 트리거 영역 이탈
            if (ActiveCollisions.ContainsKey(otherId))
            {
                ActiveCollisions.Remove(otherId);
                OnInteractionEnd(otherNetworkObject);
            }
        }
    }
    
    private void HandlePhysicsCollision(Collision collision)
    {
        var otherNetworkObject = collision.gameObject.GetComponent<NetworkObject>();
        if (otherNetworkObject == null) return;
        
        NetworkId otherId = otherNetworkObject.Id;
        
        // 쿨다운 확인
        if (lastCollisionTimes.ContainsKey(otherId))
        {
            float timeSinceLastCollision = Runner.SimulationTime - lastCollisionTimes[otherId];
            if (timeSinceLastCollision < collisionCooldown) return;
        }
        
        // 충돌 강도 계산
        float impulse = CalculateCollisionImpulse(collision);
        if (impulse < minCollisionForce) return;
        
        // 충돌 시간 기록
        lastCollisionTimes[otherId] = Runner.SimulationTime;
        LastCollisionTime = Runner.SimulationTime;
        
        // 충돌 처리
        ProcessPhysicsCollision(otherNetworkObject, collision, impulse);
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
    
    #region Collision Event Handlers
    
    private void OnInteractionStart(NetworkObject other)
    {
        Debug.Log($"Interaction Start: {name} <-> {other.name}");
        
        // 상호작용 시작 이벤트
        InteractionEventRpc(other.Id, "Start", transform.position);
    }
    
    private void OnInteractionEnd(NetworkObject other)
    {
        Debug.Log($"Interaction End: {name} <-> {other.name}");
        
        // 상호작용 종료 이벤트
        InteractionEventRpc(other.Id, "End", transform.position);
    }
    
    private void ProcessPhysicsCollision(NetworkObject other, Collision collision, float impulse)
    {
        Debug.Log($"Physics Collision: {name} <-> {other.name}, Impulse: {impulse}");
        
        Vector3 contactPoint = collision.contacts.Length > 0 ? 
            collision.contacts[0].point : transform.position;
        Vector3 normal = collision.contacts.Length > 0 ? 
            collision.contacts[0].normal : Vector3.up;
        
        // 다른 플레이어와의 충돌
        var otherPlayer = other.GetComponent<PlayerMovement>();
        if (otherPlayer != null && enablePlayerPushback)
        {
            HandlePlayerCollision(otherPlayer, collision, impulse);
        }
        
        // 물리 오브젝트와의 충돌
        var otherRigidbody = other.GetComponent<NetworkRigidbody3D>();
        if (otherRigidbody != null && enableObjectInteraction)
        {
            HandleObjectCollision(otherRigidbody, collision, impulse);
        }
        
        // 충돌 효과
        if (impulse > effectThreshold)
        {
            CollisionEffectRpc(contactPoint, impulse, normal);
        }
    }
    
    private void HandlePlayerCollision(PlayerMovement otherPlayer, Collision collision, float impulse)
    {
        // 밀려나는 힘 계산
        Vector3 pushDirection = (transform.position - otherPlayer.transform.position).normalized;
        Vector3 pushForce = pushDirection * impulse * pushbackMultiplier;
        
        // 자신에게 밀려나는 힘 적용 (PlayerMovement를 통해)
        if (playerMovement != null)
        {
            playerMovement.AddForce(pushForce, ForceMode.Impulse);
        }
        
        // 상대방에게도 반대 방향 힘 적용
        PlayerPushbackRpc(otherPlayer.Object.Id, -pushForce * 0.5f);
    }
    
    private void HandleObjectCollision(NetworkRigidbody3D otherRigidbody, Collision collision, float impulse)
    {
        // 오브젝트에 힘 전달
        Vector3 forceDirection = collision.contacts[0].normal;
        Vector3 transferForce = -forceDirection * impulse * 0.8f;
        Vector3 contactPoint = collision.contacts[0].point;
        
        // 오브젝트에 힘 적용 요청
        TransferForceToObjectRpc(otherRigidbody.Object.Id, transferForce, contactPoint);
    }
    
    #endregion
    
    #region RPC Methods
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void InteractionEventRpc(NetworkId otherId, string eventType, Vector3 position)
    {
        var otherObject = Runner.FindObject(otherId);
        if (otherObject != null)
        {
            Debug.Log($"Interaction {eventType} RPC: {name} <-> {otherObject.name}");
            // 상호작용 이벤트별 처리 로직
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void PlayerPushbackRpc(NetworkId playerId, Vector3 force)
    {
        var playerObject = Runner.FindObject(playerId);
        if (playerObject != null)
        {
            var otherPlayerMovement = playerObject.GetComponent<PlayerMovement>();
            if (otherPlayerMovement != null)
            {
                otherPlayerMovement.AddForce(force, ForceMode.Impulse);
            }
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void TransferForceToObjectRpc(NetworkId objectId, Vector3 force, Vector3 contactPoint)
    {
        var obj = Runner.FindObject(objectId);
        if (obj != null)
        {
            var networkRb = obj.GetComponent<NetworkRigidbody3D>();
            if (networkRb != null && networkRb.Rigidbody != null)
            {
                networkRb.Rigidbody.AddForceAtPosition(force, contactPoint, ForceMode.Impulse);
            }
        }
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void CollisionEffectRpc(Vector3 position, float intensity, Vector3 normal)
    {
        // 충돌 이펙트 재생
        if (collisionEffectPrefab != null)
        {
            var effect = Instantiate(collisionEffectPrefab, position, Quaternion.LookRotation(normal));
            
            // 강도에 따른 스케일 조정
            float scale = Mathf.Clamp(intensity / 10f, 0.5f, 2f);
            effect.transform.localScale = Vector3.one * scale;
            
            Destroy(effect, 2f);
        }
        
        // 사운드 재생
        if (audioSource != null && collisionSounds.Length > 0)
        {
            int soundIndex = Mathf.FloorToInt(intensity / 5f);
            soundIndex = Mathf.Clamp(soundIndex, 0, collisionSounds.Length - 1);
            audioSource.PlayOneShot(collisionSounds[soundIndex]);
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    private void CleanupCollisions()
    {
        var keysToRemove = new List<NetworkId>();
        
        foreach (var kvp in ActiveCollisions)
        {
            float timeSinceCollision = Runner.SimulationTime - kvp.Value;
            if (timeSinceCollision > 1f) // 1초 타임아웃
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            ActiveCollisions.Remove(key);
        }
        
        // 로컬 충돌 시간도 정리
        var localKeysToRemove = new List<NetworkId>();
        foreach (var kvp in lastCollisionTimes)
        {
            float timeSinceCollision = Runner.SimulationTime - kvp.Value;
            if (timeSinceCollision > 2f) // 2초 타임아웃
            {
                localKeysToRemove.Add(kvp.Key);
            }
        }
        
        foreach (var key in localKeysToRemove)
        {
            lastCollisionTimes.Remove(key);
        }
    }
    
    // 외부에서 호출 가능한 유틸리티 메서드들
    public void ApplyExplosionEffect(Vector3 center, float force, float radius)
    {
        if (Object.HasInputAuthority && playerMovement != null)
        {
            playerMovement.AddExplosionForce(force, center, radius);
        }
    }
    
    public void AddExternalForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (Object.HasInputAuthority && playerMovement != null)
        {
            playerMovement.AddForce(force, mode);
        }
    }
    
    public bool IsInteractingWith(NetworkId objectId)
    {
        return ActiveCollisions.ContainsKey(objectId);
    }
    
    public int GetActiveInteractionCount()
    {
        return ActiveCollisions.Count;
    }
    
    // PlayerMovement 설정 변경
    public void SetPlayerMovementSettings(float newMoveForce, float newMaxSpeed)
    {
        if (playerMovement != null)
        {
            playerMovement.MoveForce = newMoveForce;
            playerMovement.MaxSpeed = newMaxSpeed;
        }
    }
    
    // 충돌 설정 변경
    public void SetCollisionSettings(float newMinForce, float newPushbackMultiplier, float newEffectThreshold)
    {
        minCollisionForce = newMinForce;
        pushbackMultiplier = newPushbackMultiplier;
        effectThreshold = newEffectThreshold;
    }
    
    #endregion
    
    // 디버그용 기즈모
    private void OnDrawGizmosSelected()
    {
        // 충돌 상호작용 표시
        Gizmos.color = Color.red;
        foreach (var kvp in ActiveCollisions)
        {
            var obj = Runner.FindObject(kvp.Key);
            if (obj != null)
            {
                Gizmos.DrawLine(transform.position, obj.transform.position);
            }
        }
    }
    
    // 디버그 정보
    private void OnGUI()
    {
        if (!Object.HasInputAuthority) return;
        
        GUI.Box(new Rect(10, 260, 300, 80), "Physics Interaction");
        
        int yOffset = 280;
        GUI.Label(new Rect(20, yOffset, 280, 20), $"Active Collisions: {ActiveCollisions.Count}");
        yOffset += 20;
        GUI.Label(new Rect(20, yOffset, 280, 20), $"Player Pushback: {enablePlayerPushback}");
        yOffset += 20;
        GUI.Label(new Rect(20, yOffset, 280, 20), $"Object Interaction: {enableObjectInteraction}");
    }
}