// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace StylizedWater3.UnderwaterRendering
{
	[ExecuteAlways]
	//Component requires to be added to a GameObject with a Collider (trigger checkbox enabled)
	public class UnderwaterArea : MonoBehaviour
	{
		[Tooltip("Change the currently active water material when this volume is triggered. You can leave the material field empty, in case every water body uses the same material anyway, just at different water levels")]
		public Material waterMaterial;
		
		public BoxCollider boxCollider;
		
		public enum WaterLevelSource
		{
			FixedValue,
			Transform,
			BoxColliderTop,
			Ocean
		}
		[Tooltip("Configure what should be used to set the base water level. Either a fixed value, or based on a transform's world-space Y-position")]
		public WaterLevelSource waterLevelSource;
		[Tooltip("The base water level, this value is important and required for correct rendering. As such, underwater rendering does not work with rivers or other non-flat water")]
		public float waterLevel;
		[Tooltip("This transform's Y-position is used as the base water level, this value is important and required for correct rendering. As such, underwater rendering does not work with rivers or other non-flat water")]
		public Transform waterLevelTransform;
		
		[Range(-5, 5)]
		public int renderingOrder = 0;
		[Min(0.05f)]
		public float waterlineOffset = 0.3f;
		[Min(0.02f)]
		public float waterlineThickness = 0.05f;

		public bool visualizeWaterLevel;
		
		//Because dynamic effects can alter the perceived water height, add a height padding to intersection calculations
		private const float WATER_LEVEL_PADDING = 3;
		//To avoid the mesh culling when the camera moves forward too fast, position it a bit further out
		private const float NEAR_PLANE_OFFSET = 3f;
		
		public static readonly List<UnderwaterArea> Instances = new List<UnderwaterArea>();
		
		[SerializeField]
		private GameObject cameraGlassObject;
		[SerializeField]
		private MeshFilter meshFilter;
		[SerializeField]
		private MeshRenderer meshRenderer;
		
		[SerializeField] 
		public UnderwaterResources underwaterResources;
		
		[SerializeField]
		private Material underwaterMaterial;
		[SerializeField]
		private Material waterlineMaterial;

		public AudioMixer environmentAudioMixer;
		[Range(300f, 1800)]
		public float lowPassCutoffFrequency = 800f;
		
		public float CurrentWaterLevel
		{
			get
			{
				if (waterLevelSource == WaterLevelSource.Transform && waterLevelTransform) return waterLevelTransform.position.y;
				if (waterLevelSource == WaterLevelSource.BoxColliderTop && boxCollider) return GetColliderPlaneHeight();
				if (waterLevelSource == WaterLevelSource.Ocean && OceanFollowBehaviour.Instance)
				{
					//Store it, so that it is always valid even when the singleton hasn't loaded yet
					waterLevel = OceanFollowBehaviour.Instance.transform.position.y;
					return waterLevel;
				}
				
				return waterLevel;
			}
		}
		
		[Serializable]
		public class ShadingSettings
		{
			[Range(0.01f, 3f)]
			public float fogDensity = 0.25f;
			public float fogStartDistance = 1f;
			[Range(0f,2f)]
			public float fogBrightness = 1f;
			[Range(0.01f, 1f)]
			public float colorAbsorption = 0.01f;
			[Space]

			[Min(0.01f)]
			public float heightFogDensity = 0.1f;
			[Min(0.1f)]
			public float heightFogStart = 1f;
			[Min(2)]
			public float heightFogEnd = 20f;
			[Range(0f, 1f)]
			public float heightFogBrightness = 0.5f;
			[Space]
			
			[Min(0f)]
			public float causticsBrightness = 1f;
			[Min(0f)]
			public float translucencyStrength = 0.5f;
			[Range(1,8)]
			public float translucencyExponent = 2f;
		}
		
		public ShadingSettings shadingSettings;

		[Serializable]
		public class ParticleEffect
		{
			public ParticleSystem particleSystem;
			public bool alignToSunRotation;
			
			[Space]
			
			[Min(0f)]
			public float minDepth = 3f;
			[Min(1f)]
			public float maxDepth = 10f;

			public ParticleEffect(float minDepth, float maxDepth, bool alignToSunRotation)
			{
				this.minDepth = minDepth;
				this.maxDepth = maxDepth;
				this.alignToSunRotation = alignToSunRotation;
			}
		}
		[Tooltip("Assign Particle Effects to follow the camera around underwater")]
		public List<ParticleEffect> particleEffects = new List<ParticleEffect>();
		
		public void Reset()
		{
			gameObject.layer = LayerMask.NameToLayer("Water");
			
			boxCollider = GetComponent<BoxCollider>();
			if (!boxCollider) boxCollider = this.gameObject.AddComponent<BoxCollider>();
			
			if (boxCollider)
			{
				boxCollider.isTrigger = true;
				boxCollider.size = new Vector3(100f, 50f, 100);
				boxCollider.center = new Vector3(0, -25f + WATER_LEVEL_PADDING, 0);
			}
			
			if (OceanFollowBehaviour.Instance)
			{
				waterMaterial = OceanFollowBehaviour.Instance.material;
				waterLevelSource = WaterLevelSource.Ocean;

				Vector3 position = this.transform.position;
				position.y = OceanFollowBehaviour.Instance.transform.position.y;
				this.transform.position = position;
				
				boxCollider.size = new Vector3(1000f, 500f, 1000f);
				boxCollider.center = new Vector3(0, -250f + WATER_LEVEL_PADDING, 0);
			}
			else
			{
				WaterObject nearestWater = WaterObject.Instances.Count == 1 ? WaterObject.Instances[0] : WaterObject.Find(this.transform.position, true) ;
				if (nearestWater)
				{
					waterMaterial = nearestWater.material;
					waterLevel = nearestWater.transform.position.y;
				}
			}

			FindShaders();

			ValidateRenderer();
		}
		
		private void OnEnable()
		{
			#if UNITY_EDITOR
			if(!underwaterResources) FindShaders();
			#endif
			
			ValidateRenderer();
			
			Instances.Add(this);
		}

		private void OnDisable()
		{
			Instances.Remove(this);

			CoreUtils.Destroy(cameraGlassObject);
		}

		public bool HasValidShader()
		{
			return underwaterResources && underwaterResources.underwaterShader && underwaterResources.waterlineShader;
		}
		
		private void ValidateRenderer()
		{
			if (HasValidShader() == false)
			{
				throw new Exception("Underwater render shader is null. Cannot create underwater rendering. It has possible been deleted from the project, or the shader failed to compile.");
			}

			if (!underwaterMaterial)
			{
				underwaterMaterial = new Material(underwaterResources.underwaterShader);
				underwaterMaterial.name = "Underwater Rendering";
			}
			if (!waterlineMaterial)
			{
				waterlineMaterial = new Material(underwaterResources.waterlineShader);
				waterlineMaterial.name = "Waterline";
			}
			
			if (!cameraGlassObject)
			{
				cameraGlassObject = new GameObject("Underwater Glass Surface", typeof(MeshFilter), typeof(MeshRenderer));
				cameraGlassObject.transform.SetParent(transform);
				cameraGlassObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				
				cameraGlassObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
			}
			//cameraGlassObject.hideFlags = HideFlags.None;

			meshFilter = cameraGlassObject.GetComponent<MeshFilter>();
			meshRenderer = cameraGlassObject.GetComponent<MeshRenderer>();
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			//To be enabled when a camera renders this volume
			meshRenderer.forceRenderingOff = true;
			meshRenderer.allowOcclusionWhenDynamic = false;
			meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
			
			meshFilter.sharedMesh = UnderwaterUtilities.WaterLineMesh;
			meshRenderer.materials = new []{ underwaterMaterial, waterlineMaterial };
		}

		public void UpdateMaterial()
		{
			underwaterMaterial.CopyMatchingPropertiesFromMaterial(waterMaterial);
			underwaterMaterial.renderQueue = waterMaterial.renderQueue + renderingOrder +1;
			
			waterlineMaterial.CopyMatchingPropertiesFromMaterial(underwaterMaterial);
		}
		
		/// <summary>
		/// Positions the renderer and particle effects according to this camera. This should be called from the rendering thread.
		/// </summary>
		/// <param name="camera"></param>
		public void RenderWithCamera(Vector3 position, Vector3 direction, float nearPlaneDistance)
		{
			if (!cameraGlassObject)
			{
				ValidateRenderer();
				
				Debug.LogError($"The underwater surface object has been deleted from the trigger \"{this.name}\". This should never be done intentionally.");
			}
			
			SetVisibility(true);

			//Not ideal, as it's easy to lose track of the object
			//cameraGlassObject.transform.SetParent(camera.transform);
			//cameraGlassObject.transform.SetLocalPositionAndRotation(Vector3.forward * (camera.nearClipPlane + NEAR_PLANE_OFFSET), Quaternion.identity);
			
			cameraGlassObject.transform.position = position + (direction * (nearPlaneDistance + 0.01f + NEAR_PLANE_OFFSET));
			//cameraGlassObject.transform.forward = camera.transform.forward;

			//UpdateMaterial();
			UpdateParticleEffects(position);
			
			//Hidden feature, control a low-pass filter based on camera submersion. Requires specific setup in project, hence requires detailed documentation.
			//Will be considered as a public feature later on
			if (environmentAudioMixer)
			{
				float submersion = CameraSubmersionAmount(Camera.main);

				environmentAudioMixer.SetFloat("UnderwaterCutoffFrequency", Mathf.Lerp(22000, lowPassCutoffFrequency, submersion));
			}
		}

		public void SetVisibility(bool isVisible)
		{
			if (meshRenderer != null)
			{
				meshRenderer.forceRenderingOff = !isVisible;
			}
		}

		//Purely used for fixing incorrect setups
		public void FindShaders()
		{
			underwaterResources = UnderwaterResources.Find();
		}
		
		private void UpdateParticleEffects(Vector3 cameraPosition)
		{
			var m_waterLevel = CurrentWaterLevel;

			for (int i = 0; i < particleEffects.Count; i++)
			{
				ParticleEffect particle = particleEffects[i];
				
				if(particle.particleSystem == null) continue;

				#if UNITY_EDITOR
				if (UnityEditor.EditorUtility.IsPersistent(particle.particleSystem))
				{
					Debug.LogError($"[{this.name}] Particle system of effect #{i} does not exist in the scene. Only scene objects can be assigned, not prefabs directly");
					continue;
				}
				#endif
				
				//Follow
				Vector3 position = cameraPosition;
				
				var min = m_waterLevel - particle.maxDepth;
				var max = m_waterLevel - particle.minDepth;
				
				var isPlaying = particle.particleSystem.isPlaying;
								
				if (isPlaying == false) particle.particleSystem.Play(true);
				
				//Clamp y-position
				position.y = Mathf.Clamp(position.y, min, max);

				//If this effect is set to emit over a distance, implement teleport protection
				if (particle.particleSystem.emission.rateOverDistance.constant > 0)
				{
					Vector3 particlePosition = particle.particleSystem.transform.position;
				
					float xzDistanceSqr = (new Vector2(position.x, position.z) - new Vector2(particlePosition.x, particlePosition.z)).sqrMagnitude;
				
					//Teleport protection
					if (xzDistanceSqr > (2f * 2f))
					{
						//Debug.Log($"{particle.particleSystem.name} protected against creating a trail (delta:{xzDistanceSqr})");
					
						particle.particleSystem.Stop(true);
						particle.particleSystem.Play(true);
					}
				}
				
				particle.particleSystem.transform.position = position;
				
				if (particle.alignToSunRotation)
				{
					if (RenderSettings.sun)
					{
						Vector3 sunEuler = RenderSettings.sun.transform.eulerAngles;
						particle.particleSystem.transform.forward = RenderSettings.sun.transform.forward;
						
						ParticleSystem.ShapeModule shape = particle.particleSystem.shape;
						shape.rotation = new Vector3(-sunEuler.x, 0f, 0f);
					}
				}
			}
		}
		
		public bool CameraIntersects(Camera targetCamera)
		{
			if (!boxCollider) return false;
			if (!boxCollider.isTrigger) return false;

			Vector3 nearPlanePosition = targetCamera.transform.position + (targetCamera.transform.forward * (targetCamera.nearClipPlane + waterlineOffset));
			
			//Transform to local-space to account for box-collider rotations
			nearPlanePosition = boxCollider.transform.InverseTransformPoint(nearPlanePosition);
			
			//Bounds is in world-space
			Vector3 center = boxCollider.center;
			Vector3 extents = boxCollider.size * 0.5f;
			
			bool isInside =
				nearPlanePosition.x >= center.x - extents.x && nearPlanePosition.x <= center.x + extents.x &&
				nearPlanePosition.y >= center.y - extents.y && nearPlanePosition.y <= center.y + extents.y &&
				nearPlanePosition.z >= center.z - extents.z && nearPlanePosition.z <= center.z + extents.z;
			
			return isInside;
		}

		public bool CameraSubmerged(Camera targetCamera)
		{
			return (UnderwaterUtilities.GetNearPlaneTopPosition(targetCamera, waterlineOffset).y + (WATER_LEVEL_PADDING)) < (CurrentWaterLevel - WATER_LEVEL_PADDING);
		}

		/// <summary>
		/// Calculate a normalized submersion value by measuring the distance of the water level between the camera frustum's bottom and top position.
		/// May be used to control audio effects such as a lowpass filter.
		/// </summary>
		/// <param name="targetCamera"></param>
		/// <returns></returns>
		public float CameraSubmersionAmount(Camera targetCamera)
		{
			float top = UnderwaterUtilities.GetNearPlaneTopPosition(targetCamera, waterlineOffset).y;
			float bottom = UnderwaterUtilities.GetNearPlaneBottomPosition(targetCamera, waterlineOffset).y;

			float height = top - bottom;
			
			float submergedHeight = Mathf.Clamp(waterLevel - bottom, 0f, height);

			return submergedHeight / height;
		}

		public float GetColliderPlaneHeight()
		{
			return boxCollider.bounds.center.y + boxCollider.bounds.extents.y;
		}

		//Sets the height of the collider's top
		public void SetColliderPlaneHeight(float height)
		{
			float bottom = (boxCollider.center.y - boxCollider.transform.position.y) - boxCollider.size.y * 0.5f;
			float newHeight = height - bottom;
			boxCollider.size = new Vector3(boxCollider.size.x, newHeight, boxCollider.size.z);
			boxCollider.center = new Vector3(boxCollider.center.x, bottom + newHeight * 0.5f, boxCollider.center.z);
		}
		
		//Sets the height of the collider's bottom
		public void SetColliderPlaneDepth(float depth)
		{
			float top = (boxCollider.center.y - boxCollider.transform.position.y) + boxCollider.size.y * 0.5f;
			float newHeight = top - depth;
			boxCollider.size = new Vector3(boxCollider.size.x, newHeight, boxCollider.size.z);
			boxCollider.center = new Vector3(boxCollider.center.x, depth + newHeight * 0.5f, boxCollider.center.z);
		}
		
		private void OnDrawGizmosSelected()
		{
			if (!boxCollider) return;
			
			Gizmos.matrix = Matrix4x4.TRS(boxCollider.transform.position, boxCollider.transform.rotation, boxCollider.transform.localScale);
			Gizmos.color = new Color(0.76f, 1f, 0.51f, 0.15f);
			Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
			
			Vector3 size = new Vector3(boxCollider.bounds.size.x, 0f, boxCollider.bounds.size.z);
			Vector3 position = this.transform.position;

			if (visualizeWaterLevel)
			{
				Gizmos.color = new Color(0.76f, 1f, 0.51f, 0.5f);

				Gizmos.matrix = Matrix4x4.identity;
				position.y = CurrentWaterLevel;
				Gizmos.DrawCube(position, size);
			}
			
			//Draw max depth
			//Gizmos.color = Color.black;
			//position.y = CurrentWaterLevel - shadingSettings.heightFogEndDepth;
		}
	}
}
