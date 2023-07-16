Shader "Voxel/Compositing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SmokeTexArray ("_SmokeTexArray (A)", 2DArray) = "" {}
    }
    SubShader
    {
        CGINCLUDE
        #include "UnityCG.cginc"
        
        #include "UnityStandardBRDF.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float4 _MainTex_ST;
        
        struct appdata {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };
        
        v2f vert (appdata v) {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            return o;
        }
        
        ENDCG
        
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray
            
            UNITY_DECLARE_TEX2DARRAY(_DepthTexArray);
            UNITY_DECLARE_TEX2DARRAY(_SmokeTexArray);
            int _SmokesCount;
            
            int getNearestIndex(float2 uv, int excluded[8]) {
                int nearest = -1;
                float depth = 0;
                
                [loop]
                for (int j = 0; j < _SmokesCount; j++) {
                    bool isExcluded = false;
                    
                    for (int k = 0; k < 8; k++) {
                        if (excluded[k] == j) {
                            isExcluded = true; 
                            break;
                        }
                    }
                    
                    if (isExcluded)
                        continue;
                                        
                    float d = UNITY_SAMPLE_TEX2DARRAY(_DepthTexArray, float3(uv, j)).r;
                    float a = UNITY_SAMPLE_TEX2DARRAY(_SmokeTexArray, float3(uv, j)).a;
                    
                    if (a > 0 && (depth == 0 || d < depth)) {
                        depth = d;
                        nearest = j;
                    }
                }
                
                return nearest;
            }
            
            float4 getCol(float2 uv, float4 mainCol, int nearest) {
                if (nearest == -1)
                    return mainCol;
                    
                return UNITY_SAMPLE_TEX2DARRAY(_SmokeTexArray, float3(uv, nearest));
            }

            float4 frag(v2f i) : SV_Target {
                float4 mainCol = tex2D(_MainTex, i.uv);
                float4 smokeCol = 0;
                int excluded[8];
                
                for (int j = 0; j < 8; j++)
                    excluded[j] = -1;
                
                int count = min(7, _SmokesCount);                
                for (int k = 0; k <= count + 1; k++) {
                    excluded[k] = getNearestIndex(i.uv, excluded);
                    smokeCol += getCol(i.uv, mainCol, excluded[k]) * (1 - smokeCol.a);
                }
                
                return smokeCol;
            }
            ENDCG
        }
    }
}
