HEADER
{
	Description = "GoldSrc studio model with the engine's vertex lighting";
}

FEATURES
{
	#include "common/features.hlsl"

	Feature( F_FLATSHADE, 0..1, "Studio" );
	Feature( F_CHROME, 0..1, "Studio" );
	Feature( F_ADDITIVE, 0..1, "Studio" );
	Feature( F_MASKED, 0..1, "Studio" );
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
}

COMMON
{
	#define S_ALPHA_TEST 1

	#include "common/shared.hlsl"

	#define S_UV2 1
	#define CUSTOM_MATERIAL_INPUTS

	#define kRenderNormal 0
	#define kRenderTransColor 1
	#define kRenderTransTexture 2
	#define kRenderGlow 3
	#define kRenderTransAlpha 4
	#define kRenderTransAdd 5

	DynamicCombo( D_RENDER_MODE, 0..5, Sys( ALL ) );

	float g_flBlend < Attribute( "Blend" ); Default( 1.0 ); >;
	float g_flStudioLight < Attribute( "StudioLight" ); Default( 0.0 ); >;
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

	StaticCombo( S_FLATSHADE, F_FLATSHADE, Sys( ALL ) );
	StaticCombo( S_CHROME, F_CHROME, Sys( ALL ) );

	static const float kLambert = 1.4953241;
	float g_flGamma < Attribute( "Gamma" ); Default( 2.2 ); >;
	float g_flLightGamma < Attribute( "LightGamma" ); Default( 2.5 ); >;
	float g_flBrightness < Attribute( "Brightness" ); Default( 0.0 ); >;

	float Gamma()
	{
		return g_flGamma > 0.0 ? g_flGamma : 2.2;
	}

	float3 LightGammaTable( float3 index )
	{
		float3 light = pow( index / 1023.0, g_flLightGamma > 0.0 ? g_flLightGamma : 2.5 );
		float threshold = 0.125;

		if ( g_flBrightness > 1.0 )
		{
			light *= g_flBrightness;
			threshold = 0.05;
		}
		else if ( g_flBrightness > 0.0 )
		{
			threshold = 0.125 - g_flBrightness * g_flBrightness * 0.075;
		}

		float3 low = light / threshold * 0.125;
		float3 high = 0.125 + ( light - threshold ) / ( 1.0 - threshold ) * 0.875;

		light = lerp( low, high, step( threshold, light ) );

		return clamp( floor( pow( light, 1.0 / Gamma() ) * 1023.0 ), 0.0, 1023.0 );
	}

	float g_flAmbientLight < Attribute( "AmbientLight" ); Default( 192.0 ); >;
	float g_flShadeLight < Attribute( "ShadeLight" ); Default( 0.0 ); >;
	float3 g_vLightVector < Attribute( "LightVector" ); Default3( 0.0, 0.0, -1.0 ); >;
	float3 g_vLightColor < Attribute( "LightColor" ); Default3( 1.0, 1.0, 1.0 ); >;

	float g_flLocalLightCount < Attribute( "LocalLightCount" ); Default( 0.0 ); >;
	float4 g_vLocalLight0 < Attribute( "LocalLight0" ); Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vLocalLight1 < Attribute( "LocalLight1" ); Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float4 g_vLocalLight2 < Attribute( "LocalLight2" ); Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float3 g_vLocalLightColor0 < Attribute( "LocalLightColor0" ); Default3( 0.0, 0.0, 0.0 ); >;
	float3 g_vLocalLightColor1 < Attribute( "LocalLightColor1" ); Default3( 0.0, 0.0, 0.0 ); >;
	float3 g_vLocalLightColor2 < Attribute( "LocalLightColor2" ); Default3( 0.0, 0.0, 0.0 ); >;

	float2 g_vTextureSize < Default2( 64.0, 64.0 ); >;
	float g_flHasBoneOrigins < Attribute( "HasBoneOrigins" ); Default( 0.0 ); >;
	float g_flHasBoneFx < Attribute( "HasBoneFx" ); Default( 0.0 ); >;
	float g_flDepthRange < Attribute( "DepthRange" ); Default( 1.0 ); >;
	Texture2D g_tBoneOrigins < Attribute( "BoneOrigins" ); >;
	Texture2D g_tBoneFx < Attribute( "BoneFx" ); >;

	float LightGamma( float illum )
	{
		float index = floor( min( illum, 255.0 ) * 4.0 );
		return LightGammaTable( index ).x / 1023.0;
	}

	float Lighting( float3 normal )
	{
		float illum = g_flAmbientLight;

		#if S_FLATSHADE
			illum += g_flShadeLight * 0.8;
		#else
			float lightcos = min( dot( normal, g_vLightVector ), 1.0 );

			illum += g_flShadeLight;
			lightcos = ( lightcos + ( kLambert - 1.0 ) ) / kLambert;

			if ( lightcos > 0.0 )
				illum -= g_flShadeLight * lightcos;

			illum = max( illum, 0.0 );
		#endif

		return LightGamma( illum );
	}

	float LinearGamma( float value )
	{
		return floor( pow( floor( value * 1023.0 ) / 1023.0, Gamma() ) * 1023.0 );
	}

	float ScreenGamma( float value )
	{
		float index = floor( value );

		if ( index >= 1024.0 )
			return 1.0;

		return floor( pow( index / 1023.0, 1.0 / Gamma() ) * 1023.0 ) / 1023.0;
	}

	float3 LocalLight( float4 light, float3 color, float3 position, float3 normal )
	{
		float3 direction = position - light.xyz;
		float r = -dot( direction, normal );

		if ( r <= 0.0 )
			return float3( 0.0, 0.0, 0.0 );

		float r2 = dot( direction, direction );
		float strength = r2 > 0.0 ? light.w / ( r2 * sqrt( r2 ) ) : 1.0;

		return color * ( r * strength );
	}

	float3 Lambert( float3 position, float3 normal, float3 source )
	{
		float3 add = float3( 0.0, 0.0, 0.0 );

		if ( g_flLocalLightCount > 0.5 )
			add += LocalLight( g_vLocalLight0, g_vLocalLightColor0, position, normal );

		if ( g_flLocalLightCount > 1.5 )
			add += LocalLight( g_vLocalLight1, g_vLocalLightColor1, position, normal );

		if ( g_flLocalLightCount > 2.5 )
			add += LocalLight( g_vLocalLight2, g_vLocalLightColor2, position, normal );

		if ( add.x == 0.0 && add.y == 0.0 && add.z == 0.0 )
			return source;

		return float3(
			ScreenGamma( LinearGamma( source.x ) + add.x ),
			ScreenGamma( LinearGamma( source.y ) + add.y ),
			ScreenGamma( LinearGamma( source.z ) + add.z ) );
	}

	float2 Chrome( float3 normal, float3 boneOrigin )
	{
		float3 forward = normalize( boneOrigin - g_vCameraPositionWs );
		float3 right = cross( g_vCameraDirWs, g_vCameraUpDirWs );
		float3 chromeUp = normalize( cross( forward, right ) );
		float3 chromeRight = normalize( cross( chromeUp, forward ) );

		float2 chrome = float2( dot( normal, chromeRight ), dot( normal, chromeUp ) );

		return trunc( ( chrome + 1.0 ) * 32.0 * 1024.0 ) / 1024.0 / g_vTextureSize;
	}

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );

		if ( g_flHasBoneFx > 0.0 )
		{
			int bone = (int)v.vTexCoord2.x;
			float4 row0 = g_tBoneFx.Load( int3( bone, 0, 0 ) );
			float4 row1 = g_tBoneFx.Load( int3( bone, 1, 0 ) );
			float4 row2 = g_tBoneFx.Load( int3( bone, 2, 0 ) );
			float3 position = i.vPositionWs.xyz;

			i.vPositionWs.xyz = float3( dot( row0.xyz, position ) + row0.w, dot( row1.xyz, position ) + row1.w, dot( row2.xyz, position ) + row2.w );
			i.vPositionPs = Position3WsToPs( i.vPositionWs.xyz );
		}

		#if D_RENDER_MODE == kRenderTransAdd
			i.vVertexColor = float4( g_flBlend, g_flBlend, g_flBlend, 1.0 );
		#else
			i.vVertexColor = float4( Lambert( i.vPositionWs, i.vNormalWs, Lighting( i.vNormalWs ) * g_vLightColor ), 1.0 );
		#endif

		#if S_CHROME
			float3 boneOrigin = i.vPositionWs;

			if ( g_flHasBoneOrigins > 0.0 )
				boneOrigin = g_tBoneOrigins.Load( int3( (int)v.vTexCoord2.x, 0, 0 ) ).xyz;

			i.vTextureCoords.xy = Chrome( i.vNormalWs, boneOrigin );
		#endif

		PixelInput o = FinalizeVertex( i );

		o.vPositionPs.z = lerp( o.vPositionPs.w, o.vPositionPs.z, g_flDepthRange );

		return o;
	}
}

