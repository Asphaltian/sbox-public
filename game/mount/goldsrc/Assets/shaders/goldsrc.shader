HEADER
{
	Description = "GoldSrc texture lit by the scene";
}

FEATURES
{
	#include "common/features.hlsl"

	Feature( F_MASKED, 0..1, "GoldSrc" );
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

	StaticCombo( S_MASKED, F_MASKED, Sys( ALL ) );
	StaticCombo( S_MODE_DEPTH, 0..1, Sys( ALL ) );
	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	#if D_GAMMA_SPACE
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, ONE );
		RenderState( DstBlend, ZERO );
		RenderState( SrcBlendAlpha, ZERO );
		RenderState( DstBlendAlpha, ZERO );
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

	static const float kAlphaMin = 0.25;

	float3 GammaToLinear( float3 color )
	{
		float3 low = color / 12.92;
		float3 high = pow( ( color + 0.055 ) / 1.055, 2.4 );
		return lerp( low, high, step( 0.04045, color ) );
	}

	float3 LinearToGamma( float3 color )
	{
		float3 low = color * 12.92;
		float3 high = 1.055 * pow( max( color, 0.0 ), 1.0 / 2.4 ) - 0.055;
		return lerp( low, high, step( 0.0031308, color ) );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float4 albedo = SampleColor( g_tColor, i.vTextureCoords.xy );

		#if S_MASKED
			if ( albedo.a <= kAlphaMin )
				discard;
		#endif

		Material m = Material::Init();

		m.Albedo = GammaToLinear( albedo.rgb );
		m.Normal = normalize( i.vNormalWs );
		m.Roughness = 1.0;
		m.Metalness = 0.0;
		m.AmbientOcclusion = 1.0;
		m.Opacity = 1.0;
		m.TextureCoords = i.vTextureCoords.xy;

		float4 color = ShadingModelStandard::Shade( i, m );

		#if D_GAMMA_SPACE
			if ( !DepthNormals::WantsDepthNormals() && !g_bWireframeMode && !ToolsVis::WantsToolsVis() )
				color.rgb = LinearToGamma( color.rgb );
		#endif

		return color;
	}
}
