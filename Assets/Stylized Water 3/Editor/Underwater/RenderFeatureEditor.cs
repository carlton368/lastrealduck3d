// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using System;
using StylizedWater3.UnderwaterRendering;
using UnityEditor;
using UnityEngine;

namespace StylizedWater3
{
    public partial class RenderFeatureEditor : Editor
    {
        private SerializedProperty underwaterRenderingSettings;

        partial void UnderwaterRenderingOnEnable()
        {
            underwaterRenderingSettings = serializedObject.FindProperty("underwaterRenderingSettings");
        }
        
        partial void UnderwaterRenderingOnInspectorGUI()
        {
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField(StylizedWater3.UnderwaterRendering.UnderwaterRendering.extension.name, EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Version {StylizedWater3.UnderwaterRendering.UnderwaterRendering.extension.version}", EditorStyles.miniLabel);
            }

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            
            EditorGUILayout.HelpBox($"Triggers in scene: {UnderwaterArea.Instances.Count}", MessageType.None);

            EditorGUILayout.PropertyField(underwaterRenderingSettings);
            
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            /*
            if (resources.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Internal shader resources object not referenced!", MessageType.Error);
                if (GUILayout.Button("Find & assign"))
                {
                    resources.objectReferenceValue = UnderwaterResources.Find();
                    serializedObject.ApplyModifiedProperties();
                }
            }
            */
        }
    }
}