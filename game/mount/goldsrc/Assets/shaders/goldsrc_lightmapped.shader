
HEADER
{
	Description = "GoldSrc world surface with baked lightmaps and animated light styles";
}

FEATURES
{
	#include "common/features.hlsl"

	Feature( F_ANIMATED, 0..1, "Animated" );
	Feature( F_DECAL, 0..1, "Decal" );
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

	float g_flClientTime < Attribute( "ClientTime" ); Default( 0.0 ); >;

	#define S_UV2 1
	#define CUSTOM_MATERIAL_INPUTS

}

struct VertexInput
{
	#include "common/vertexinput.hlsl"

	float4 vLightmapBlock : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"

	nointerpolation float2 vLightmapBlock : TEXCOORD12;
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		i.vLightmapBlock = round( v.vLightmapBlock.xy * 255.0 );
		return FinalizeVertex( i );
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

	StaticCombo( S_ANIMATED, F_ANIMATED, Sys( ALL ) );
	StaticCombo( S_MODE_DEPTH, 0..1, Sys( ALL ) );
	StaticCombo( S_DECAL, F_DECAL, Sys( ALL ) );
	DynamicCombo( D_RENDER_MODE, 0..5, Sys( ALL ) );

	#if S_DECAL
		RenderState( BlendEnable, true );
		RenderState( SrcBlend, SRC_ALPHA );
		RenderState( DstBlend, INV_SRC_ALPHA );
		RenderState( DepthWriteEnable, false );
		RenderState( DepthBias, 4 );
		RenderState( SlopeScaleDepthBias, 1 );
	#elif D_RENDER_MODE == kRenderTransColor || D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow
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

