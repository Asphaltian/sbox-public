
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"

	Feature( F_ALPHA_TEST, 0..1, "Rendering" );
	Feature( F_TRANSLUCENT, 0..1, "Rendering" );
	Feature( F_SELF_ILLUM, 0..1, "Rendering" );
	Feature( F_NORMAL_MAP, 0..1, "Rendering" ); 
    Feature( F_SSBUMP, 0..1, "Rendering" );
	Feature( F_DETAIL, 0..1, "Detail" );
	Feature( F_BASETEXTURE2, 0..1, "Blending" );
    Feature( F_MASKED_BLENDING, 0..1, "Blending" );
	Feature( F_ENVMAP, 0..1, "Environment Map" );
	Feature( F_ENVMAP_MASK, 0..1, "Environment Map" );
	Feature( F_LIGHT_WARP, 0..1, "Lighting" );

	FeatureRule( Allow1( F_ALPHA_TEST, F_TRANSLUCENT ), "Alpha Test and Translucent are mutually exclusive" );
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#include "common/shared.hlsl"
	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"

	StaticCombo( S_ALPHA_TEST, F_ALPHA_TEST, Sys( ALL ) );
	StaticCombo( S_SELF_ILLUM, F_SELF_ILLUM, Sys( ALL ) );
	StaticCombo( S_NORMAL_MAP, F_NORMAL_MAP, Sys( ALL ) );
	StaticCombo( S_DETAIL, F_DETAIL, Sys( ALL ) );
	StaticCombo( S_BASETEXTURE2, F_BASETEXTURE2, Sys( ALL ) );
	StaticCombo( S_ENVMAP, F_ENVMAP, Sys( ALL ) );
	StaticCombo( S_ENVMAP_MASK, F_ENVMAP_MASK, Sys( ALL ) );
	StaticCombo( S_SSBUMP, F_SSBUMP, Sys( ALL ) );
	StaticCombo( S_LIGHT_WARP, F_LIGHT_WARP, Sys( ALL ) );
	StaticCombo( S_MASKED_BLENDING, F_MASKED_BLENDING, Sys( ALL ) );

	SamplerState g_sSampler0 < Filter( Anisotropic ); AddressU( WRAP ); AddressV( WRAP ); >;

	CreateInputTexture2D( Color, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( Color2, Srgb, 8, "None", "_color2", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tColor2 < Channel( RGBA, Box( Color2 ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( Normal, Linear, 8, "NormalizeNormals", "_normal", ",0/,0/0", Default4( 0.5, 0.5, 1.0, 1.0 ) );
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( Normal2, Linear, 8, "NormalizeNormals", "_normal2", ",0/,0/0", Default4( 0.5, 0.5, 1.0, 1.0 ) );
	Texture2D g_tNormal2 < Channel( RGBA, Box( Normal2 ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( Detail, Srgb, 8, "None", "_detail", ",0/,0/0", Default4( 0.5, 0.5, 0.5, 1.0 ) );
	Texture2D g_tDetail < Channel( RGBA, Box( Detail ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( EnvMapMask, Linear, 8, "None", "_envmapmask", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tEnvMapMask < Channel( RGBA, Box( EnvMapMask ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( BlendModulationTexture, Linear, 8, "None", "_blendmod", ",0/,0/0", Default4( 0.5, 0.5, 0.5, 1.0 ) );
	Texture2D g_tBlendModulation < Channel( RGBA, Box( BlendModulationTexture ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( LightWarpTexture, Srgb, 8, "None", "_lightwarp", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tLightWarp < Channel( RGB, Box( LightWarpTexture ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	float3 g_vColor < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flAlpha < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	float3 g_vSelfIllumTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float3 g_vEnvmapTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flEnvmapContrast < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flEnvmapSaturation < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	float g_flFresnelReflection < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	int g_nBaseAlphaEnvMapMask < Default( 0 ); >;
	int g_nNormalMapAlphaEnvMapMask < Default( 0 ); >;
	int g_nBaseTextureNoEnvmap < Default( 0 ); >;
	int g_nBaseTexture2NoEnvmap < Default( 0 ); >;
	int g_nNoDiffuseBumpLighting < Default( 0 ); >;
	float g_flBlendAmount < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	int g_nFancyBlending < Default( 0 ); >;
	float g_flDetailScale < Default( 4.0 ); Range( 0.0, 32.0 ); >;
	float g_flDetailBlendFactor < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	int g_nDetailBlendMode < Default( 0 ); >;
	float3 g_vDetailTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;

	#define SSBUMP_BASIS0 float3( 0.8164966, 0.0, 0.5773503 )
	#define SSBUMP_BASIS1 float3( -0.4082483, 0.7071068, 0.5773503 )
	#define SSBUMP_BASIS2 float3( -0.4082483, -0.7071068, 0.5773503 )

	float4 TextureCombine( float4 baseColor, float4 detailColor, int nCombineMode, float fBlendFactor )
	{
		if ( nCombineMode == 7 )
		{
			float3 dc = lerp( detailColor.rrr, detailColor.aaa, baseColor.a );
			baseColor.rgb *= lerp( float3( 1, 1, 1 ), 2.0 * dc, fBlendFactor );
		}
		if ( nCombineMode == 0 )
			baseColor.rgb *= lerp( float3( 1, 1, 1 ), 2.0 * detailColor.rgb, fBlendFactor );
		if ( nCombineMode == 1 )
			baseColor.rgb += fBlendFactor * detailColor.rgb;
		if ( nCombineMode == 2 )
		{
			float fblend = fBlendFactor * detailColor.a;
			baseColor.rgb = lerp( baseColor.rgb, detailColor.rgb, fblend );
		}
		if ( nCombineMode == 3 )
			baseColor = lerp( baseColor, detailColor, fBlendFactor );
		if ( nCombineMode == 4 )
		{
			float fblend = fBlendFactor * ( 1 - baseColor.a );
			baseColor.rgb = lerp( baseColor.rgb, detailColor.rgb, fblend );
			baseColor.a = detailColor.a;
		}
		if ( nCombineMode == 8 )
			baseColor = lerp( baseColor, baseColor * detailColor, fBlendFactor );
		if ( nCombineMode == 9 )
			baseColor.a = lerp( baseColor.a, baseColor.a * detailColor.a, fBlendFactor );
		if ( nCombineMode == 11 )
			baseColor.rgb = baseColor.rgb * dot( detailColor.rgb, 2.0 / 3.0 );
		return baseColor;
	}

	float3 TextureCombinePostLighting( float3 litBaseColor, float4 detailColor, int nCombineMode, float fBlendFactor )
	{
		if ( nCombineMode == 5 )
			litBaseColor += fBlendFactor * detailColor.rgb;
		if ( nCombineMode == 6 )
		{
			float f = fBlendFactor - 0.5;
			float fMult = ( f >= 0 ) ? 1.0 / fBlendFactor : 4 * fBlendFactor;
			float fAdd = ( f >= 0 ) ? 1.0 - fMult : -0.5 * fMult;
			litBaseColor += saturate( fMult * detailColor.rgb + fAdd );
		}
		return litBaseColor;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );

		float2 uv = i.vTextureCoords.xy;
		float4 baseColor = Tex2DS( g_tColor, g_sSampler0, uv );
		float3 albedo = baseColor.rgb * g_vColor;

		float3 worldPos = i.vPositionWithOffsetWs + g_vHighPrecisionLightingOffsetWs.xyz;
		m.WorldPosition = worldPos;
		m.WorldPositionWithOffset = i.vPositionWithOffsetWs;
		m.ScreenPosition = i.vPositionSs;
		m.Normal = i.vNormalWs;
		m.TextureCoords = uv;

		float3 worldNormal = i.vNormalWs;
		float3 geometryNormal = i.vNormalWs;

		float4 normalTexel = float4( 0.5, 0.5, 1.0, 1.0 );

		#if S_NORMAL_MAP
		{
			normalTexel = Tex2DS( g_tNormal, g_sSampler0, uv );

			#if S_SSBUMP
			{
				float3 ssbump = normalTexel.xyz;
				float3 tsNormal = normalize(
					SSBUMP_BASIS0 * ssbump.x +
					SSBUMP_BASIS1 * ssbump.y +
					SSBUMP_BASIS2 * ssbump.z
				);
				worldNormal = TransformNormal( tsNormal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
			}
			#else
			{
				worldNormal = TransformNormal( normalTexel.xyz, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
			}
			#endif

			m.Normal = worldNormal;
			m.WorldTangentU = i.vTangentUWs;
			m.WorldTangentV = i.vTangentVWs;
		}
		#endif

		float3 diffuseNormal = worldNormal;
		if ( g_nNoDiffuseBumpLighting > 0 )
			diffuseNormal = geometryNormal;

		float blendfactor = 0.0;
		float blendedAlpha = baseColor.a;
		#if S_BASETEXTURE2
		{
			float4 baseColor2 = Tex2DS( g_tColor2, g_sSampler0, uv );

			#if S_MASKED_BLENDING
			{
				blendfactor = 0.5;
				#if !S_SELF_ILLUM
				if ( g_nFancyBlending > 0 )
				{
					float4 modt = Tex2DS( g_tBlendModulation, g_sSampler0, uv );
					blendfactor = modt.g;
				}
				#endif
			}
			#else
			{
				blendfactor = g_flBlendAmount;

				#if !S_SELF_ILLUM
				if ( g_nFancyBlending > 0 )
				{
					float4 modt = Tex2DS( g_tBlendModulation, g_sSampler0, uv );
					float minb = saturate( modt.g - modt.r );
					float maxb = saturate( modt.g + modt.r );
					blendfactor = smoothstep( minb, maxb, blendfactor );
				}
				#endif
			}
			#endif

			albedo = lerp( albedo, baseColor2.rgb * g_vColor, blendfactor );
			blendedAlpha = lerp( baseColor.a, baseColor2.a, blendfactor );

			#if S_NORMAL_MAP
			{
				float4 normalTexel2 = Tex2DS( g_tNormal2, g_sSampler0, uv );

				#if S_SSBUMP
				{
					float3 ssbump2 = normalTexel2.xyz;
					float3 tsNormal2 = normalize(
						SSBUMP_BASIS0 * ssbump2.x +
						SSBUMP_BASIS1 * ssbump2.y +
						SSBUMP_BASIS2 * ssbump2.z
					);
					float3 worldNormal2 = TransformNormal( tsNormal2, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
					worldNormal = normalize( lerp( worldNormal, worldNormal2, blendfactor ) );
				}
				#else
				{
					if ( g_nNormalMapAlphaEnvMapMask > 0 )
					{
						float4 blendedNormalFull = lerp( normalTexel, normalTexel2, blendfactor );
						normalTexel.a = blendedNormalFull.a;
						worldNormal = TransformNormal( blendedNormalFull.xyz, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
					}
					else
					{
						float3 blendedNormal = lerp( normalTexel.xyz, normalTexel2.xyz, blendfactor );
						worldNormal = TransformNormal( blendedNormal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
					}
				}
				#endif

				m.Normal = worldNormal;

				if ( g_nNoDiffuseBumpLighting == 0 )
					diffuseNormal = worldNormal;
			}
			#endif
		}
		#endif

		#if S_DETAIL
		{
			float4 detailColor = float4( g_vDetailTint, 1.0 ) * Tex2DS( g_tDetail, g_sSampler0, uv * g_flDetailScale );
			float4 combined = float4( albedo, baseColor.a );
			combined = TextureCombine( combined, detailColor, g_nDetailBlendMode, g_flDetailBlendFactor );
			albedo = combined.rgb;
		}
		#endif

		float alphaForMasking = blendedAlpha;

		float opacity = g_flAlpha;
		#if !S_SELF_ILLUM
		{
			if ( g_nBaseAlphaEnvMapMask == 0 )
				opacity *= alphaForMasking;
		}
		#endif

		#if S_ALPHA_TEST
			clip( opacity - g_flAlphaTestReference );
		#endif

		m.Albedo = albedo;
		m.Opacity = opacity;

		#if S_MODE_DEPTH
			return DepthNormals::Output( m.Normal, 1.0, opacity );
		#endif

		float3 diffuseLighting = 0;
		float3 viewDir = CalculatePositionToCameraDirWs( worldPos );

		uint lightCount = Light::Count( worldPos );
		for ( uint idx = 0; idx < lightCount; idx++ )
		{
			Light light = Light::From( worldPos, idx, i.vLightmapUV );

			float NdotL = saturate( dot( diffuseNormal, light.Direction ) );
			diffuseLighting += NdotL * light.Color * light.Attenuation * light.Visibility;
		}

		float3 ambientLighting = AmbientLight::From( worldPos, diffuseNormal );
		diffuseLighting += ambientLighting;

		#if S_LIGHT_WARP
		{
			float len = 0.5 * length( diffuseLighting );
			float3 warpColor = Tex2DS( g_tLightWarp, g_sSampler0, float2( len, 0.0 ) ).rgb;
			diffuseLighting *= 2.0 * warpColor;
		}
		#endif

		float ssao = ScreenSpaceAmbientOcclusion::Sample( i.vPositionSs );
		diffuseLighting *= ssao;

		float3 envmapLighting = 0;
		#if S_ENVMAP
		{
			float specularFactor = 1.0;

			#if S_ENVMAP_MASK
				specularFactor *= Tex2DS( g_tEnvMapMask, g_sSampler0, uv ).r;
			#endif

			if ( g_nNormalMapAlphaEnvMapMask > 0 )
				specularFactor *= normalTexel.a;

			if ( g_nBaseAlphaEnvMapMask > 0 )
				specularFactor *= 1.0 - alphaForMasking;

			#if S_BASETEXTURE2
			{
				float envFactor1 = g_nBaseTextureNoEnvmap > 0 ? 0.0 : 1.0;
				float envFactor2 = g_nBaseTexture2NoEnvmap > 0 ? 0.0 : 1.0;
				specularFactor *= lerp( envFactor1, envFactor2, blendfactor );
			}
			#else
			{
				if ( g_nBaseTextureNoEnvmap > 0 )
					specularFactor = 0.0;
			}
			#endif

			float3 envColor = EnvMap::From( worldPos, worldNormal );
			envColor *= specularFactor;
			envColor *= g_vEnvmapTint;

			if ( g_nFancyBlending == 0 )
			{
				float3 envColorSquared = envColor * envColor;
				envColor = lerp( envColor, envColorSquared, g_flEnvmapContrast );

				float3 greyScale = dot( envColor, float3( 0.299, 0.587, 0.114 ) );
				envColor = lerp( greyScale, envColor, g_flEnvmapSaturation );
			}

			float NdotV = saturate( dot( worldNormal, viewDir ) );
			float fresnel = pow( 1.0 - NdotV, 5.0 );
			fresnel = fresnel * ( 1.0 - g_flFresnelReflection ) + g_flFresnelReflection;
			envColor *= fresnel;

			envmapLighting = envColor;
		}
		#endif

		float3 diffuseComponent = albedo * diffuseLighting;

		#if S_SELF_ILLUM
		{
			float selfIllumMask = alphaForMasking;
			float3 selfIllumComponent = g_vSelfIllumTint * albedo;
			diffuseComponent = lerp( diffuseComponent, selfIllumComponent, selfIllumMask );
		}
		#endif

		float4 result = float4( diffuseComponent + envmapLighting, opacity );

		#if S_DETAIL
		{
			float4 detailColor = float4( g_vDetailTint, 1.0 ) * Tex2DS( g_tDetail, g_sSampler0, uv * g_flDetailScale );
			result.rgb = TextureCombinePostLighting( result.rgb, detailColor, g_nDetailBlendMode, g_flDetailBlendFactor );
		}
		#endif

		if ( ToolsVis::WantsToolsVis() )
		{
			LightingTerms_t lightingTerms = InitLightingTerms();
			lightingTerms.vDiffuse = diffuseLighting;
			lightingTerms.vSpecular = envmapLighting;
			lightingTerms.vIndirectDiffuse = ambientLighting;
			return ShadingModelStandard::DoToolsVis( result, m, lightingTerms );
		}

		result = DoAtmospherics( worldPos, i.vPositionSs.xy, result );

		return result;
	}
}
