
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
    Feature( F_SELF_ILLUM_MASK, 0..1, "Rendering" );
    Feature( F_SELFILLUM_FRESNEL, 0..1, "Rendering" );
	Feature( F_NORMAL_MAP, 0..1, "Rendering" );
	Feature( F_DETAIL, 0..1, "Detail" );
	Feature( F_ENVMAP, 0..1, "Environment Map" );
	Feature( F_ENVMAP_MASK, 0..1, "Environment Map" );
    Feature( F_PHONG, 0..1, "Phong" );
    Feature( F_PHONG_WARP, 0..1, "Phong" );
    Feature( F_RIM_LIGHT, 0..1, "Phong" );
	Feature( F_LIGHT_WARP, 0..1, "Lighting" );
	Feature( F_HALF_LAMBERT, 0..1, "Lighting" );

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
	StaticCombo( S_ENVMAP, F_ENVMAP, Sys( ALL ) );
	StaticCombo( S_ENVMAP_MASK, F_ENVMAP_MASK, Sys( ALL ) );
	StaticCombo( S_PHONG, F_PHONG, Sys( ALL ) );
	StaticCombo( S_SELF_ILLUM_MASK, F_SELF_ILLUM_MASK, Sys( ALL ) );
	StaticCombo( S_LIGHT_WARP, F_LIGHT_WARP, Sys( ALL ) );
	StaticCombo( S_RIM_LIGHT, F_RIM_LIGHT, Sys( ALL ) );
	StaticCombo( S_HALF_LAMBERT, F_HALF_LAMBERT, Sys( ALL ) );
	StaticCombo( S_SELFILLUM_FRESNEL, F_SELFILLUM_FRESNEL, Sys( ALL ) );
	StaticCombo( S_PHONG_WARP, F_PHONG_WARP, Sys( ALL ) );

	SamplerState g_sSampler0 < Filter( Anisotropic ); AddressU( WRAP ); AddressV( WRAP ); >;

	CreateInputTexture2D( Color, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( Normal, Linear, 8, "NormalizeNormals", "_normal", ",0/,0/0", Default4( 0.5, 0.5, 1.0, 1.0 ) );
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( Detail, Srgb, 8, "None", "_detail", ",0/,0/0", Default4( 0.5, 0.5, 0.5, 1.0 ) );
	Texture2D g_tDetail < Channel( RGBA, Box( Detail ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( EnvMapMask, Linear, 8, "None", "_envmapmask", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tEnvMapMask < Channel( RGBA, Box( EnvMapMask ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( SelfIllumMask, Linear, 8, "None", "_selfillummask", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tSelfIllumMask < Channel( RGBA, Box( SelfIllumMask ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( LightWarpTexture, Srgb, 8, "None", "_lightwarp", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tLightWarp < Channel( RGB, Box( LightWarpTexture ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( PhongExponentTexture, Linear, 8, "None", "_phongexp", ",0/,0/0", Default4( 0.0, 0.0, 0.0, 1.0 ) );
	Texture2D g_tPhongExponent < Channel( RGBA, Box( PhongExponentTexture ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( PhongWarpTexture, Srgb, 8, "None", "_phongwarp", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tPhongWarp < Channel( RGB, Box( PhongWarpTexture ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	float3 g_vColor < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flAlpha < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	float3 g_vSelfIllumTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float3 g_vEnvmapTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flEnvmapContrast < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flEnvmapSaturation < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	float g_flEnvmapFresnel < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	int g_nBaseAlphaEnvMapMask < Default( 0 ); >;
	int g_nNormalMapAlphaEnvMapMask < Default( 0 ); >;
	float g_flDetailScale < Default( 4.0 ); Range( 0.0, 32.0 ); >;
	float g_flDetailBlendFactor < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	int g_nDetailBlendMode < Default( 0 ); >;
	float3 g_vDetailTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flPhongExponent < Default( 5.0 ); Range( 0.0, 150.0 ); >;
	float g_flPhongBoost < Default( 1.0 ); Range( 0.0, 10.0 ); >;
	float3 g_vPhongFresnelRanges < Default3( 0.0, 0.5, 1.0 ); >;
	float3 g_vPhongTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	int g_nBaseAlphaPhongMask < Default( 0 ); >;
	int g_nPhongAlbedoTint < Default( 0 ); >;
	int g_nInvertPhongMask < Default( 0 ); >;
	int g_nRimMask < Default( 0 ); >;
	float g_flRimLightExponent < Default( 4.0 ); Range( 1.0, 20.0 ); >;
	float g_flRimLightBoost < Default( 1.0 ); Range( 0.0, 10.0 ); >;
	int g_nHasPhongExponentTexture < Default( 0 ); >;
	int g_nBlendTintByBaseAlpha < Default( 0 ); >;
	float g_flBlendTintColorOverBase < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float3 g_vSelfIllumFresnelMinMaxExp < Default3( 0.0, 1.0, 1.0 ); >;

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

	float FresnelRanges( float3 vNormal, float3 vEyeDir, float3 vRanges )
	{
		float f = saturate( 1 - dot( vNormal, vEyeDir ) );
		f = f * f - 0.5;

		float3 enc = float3( ( vRanges.y - vRanges.x ) * 2.0, vRanges.y, ( vRanges.z - vRanges.y ) * 2.0 );
		return enc.y + ( f >= 0.0 ? enc.z : enc.x ) * f;
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );

		float2 uv = i.vTextureCoords.xy;
		float4 baseColor = Tex2DS( g_tColor, g_sSampler0, uv );
		float3 albedo = baseColor.rgb * g_vColor;

		if ( g_nBlendTintByBaseAlpha > 0 )
		{
			float3 tintedColor = albedo;
			tintedColor = lerp( tintedColor, g_vColor, g_flBlendTintColorOverBase );
			albedo = lerp( baseColor.rgb, tintedColor, baseColor.a );
		}

		float3 worldPos = i.vPositionWithOffsetWs + g_vHighPrecisionLightingOffsetWs.xyz;
		m.WorldPosition = worldPos;
		m.WorldPositionWithOffset = i.vPositionWithOffsetWs;
		m.ScreenPosition = i.vPositionSs;
		m.Normal = i.vNormalWs;
		m.TextureCoords = uv;

		float3 worldNormal = i.vNormalWs;
		float4 normalTexel = float4( 0.5, 0.5, 1.0, 1.0 );

		#if S_NORMAL_MAP
		{
			normalTexel = Tex2DS( g_tNormal, g_sSampler0, uv );
			float3 tsNormal = normalTexel.xyz;

			#if S_PHONG
			if ( g_nBaseAlphaPhongMask > 0 )
				tsNormal = float3( 0.5, 0.5, 1.0 );
			#endif

			worldNormal = TransformNormal( tsNormal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
			m.Normal = worldNormal;
			m.WorldTangentU = i.vTangentUWs;
			m.WorldTangentV = i.vTangentVWs;
		}
		#endif

		#if S_DETAIL
		{
			float4 detailColor = float4( g_vDetailTint, 1.0 ) * Tex2DS( g_tDetail, g_sSampler0, uv * g_flDetailScale );
			float4 combined = float4( albedo, baseColor.a );
			combined = TextureCombine( combined, detailColor, g_nDetailBlendMode, g_flDetailBlendFactor );
			albedo = combined.rgb;
			baseColor.a = combined.a;
		}
		#endif

		float opacity = g_flAlpha;
		#if !S_SELF_ILLUM
		{
			if ( g_nBlendTintByBaseAlpha == 0 )
				opacity = lerp( baseColor.a * opacity, opacity, float( g_nBaseAlphaPhongMask ) );
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
		float3 specularLighting = 0;
		float3 rimLighting = 0;
		float3 viewDir = CalculatePositionToCameraDirWs( worldPos );

		#if S_PHONG
		float4 vSpecExpMap = float4( 0, 0, 0, 1 );
		float fSpecExp = max( g_flPhongExponent, 0.0 );
		float fFresnelRanges = FresnelRanges( worldNormal, viewDir, g_vPhongFresnelRanges );
		if ( g_nHasPhongExponentTexture > 0 )
		{
			vSpecExpMap = Tex2DS( g_tPhongExponent, g_sSampler0, uv );
			fSpecExp = ( g_flPhongExponent >= 0.0 ) ? g_flPhongExponent : ( 1.0 + 149.0 * vSpecExpMap.r );
		}
		#endif

		uint lightCount = DynamicLight::Count( worldPos );
		for ( uint idx = 0; idx < lightCount; idx++ )
		{
			Light light = DynamicLight::From( worldPos, idx );

			float NdotL = dot( worldNormal, light.Direction );
			float3 lightAtten = light.Color * light.Attenuation * light.Visibility;

			float3 diffuse;
			#if S_PHONG || S_HALF_LAMBERT
			{
				float halfLambert = saturate( NdotL * 0.5 + 0.5 );

				#if S_LIGHT_WARP
				{
					diffuse = 2.0 * Tex2DS( g_tLightWarp, g_sSampler0, float2( halfLambert, 0.5 ) ).rgb;
				}
				#else
				{
					diffuse = halfLambert * halfLambert;
				}
				#endif
			}
			#else
			{
				diffuse = saturate( NdotL );
			}
			#endif

			diffuseLighting += diffuse * lightAtten;

			#if S_PHONG
			{
				float3 reflectVec = 2 * worldNormal * dot( worldNormal, viewDir ) - viewDir;
				float LdotR = saturate( dot( light.Direction, reflectVec ) );
				float spec = pow( LdotR, fSpecExp );

				float3 specColor;
				#if S_PHONG_WARP
				{
					specColor = spec * Tex2DS( g_tPhongWarp, g_sSampler0, float2( spec, fFresnelRanges ) ).rgb;
				}
				#else
				{
					specColor = spec;
				}
				#endif

				specColor *= saturate( NdotL );
				specularLighting += specColor * lightAtten;

				#if S_RIM_LIGHT
				{
					float rim = pow( LdotR, g_flRimLightExponent );
					rim *= saturate( NdotL );
					rimLighting += rim * lightAtten;
				}
				#endif
			}
			#endif
		}

		float3 ambientLighting = AmbientLight::From( worldPos, worldNormal );
		diffuseLighting += ambientLighting;

		float ssao = ScreenSpaceAmbientOcclusion::Sample( i.vPositionSs );
		diffuseLighting *= ssao;

		float3 vSpecularTint = float3( 1, 1, 1 );

		#if S_PHONG
		{
			float fSpecMask;
			#if S_NORMAL_MAP
				fSpecMask = lerp( normalTexel.a, baseColor.a, float( g_nBaseAlphaPhongMask ) );
			#else
				fSpecMask = baseColor.a;
			#endif

			#if !S_PHONG_WARP
				fSpecMask *= fFresnelRanges;
			#endif

			specularLighting *= fSpecMask * g_flPhongBoost;

			vSpecularTint = g_vPhongTint;
			if ( g_nPhongAlbedoTint > 0 )
			{
				float albedoTintAmount = g_nHasPhongExponentTexture > 0 ? vSpecExpMap.g : 0.0;
				vSpecularTint = lerp( float3( 1, 1, 1 ), baseColor.rgb, albedoTintAmount );
			}

			#if S_RIM_LIGHT
			{
				float fRimFresnel = saturate( 1.0 - dot( worldNormal, viewDir ) );
				fRimFresnel = fRimFresnel * fRimFresnel;
				fRimFresnel = fRimFresnel * fRimFresnel;

				float fRimMask = ( g_nRimMask > 0 && g_nHasPhongExponentTexture > 0 ) ? vSpecExpMap.a : 1.0;

				float fRimMultiply = fRimMask * fRimFresnel;
				rimLighting *= fRimMultiply;

				specularLighting = max( specularLighting, rimLighting );

				float3 vRimAmbientCubeColor = AmbientLight::From( worldPos, viewDir );
				specularLighting += ( vRimAmbientCubeColor * g_flRimLightBoost ) * saturate( fRimMultiply * worldNormal.z );
			}
			#endif
		}
		#endif

		float3 envmapLighting = 0;
		#if S_ENVMAP
		{
			float3 envColor = EnvMap::From( worldPos, worldNormal );

			#if !S_PHONG
			{
				float specularFactor = 1.0;

				#if S_ENVMAP_MASK
					specularFactor *= Tex2DS( g_tEnvMapMask, g_sSampler0, uv ).r;
				#endif

				if ( g_nBaseAlphaEnvMapMask > 0 )
					specularFactor *= 1.0 - baseColor.a;

				if ( g_nNormalMapAlphaEnvMapMask > 0 )
					specularFactor *= normalTexel.a;

				envColor *= specularFactor;
				envColor *= g_vEnvmapTint;

				float3 envColorSquared = envColor * envColor;
				envColor = lerp( envColor, envColorSquared, g_flEnvmapContrast );

				float3 greyScale = dot( envColor, float3( 0.299, 0.587, 0.114 ) );
				envColor = lerp( greyScale, envColor, g_flEnvmapSaturation );
			}
			#else
			{
				float fSpecMaskForEnv;
				#if S_NORMAL_MAP
					fSpecMaskForEnv = lerp( normalTexel.a, baseColor.a, float( g_nBaseAlphaPhongMask ) );
				#else
					fSpecMaskForEnv = baseColor.a;
				#endif
				float fEnvMapMask = lerp( baseColor.a, fSpecMaskForEnv, float( g_nNormalMapAlphaEnvMapMask ) );

				envColor *= fEnvMapMask;
				envColor *= g_vEnvmapTint;
				envColor *= lerp( 1.0, fFresnelRanges, g_flEnvmapFresnel );
				envColor *= lerp( fEnvMapMask, 1.0 - fEnvMapMask, float( g_nInvertPhongMask ) );
			}
			#endif

			envmapLighting = envColor;
		}
		#endif

		float3 diffuseComponent = albedo * diffuseLighting;

		#if S_SELF_ILLUM
		{
			#if S_SELFILLUM_FRESNEL
			{
				float3 vVertexNormal = normalize( i.vNormalWs );
				float NdotV_si = saturate( dot( vVertexNormal, viewDir ) );

				float flMin = g_vSelfIllumFresnelMinMaxExp.x;
				float flMax = g_vSelfIllumFresnelMinMaxExp.y;
				float flExp = g_vSelfIllumFresnelMinMaxExp.z;

				float bias = ( flMax > 0.0 ) ? ( flMin / flMax ) : 0.0;
				float scale = 1.0 - bias;

				float selfIllumFresnel = pow( NdotV_si, flExp ) * scale + bias;
				selfIllumFresnel = saturate( selfIllumFresnel );

				float3 selfIllumComponent = g_vSelfIllumTint * albedo * flMax;
				diffuseComponent = lerp( diffuseComponent, selfIllumComponent, baseColor.a * selfIllumFresnel );
			}
			#else
			{
				float selfIllumMask = baseColor.a;

				#if S_SELF_ILLUM_MASK
					selfIllumMask = Tex2DS( g_tSelfIllumMask, g_sSampler0, uv ).r;
				#endif

				float3 selfIllumComponent = g_vSelfIllumTint * albedo;
				diffuseComponent = lerp( diffuseComponent, selfIllumComponent, selfIllumMask );
			}
			#endif

			diffuseComponent = max( 0.0, diffuseComponent );
		}
		#endif

		#if S_DETAIL
		{
			float4 detailColor2 = float4( g_vDetailTint, 1.0 ) * Tex2DS( g_tDetail, g_sSampler0, uv * g_flDetailScale );
			diffuseComponent = TextureCombinePostLighting( diffuseComponent, detailColor2, g_nDetailBlendMode, g_flDetailBlendFactor );
		}
		#endif

		float4 result = float4( diffuseComponent + specularLighting * vSpecularTint + envmapLighting, opacity );

		if ( ToolsVis::WantsToolsVis() )
		{
			LightingTerms_t lightingTerms = InitLightingTerms();
			lightingTerms.vDiffuse = diffuseLighting;
			lightingTerms.vSpecular = specularLighting + envmapLighting;
			lightingTerms.vIndirectDiffuse = ambientLighting;
			return ShadingModelStandard::DoToolsVis( result, m, lightingTerms );
		}

		result = DoAtmospherics( worldPos, i.vPositionSs.xy, result );

		return result;
	}
}
