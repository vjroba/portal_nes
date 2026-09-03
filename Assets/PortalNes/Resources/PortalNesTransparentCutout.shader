Shader "PortalNes/Transparent Cutout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [PerRendererData] _InstanceTexST ("Instance Texture ST", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Back
        ZWrite On
        Blend Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;
            UNITY_INSTANCING_BUFFER_START(PortalNesProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTexST)
            UNITY_INSTANCING_BUFFER_END(PortalNesProps)
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.vertex = UnityObjectToClipPos(v.vertex);
                #if defined(UNITY_INSTANCING_ENABLED)
                    float4 textureST = UNITY_ACCESS_INSTANCED_PROP(PortalNesProps, _InstanceTexST);
                    o.uv = v.uv * textureST.xy + textureST.zw;
                #else
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                #endif
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv);
                clip(color.a - _Cutoff);
                return color;
            }
            ENDHLSL
        }
    }
}
