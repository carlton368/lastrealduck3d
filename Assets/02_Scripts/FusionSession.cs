using System;
using System.Collections;
using System.Collections.Generic;
using CuteDuckGame;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CuteDuckGame
{
    public class FusionSession : MonoBehaviour, INetworkRunnerCallbacks
    {
        // NetworkRunner 프리팹 참조
        public NetworkRunner runnerPrefab;

        // 인스펙터에서 설정할 씬 이름 (게임 씬, 타이틀 씬)
        [SerializeField] [ScenePath] private string gameScene;
        [SerializeField] [ScenePath] private string titleScene;

        // 현재 사용 중인 NetworkRunner 인스턴스 접근 프로퍼티
        public NetworkRunner Runner { get; private set; }

        // 오브젝트가 씬 전환 시 파괴되지 않도록 설정
        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        // 게임 시작 시 타이틀 씬부터 로드
        private void Start()
        {
            SceneManager.LoadScene(titleScene);
        }

        // 세션에 접속 시도하는 메서드
        public void TryConnect()
        {
            // StaticData.CurrentStageInfo.stageName을 세션 이름에 포함
            StartCoroutine(ConnectSharedSessionRoutine($"{StaticData.CurrentRoomName}"));
        }

        // 실질적으로 공유 세션에 접속을 시도하는 코루틴
        private IEnumerator ConnectSharedSessionRoutine(string sessionCode)
        {
            // UI의 상호작용을 잠시 비활성화
            UIManager.Instance.ToggleInteraction(false);

            // 기존의 Runner가 존재한다면 종료 후 갱신
            if (Runner)
                Runner.Shutdown();
            Runner = Instantiate(runnerPrefab);
            Runner.AddCallbacks(this);

            // 세션을 시작하는 비동기 작업
            var task = Runner.StartGame(
                new StartGameArgs
                {
                    GameMode = GameMode.Shared,
                    SessionName = sessionCode,
                    SceneManager = Runner.GetComponent<INetworkSceneManager>(),
                    ObjectProvider = Runner.GetComponent<INetworkObjectProvider>(),
                    Scene = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath(gameScene))
                });
            // 세션 시작 작업이 완료될 때까지 대기
            yield return new WaitUntil(() => task.IsCompleted);

            // 작업 완료 후 UI 다시 활성화
            UIManager.Instance.ToggleInteraction(true);

            // 결과 확인 및 로그 출력
            var result = task.Result;
            Debug.Log($"StartGame Result: {result.ShutdownReason}");
            if (!result.Ok)
            {
                Debug.LogWarning(result.ShutdownReason);
            }
        }

        // 세션 연결 해제 시도
        public void TryDisconnect()
        {
            Runner.Shutdown();
        }

        // NetworkRunner가 종료될 때 호출되는 콜백
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Runner = null;
            // 정상적으로 종료되었다면 타이틀 씬으로 되돌아감
            if (shutdownReason == ShutdownReason.Ok)
            {
                SceneManager.LoadScene(titleScene);
            }
            else
            {
                // 오류나 기타 이유로 종료되었을 때 경고 로그 출력
                Debug.LogWarning(shutdownReason);
            }
        }

        #region INetworkRunnerCallbacks

        // 서버에 접속 완료
        public void OnConnectedToServer(NetworkRunner runner)
        {
        }

        // 서버에 접속 실패
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }

        // 접속 요청 처리
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
        }

        // 사용자 인증 반환값 처리
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        // 호스트가 변경될 때 (호스트 마이그레이션)
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        // 입력을 받는 콜백
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        // 입력이 누락되었을 때 콜백
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        // 씬 로드가 시작될 때 콜백
        public void OnSceneLoadStart(NetworkRunner runner)
        {
        }

        // 씬 로드가 완료되었을 때 콜백
        public void OnSceneLoadDone(NetworkRunner runner)
        {
        }

        // 세션 목록이 갱신될 때 콜백
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        // 플레이어가 접속했을 때 콜백
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
        }

        // 플레이어가 접속을 종료했을 때 콜백
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
        }

        // 시뮬레이션 메시지가 도착했을 때 처리
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        // Observer Authority(시야)에서 오브젝트가 빠져나갔을 때 콜백
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        // Observer Authority(시야)로 오브젝트가 진입했을 때 콜백
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        // 서버와의 연결이 끊어졌을 때 콜백
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

        // 신뢰성 있는(NetworkRunner에서 보장되는) 데이터가 수신되었을 때
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            ArraySegment<byte> data)
        {
        }

        // 신뢰성 있는 데이터 전송 진행 상황 콜백
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        #endregion
    }
}