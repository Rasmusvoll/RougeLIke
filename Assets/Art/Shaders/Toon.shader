// Storybook toon look (see ArtSource/STYLE.md): flat colour, one hard shadow step tinted warm,
// and an ink outline drawn as an inverted hull.
//
// Outline: the hull is pushed out in screen space so lines stay a steady pixel width. Meshes built
// by ArtSource/build_*.py store smoothed normals in the vertex colour (rgb = n * 0.5 + 0.5) so the
// hull has no cracks on faceted shapes; set _SmoothNormals to 0 for meshes without them.
Shader "RougeLike/Toon"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.52, 0.56, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.05
        _ShadowSoftness ("Shadow Softness", Range(0.001, 0.5)) = 0.03
        _FacetShade ("Facet Shading", Range(0, 0.5)) = 0.12
        _GrainStrength ("Paper Grain", Range(0, 0.5)) = 0
        _GrainScale ("Paper Grain Scale", Float) = 1.5
        _OutlineColor ("Outline Colour", Color) = (0.17, 0.12, 0.1, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 8)) = 3
        [Toggle] _SmoothNormals ("Smoothed Normals In Vertex Colour", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "TOON"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _ShadowTint;
            float _ShadowThreshold, _ShadowSoftness, _FacetShade, _GrainStrength, _GrainScale;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

                // Painted-paper blotches for big flat surfaces like the board.
                if (_GrainStrength > 0)
                {
                    float2 p = i.worldPos.xz / _GrainScale;
                    float n = noise(p) * 0.6 + noise(p * 3.1) * 0.3 + noise(p * 9.7) * 0.1;
                    albedo.rgb *= 1 - _GrainStrength * (n - 0.5) * 2;
                }

                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = dot(n, l);
                float atten = SHADOW_ATTENUATION(i);
                float lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl);
                lit *= smoothstep(0.3, 0.6, atten);

                // Lit side keeps a hint of the facets so low-poly forms still read.
                float facet = 1 - _FacetShade * (1 - saturate(ndl));
                float3 light = lerp(_ShadowTint.rgb, facet.xxx, lit) * _LightColor0.rgb;
                return fixed4(albedo.rgb * light, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "Always" }
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth, _SmoothNormals;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
            };

            float4 vert(appdata v) : SV_POSITION
            {
                float3 n = v.normal;
                if (_SmoothNormals > 0.5)
                {
                    float3 s = v.color.rgb * 2 - 1;
                    // Ignore empty or white vertex colours; flip if the source mesh was mirrored.
                    if (dot(s, s) > 0.25 && dot(s, s) < 2.5) n = normalize(s) * (dot(s, v.normal) < -0.3 ? -1 : 1);
                }
                float4 pos = UnityObjectToClipPos(v.vertex);
                float3 viewN = mul((float3x3)UNITY_MATRIX_IT_MV, n);
                float2 dir = TransformViewToProjection(viewN.xy);
                float len = length(dir);
                if (len > 1e-5)
                    pos.xy += dir / len * (_OutlineWidth * 2 / _ScreenParams.xy) * pos.w;
                return pos;
            }

            fixed4 frag() : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }

    Fallback "Legacy Shaders/VertexLit"
}
