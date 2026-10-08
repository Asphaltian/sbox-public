using System;

namespace GoldSrc;

enum TextureType
{
	Opaque,
	Alpha,
	AlphaGradient
}

static class TextureUpload
{
	private const float DefaultGamma = 2.2f;
	private const float DefaultTexGamma = 2.0f;
	private const float DefaultLightGamma = 2.5f;
	private const float DefaultBrightness = 0.0f;

	private static byte[] _texGammaTable;
	private static float _tableGamma;
	private static float _tableTexGamma;

	public static float Gamma => ReadCvar( "gamma", DefaultGamma );

	public static float TexGamma => ReadCvar( "texgamma", DefaultTexGamma );

	public static float LightGamma => ReadCvar( "lightgamma", DefaultLightGamma );

	public static float Brightness => ReadCvar( "brightness", DefaultBrightness );

	public static byte[] TexGammaTable
	{
		get
		{
			var gamma = Gamma;
			var texGamma = TexGamma;

			if ( _texGammaTable is not null && gamma == _tableGamma && texGamma == _tableTexGamma )
				return _texGammaTable;

			_tableGamma = gamma;
			_tableTexGamma = texGamma;

			return _texGammaTable = [.. Enumerable.Range( 0, 256 )
				.Select( i => (byte)Math.Clamp( (int)(Math.Pow( i / 255.0, texGamma / gamma ) * 255.0), 0, 255 ) )];
		}
	}

	private static float ReadCvar( string name, float defaultValue )
	{
		return float.TryParse( ConsoleSystem.GetValue( name ), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value ) ? value : defaultValue;
	}

	public static int LightGammaTable( float index )
	{
		var brightness = Brightness;
		var light = MathF.Pow( index / 1023f, LightGamma );
		var threshold = 0.125f;

		if ( brightness > 1f )
		{
			light *= brightness;
			threshold = 0.05f;
		}
		else if ( brightness > 0f )
		{
			threshold = 0.125f - (brightness * brightness * 0.075f);
		}

		light = light > threshold
			? 0.125f + ((light - threshold) / (1f - threshold) * 0.875f)
			: light / threshold * 0.125f;

		return Math.Clamp( (int)(MathF.Pow( light, 1f / Gamma ) * 1023f), 0, 1023 );
	}

	public static byte Dither( byte value )
	{
		return (byte)(value | (value >> 6));
	}

	public static Texture CreateTexture( byte[] pixels, int width, int height, bool mipmap = true )
	{
		if ( !mipmap )
		{
			return Texture.Create( width, height )
				.WithData( pixels )
				.Finish();
		}

		var scaled = new byte[pixels.Length + ((width + height) * 4) + 8];
		pixels.CopyTo( scaled, 0 );

		var levels = new List<byte[]> { pixels };
		var mipWidth = width;
		var mipHeight = height;

		while ( mipWidth > 1 || mipHeight > 1 )
		{
			MipMap( scaled, mipWidth, mipHeight );

			mipWidth = Math.Max( 1, mipWidth >> 1 );
			mipHeight = Math.Max( 1, mipHeight >> 1 );

			levels.Add( scaled[..(mipWidth * mipHeight * 4)] );
		}

		using var data = new MemoryStream();

		for ( var i = levels.Count - 1; i >= 0; i-- )
			data.Write( levels[i] );

		return Texture.Create( width, height )
			.WithData( data.ToArray() )
			.WithMips( levels.Count )
			.Finish();
	}

	public static void ApplyGamma( byte[] pixels, int width, int height, TextureType type )
	{
		var dither = type == TextureType.Opaque;

		if ( type == TextureType.Alpha && !HasTransparentPixel( pixels ) )
			type = TextureType.Opaque;

		var table = TexGammaTable;

		for ( var i = 0; i < pixels.Length; i += 4 )
		{
			for ( var channel = 0; channel < 3; channel++ )
			{
				var value = table[pixels[i + channel]];
				pixels[i + channel] = dither ? Dither( value ) : value;
			}
		}

		if ( type != TextureType.Opaque )
			BlendTransparentPixels( pixels, width, height );
	}

	private static bool HasTransparentPixel( byte[] pixels )
	{
		for ( var i = 3; i < pixels.Length; i += 4 )
		{
			if ( pixels[i] == 0 )
				return true;
		}

		return false;
	}

	private static void MipMap( byte[] data, int width, int height )
	{
		var stride = width * 4;
		var rows = height >> 1;
		var input = 0;
		var output = 0;

		for ( var i = 0; i < rows; i++, input += stride )
		{
			for ( var j = 0; j < stride; j += 8, output += 4, input += 8 )
			{
				for ( var channel = 0; channel < 4; channel++ )
				{
					data[output + channel] = (byte)((data[input + channel] + data[input + 4 + channel]
						+ data[input + stride + channel] + data[input + stride + 4 + channel]) >> 2);
				}
			}
		}
	}

	private static void BlendTransparentPixels( byte[] pixels, int width, int height )
	{
		for ( var y = 0; y < height; y++ )
		{
			for ( var x = 0; x < width; x++ )
			{
				var pixel = ((y * width) + x) * 4;
				if ( BitConverter.ToUInt32( pixels, pixel ) != 0 )
					continue;

				BoxFilter3x3( pixels, width, height, x, y );
			}
		}
	}

	private static void BoxFilter3x3( byte[] pixels, int width, int height, int x, int y )
	{
		var r = 0;
		var g = 0;
		var b = 0;
		var count = 0;

		for ( var i = x - 1; i <= x + 1; i++ )
		{
			for ( var j = y - 1; j <= y + 1; j++ )
			{
				if ( i < 0 || i >= width || j < 0 || j >= height )
					continue;

				var source = ((j * width) + i) * 4;
				if ( pixels[source + 3] == 0 )
					continue;

				r += pixels[source + 0];
				g += pixels[source + 1];
				b += pixels[source + 2];
				count++;
			}
		}

		if ( count == 0 )
			count = 1;

		var pixel = ((y * width) + x) * 4;
		pixels[pixel + 0] = (byte)(r / count);
		pixels[pixel + 1] = (byte)(g / count);
		pixels[pixel + 2] = (byte)(b / count);
		pixels[pixel + 3] = 0;
	}
}
