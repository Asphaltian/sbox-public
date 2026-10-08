class DecalTextureLoader( Wad wad, string lumpName ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var mipTex = new GoldSrc.MipTex( wad.GetLumpData( lumpName ) );
		return mipTex.HasPixels ? mipTex.CreateDecalTexture() : null;
	}
}

class WadLumpLoader( Wad wad, string lumpName ) : ResourceLoader<GameMount>
{
	protected override object Load() => wad.GetLumpData( lumpName );
}

class PicTextureLoader( Wad wad, string lumpName ) : ResourceLoader<GameMount>
{
	private const byte TransparentIndex = 255;

	protected override object Load()
	{
		var data = wad.GetLumpData( lumpName );
		var width = System.BitConverter.ToInt32( data, 0 );
		var height = System.BitConverter.ToInt32( data, 4 );
		var palette = 8 + (width * height) + 2;
		var pixels = new byte[width * height * 4];

		for ( var i = 0; i < width * height; i++ )
		{
			var index = data[8 + i];

			if ( index == TransparentIndex )
				continue;

			pixels[(i * 4) + 0] = data[palette + (index * 3) + 0];
			pixels[(i * 4) + 1] = data[palette + (index * 3) + 1];
			pixels[(i * 4) + 2] = data[palette + (index * 3) + 2];
			pixels[(i * 4) + 3] = 255;
		}

		GoldSrc.TextureUpload.ApplyGamma( pixels, width, height, GoldSrc.TextureType.Alpha );

		return Texture.Create( width, height )
			.WithData( pixels )
			.Finish();
	}
}

class TgaTextureLoader( string fullPath ) : ResourceLoader<GameMount>
{
	private const int HeaderSize = 18;
	private const int TypeRgb = 2;
	private const int TypeRgbRle = 10;
	private const int MaxBufferSize = 0x100000;

	protected override object Load()
	{
		var data = System.IO.File.ReadAllBytes( fullPath );

		if ( data.Length < HeaderSize + 2 )
			return null;

		var type = data[2];
		var width = data[12] | (data[13] << 8);
		var height = data[14] | (data[15] << 8);
		var bits = data[16];

		if ( (type != TypeRgb && type != TypeRgbRle) || data[1] != 0 || (bits != 32 && bits != 24) || width == 0 || height == 0 )
			return null;

		if ( width * height * 4 > MaxBufferSize )
			return null;

		var bytes = bits / 8;
		var pixels = new byte[width * height * 4];
		var source = HeaderSize + data[0];
		var row = height - 1;
		var column = 0;

		void Write( int offset )
		{
			var target = ((row * width) + column) * 4;

			pixels[target + 0] = data[offset + 2];
			pixels[target + 1] = data[offset + 1];
			pixels[target + 2] = data[offset + 0];
			pixels[target + 3] = bytes == 4 ? data[offset + 3] : (byte)255;

			if ( ++column == width )
			{
				column = 0;
				row--;
			}
		}

		if ( type == TypeRgb )
		{
			if ( source + (width * height * bytes) > data.Length )
				return null;

			for ( var i = 0; i < width * height; i++, source += bytes )
				Write( source );
		}
		else
		{
			while ( row >= 0 && source < data.Length )
			{
				var header = data[source++];
				var count = (header & 0x7f) + 1;

				if ( (header & 0x80) != 0 )
				{
					for ( var i = 0; i < count && row >= 0; i++ )
						Write( source );

					source += bytes;
				}
				else
				{
					for ( var i = 0; i < count && row >= 0; i++, source += bytes )
						Write( source );
				}
			}
		}

		return Texture.Create( width, height )
			.WithData( pixels )
			.Finish();
	}
}

class WadTextureLoader( Wad wad, string lumpName ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var mipTex = new GoldSrc.MipTex( wad.GetLumpData( lumpName ) );
		if ( !mipTex.HasPixels )
			return null;

		var pixels = mipTex.GetPixels();

		GoldSrc.TextureUpload.ApplyGamma( pixels, mipTex.Width, mipTex.Height, mipTex.IsTransparent ? GoldSrc.TextureType.Alpha : GoldSrc.TextureType.Opaque );

		return GoldSrc.TextureUpload.CreateTexture( pixels, mipTex.Width, mipTex.Height );
	}
}
