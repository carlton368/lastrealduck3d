using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [Tooltip("플레이어 캐릭터 프리팹 (NetworkObject 포함)")]
    public GameObject PlayerPrefab;

    // 초기 스폰 위치. 필요에 따라 배열로 바꿔 팀별 스폰 지점 구현 가능
    [SerializeField] private Vector3 _initialSpawnPos;

    public void PlayerJoined(PlayerRef player)
    {
        // 로컬 플레이어만 스폰 (호스트 또는 클라이언트 자기 자신)
        if (player == Runner.LocalPlayer)
        {
            // NetworkObject에 InputAuthority를 부여하려면 4번째 매개변수로 PlayerRef를 전달해야 함
            Runner.Spawn(PlayerPrefab,
                         _initialSpawnPos,
                         Quaternion.identity,
                         player); // <-- 핵심 수정: InputAuthority 설정
        }
    }
}