PS
{
	#include "common/pixel.hlsl"

	StaticCombo( S_ADDITIVE, F_ADDITIVE, Sys( ALL ) );
	StaticCombo( S_MASKED, F_MASKED, Sys( ALL ) );
	StaticCombo( S_MODE_DEPTH, 0..1, Sys( ALL ) );
	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	#if D_GAMMA_SPACE
		#if D_RENDER_MODE != kRenderNormal || S_ADDITIVE
			RenderState( SrcBlendAlpha, ZERO );
			RenderState( DstBlendAlpha, ONE );
		#else
			RenderState( BlendEnable, true );
			RenderState( SrcBlend, ONE );
			RenderState( DstBlend, ZERO );
			RenderState( SrcBlendAlpha, ZERO );
			RenderState( DstBlendAlpha, ZERO );
		#endif
	#endif

	#if D_RENDER_MODE == kRenderTransAdd || ( D_RENDER_MODE == kRenderNormal && S_ADDITIVE )
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ONE );
		RenderState( DepthWriteEnable, false );
	#elif D_RENDER_MODE != kRenderNormal
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
	#endif

	SamplerState g_sColorSampler < Filter( Anisotropic ); MaxAniso( 16 ); AddressU( WRAP ); AddressV( WRAP ); >;
	SamplerState g_sNearestSampler < Filter( Point ); AddressU( WRAP ); AddressV( WRAP ); >;

	float g_flTextureNearest < Attribute( "TextureNearest" ); Default( 0.0 ); >;
	float g_flTextureNoMips < Attribute( "TextureNoMips" ); Default( 0.0 ); >;

	float4 SampleColor( Texture2D color, float2 uv )
	{
		if ( g_flTextureNearest > 0.0 )
			return g_flTextureNoMips > 0.0 ? color.SampleLevel( g_sNearestSampler, uv, 0 ) : color.Sample( g_sNearestSampler, uv );

		if ( g_flTextureNoMips > 0.0 )
			return color.SampleLevel( g_sColorSampler, uv, 0 );

		return Tex2DS( color, g_sColorSampler, uv );
	}

	CreateInputTexture2D( Color, Linear, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;

	static const float kAlphaReference = 0.5;

	float3 GammaToLinear( float3 color )
	{
		float3 low = color / 12.92;
		float3 high = pow( ( color + 0.055 ) / 1.055, 2.4 );
		return lerp( low, high, step( 0.04045, color ) );
	}

	float4 Output( float3 color, float alpha )
	{
		#if D_GAMMA_SPACE
			return float4( color, alpha );
		#else
			return float4( GammaToLinear( color ), alpha );
		#endif
	}

	float4 g_vFog < Attribute( "Fog" ); Default4( 0.0, 0.0, 0.0, 0.0 ); >;
	float g_flNoFog < Attribute( "NoFog" ); Default( 0.0 ); >;

	float3 WaterFog( float3 color, float3 offset )
	{
		if ( g_vFog.a <= 0.0 || g_flNoFog > 0.0 )
			return color;

		float distance = abs( dot( offset, g_vCameraDirWs ) );

		return lerp( g_vFog.rgb, color, saturate( ( g_vFog.a - distance ) / g_vFog.a ) );
	}

	float3 LinearToGamma( float3 color )
	{
		float3 low = color * 12.92;
		float3 high = 1.055 * pow( max( color, 0.0 ), 1.0 / 2.4 ) - 0.055;
		return lerp( low, high, step( 0.0031308, color ) );
	}

	float4 Finish( float4 color, float3 source )
	{
		#if D_GAMMA_SPACE
			if ( !DepthNormals::WantsDepthNormals() && !g_bWireframeMode && !ToolsVis::WantsToolsVis() )
				color.rgb = source + ( LinearToGamma( color.rgb ) - LinearToGamma( GammaToLinear( source ) ) );
		#endif

		return color;
	}

	float4 Standard( PixelInput i, float3 albedo, float3 baked, float occlusion )
	{
		Material m = Material::Init();

		m.Albedo = GammaToLinear( albedo );
		m.Emission = GammaToLinear( baked ) * ( any( albedo ) ? ScreenSpaceAmbientOcclusion::Sample( i.vPositionSs ) : 1.0 );
		m.Normal = normalize( i.vNormalWs );
		m.Roughness = 1.0;
		m.Metalness = 0.0;
		m.AmbientOcclusion = occlusion;
		m.Opacity = 1.0;
		m.TextureCoords = i.vTextureCoords.xy;

		return Finish( ShadingModelStandard::Shade( i, m ), baked );
	}

	float4 Scene( PixelInput i, float3 albedo, float alpha )
	{
		float3 lit = WaterFog( saturate( albedo * i.vVertexColor.rgb ), i.vPositionWithOffsetWs );
		float4 color;

		#if S_ADDITIVE
			color = Standard( i, float3( 0.0, 0.0, 0.0 ), lit, 0.0 );
		#else
			if ( g_flStudioLight > 0.0 )
				color = Standard( i, albedo, lit, 0.0 );
			else
				color = Standard( i, albedo, float3( 0.0, 0.0, 0.0 ), 1.0 );
		#endif

		return float4( color.rgb, alpha );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		#if S_MODE_DEPTH && ( D_RENDER_MODE != kRenderNormal || S_ADDITIVE )
			discard;
		#endif

		float4 albedo = SampleColor( g_tColor, i.vTextureCoords.xy );

		#if S_MASKED
			if ( albedo.a <= kAlphaReference )
				discard;
		#endif

		#if D_RENDER_MODE == kRenderNormal
			return Scene( i, albedo.rgb, albedo.a * g_flBlend );
		#else
			return Output( saturate( albedo.rgb * i.vVertexColor.rgb ), albedo.a * g_flBlend );
		#endif
	}
}
