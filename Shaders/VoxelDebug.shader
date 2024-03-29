Shader "Voxel/VoxelDebugShader" {
    Properties {
        _MainTex ("Albedo (RGB)", 2D) = "white"
        _VoxelScale ("Voxel Scale", Float) = 1
    }
    SubShader {

        Pass {

            Tags
            {
                "RenderType"="Opaque"
            }

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma target 4.5

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;

            float _VoxelScale;

        	struct voxel {
                float3 Position;
                float3 Color;
            };

            StructuredBuffer<voxel> voxelBuffer;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv_MainTex : TEXCOORD0;
                float3 ambient : TEXCOORD1;
                float3 diffuse : TEXCOORD2;
                float3 color : TEXCOORD3;
                SHADOW_COORDS(4)
            };

            v2f vert (appdata_full v, uint instanceID : SV_InstanceID)
            {
				voxel data = voxelBuffer[instanceID];

                float3 localPosition = v.vertex.xyz;
                float3 worldPosition = data.Position.xyz + localPosition;
                float3 worldNormal = v.normal;

                float3 ndotl = saturate(dot(worldNormal, _WorldSpaceLightPos0.xyz));
                float3 ambient = ShadeSH9(float4(worldNormal, 1.0f));
                float3 diffuse = (ndotl * _LightColor0.rgb);

                v2f o;
                o.pos = UnityObjectToClipPos(worldPosition*_VoxelScale);
                o.uv_MainTex = v.texcoord;
                o.ambient = ambient;
                o.diffuse = diffuse;
                o.color = data.Color;

                TRANSFER_SHADOW(o)
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed shadow = SHADOW_ATTENUATION(i);
                fixed4 albedo = tex2D(_MainTex, i.uv_MainTex);
                float3 lighting = i.diffuse * shadow + i.ambient * 0.5;
                fixed4 output = fixed4(albedo.rgb * i.color * lighting, 1);

                return output;
            }

            ENDCG
        }
    }
}