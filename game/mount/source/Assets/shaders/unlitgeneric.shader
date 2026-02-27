
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"

	Feature( F_ALPHA_TEST, 0..1, "Rendering" );
	Feature( F_TRANSLUCENT, 0..1, "Rendering" );
	Feature( F_DETAIL, 0..1, "Detail" );
	Feature( F_VERTEX_COLOR, 0..1, "Rendering" );
	Feature( F_VERTEX_ALPHA, 0..1, "Rendering" );
	Feature( F_ENVMAP, 0..1, "Environment Map" );
	Feature( F_ENVMAP_MASK, 0..1, "Environment Map" );

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
	StaticCombo( S_DETAIL, F_DETAIL, Sys( ALL ) );
	StaticCombo( S_VERTEX_COLOR, F_VERTEX_COLOR, Sys( ALL ) );
	StaticCombo( S_VERTEX_ALPHA, F_VERTEX_ALPHA, Sys( ALL ) );
	StaticCombo( S_ENVMAP, F_ENVMAP, Sys( ALL ) );
	StaticCombo( S_ENVMAP_MASK, F_ENVMAP_MASK, Sys( ALL ) );

	SamplerState g_sSampler0 < Filter( Anisotropic ); AddressU( WRAP ); AddressV( WRAP ); >;

	CreateInputTexture2D( Color, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( Detail, Srgb, 8, "None", "_detail", ",0/,0/0", Default4( 0.5, 0.5, 0.5, 1.0 ) );
	Texture2D g_tDetail < Channel( RGBA, Box( Detail ), Srgb ); OutputFormat( DXT5 ); SrgbRead( true ); >;

	CreateInputTexture2D( EnvMapMask, Linear, 8, "None", "_envmapmask", ",0/,0/0", Default4( 1.0, 1.0, 1.0, 1.0 ) );
	Texture2D g_tEnvMapMask < Channel( RGBA, Box( EnvMapMask ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	float3 g_vColor < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flAlpha < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	float3 g_vEnvmapTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;
	float g_flEnvmapContrast < Default( 0.0 ); Range( 0.0, 1.0 ); >;
	float g_flEnvmapSaturation < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	int g_nBaseAlphaEnvMapMask < Default( 0 ); >;
	float g_flDetailScale < Default( 4.0 ); Range( 0.0, 32.0 ); >;
	float g_flDetailBlendFactor < Default( 1.0 ); Range( 0.0, 1.0 ); >;
	int g_nDetailBlendMode < Default( 0 ); >;
	float3 g_vDetailTint < UiType( Color ); Default3( 1.0, 1.0, 1.0 ); >;

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
		float2 uv = i.vTextureCoords.xy;
		float4 baseColor = Tex2DS( g_tColor, g_sSampler0, uv );
		float3 albedo = baseColor.rgb * g_vColor;

		#if S_VERTEX_COLOR
		{
			albedo *= i.vVertexColor.rgb;
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
		if ( g_nBaseAlphaEnvMapMask == 0 )
			opacity *= baseColor.a;

		#if S_VERTEX_ALPHA
		{
			opacity *= i.vVertexColor.a;
		}
		#endif

		#if S_ALPHA_TEST
			clip( opacity - g_flAlphaTestReference );
		#endif

		#if S_MODE_DEPTH
			return DepthNormals::Output( i.vNormalWs, 1.0, opacity );
		#endif

		float3 result = albedo;

		float3 worldPos = i.vPositionWithOffsetWs + g_vHighPrecisionLightingOffsetWs.xyz;

		#if S_ENVMAP
		{
			float specularFactor = 1.0;

			#if S_ENVMAP_MASK
				specularFactor *= Tex2DS( g_tEnvMapMask, g_sSampler0, uv ).r;
			#endif

			if ( g_nBaseAlphaEnvMapMask > 0 )
				specularFactor *= 1.0 - baseColor.a;

			float3 worldNormal = i.vNormalWs;
			float3 envColor = EnvMap::From( worldPos, worldNormal );
			envColor *= specularFactor;
			envColor *= g_vEnvmapTint;

			float3 envColorSquared = envColor * envColor;
			envColor = lerp( envColor, envColorSquared, g_flEnvmapContrast );

			float3 greyScale = dot( envColor, float3( 0.299, 0.587, 0.114 ) );
			envColor = lerp( greyScale, envColor, g_flEnvmapSaturation );

			result += envColor;
		}
		#endif

		#if S_DETAIL
		{
			float4 detailColor = float4( g_vDetailTint, 1.0 ) * Tex2DS( g_tDetail, g_sSampler0, uv * g_flDetailScale );
			result = TextureCombinePostLighting( result, detailColor, g_nDetailBlendMode, g_flDetailBlendFactor );
		}
		#endif

		float4 finalResult = float4( result, opacity );
		finalResult = DoAtmospherics( worldPos, i.vPositionSs.xy, finalResult );

		return finalResult;
	}
}
