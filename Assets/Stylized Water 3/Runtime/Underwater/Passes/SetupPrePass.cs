using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StylizedWater3.UnderwaterRendering
{
    public class SetupPrePass : ScriptableRenderPass
    {
        private UnderwaterArea volume;
        private bool renderingEnabled;
        
        public void Setup(bool enabled, UnderwaterArea volume)
        {
            this.renderingEnabled = enabled;
            this.volume = volume;
        }
        private class PassData
        {
            public bool cameraIntersecting;
            public bool submerged;
            public float lensOffset;
            public float waterlineWidth;
            public Vector4 parameters;

            public Vector3 cameraPosition;
            public Vector3 cameraForward;
            public float nearPlaneDistance;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater Rendering Setup", out var passData))
            {
                StylizedWaterRenderFeature.UnderwaterRenderingData underwaterRenderingData = frameData.GetOrCreate<StylizedWaterRenderFeature.UnderwaterRenderingData>();
                
                passData.cameraIntersecting = renderingEnabled;
                
                if (passData.cameraIntersecting)
                {
                    var cameraData = frameData.Get<UniversalCameraData>();
                    passData.submerged = volume.CameraSubmerged(cameraData.camera);
                    passData.lensOffset = volume.waterlineOffset;
                    passData.waterlineWidth = volume.waterlineThickness;
                    
                    underwaterRenderingData.enabled = passData.cameraIntersecting;
                    underwaterRenderingData.fullySubmerged = passData.submerged;
                    
                    underwaterRenderingData.volume = volume;

                    //Require the opaque texture
                    if (volume.waterMaterial.IsKeywordEnabled(ShaderParams.Keywords.Refraction))
                    {
                        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                    }
                    else
                    {
                        ConfigureInput(ScriptableRenderPassInput.Depth);
                    }
                    passData.cameraPosition = cameraData.camera.transform.position;
                    passData.cameraForward = cameraData.camera.transform.forward;
                    passData.nearPlaneDistance = cameraData.camera.nearClipPlane;
                    
                    //This doesn't actually appear to work with multiple cameras active!
                    volume.RenderWithCamera(passData.cameraPosition, passData.cameraForward, passData.nearPlaneDistance);
                }
                else
                {
                    passData.submerged = false;
                    
                    underwaterRenderingData.fullySubmerged = false;
                    underwaterRenderingData.enabled = false;
                    underwaterRenderingData.volume = null;
                }
                
                //Pass should always execute
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext rgContext) =>
                {
                    Execute(rgContext.cmd, data);
                });
            }
        }
        
        private readonly int _ClipOffset = Shader.PropertyToID("_ClipOffset");
        private readonly int _FullySubmerged = Shader.PropertyToID("_FullySubmerged");
        private readonly int _WaterLineWidth = Shader.PropertyToID("_WaterLineWidth");
        private readonly int _UnderwaterRenderingEnabled = Shader.PropertyToID("_UnderwaterRenderingEnabled");
        
        void Execute(RasterCommandBuffer cmd, PassData passData)
        {
            if (passData.cameraIntersecting)
            {
                cmd.EnableShaderKeyword(ShaderParams.Keywords.UnderwaterRendering);
                cmd.SetGlobalFloat(_ClipOffset, passData.lensOffset);
                cmd.SetGlobalFloat(_FullySubmerged, passData.submerged ? 1 : 0);
                cmd.SetGlobalFloat(_WaterLineWidth, passData.waterlineWidth);
            }
            else
            {
                cmd.DisableShaderKeyword(ShaderParams.Keywords.UnderwaterRendering);
            }
            
            cmd.SetGlobalFloat(_UnderwaterRenderingEnabled, passData.cameraIntersecting ? 1 : 0);
        }
    }
}