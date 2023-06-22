Shader "Voxel/VoxelShader" {
    Properties {
		_MainTex ("Texture", 2D) = "white" {}
		_VoxelResolution ("Voxel resolution", Vector) = (0, 0, 0, 0)
		v_offset ("v_offset", Vector) = (0, 0, 0, 0)
    }
    SubShader {
		Cull Off ZWrite Off ZTest Always
		
        Pass {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
			
			#pragma target 3.0
			
			#include "UnityCG.cginc"
			

            struct voxel {
                float3 Position;
            };

            struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
                float3 rayDirection : TEXCOORD1;
			};
            
            float3 _VoxelResolution;
            StructuredBuffer<voxel> voxelBuffer;
			int voxelsCount;
			float3 boundsMin, boundsMax, boundsExtent;
			
            v2f vert (appdata v, uint instanceID : SV_InstanceID) {
                v2f o;
				
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				
				float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
				o.rayDirection = mul(unity_CameraToWorld, float4(viewVector,0));
				
				return o;
            }
			
			sampler2D _MainTex;
			sampler2D _CameraDepthTexture;

			// Noise
			Texture3D<float4> NoiseTex;
			SamplerState samplerNoiseTex;
			float scale;
			float densityMultiplier;
			float densityOffset;

			// Ray-March
			int marchSteps;
			int lightmarchSteps;
			float rayOffset;
			Texture2D<float4> BlueNoise;
			SamplerState samplerBlueNoise;

			// Lighting 
			float4 scatterColor;
			float brightness;
			float transmitThreshold;
			float inScatterMultiplier;
			float outScatterMultiplier;
			float forwardScatter;
			float backwardScatter;
			float scatterMultiplier;
			float4 _LightColor0;

			// Transform
			float3 cloudSpeed;

			#define MOD3 float3(.16532,.17369,.15787)
			
			// Used to scale the blue-noise to fit the view
			float2 scaleUV(float2 uv, float scale) {
				float x = uv.x * _ScreenParams.x;
				float y = uv.y * _ScreenParams.y;
				return float2 (x,y)/scale;
			}

			float maxComponent(float3 vec) {
				return max(max(vec.x, vec.y), vec.z);
			}
			float minComponent(float3 vec) {
				return min(min(vec.x, vec.y), vec.z);
			}

			// Returns min and max t distances
			float2 slabs(float3 p1, float3 p2, float3 rayPos, float3 invRayDir) {
				float3 t1 = (p1 - rayPos) * invRayDir;
				float3 t2 = (p2 - rayPos) * invRayDir;
				return float2(maxComponent(min(t1, t2)), minComponent(max(t1, t2)));
			}

			// Returns the distance to cloud box (x) and distance inside cloud box (y)
			float2 rayBox(float3 boundsMin, float3 boundsMax, float3 rayPos, float3 invRayDir) {
				float2 slabD = slabs(boundsMin, boundsMax, rayPos, invRayDir);
				float toBox = max(0, slabD.x);
				return float2(toBox, max(0, slabD.y - toBox));
			}

			float henyeyGreenstein(float g, float angle) {
				return (1.0f - pow(g,2)) / (4.0f * 3.14159 * pow(1 + pow(g, 2) - 2.0f * g * angle, 1.5f));
			}

			float hgScatter(float angle) {
				float scatterAverage = (henyeyGreenstein(forwardScatter, angle) + henyeyGreenstein(-backwardScatter, angle)) / 2.0f;
				// Scale the brightness by sun position
				float sunPosModifier = 1.0f;
				if (_WorldSpaceLightPos0.y < 0) {
					sunPosModifier = pow(_WorldSpaceLightPos0.y + 1,3);
				}
				return brightness * sunPosModifier + scatterAverage * scatterMultiplier;
			}

			float beer(float d) {
				return exp(-d);
			}

			float Hash(float3 p)
			{
				p = frac(p * MOD3);
				p += dot(p.xyz, p.yzx + 19.19);
				return frac(p.x * p.y * p.z);
			}

			float Noise(in float3 p)
			{
				float3 i = floor(p);
				float3 f = frac(p);
				f *= f * (3.0 - 2.0*f);

				return lerp(
					lerp(lerp(Hash(i + float3(0., 0., 0.)), Hash(i + float3(1., 0., 0.)), f.x),
						lerp(Hash(i + float3(0., 1., 0.)), Hash(i + float3(1., 1., 0.)), f.x),
						f.y),
					lerp(lerp(Hash(i + float3(0., 0., 1.)), Hash(i + float3(1., 0., 1.)), f.x),
						lerp(Hash(i + float3(0., 1., 1.)), Hash(i + float3(1., 1., 1.)), f.x),
						f.y),
					f.z);
			}

			float FBM(float3 p, float freq = .25)
			{
				p *= freq;
				float f;

				f = 0.5000		* Noise(p); p = p * 3.02;
				f += 0.2500		* Noise(p); p = p * 3.03;
				f += 0.1250		* Noise(p); p = p * 3.01;
				f += 0.0625		* Noise(p); p = p * 3.03;
				f += 0.03125	* Noise(p); p = p * 3.02;
				f += 0.015625	* Noise(p);
				return f;
			}
			
			float3 _SmokeOrigin;
			float _Radius;
			float _DensityFalloff;
			
			sampler3D voxelGrid;
			
			float3 SnapToGrid(float3 position, float gridSize) {
				float3 snappedPosition = float3(
					round(position.x / gridSize) * gridSize + gridSize / 2,
					round(position.y / gridSize) * gridSize + gridSize / 2,
					round(position.z / gridSize) * gridSize + gridSize / 2
				);
				return snappedPosition;
			}
			
			float3 world_to_box(float3 world_pos, float3 box_pos, float3 box_size, bool snap = true) {
				float3 relativePos = snap ? SnapToGrid(world_pos - box_pos, 1) : world_pos - box_pos;
				return relativePos / box_size;
			}

			float densityAtPosition(float3 rayPos) {
				float n = max(0, FBM(rayPos + cloudSpeed*_Time.x, scale) - densityOffset) * densityMultiplier;
				
				float3 uvw = world_to_box(rayPos, boundsMin, boundsExtent, false);
				float vp = (tex3Dlod(voxelGrid, float4(uvw, 0)).g);
				
				// float v = vp;
	
				float dist = min(1.0f, 1 - vp);
				// float voxelDist = min(1.0f, 1 - (v / 16.0f));
				// dist = max(dist, voxelDist);
	
				dist = smoothstep(_DensityFalloff, 1.0f, dist);
				
				float falloff = min(1.0f, dist + n);
				
				return 1 - falloff;
			}
			
			float3 v_offset;
			float3 boundsCenter;
			
			// Calculate proportion of light that reaches the given point from the lightsource
			float lightmarch(float3 position) {
				float3 L = _WorldSpaceLightPos0.xyz;
				
				float stepSize = rayBox(boundsMin, boundsMax, position, 1 / L).y / lightmarchSteps;
				
				float density = 0;

				for (int i = 0; i < lightmarchSteps; i++) {
					position += L * stepSize;
					density += max(0, densityAtPosition(position) * stepSize);
				}

				float transmit = beer(density * (1 - outScatterMultiplier));
				return lerp(transmit, 1, transmitThreshold);
			} 
			
            float4 frag (v2f i) : SV_Target {
				float3 rayOrigin = _WorldSpaceCameraPos;
				float viewLength = length(i.rayDirection);
                float3 rayDir = i.rayDirection / viewLength;
                
                float nonlin_depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float sceneDepth = LinearEyeDepth(nonlin_depth) * viewLength;
				
				float2 rayBoxInfo = rayBox(boundsMin, boundsMax, rayOrigin, 1/rayDir);
				
				if (rayBoxInfo.y == 0)
					return tex2D(_MainTex, i.uv); 
				
				float3 entryPos = rayOrigin + rayDir * rayBoxInfo.x;
					
				// Henyey-Greenstein scatter
				float scatter = hgScatter(dot(rayDir, _WorldSpaceLightPos0.xyz));

				// Blue Noise
				float randomOffset = BlueNoise.SampleLevel(samplerBlueNoise, scaleUV(i.uv, 72), 0);
				float offset = randomOffset * rayOffset;

				float stepLimit = min(sceneDepth - rayBoxInfo.x, rayBoxInfo.y);
				
				float stepSize = rayBoxInfo.y / marchSteps; 
				float transmit = 1;
				
				float3 I = 0; // Illumination
				
				[loop]
				for (float steps = offset; steps < stepLimit; steps += stepSize) {
					float3 samplePos = entryPos + rayDir * steps;
					float sampleDensity = 0;
					
					float3 uvw = world_to_box(samplePos, boundsMin, boundsExtent);
					
					if (saturate(tex3Dlod(voxelGrid, float4(uvw, 0)).r) == 1)
						sampleDensity = 1;
					
					if (sampleDensity > 0) {
						I += sampleDensity * transmit * 1 * scatter;
						transmit *= beer(sampleDensity  * (1 - inScatterMultiplier));
					}
				}
                
                float3 color = (I * _LightColor0 * scatterColor) + tex2D(_MainTex, i.uv) * transmit;
				return float4 (color, 0);
            }

            ENDCG
        }
	}
}
