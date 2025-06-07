using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    [Tooltip("플레이어 캐릭터 프리팹 (NetworkObject 포함)")]
    public GameObject PlayerPrefab;
    
    [Tooltip("독립 카메라 프리팹")]
    public GameObject CameraPrefab;
    
    [SerializeField] private Vector3 _initialSpawnPos;
    [SerializeField] private Vector3 _initialCameraPos;
    
    public void PlayerJoined(PlayerRef player)
    {
        // 로컬 플레이어만 처리
        if (player == Runner.LocalPlayer)
        {
            // 1. 플레이어 스폰
            Runner.Spawn(PlayerPrefab, _initialSpawnPos, Quaternion.identity, player);
            
            // 2. 독립적인 로컬 카메라 생성 (네트워크 오브젝트가 아님)
            CreateLocalCamera();
        }
    }
    
    private void CreateLocalCamera()
    {
        GameObject cameraObj;
        
        if (CameraPrefab != null)
        {
            // 프리팹이 있으면 인스턴시에이트
            cameraObj = Instantiate(CameraPrefab, _initialCameraPos, Quaternion.identity);
        }
        else
        {
            // 프리팹이 없으면 런타임에 생성
            cameraObj = new GameObject("LocalCamera");
            cameraObj.transform.position = _initialCameraPos;
            cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<FreeCameraController>();
        }
        
        // 로컬 전용으로 표시 (선택사항)
        cameraObj.name = "LocalCamera_" + Runner.LocalPlayer;
    }
}