	#define ColorFrame( name ) \
		CreateInputTexture2D( name, Linear, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) ); \
		Texture2D g_t##name < Channel( RGBA, Box( name ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >

	ColorFrame( Color0 );

	#if S_ANIMATED
		ColorFrame( Color1 );
		ColorFrame( Color2 );
		ColorFrame( Color3 );
		ColorFrame( Color4 );
		ColorFrame( Color5 );
		ColorFrame( Color6 );
		ColorFrame( Color7 );
		ColorFrame( Color8 );
		ColorFrame( Color9 );

		ColorFrame( AlternateColor0 );
		ColorFrame( AlternateColor1 );
		ColorFrame( AlternateColor2 );
		ColorFrame( AlternateColor3 );
		ColorFrame( AlternateColor4 );
		ColorFrame( AlternateColor5 );
		ColorFrame( AlternateColor6 );
		ColorFrame( AlternateColor7 );
		ColorFrame( AlternateColor8 );
		ColorFrame( AlternateColor9 );

		float g_flFrameCount < Default( 1.0 ); >;
		float g_flAlternateFrameCount < Default( 0.0 ); >;
	#endif

	CreateInputTexture2D( Lightmap0, Linear, 8, "None", "_color", ",0/,0/0", Default4( 0.00, 0.00, 0.00, 0.00 ) );
	CreateInputTexture2D( Lightmap1, Linear, 8, "None", "_color", ",0/,0/0", Default4( 0.00, 0.00, 0.00, 0.00 ) );
	CreateInputTexture2D( Lightmap2, Linear, 8, "None", "_color", ",0/,0/0", Default4( 0.00, 0.00, 0.00, 0.00 ) );
	CreateInputTexture2D( Lightmap3, Linear, 8, "None", "_color", ",0/,0/0", Default4( 0.00, 0.00, 0.00, 0.00 ) );

	Texture2D g_tLightmap0 < Channel( RGBA, Box( Lightmap0 ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;
	Texture2D g_tLightmap1 < Channel( RGBA, Box( Lightmap1 ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;
	Texture2D g_tLightmap2 < Channel( RGBA, Box( Lightmap2 ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;
	Texture2D g_tLightmap3 < Channel( RGBA, Box( Lightmap3 ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;

	CreateInputTexture2D( LightStyles, Linear, 8, "None", "_mask", ",0/,0/0", Default4( 0.00, 0.00, 0.00, 0.00 ) );
	Texture2D g_tLightStyles < Channel( RGBA, Box( LightStyles ), Linear ); OutputFormat( RGBA8888 ); SrgbRead( false ); >;

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
	static const float kAlphaReference = 0.25;

	static const int kFullbrightStyle = 254;
	static const int kNoStyle = 255;
	static const int kLightmapBlock = 128;

	float g_flDrawTiled < Default( 0.0 ); >;

	float g_flBlend < Attribute( "Blend" ); >;
	float g_flFrame < Attribute( "Frame" ); >;
	float3 g_vRenderColor < Attribute( "RenderColor" ); >;

	DynamicCombo( D_GAMMA_SPACE, 0..1, Sys( ALL ) );

	#if D_GAMMA_SPACE
		#if S_DECAL || D_RENDER_MODE == kRenderTransColor || D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow || D_RENDER_MODE == kRenderTransAdd
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

	float g_flHasLightStyleValues < Attribute( "HasLightStyleValues" ); Default( 0.0 ); >;

	float g_flHasDynamicLight < Attribute( "HasDynamicLight" ); Default( 0.0 ); >;
	Texture2D g_tDynamicLight < Attribute( "DynamicLight" ); >;
	Texture2D g_tLightStyleValues < Attribute( "LightStyleValues" ); >;

	float LightStyleValue( int style )
	{
		if ( style == kNoStyle )
			return 0.0;

		if ( g_flHasLightStyleValues > 0.0 )
			return g_tLightStyleValues.Load( int3( style, 0, 0 ) ).r;

		int length = (int)round( g_tLightStyles.Load( int3( 0, style, 0 ) ).g * 255.0 );
		if ( length == 0 )
			return 256.0;

		int frame = (int)( g_flClientTime * 10.0 ) % length;
		return round( g_tLightStyles.Load( int3( frame, style, 0 ) ).r * 255.0 ) * 22.0;
	}

	float3 Blocklights( float4 sample )
	{
		int style = (int)round( sample.a * 255.0 );
		if ( style == kFullbrightStyle )
			return 0xff00;

		return round( sample.rgb * 255.0 ) * LightStyleValue( style );
	}

	float3 LightmapTexel( int2 texel, int2 block )
	{
		int3 location = int3( ( texel % kLightmapBlock + kLightmapBlock ) % kLightmapBlock + block * kLightmapBlock, 0 );

		float3 blocklights = Blocklights( g_tLightmap0.Load( location ) );
		blocklights += Blocklights( g_tLightmap1.Load( location ) );
		blocklights += Blocklights( g_tLightmap2.Load( location ) );
		blocklights += Blocklights( g_tLightmap3.Load( location ) );

		if ( g_flHasDynamicLight > 0.0 )
			blocklights += g_tDynamicLight.Load( location ).rgb;

		float3 index = min( floor( blocklights / 64.0 ), 1023.0 );
		float3 screen = LightGammaTable( index );

		return floor( screen / 4.0 ) / 255.0;
	}

	float3 Lightmap( float2 uv, int2 block )
	{
		float2 position = uv * kLightmapBlock - 0.5;
		int2 texel = (int2)floor( position );
		float2 blend = frac( position );

		float3 top = lerp( LightmapTexel( texel, block ), LightmapTexel( texel + int2( 1, 0 ), block ), blend.x );
		float3 bottom = lerp( LightmapTexel( texel + int2( 0, 1 ), block ), LightmapTexel( texel + int2( 1, 1 ), block ), blend.x );

		return lerp( top, bottom, blend.y );
	}

	float ScrollOffset()
	{
		float2 size;
		g_tColor0.GetDimensions( size.x, size.y );

		float speed = ( g_vRenderColor.g * 256.0 + g_vRenderColor.b ) / 16.0;
		if ( g_vRenderColor.r == 0.0 )
			speed = -speed;

		return fmod( speed * g_flClientTime / size.x, 1.0 );
	}

	float4 Color( float2 uv )
	{
		#if S_ANIMATED
			int tick = (int)( g_flClientTime * 10.0 );

			if ( g_flFrame != 0.0 && g_flAlternateFrameCount > 0.0 )
			{
				switch ( tick % (int)g_flAlternateFrameCount )
				{
					case 0: return SampleColor( g_tAlternateColor0, uv );
					case 1: return SampleColor( g_tAlternateColor1, uv );
					case 2: return SampleColor( g_tAlternateColor2, uv );
					case 3: return SampleColor( g_tAlternateColor3, uv );
					case 4: return SampleColor( g_tAlternateColor4, uv );
					case 5: return SampleColor( g_tAlternateColor5, uv );
					case 6: return SampleColor( g_tAlternateColor6, uv );
					case 7: return SampleColor( g_tAlternateColor7, uv );
					case 8: return SampleColor( g_tAlternateColor8, uv );
					case 9: return SampleColor( g_tAlternateColor9, uv );
				}
			}

			switch ( tick % (int)g_flFrameCount )
			{
				case 1: return SampleColor( g_tColor1, uv );
				case 2: return SampleColor( g_tColor2, uv );
				case 3: return SampleColor( g_tColor3, uv );
				case 4: return SampleColor( g_tColor4, uv );
				case 5: return SampleColor( g_tColor5, uv );
				case 6: return SampleColor( g_tColor6, uv );
				case 7: return SampleColor( g_tColor7, uv );
				case 8: return SampleColor( g_tColor8, uv );
				case 9: return SampleColor( g_tColor9, uv );
			}
		#endif

		return SampleColor( g_tColor0, uv );
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

	float3 SceneLight( PixelInput i )
	{
		Material m = Material::Init( i.vPositionWithOffsetWs, i.vPositionSs );

		m.Normal = normalize( i.vNormalWs );
		m.Roughness = 1.0;
		m.Metalness = 0.0;

		LightingTerms_t terms = InitLightingTerms();
		ComputeDirectLighting( terms, ShadingModelStandard::MaterialToCombinerInput( m ) );

		return terms.vDiffuse.rgb;
	}

	float4 Blended( PixelInput i, float3 color, float3 albedo, float alpha )
	{
		float3 position = i.vPositionWithOffsetWs + g_vHighPrecisionLightingOffsetWs.xyz;
		float3 result = GammaToLinear( color ) * ScreenSpaceAmbientOcclusion::Sample( i.vPositionSs ) + GammaToLinear( albedo ) * SceneLight( i );

		return Finish( float4( Fog::Apply( position, i.vPositionSs.xy, result ), alpha ) );
	}

	float4 Standard( PixelInput i, float3 albedo, float3 baked )
	{
		Material m = Material::Init( i.vPositionWithOffsetWs, i.vPositionSs );

		m.Albedo = GammaToLinear( albedo );
		m.Emission = GammaToLinear( baked ) * ScreenSpaceAmbientOcclusion::Sample( i.vPositionSs );
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
		#if S_MODE_DEPTH && ( S_DECAL || D_RENDER_MODE == kRenderTransColor )
			discard;
		#endif

		#if S_DECAL
			float4 decal = SampleColor( g_tColor0, i.vTextureCoords.xy );

			if ( decal.a == 0.0 )
				discard;

			#if D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow
				return Output( decal.rgb, decal.a * g_flBlend );
			#elif D_RENDER_MODE == kRenderTransAdd
				return Output( decal.rgb * g_flBlend, decal.a );
			#endif

			float3 decalLight = Lightmap( i.vTextureCoords.zw, (int2)i.vLightmapBlock ) * ( 128.0 / 192.0 );

			float3 decalColor = saturate( decal.rgb * decalLight * 2.0 );

			#if D_RENDER_MODE == kRenderNormal
				decalColor = WaterFog( decalColor, i.vPositionWithOffsetWs );
			#endif

			return Blended( i, decalColor, decal.rgb, decal.a );
		#endif

		#if D_RENDER_MODE == kRenderTransColor
			return Output( g_vRenderColor / 256.0, g_flBlend );
		#endif

		float2 uv = i.vTextureCoords.xy;
		if ( g_flDrawTiled > 0.0 )
			uv.x += ScrollOffset();

		float4 albedo = Color( uv );

		#if D_RENDER_MODE == kRenderTransTexture || D_RENDER_MODE == kRenderGlow
			return Output( albedo.rgb, albedo.a * g_flBlend );
		#elif D_RENDER_MODE == kRenderTransAdd
			return Output( albedo.rgb * g_flBlend, 1.0 );
		#endif

		#if D_RENDER_MODE == kRenderTransAlpha
			if ( albedo.a <= kAlphaReference )
				discard;
		#endif

		float3 lightmap = Lightmap( i.vTextureCoords.zw, (int2)i.vLightmapBlock ) * ( 128.0 / 192.0 );
		float3 diffuse = albedo.rgb * lightmap * 2.0;

		#if D_RENDER_MODE == kRenderNormal
			return Standard( i, albedo.rgb, WaterFog( saturate( diffuse ), i.vPositionWithOffsetWs ) );
		#else
			return Standard( i, albedo.rgb, saturate( diffuse ) );
		#endif
	}
}
