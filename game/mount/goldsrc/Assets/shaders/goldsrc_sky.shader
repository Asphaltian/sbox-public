
HEADER
{
	Description = "GoldSrc sky box";
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

	PixelInput MainVs( VertexInput v )
	{
		v.vPositionOs += g_vCameraPositionWs;

		PixelInput i = ProcessVertex( v );
		PixelInput o = FinalizeVertex( i );

		o.vPositionPs.z = 0.0;

		return o;
	}
}

PS
{
	#include "common/pixel.hlsl"

	RenderState( CullMode, NONE );
	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, true );
	RenderState( DepthFunc, GREATER_EQUAL );

	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	#if D_GAMMA_SPACE
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ZERO );
		RenderState( SrcBlendAlpha, ZERO );
		RenderState( DstBlendAlpha, ZERO );
	#endif

	SamplerState g_sColorSampler < Filter( Bilinear ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	CreateInputTexture2D( Color, Linear, 8, "None", "_color", ",0/,0/0", Default4( 0.00, 0.00, 0.00, 1.00 ) );
	Texture2D g_tColor < Channel( RGBA, Box( Color ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;

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

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 color = Tex2DS( g_tColor, g_sColorSampler, i.vTextureCoords.xy ).rgb;

		return Output( color, 1.0 );
	}
}
