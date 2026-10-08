HEADER
{
	Description = "GoldSrc particles and tracers";
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

	float4 vColor : COLOR0 < Semantic( Color ); >;
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
		i.vVertexColor = v.vColor;
		return FinalizeVertex( i );
	}
}

PS
{
	#include "common/pixel.hlsl"

	DynamicCombo( D_ADDITIVE, 0..1, Sys( ALL ) );
	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	#if D_GAMMA_SPACE
		RenderState( SrcBlendAlpha, ZERO );
		RenderState( DstBlendAlpha, ONE );
	#endif

	RenderState( BlendEnable, true );
	RenderState( CullMode, NONE );

	#if D_ADDITIVE
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ONE );
		RenderState( DepthWriteEnable, false );
	#else
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
	#endif

	SamplerState g_sColorSampler < Filter( Bilinear ); AddressU( WRAP ); AddressV( WRAP ); >;

	CreateInputTexture2D( Color, Linear, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
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
		float4 color = Tex2DS( g_tColor, g_sColorSampler, i.vTextureCoords.xy ) * i.vVertexColor;

		#if !D_ADDITIVE
			if ( color.a == 0.0 )
				discard;
		#endif

		return Output( color.rgb, color.a );
	}
}
