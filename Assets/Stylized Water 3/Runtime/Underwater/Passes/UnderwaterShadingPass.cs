// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;
#if URP
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace StylizedWater3.UnderwaterRendering
{
    public class UnderwaterShadingPass : ScriptableRenderPass
    {
        private const string profilerTag = "Underwater Rendering: Shading";
        private static readonly ProfilingSampler profilerSampler = new ProfilingSampler(profilerTag);
        

        //The extra pass incorperated into the water shader
        public const string PASS_NAME = "Underwater Shading";
        private const string LIGHTMODE_TAG = "UnderwaterShading";

        /// <summary>
        /// For indirect lighting the rendering uses Unity's internal global reflection probe. For custom or third-party dynamic lighting systems you can provide a custom environment cubemap instead
        /// </summary>
        public static ReflectionProbe AmbientLightOverride;
        
        public UnderwaterShadingPass(StylizedWaterRenderFeature renderFeature)
        {

        }

        public const string DEPTH_NORMALS_KEYWORD = "_REQUIRE_DEPTH_NORMALS";
        public const string SOURCE_DEPTH_NORMALS_KEYWORD = "_SOURCE_DEPTH_NORMALS";
        
        FilteringSettings m_FilteringSettings;
        private readonly List<ShaderTagId> m_ShaderTagIdList = new()
        {
            new ShaderTagId(LIGHTMODE_TAG),
            //new ShaderTagId("UniversalForward")
        };
        private RendererListParams rendererListParams;
        
        public void Setup(StylizedWaterRenderFeature.UnderwaterRenderingSettings settings, UnderwaterResources resources)
        {
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent,  -1);
        }

        public class PassData
        {
            //public TextureHandle sourceColor;
            //public TextureHandle renderTarget;

            public TextureHandle skyboxCubemap;
            public Vector4 skyboxHDRDecodeValues;

            public float waterLevel;
            public UnderwaterArea.ShadingSettings shadingSettings;
        }

        private RTHandle skyboxCubemapTextureHandle;
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            
            /*
            // The destination texture is created here, 
            // the texture is created with the same dimensions as the active color texture
            var source = resourceData.cameraOpaqueTexture;
            
            TextureDesc rtDsc = renderGraph.GetTextureDesc(source);
            rtDsc.name = "Underwater color copy";
            rtDsc.clearBuffer = false;
            
            TextureHandle colorCopy = renderGraph.CreateTexture(rtDsc);

            if (RenderGraphUtils.CanAddCopyPassMSAA() == false)
            {
                //Debug.LogError("Can't add the copy pass due to MSAA");
            }
            renderGraph.AddCopyPass(source, colorCopy, passName:"Underwater Rendering: Color copy");
            */
            
            //using (var builder = renderGraph.AddUnsafePass<PassData>("Underwater Rendering", out var passData))
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Underwater Rendering", out var passData))
            {
                StylizedWaterRenderFeature.UnderwaterRenderingData data = frameData.Get<StylizedWaterRenderFeature.UnderwaterRenderingData>();

                data.volume.UpdateMaterial();
                passData.waterLevel = data.volume.CurrentWaterLevel;
                passData.shadingSettings = data.volume.shadingSettings;
                
                if (RenderSettings.ambientMode == AmbientMode.Skybox)
                {
                    Texture environmentCubemap = AmbientLightOverride ? AmbientLightOverride.texture : ReflectionProbe.defaultTexture;
                    //Have to specifically create a descriptor. Enviro 3 will create a cubemap with both a color and depth format, which is invalid
                    RenderTextureDescriptor environmentCubemapDescriptor = new RenderTextureDescriptor(environmentCubemap.width, environmentCubemap.height, environmentCubemap.graphicsFormat, 0, environmentCubemap.mipmapCount);
                    
                    if (SystemInfo.IsFormatSupported(environmentCubemap.graphicsFormat, GraphicsFormatUsage.Render) == false)
                    {
                        Debug.LogWarning($"[Underwater Rendering] The skybox reflection cubemap \"{environmentCubemap.name}\" format \"{environmentCubemap.graphicsFormat}\" is reportedly not supported. " +
                                         $"This affects negatively underwater lighting. A third-party script is likely overriding this cubemap, but with an incorrect (or compressed) format.");
                        
                        //Fallback to a usable HDR format
                        environmentCubemapDescriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_UNorm;
                    }

                    skyboxCubemapTextureHandle = RTHandles.Alloc(environmentCubemapDescriptor);

                    passData.skyboxCubemap = renderGraph.ImportTexture(skyboxCubemapTextureHandle);
                    builder.UseTexture(passData.skyboxCubemap, AccessFlags.Read);
                    
                    passData.skyboxHDRDecodeValues = AmbientLightOverride ? AmbientLightOverride.textureHDRDecodeValues : ReflectionProbe.defaultTextureHDRDecodeValues;
                }
                
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                
                //passData.sourceColor = colorCopy;
                //builder.UseTexture(colorCopy, AccessFlags.Read);
                
                //passData.renderTarget = resourceData.activeColorTexture;
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Execute(context, data);
                });
            }
        }
        
        private static readonly int _UnderwaterAmbientParams = Shader.PropertyToID("_UnderwaterAmbientParams");
        private static readonly int _UnderwaterAmbientColor = Shader.PropertyToID("_UnderwaterAmbientColor");
        
        //Global values that needs to be set up again, won't survive opaque pass
        private static readonly int skyboxCubemap = Shader.PropertyToID("skyboxCubemap");
        private static readonly int skyboxCubemap_HDR = Shader.PropertyToID("skyboxCubemap_HDR");

        private static Vector4 ambientParams;

        private void Execute(RasterGraphContext context, PassData data)
        {
            var cmd = context.cmd;
            using (new ProfilingScope(cmd, profilerSampler))
            {
                context.cmd.SetGlobalFloat("_WaterLevel", data.waterLevel);
                
                context.cmd.SetGlobalFloat("_StartDistance", data.shadingSettings.fogStartDistance);
                context.cmd.SetGlobalFloat("_UnderwaterFogDensity", data.shadingSettings.fogDensity * 0.1f);
                context.cmd.SetGlobalFloat("_UnderwaterSubsurfaceStrength", data.shadingSettings.translucencyStrength);
                context.cmd.SetGlobalFloat("_UnderwaterSubsurfaceExponent", data.shadingSettings.translucencyExponent);
                context.cmd.SetGlobalFloat("_UnderwaterCausticsStrength", data.shadingSettings.causticsBrightness);
                
                context.cmd.SetGlobalFloat("_UnderwaterFogBrightness", data.shadingSettings.fogBrightness);
                context.cmd.SetGlobalFloat("_UnderwaterColorAbsorption", Mathf.Pow(data.shadingSettings.colorAbsorption, 3f));
                
                context.cmd.SetGlobalVector("_UnderwaterHeightFogParams", new Vector4(data.shadingSettings.heightFogStart, data.shadingSettings.heightFogEnd, data.shadingSettings.heightFogDensity * 0.01f, data.shadingSettings.heightFogBrightness));

                //context.cmd.SetGlobalTexture("_SourceTex", data.sourceColor);
                    
                if (RenderSettings.ambientMode == AmbientMode.Skybox)
                {
                    context.cmd.SetGlobalTexture(skyboxCubemap, data.skyboxCubemap);
                    context.cmd.SetGlobalVector(skyboxCubemap_HDR, data.skyboxHDRDecodeValues);
                }
                else if (RenderSettings.ambientMode == AmbientMode.Flat)
                {
                    context.cmd.SetGlobalColor(_UnderwaterAmbientColor, RenderSettings.ambientLight.linear);
                }
                else //Tri-light
                {
                    context.cmd.SetGlobalColor(_UnderwaterAmbientColor, RenderSettings.ambientEquatorColor.linear);
                }

                ambientParams.x = Mathf.GammaToLinearSpace(RenderSettings.ambientIntensity);
                ambientParams.y = RenderSettings.ambientMode == AmbientMode.Skybox ? 1 : 0;
                context.cmd.SetGlobalVector(_UnderwaterAmbientParams, ambientParams);
            }
        }
       
        public void Dispose()
        {
            skyboxCubemapTextureHandle?.Release();
        }
        
        #pragma warning disable CS0672
        #pragma warning disable CS0618
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) { }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
        #pragma warning restore CS0672
        #pragma warning restore CS0618
    }

}
#endif