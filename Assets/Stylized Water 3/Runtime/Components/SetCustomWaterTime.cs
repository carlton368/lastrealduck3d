// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StylizedWater3
{
    [ExecuteAlways]
    [AddComponentMenu("Stylized Water 3/Water Custom Time")]
    public class SetCustomWaterTime : MonoBehaviour
    {
        public enum Mode
        {
            None,
            Interval,
            Time,
            EditorTime,
            Speed,
            Custom
        }

        public Mode mode = Mode.Custom;

        //Parameters for different modes
        [Min(0.02f)]
        public float interval = 0.2f;
        public float speed = 0f;
        [Min(0f)]
        public float customTime = 0f;
        
        private float elapsedTime;
        
        private void OnEnable()
        {
            RenderPipelineManager.beginContextRendering += OnBeginFrame;
        }

        private void OnBeginFrame(ScriptableRenderContext context, List<Camera> cams)
        {
            SetTime();
        }

        private void SetTime()
        {
            if (mode == Mode.None)
            {
                ResetTime();
                return;
            }

            if (mode == Mode.Interval)
            {
                elapsedTime += Time.deltaTime;

                if (elapsedTime >= interval)
                {
                    elapsedTime = 0;

                    WaterObject.CustomTime = Time.time;
                }
            }

            if (mode == Mode.Time)
            {
                WaterObject.CustomTime = Time.time;
            }
			
			#if UNITY_EDITOR
            if (mode == Mode.EditorTime)
            {
                WaterObject.CustomTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
            }
			#endif

            if (mode == Mode.Speed)
            {
                elapsedTime += Time.deltaTime * speed;
                WaterObject.CustomTime = elapsedTime;
            }

            if (mode == Mode.Custom)
            {
                WaterObject.CustomTime = customTime;
            }
        }

        private void ResetTime()
        {
            //Revert to using normal time
            WaterObject.CustomTime = -1;
        }
        
        private void OnDisable()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginFrame;

            ResetTime();
        }
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(SetCustomWaterTime))]
    public class SetCustomWaterTimeEditor : Editor
    {
        private SerializedProperty mode;
        
        private SerializedProperty interval;
        private SerializedProperty speed;
        private SerializedProperty customTime;

        private void OnEnable()
        {
            mode = serializedObject.FindProperty("mode");
            interval = serializedObject.FindProperty("interval");
            speed = serializedObject.FindProperty("speed");
            customTime = serializedObject.FindProperty("customTime");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(mode);

            if (mode.intValue == (int)SetCustomWaterTime.Mode.Interval)
            { 
                EditorGUILayout.PropertyField(interval);
            }
            else if (mode.intValue == (int)SetCustomWaterTime.Mode.Speed)
            {
                EditorGUILayout.PropertyField(speed);
            }
            else if (mode.intValue == (int)SetCustomWaterTime.Mode.Custom)
            {
                EditorGUILayout.PropertyField(customTime);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
    #endif
}