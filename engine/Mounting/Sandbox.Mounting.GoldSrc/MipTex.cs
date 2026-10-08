using System;
using System.Text;

namespace GoldSrc;

class MipTex
{
	public const int MipLevels = 4;

	private const int HeaderSize = 40;
	private const int PaletteSize = 256 * 3;
	private const byte TransparentIndex = 255;

	public string Name { get; }
	public int Width { get; }
	public int Height { get; }

	public bool HasPixels => _pixels is not null;

	public bool IsTransparent => Name.StartsWith( '{' );

	private readonly byte[] _pixels;
	private readonly byte[] _palette;

	public MipTex( ReadOnlySpan<byte> data )
	{
		if ( data.Length < HeaderSize )
			throw new InvalidDataException( "Miptex is smaller than its header" );

		var name = data[..16];
		var terminator = name.IndexOf( (byte)0 );
		Name = Encoding.ASCII.GetString( terminator < 0 ? name : name[..terminator] );

		Width = BitConverter.ToInt32( data[16..] );
		Height = BitConverter.ToInt32( data[20..] );

		var firstMip = BitConverter.ToInt32( data[24..] );
		var lastMip = BitConverter.ToInt32( data[36..] );
		if ( firstMip == 0 )
			return;

		var palette = lastMip + ((Width >> 3) * (Height >> 3)) + sizeof( short );
		if ( Width <= 0 || Height <= 0 || firstMip + (Width * Height) > data.Length || palette + PaletteSize > data.Length )
			throw new InvalidDataException( $"Miptex '{Name}' is truncated" );

		_pixels = data.Slice( firstMip, Width * Height ).ToArray();
		_palette = data.Slice( palette, PaletteSize ).ToArray();
	}

	public byte[] GetPixels()
	{
		var transparent = IsTransparent;
		var rgba = new byte[_pixels.Length * 4];

		for ( var i = 0; i < _pixels.Length; i++ )
		{
			var index = _pixels[i];
			if ( transparent && index == TransparentIndex )
				continue;

			rgba[(i * 4) + 0] = _palette[(index * 3) + 0];
			rgba[(i * 4) + 1] = _palette[(index * 3) + 1];
			rgba[(i * 4) + 2] = _palette[(index * 3) + 2];
			rgba[(i * 4) + 3] = 255;
		}

		return rgba;
	}

	public Texture CreateDecalTexture()
	{
		var last = TransparentIndex * 3;
		var alphaTest = _palette[last] == 0 && _palette[last + 1] == 0 && _palette[last + 2] == 255;
		var rgba = new byte[_pixels.Length * 4];

		for ( var i = 0; i < _pixels.Length; i++ )
		{
			var index = _pixels[i];
			if ( alphaTest && index == TransparentIndex )
				continue;

			var color = alphaTest ? index * 3 : last;

			rgba[(i * 4) + 0] = _palette[color + 0];
			rgba[(i * 4) + 1] = _palette[color + 1];
			rgba[(i * 4) + 2] = _palette[color + 2];
			rgba[(i * 4) + 3] = alphaTest ? (byte)255 : index;
		}

		TextureUpload.ApplyGamma( rgba, Width, Height, alphaTest ? TextureType.Alpha : TextureType.AlphaGradient );

		return TextureUpload.CreateTexture( rgba, Width, Height );
	}
}
