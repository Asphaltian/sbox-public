
HEADER
{
	Description = "GoldSrc liquid surface";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
}

COMMON
{
	#define S_ALPHA_TEST 1

	#include "common/shared.hlsl"

	float g_flClientTime < Attribute( "ClientTime" ); Default( 0.0 ); >;

	#define S_UV2 1
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

	static const float kTurbScale = 256.0 / ( 2.0 * 3.14159265 );

	float g_flWaveHeight < Attribute( "WaveHeight" ); >;
	float g_flTurbMins < Attribute( "TurbMins" ); Default( 0.0 ); >;
	float g_flWaterSides < Attribute( "WaterSides" ); Default( 0.0 ); >;

	float TurbSin( float value )
	{
		int index = (int)value & 255;
		return sin( index * ( 2.0 * 3.14159265 / 256.0 ) ) * 8.0;
	}

	PixelInput MainVs( VertexInput v )
	{
		float3 position = v.vPositionOs;
		float scale = g_vCameraPositionWs.z <= position.z ? -g_flWaveHeight : g_flWaveHeight;

		float wave = ( TurbSin( position.x * 5.0 + g_flClientTime * 171.0 - position.y ) + 8.0 ) * 0.8;
		wave += TurbSin( g_flClientTime * 160.0 + position.x + position.y ) + 8.0;

		v.vPositionOs.z += wave * scale;

		PixelInput i = ProcessVertex( v );

		float2 st = v.vTexCoord;
		float s = st.x + TurbSin( ( st.y * 0.125 + g_flClientTime ) * kTurbScale );
		float t = st.y + TurbSin( ( st.x * 0.125 + g_flClientTime ) * kTurbScale );

		i.vTextureCoords.xy = float2( s, t ) * ( 1.0 / 64.0 );

		i = FinalizeVertex( i );

		if ( v.vTexCoord2.y >= 0.0 && !( ( v.vTexCoord2.y > 0.0 || g_flWaterSides != 0.0 ) && g_flTurbMins + 1.0 < v.vTexCoord2.x ) )
			i.vPositionPs = float4( 2.0, 2.0, 2.0, 1.0 );

		return i;
	}
}

PS
{
	#include "common/pixel.hlsl"

	RenderState( AlphaToCoverageEnable, false );

	#define kRenderNormal 0
	#define kRenderTransColor 1
	#define kRenderTransTexture 2
	#define kRenderGlow 3
	#define kRenderTransAlpha 4
	#define kRenderTransAdd 5

	DynamicCombo( D_RENDER_MODE, 0..5, Sys( ALL ) );

	#if D_RENDER_MODE == kRenderTransColor || D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
	#elif D_RENDER_MODE == kRenderTransAdd
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ONE );
	#endif

	#if D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow || D_RENDER_MODE == kRenderTransAdd
		RenderState( DepthWriteEnable, false );
	#endif

	RenderState( CullMode, NONE );

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

	static const float kAlphaReference = 0.25;

	float g_flBlend < Attribute( "Blend" ); >;
	float3 g_vRenderColor < Attribute( "RenderColor" ); >;

	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	#if D_GAMMA_SPACE
		#if D_RENDER_MODE == kRenderTransColor || D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow || D_RENDER_MODE == kRenderTransAdd
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

	float3 LinearToGamma( float3 color )
	{
		float3 low = color * 12.92;
		float3 high = 1.055 * pow( max( color, 0.0 ), 1.0 / 2.4 ) - 0.055;
		return lerp( low, high, step( 0.0031308, color ) );
	}

	float4 Finish( float4 color )
	{
		#if D_GAMMA_SPACE
			if ( !DepthNormals::WantsDepthNormals() && !g_bWireframeMode && !ToolsVis::WantsToolsVis() )
				color.rgb = LinearToGamma( color.rgb );
		#endif

		return color;
	}

	float4 Standard( PixelInput i, float3 baked )
	{
		Material m = Material::Init( i.vPositionWithOffsetWs, i.vPositionSs );

		m.Albedo = float3( 0.0, 0.0, 0.0 );
		m.Emission = GammaToLinear( baked );
		m.Normal = normalize( i.vNormalWs );
		m.Roughness = 1.0;
		m.Metalness = 0.0;
		m.AmbientOcclusion = 0.0;
		m.Opacity = 1.0;
		m.TextureCoords = i.vTextureCoords.xy;

		return Finish( ShadingModelStandard::Shade( m ) );
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

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		#if D_RENDER_MODE == kRenderTransColor
			return Output( g_vRenderColor / 256.0, g_flBlend );
		#endif

		float4 color = SampleColor( g_tColor, i.vTextureCoords.xy );

		#if D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow
			return Output( color.rgb, color.a * g_flBlend );
		#elif D_RENDER_MODE == kRenderTransAdd
			return Output( color.rgb * g_flBlend, 1.0 );
		#elif D_RENDER_MODE == kRenderTransAlpha
			if ( color.a <= kAlphaReference )
				discard;
		#endif

		#if D_RENDER_MODE == kRenderNormal
			return Standard( i, WaterFog( color.rgb, i.vPositionWithOffsetWs ) );
		#else
			return Standard( i, color.rgb );
		#endif
	}
}
