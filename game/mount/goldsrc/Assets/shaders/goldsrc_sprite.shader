HEADER
{
	Description = "GoldSrc sprite frame";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
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

	#define kRenderGlow 3
	#define kViewModelDepthRange 0.3

	DynamicCombo( D_RENDER_MODE, 0..5, Sys( ALL ) );

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		PixelInput o = FinalizeVertex( i );

		#if D_RENDER_MODE == kRenderGlow
			o.vPositionPs.z = o.vPositionPs.w * ( 1.0 - kViewModelDepthRange );
		#endif

		return o;
	}
}

PS
{
	#include "common/pixel.hlsl"

	#define kRenderNormal 0
	#define kRenderTransColor 1
	#define kRenderTransTexture 2
	#define kRenderGlow 3
	#define kRenderTransAlpha 4
	#define kRenderTransAdd 5

	DynamicCombo( D_RENDER_MODE, 0..5, Sys( ALL ) );
	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	#if D_GAMMA_SPACE
		RenderState( SrcBlendAlpha, ZERO );
		RenderState( DstBlendAlpha, ONE );
	#endif

	RenderState( BlendEnable, true );

	#if D_RENDER_MODE == kRenderGlow || D_RENDER_MODE == kRenderTransAdd
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ONE );
	#else
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
	#endif

	#if D_RENDER_MODE == kRenderGlow || D_RENDER_MODE == kRenderTransAlpha || D_RENDER_MODE == kRenderTransAdd
		RenderState( DepthWriteEnable, false );
	#endif


	SamplerState g_sColorSampler < Filter( Anisotropic ); MaxAniso( 16 ); AddressU( WRAP ); AddressV( WRAP ); >;

	CreateInputTexture2D( Color, Linear, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;

	float4 g_vSpriteColor < Attribute( "SpriteColor" ); Default4( 1.0, 1.0, 1.0, 1.0 ); >;

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

	float4 Finish( float4 color, float3 source )
	{
		#if D_GAMMA_SPACE
			if ( !DepthNormals::WantsDepthNormals() && !g_bWireframeMode && !ToolsVis::WantsToolsVis() )
				color.rgb = source + ( LinearToGamma( color.rgb ) - LinearToGamma( GammaToLinear( source ) ) );
		#endif

		return color;
	}

	float4 Blended( PixelInput i, float3 color, float alpha )
	{
		float3 position = i.vPositionWithOffsetWs + g_vCameraPositionWs;

		return Finish( float4( Fog::Apply( position, i.vPositionSs.xy, GammaToLinear( color ) ), alpha ), color );
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
		float4 color = Tex2DS( g_tColor, g_sColorSampler, i.vTextureCoords.xy ) * g_vSpriteColor;

		if ( color.a == 0.0 )
			discard;

		#if D_RENDER_MODE == kRenderNormal
			return Blended( i, WaterFog( color.rgb, i.vPositionWithOffsetWs ), color.a );
		#else
			return Output( color.rgb, color.a );
		#endif
	}
}
