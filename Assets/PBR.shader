Shader "PBR"
{
    Properties
    {
        u_LightDirection("Light Direction", Vector) = (0, 0, 1, 0)
        u_LightColor("Light Color", Color) = (1, 1, 1, 1)
        u_DiffuseEnvSampler("Diffuse Env", Cube) = "white" {}
        u_SpecularEnvSampler("Specular Env", Cube) = "white" {}
        u_brdfLUT("brdf LUT", 2D) = "white" {}
        u_BaseColorSampler("Base Color Texture", 2D) = "white" {}
        u_BaseColorFactor("Base Color", Color) = (1, 1, 1, 1)
        u_NormalSampler("Normal Texture", 2D) = "bump" {}
        u_NormalScale("Normal Scale", Range(0, 10)) = 1
        u_EmissiveSampler("Emissive Texture", 2D) = "black" {}
        u_EmissiveFactor("Emissive", Color) = (0, 0, 0, 0)
        u_MetallicRoughnessSampler("Metallic Roughness Texture", 2D) = "white" {}
        u_Metallic("Metallic", Range(0, 1)) = 1
        u_Roughness("Roughness", Range(0, 1)) = 1
        u_OcclusionSampler("Occlusion Texture", 2D) = "bump" {}
        u_OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define vec2 float2
            #define vec3 float3
            #define vec4 float4
            #define samplerCube samplerCUBE
            #define texture2D tex2D
            #define textureCube texCUBE
            #define textureCubeLodEXT texCUBElod
            #define mix lerp
            #define u_Camera _WorldSpaceCameraPos
            #define M_PI 3.141592653589793
            #define c_MinRoughness 0.04

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 v_Position : TEXCOORD0;
                float2 v_UV : TEXCOORD4;
                half3 tspace0 : TEXCOORD1;
                half3 tspace1 : TEXCOORD2;
                half3 tspace2 : TEXCOORD3;
            };
            
            vec2 RevertUV(vec2 uv)
            {
                return vec2(uv.x, 1.0 - uv.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.v_Position = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 wNormal = UnityObjectToWorldNormal(v.normal);
                half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
                half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
                o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
                o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
                o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);
                o.v_UV = v.uv;
                return o;
            }

            uniform vec3 u_LightDirection;
            uniform vec4 u_LightColor;
            uniform samplerCube u_DiffuseEnvSampler;
            uniform samplerCube u_SpecularEnvSampler;
            uniform sampler2D u_brdfLUT;
            uniform sampler2D u_BaseColorSampler;
            uniform vec4 u_BaseColorFactor;
            uniform sampler2D u_NormalSampler;
            uniform float u_NormalScale;
            uniform sampler2D u_EmissiveSampler;
            uniform vec4 u_EmissiveFactor;
            uniform sampler2D u_MetallicRoughnessSampler;
            uniform float u_Metallic;
            uniform float u_Roughness;
            uniform sampler2D u_OcclusionSampler;
            uniform float u_OcclusionStrength;

            struct PBRInfo
            {
                float NdotL;                  // cos angle between normal and light direction
                float NdotV;                  // cos angle between normal and view direction
                float NdotH;                  // cos angle between normal and half vector
                float LdotH;                  // cos angle between light direction and half vector
                float VdotH;                  // cos angle between view direction and half vector
                float perceptualRoughness;    // roughness value, as authored by the model creator (input to shader)
                float metalness;              // metallic value at the surface
                vec3 reflectance0;            // full reflectance color (normal incidence angle)
                vec3 reflectance90;           // reflectance color at grazing angle
                float alphaRoughness;         // roughness mapped to a more linear change in the roughness (proposed by [2])
                vec3 diffuseColor;            // color contribution from diffuse lighting
                vec3 specularColor;           // color contribution from specular lighting
            };

            vec4 SRGBtoLINEAR(vec4 srgbIn)
            {
                vec3 linOut = pow(srgbIn.xyz, vec3(2.2, 2.2, 2.2));
                return vec4(linOut, srgbIn.w);
            }

            vec3 getNormal(v2f i)
            {
                half3 tnormal = UnpackNormal(tex2D(u_NormalSampler, i.v_UV));
                half3 worldNormal;
                worldNormal.x = dot(i.tspace0, tnormal);
                worldNormal.y = dot(i.tspace1, tnormal);
                worldNormal.z = dot(i.tspace2, tnormal);
                return normalize(worldNormal);
            }

            vec3 getIBLContribution(PBRInfo pbrInputs, vec3 n, vec3 reflection)
            {
                float mipCount = 9.0; // resolution of 512x512
                float lod = (pbrInputs.perceptualRoughness * mipCount);
                vec3 brdf = SRGBtoLINEAR(texture2D(u_brdfLUT, RevertUV(vec2(pbrInputs.NdotV, 1.0 - pbrInputs.perceptualRoughness)))).rgb;
                vec3 diffuseLight = SRGBtoLINEAR(textureCube(u_DiffuseEnvSampler, n)).rgb;

                vec3 specularLight = SRGBtoLINEAR(textureCubeLodEXT(u_SpecularEnvSampler, vec4(reflection, lod))).rgb;

                vec3 diffuse = diffuseLight * pbrInputs.diffuseColor;
                vec3 specular = specularLight * (pbrInputs.specularColor * brdf.x + brdf.y);
                return diffuse + specular;
            }

            vec3 diffuse(PBRInfo pbrInputs)
            {
                return pbrInputs.diffuseColor / M_PI;
            }

            vec3 specularReflection(PBRInfo pbrInputs)
            {
                return pbrInputs.reflectance0 + (pbrInputs.reflectance90 - pbrInputs.reflectance0) * pow(clamp(1.0 - pbrInputs.VdotH, 0.0, 1.0), 5.0);
            }

            float geometricOcclusion(PBRInfo pbrInputs)
            {
                float NdotL = pbrInputs.NdotL;
                float NdotV = pbrInputs.NdotV;
                float r = pbrInputs.alphaRoughness;
                float attenuationL = 2.0 * NdotL / (NdotL + sqrt(r * r + (1.0 - r * r) * (NdotL * NdotL)));
                float attenuationV = 2.0 * NdotV / (NdotV + sqrt(r * r + (1.0 - r * r) * (NdotV * NdotV)));
                return attenuationL * attenuationV;
            }

            float microfacetDistribution(PBRInfo pbrInputs)
            {
                float roughnessSq = pbrInputs.alphaRoughness * pbrInputs.alphaRoughness;
                float f = (pbrInputs.NdotH * roughnessSq - pbrInputs.NdotH) * pbrInputs.NdotH + 1.0;
                return roughnessSq / (M_PI * f * f);
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                float perceptualRoughness = u_Roughness;
                float metallic = u_Metallic;
                
                vec4 mrSample = texture2D(u_MetallicRoughnessSampler, i.v_UV);
                perceptualRoughness = mrSample.g * perceptualRoughness;
                metallic = mrSample.b * metallic;
                perceptualRoughness = clamp(perceptualRoughness, c_MinRoughness, 1.0);
                metallic = clamp(metallic, 0.0, 1.0);
                float alphaRoughness = perceptualRoughness * perceptualRoughness;

                vec4 baseColor = SRGBtoLINEAR(texture2D(u_BaseColorSampler, i.v_UV)) * u_BaseColorFactor;
                vec3 f0 = vec3(0.04, 0.04, 0.04);
                vec3 diffuseColor = baseColor.rgb * (vec3(1.0, 1.0, 1.0) - f0);
                diffuseColor *= 1.0 - metallic;
                vec3 specularColor = mix(f0, baseColor.rgb, metallic);
                float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);
                float reflectance90 = clamp(reflectance * 25.0, 0.0, 1.0);
                vec3 specularEnvironmentR0 = specularColor.rgb;
                vec3 specularEnvironmentR90 = vec3(1.0, 1.0, 1.0) * reflectance90;

                vec3 n = getNormal(i);                            // normal at surface point
                vec3 v = normalize(u_Camera - i.v_Position);      // Vector from surface point to camera
                vec3 l = normalize(-u_LightDirection);            // Vector from surface point to light
                vec3 h = normalize(l + v);                        // Half vector between both l and v
                vec3 reflection = -normalize(reflect(v, n));

                float NdotL = clamp(dot(n, l), 0.001, 1.0);
                float NdotV = abs(dot(n, v)) + 0.001;
                float NdotH = clamp(dot(n, h), 0.0, 1.0);
                float LdotH = clamp(dot(l, h), 0.0, 1.0);
                float VdotH = clamp(dot(v, h), 0.0, 1.0);

                PBRInfo pbrInputs = {
                    NdotL,
                    NdotV,
                    NdotH,
                    LdotH,
                    VdotH,
                    perceptualRoughness,
                    metallic,
                    specularEnvironmentR0,
                    specularEnvironmentR90,
                    alphaRoughness,
                    diffuseColor,
                    specularColor
                };

                // Calculate the shading terms for the microfacet specular shading model
                vec3 F = specularReflection(pbrInputs);
                float G = geometricOcclusion(pbrInputs);
                float D = microfacetDistribution(pbrInputs);

                // Calculation of analytical lighting contribution
                vec3 diffuseContrib = (1.0 - F) * diffuse(pbrInputs);
                vec3 specContrib = F * G * D / (4.0 * NdotL * NdotV);

                // Obtain final intensity as reflectance (BRDF) scaled by the energy of the light (cosine law)
                vec3 color = NdotL * u_LightColor.rgb * (diffuseContrib + specContrib);

                // Calculate lighting contribution from image based lighting source (IBL)
                color += getIBLContribution(pbrInputs, n, reflection);

                // Apply optional PBR terms for additional (optional) shading
                float ao = texture2D(u_OcclusionSampler, i.v_UV).r;
                color = mix(color, color * ao, u_OcclusionStrength);

                vec3 emissive = SRGBtoLINEAR(texture2D(u_EmissiveSampler, i.v_UV)).rgb * u_EmissiveFactor.rgb;
                color += emissive;

                return vec4(pow(color, vec3(1.0 / 2.2, 1.0 / 2.2, 1.0 / 2.2)), baseColor.a);
            }
            ENDCG
        }
    }
}
