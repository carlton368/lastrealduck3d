using System;
using Fusion;
using UnityEngine;

namespace CuteDuckGame
{
    [Serializable]
    public class StageInfo
    {
        [Header("Stage Information")]
        public string stageName;
        public string stageDisplayName;
        public string stageDescription;
        
        [Header("Stage Settings")]
        public int maxPlayers = 4;
        public float timeLimit = 300f; // 5분
        public Sprite stageIcon;
        
        [Header("Scene References")]
        [ScenePath]
        public string scenePath;
        
        public StageInfo()
        {
            stageName = "DefaultStage";
            stageDisplayName = "Default Stage";
            stageDescription = "A default stage for testing";
            maxPlayers = 4;
            timeLimit = 300f;
        }
        
        public StageInfo(string name, string displayName, string description = "")
        {
            stageName = name;
            stageDisplayName = displayName;
            stageDescription = description;
            maxPlayers = 4;
            timeLimit = 300f;
        }
    }
}