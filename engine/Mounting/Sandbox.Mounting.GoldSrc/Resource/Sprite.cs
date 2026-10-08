using System;
using GoldSrc;

class Sprite( string fullPath )
{
	public const int Ident = 0x50534449;
	public const int Version = 2;
	public const int NumFramesOffset = 28;

	private const int PaletteSize = 256 * 3;
	private const byte TransparentIndex = 255;

	private enum TexFormat
	{
		Normal,
		Additive,
		IndexAlpha,
		AlphaTest
	}

	private Texture[] _frames;

	public Texture GetFrame( int index )
	{
		_frames ??= LoadFrames();

		return index < _frames.Length ? _frames[index] : null;
	}

	private Texture[] LoadFrames()
	{
		using var reader = new BinaryReader( new MemoryStream( File.ReadAllBytes( fullPath ) ) );

		if ( reader.ReadInt32() != Ident || reader.ReadInt32() != Version )
			throw new InvalidDataException( $"{fullPath} has wrong version number" );

		reader.ReadInt32();
		var format = (TexFormat)reader.ReadInt32();
		reader.ReadSingle();
		reader.ReadInt32();
		reader.ReadInt32();
		var numFrames = reader.ReadInt32();
		reader.ReadSingle();
		reader.ReadInt32();

		var palette = new byte[PaletteSize];
		reader.ReadBytes( reader.ReadInt16() * 3 ).AsSpan( 0, PaletteSize ).CopyTo( palette );

		var frames = new Texture[numFrames];

		for ( var i = 0; i < numFrames; i++ )
		{
			if ( reader.ReadInt32() == 0 )
			{
				frames[i] = ReadFrame( reader, palette, format );
				continue;
			}

			var groupFrames = reader.ReadInt32();
			reader.BaseStream.Seek( groupFrames * sizeof( float ), SeekOrigin.Current );

			for ( var frame = 0; frame < groupFrames; frame++ )
				ReadFrame( reader, palette, format );
		}

		return frames;
	}

	private static Texture ReadFrame( BinaryReader reader, byte[] palette, TexFormat format )
	{
		reader.ReadInt32();
		reader.ReadInt32();

		var width = reader.ReadInt32();
		var height = reader.ReadInt32();
		var indices = reader.ReadBytes( width * height );
		var pixels = new byte[indices.Length * 4];

		for ( var i = 0; i < indices.Length; i++ )
		{
			var index = indices[i];
			var color = format == TexFormat.IndexAlpha ? TransparentIndex : index;

			if ( format == TexFormat.AlphaTest && index == TransparentIndex )
				continue;

			pixels[(i * 4) + 0] = palette[(color * 3) + 0];
			pixels[(i * 4) + 1] = palette[(color * 3) + 1];
			pixels[(i * 4) + 2] = palette[(color * 3) + 2];
			pixels[(i * 4) + 3] = format == TexFormat.IndexAlpha ? index : (byte)255;
		}

		TextureUpload.ApplyGamma( pixels, width, height, format switch
		{
			TexFormat.IndexAlpha => TextureType.AlphaGradient,
			TexFormat.AlphaTest => TextureType.Alpha,
			_ => TextureType.Opaque
		} );

		return TextureUpload.CreateTexture( pixels, width, height );
	}
}

class SpriteTextureLoader( Sprite sprite, int frame ) : ResourceLoader<GameMount>
{
	protected override object Load() => sprite.GetFrame( frame );
}

class SpriteMaterialLoader( Sprite sprite, int frame ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "goldsrc_sprite", false );
		material?.Set( "Color", sprite.GetFrame( frame ) );

		return material;
	}
}
