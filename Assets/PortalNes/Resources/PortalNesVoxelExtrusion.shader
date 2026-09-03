Shader "PortalNes/Voxel Extrusion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _IgnoredEdgeColor ("Ignored Edge Color", Color) = (0,0,0,1)
        _IgnoredEdgeTolerance ("Ignored Edge Tolerance", Range(0,0.5)) = 0.05
        [PerRendererData] _InstanceTexST ("Instance Texture ST", Vector) = (1,1,0,0)
        [PerRendererData] _InstanceMask ("Instance Mask", Vector) = (0,0,0,0)
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
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 objectPosition : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FragmentOutput
            {
                fixed4 color : SV_Target;
                float depth : SV_Depth;
            };

            sampler2D _MainTex;
            fixed4 _IgnoredEdgeColor;
            float _IgnoredEdgeTolerance;

            UNITY_INSTANCING_BUFFER_START(PortalNesVoxelProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceTexST)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceMask)
            UNITY_INSTANCING_BUFFER_END(PortalNesVoxelProps)

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.objectPosition = v.vertex.xyz;
                return o;
            }

            bool IsOccupied(uint low, uint high, int cell)
            {
                return cell < 32 ? ((low >> cell) & 1u) != 0u :
                    ((high >> (cell - 32)) & 1u) != 0u;
            }

            bool IntersectBox(float3 origin, float3 direction, float3 minimum,
                float3 maximum, out float nearDistance)
            {
                float3 inverseDirection = 1.0 / direction;
                float3 first = (minimum - origin) * inverseDirection;
                float3 second = (maximum - origin) * inverseDirection;
                float3 nearer = min(first, second);
                float3 farther = max(first, second);
                float nearValue = max(max(nearer.x, nearer.y), nearer.z);
                float farValue = min(min(farther.x, farther.y), farther.z);
                nearDistance = max(nearValue, 0.0);
                return farValue >= nearDistance;
            }

            fixed4 SampleCell(float4 textureST, int x, int y)
            {
                float2 localUv = float2((x + 0.5) / 8.0, 1.0 - (y + 0.5) / 8.0);
                return tex2D(_MainTex, localUv * textureST.xy + textureST.zw);
            }

            FragmentOutput frag(v2f i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 packed = UNITY_ACCESS_INSTANCED_PROP(PortalNesVoxelProps, _InstanceMask);
                uint low = (uint)round(packed.x) | ((uint)round(packed.y) << 16);
                uint high = (uint)round(packed.z) | ((uint)round(packed.w) << 16);
                float3 rayOrigin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 rayDirection = normalize(i.objectPosition - rayOrigin);

                float nearest = 1e20;
                int hitX = -1;
                int hitY = -1;
                [unroll] for (int y = 0; y < 8; y++)
                {
                    [unroll] for (int x = 0; x < 8; x++)
                    {
                        int cell = y * 8 + x;
                        if (!IsOccupied(low, high, cell)) continue;
                        float3 minimum = float3(-0.5 + x / 8.0,
                            0.5 - (y + 1) / 8.0, 0.0);
                        float3 maximum = float3(-0.5 + (x + 1) / 8.0,
                            0.5 - y / 8.0, 1.0);
                        float distance;
                        if (IntersectBox(rayOrigin, rayDirection, minimum, maximum, distance) &&
                            distance < nearest)
                        {
                            nearest = distance;
                            hitX = x;
                            hitY = y;
                        }
                    }
                }
                clip(hitX + 0.5);

                float4 textureST = UNITY_ACCESS_INSTANCED_PROP(
                    PortalNesVoxelProps, _InstanceTexST);
                fixed4 color = SampleCell(textureST, hitX, hitY);
                float3 hitPosition = rayOrigin + rayDirection * nearest;
                bool side = hitPosition.z > 0.001 && hitPosition.z < 0.999;
                float3 difference = color.rgb - _IgnoredEdgeColor.rgb;
                if (side && dot(difference, difference) <=
                    _IgnoredEdgeTolerance * _IgnoredEdgeTolerance)
                {
                    int bestDistance = 999;
                    [unroll] for (int candidateY = 0; candidateY < 8; candidateY++)
                    {
                        [unroll] for (int candidateX = 0; candidateX < 8; candidateX++)
                        {
                            int candidate = candidateY * 8 + candidateX;
                            if (!IsOccupied(low, high, candidate)) continue;
                            fixed4 candidateColor = SampleCell(textureST, candidateX, candidateY);
                            float3 candidateDifference =
                                candidateColor.rgb - _IgnoredEdgeColor.rgb;
                            if (dot(candidateDifference, candidateDifference) <=
                                _IgnoredEdgeTolerance * _IgnoredEdgeTolerance) continue;
                            int distance = abs(candidateX - hitX) + abs(candidateY - hitY);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                color = candidateColor;
                            }
                        }
                    }
                }
                clip(color.a - 0.5);
                float4 hitClip = UnityObjectToClipPos(float4(hitPosition, 1));
                FragmentOutput output;
                output.color = color;
                output.depth = hitClip.z / hitClip.w;
                return output;
            }
            ENDHLSL
        }
    }
}
