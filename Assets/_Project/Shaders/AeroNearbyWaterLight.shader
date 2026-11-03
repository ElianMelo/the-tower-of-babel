Shader "Hidden/TowerOfBabel/Nearby Water Light"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Nearby caustics and light glints"
            ZWrite Off ZTest Always Cull Off
            Blend DstColor One
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define USE_FULL_PRECISION_BLIT_TEXTURE
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            // 16-frame CC0 caustics flipbook (Environment/Lighting/Caustics/PoolCausticsFlipbook.png).
            TEXTURE2D_ARRAY(_AeroCaustics); SAMPLER(sampler_AeroCaustics);
            #include "Aero/AeroCaustics.hlsl"
            #define AERO_POOL_FRAMES 16
            TEXTURECUBE(_AeroEnvironment); SAMPLER(sampler_AeroEnvironment);
            float4 _AeroRange, _AeroMotion;
            half4 _AeroTint;
            int _AeroRegionCount;
            float4 _AeroRegionCenters[8], _AeroRegionShapes[8], _AeroRegionLimits[8];

            float3 PositionAt(float2 uv)
            {
                float d = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
                #if !UNITY_REVERSED_Z
                    d = lerp(UNITY_NEAR_CLIP_VALUE, 1, d);
                #endif
                return ComputeWorldSpacePosition(uv, d, UNITY_MATRIX_I_VP);
            }

            float RegionWeight(float3 position)
            {
                float weight = 0;
                [loop] for (int i = 0; i < _AeroRegionCount; i++)
                {
                    float3 p = position - _AeroRegionCenters[i].xyz;
                    float4 shape = _AeroRegionShapes[i];
                    float2 local = float2(p.x * shape.z - p.z * shape.w, p.x * shape.w + p.z * shape.z);
                    float radius = length(local / shape.xy);
                    float edge = _AeroRegionCenters[i].w / min(shape.x, shape.y);
                    float vertical = p.y >= 0 ? p.y / _AeroRegionLimits[i].x : -p.y / _AeroRegionLimits[i].y;
                    weight = max(weight, (1-smoothstep(1, 1+edge, radius)) * (1-smoothstep(.35, 1, vertical)));
                }
                return weight;
            }

            // Two layers of the animated flipbook at unrelated angles and scales; their minimum keeps fine,
            // sharp crossings instead of broad white blobs. lod blurs distant tiles so they do not shimmer.
            half WaterLight(float2 uv, float clock, float lod)
            {
                float frame = clock * 80;
                float2 b = float2(.766 * uv.x - .643 * uv.y, .643 * uv.x + .766 * uv.y) * 1.37 + float2(.41, .23) + clock * float2(.9, -.6);
                return AeroLayeredFlipbook(TEXTURE2D_ARRAY_ARGS(_AeroCaustics, sampler_AeroCaustics), uv + clock * float2(-.5, .8), b, frame, AERO_POOL_FRAMES, lod);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_AeroRegionCount == 0) return 0;
                float depth = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0).r;
                #if UNITY_REVERSED_Z
                    bool sky = depth < .000001;
                #else
                    bool sky = depth > .999999;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
                #endif
                float3 position = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                if (sky) return 0;
                float sourceWeight = RegionWeight(position);
                if (sourceWeight <= 0) return 0;
                // Choose neighbours on the same surface. Quad derivatives straddled sky/geometry,
                // producing invalid reflection coordinates and black/white lines that bloom spread.
                float2 pixel = _BlitTexture_TexelSize.xy;
                float3 left = position - PositionAt(uv-float2(pixel.x,0));
                float3 right = PositionAt(uv+float2(pixel.x,0)) - position;
                float3 down = position - PositionAt(uv-float2(0,pixel.y));
                float3 up = PositionAt(uv+float2(0,pixel.y)) - position;
                float3 dx = dot(left,left) < dot(right,right) ? left : right;
                float3 dy = dot(down,down) < dot(up,up) ? down : up;
                float3 normal = SafeNormalize(cross(dy,dx));
                float3 view = GetWorldSpaceNormalizeViewDir(position);
                normal *= dot(normal, view) < 0 ? -1 : 1;
                float metres = distance(position, _WorldSpaceCameraPos);
                if (metres >= _AeroRange.x) return 0;
                float fade = 1 - smoothstep(_AeroRange.y, _AeroRange.x, metres);
                float clock = (_AeroMotion.z >= 0 ? _AeroMotion.z : _Time.y) * _AeroMotion.y;
                // World-space triplanar projection stays attached to surfaces as the camera moves.
                float3 weights = pow(abs(normal), 4);
                weights /= max(dot(weights, 1), .0001);
                float3 p = position * _AeroMotion.x;
                float lod = max(0, log2(max(metres * _AeroMotion.x * .27, 1e-4)));
                half pattern = WaterLight(p.zy, clock, lod) * weights.x + WaterLight(p.xz, clock, lod) * weights.y + WaterLight(p.xy, clock, lod) * weights.z;
                // Broad drifting reflected light accompanies the narrow focused caustic strands.
                half swell = pow(saturate(.5 + .5 * sin(dot(p, float3(.9,.45,.75)) * 2 - clock * 1.7)), 6);
                Light sun = GetMainLight();
                half facing = .5 + .5 * saturate(dot(normal, sun.direction));
                half glint = pow(saturate(dot(reflect(-sun.direction, normal), view)), 64);
                half3 light = _AeroTint.rgb * (pattern * _AeroRange.z * facing + swell * _AeroRange.w * .2);
                light += sun.color * glint * _AeroRange.w * (.4 + pattern);
                float3 reflected = reflect(-view, normal);
                reflected += sin(p.yzx * 1.3 + clock * 2) * .018;
                half3 environment = SAMPLE_TEXTURECUBE_LOD(_AeroEnvironment, sampler_AeroEnvironment, reflected, 2).rgb;
                // Keep the HDR studio highlights from whitening matte terrain and pale props.
                environment = min(environment, half3(4,4,4));
                half fresnel = .07 + .45 * pow(1-saturate(dot(normal,view)),3);
                light += environment * fresnel * _AeroRange.w;
                // Finite, nonnegative light cannot punch dark outlines into the color attachment.
                light = all(isfinite(light)) ? clamp(light, 0, 4) : 0;
                return half4(light * fade * sourceWeight, 0);
            }
            ENDHLSL
        }
    }
}
