using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StageInfo", menuName = "Scriptable Objects/StageInfo")]
public class StageInfo : ScriptableObject
{
    [Serializable]
    public class Info
    {
        public ItemType type; 
        public int count; 
        public Vector3 spawnOffset; 
        public string plateName;
    }
    public string stageName; 
    public List<Info> infos = new();
    
}
