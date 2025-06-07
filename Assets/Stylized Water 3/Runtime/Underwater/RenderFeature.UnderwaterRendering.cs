// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using UnityEngine;
using StylizedWater3.UnderwaterRendering;
#if URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StylizedWater3
{
    public partial class StylizedWaterRenderFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class UnderwaterRenderingSettings
        {
            public bool enable = true;            
            public bool ignoreSceneView;
        }
        public UnderwaterRenderingSettings underwaterRenderingSettings = new UnderwaterRenderingSettings();
        
        //Shared resources, ensures they're included in a build when the render feature is in use
        public UnderwaterResources underwaterResources;
        
        private SetupPrePass underwaterPrePass;
        private UnderwaterMaskPass maskPass;
        private UnderwaterShadingPass shadingPass;

        //Shared between passes
        public class UnderwaterRenderingData : ContextItem
        {
            public bool enabled;
            public bool fullySubmerged;

            public UnderwaterArea volume;
            
            public override void Reset()
            {
                
            }
        }

        partial void VerifyUnderwaterRendering()
        {
            if (!underwaterResources) underwaterResources = UnderwaterResources.Find();
        }

        partial void CreateUnderwaterRenderingPasses()
        {
            #if UNITY_EDITOR
            if (!underwaterResources) underwaterResources = UnderwaterResources.Find();
            #endif
            
            underwaterPrePass = new SetupPrePass();
            underwaterPrePass.renderPassEvent = RenderPassEvent.BeforeRendering;
            
            maskPass = new UnderwaterMaskPass();
            maskPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

            shadingPass = new UnderwaterShadingPass(this);
            shadingPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }
        
        public static UnderwaterArea GetFirstIntersectingVolume(Camera camera)
        {
            int index = -1;
            float closestDistanceSqr  = float.MaxValue;
            Vector3 cameraPosition = camera.transform.position;
            
            for (int i = 0; i < UnderwaterArea.Instances.Count; i++)
            {
                UnderwaterArea trigger = UnderwaterArea.Instances[i];
                
                if(!trigger.waterMaterial || !trigger.boxCollider) continue;
                
                if (trigger.CameraIntersects(camera))
                {
                    //Also check the distance, since two or more volumes can overlap
                    Vector3 boxSurface = trigger.boxCollider.ClosestPoint(cameraPosition);
                    float distanceToCamera = (cameraPosition - boxSurface).sqrMagnitude;
                    
                    if (distanceToCamera < closestDistanceSqr)
                    {
                        closestDistanceSqr = distanceToCamera;
                        index = i;
                    }
                }
                
            }

            if (index >= 0)
            {
                return UnderwaterArea.Instances[index];
            }

            return null;
        }

        private bool RenderForCamera(CameraData cameraData, Camera camera)
        {
            if (camera.cameraType == CameraType.SceneView && underwaterRenderingSettings.ignoreSceneView) return false;
                
            //Camera stacking and depth-based effects is essentially non-functional.
            //All effects render twice to the screen, causing double brightness. Next to fog causing overlay objects to appear transparent
            //- Best option is to not render anything for overlay cameras
            //- Reflection probes do not capture the water line correctly
            //- Preview cameras end up rendering the effect into asset thumbnails
            if (cameraData.renderType == CameraRenderType.Overlay || camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview) return false;

#if UNITY_EDITOR
            //Skip if post-processing is disabled in scene-view
            if (cameraData.camera.cameraType == CameraType.SceneView && UnityEditor.SceneView.lastActiveSceneView && !UnityEditor.SceneView.lastActiveSceneView.sceneViewState.showImageEffects) return false;
#endif
            
            #if UNITY_EDITOR
            //Skip rendering if editing a prefab
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage()) return false;
            #endif
            
            return true;
        }
        
        partial void AddUnderwaterRenderingPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!underwaterRenderingSettings.enable) return;
            
            var camera = renderingData.cameraData.camera;

            foreach (var trigger in UnderwaterArea.Instances)
            {
                //Default state should be invisible, until a camera renders it
                trigger.SetVisibility(false);
            }
            
            UnderwaterArea volume = GetFirstIntersectingVolume(camera);

            bool render = underwaterRenderingSettings.enable && volume;
            render &= RenderForCamera(renderingData.cameraData, camera);
            
            underwaterPrePass.Setup(render, volume);
            renderer.EnqueuePass(underwaterPrePass);

            if (render == false)
            {
                return;
            }

            //Debug.Log($"Rendering for: {camera.name} (intersects with {volume.name})");
            
            renderer.EnqueuePass(maskPass);
            
            shadingPass.Setup(underwaterRenderingSettings, underwaterResources);
            renderer.EnqueuePass(shadingPass);
        }

        partial void DisposeUnderwaterRenderingPasses()
        {
            maskPass.Dispose();
            shadingPass.Dispose();
            
            Shader.DisableKeyword(ShaderParams.Keywords.UnderwaterRendering);
            
            foreach (var trigger in UnderwaterArea.Instances)
            {
                trigger.SetVisibility(false);
            }
        }
    }
}
#endif