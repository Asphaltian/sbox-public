using System;

partial class BrushModel
{
	private const int BlockWidth = 128;
	private const int BlockHeight = 128;
	private const int LightmapColumns = 8;
	private const int LuxelSize = 16;
	private const byte FullbrightStyle = 254;
	private const int MaxLightStyles = 64;
	private const int MaxStyleString = 64;

	private static readonly Dictionary<int, string> DefaultLightStyles = new()
	{
		[0] = "m",
		[1] = "mmnmmommommnonmmonqnmmo",
		[2] = "abcdefghijklmnopqrstuvwxyzyxwvutsrqponmlkjihgfedcba",
		[3] = "mmmmmaaaaammmmmaaaaaabcdefgabcdefg",
		[4] = "mamamamamama",
		[5] = "jklmnopqrstuvwxyzyxwvutsrqponmlkj",
		[6] = "nmonqnmomnmomomno",
		[7] = "mmmaaaabcdefgmmmmaaaammmaamm",
		[8] = "mmmaaammmaaammmabcdefaaaammmmabcdefmmmaaaa",
		[9] = "aaaaaaaazzzzzzzz",
		[10] = "mmamammmmammamamaaamammma",
		[11] = "abcdefghijklmnopqrrqponmlkjihgfedcba",
		[12] = "mmnnmmnnnmmnn",
		[63] = "a",
	};

	private readonly record struct Surface( Vector2 TextureMins, int Width, int Height, int Page, int X, int Y )
	{
		public Vector2 GetLightmapCoord( Vector2 texel )
		{
			var block = new Vector2( X, Y ) * LuxelSize;
			var center = new Vector2( LuxelSize / 2f );

			return (texel - TextureMins + block + center) / new Vector2( BlockWidth * LuxelSize, BlockHeight * LuxelSize );
		}

		public Color32 LightmapBlock => new( (byte)(Page % LightmapColumns), (byte)(Page / LightmapColumns), 0 );
	}

	private Surface[] _surfaces;
	private Texture[] _lightmaps;
	private Texture _lightStyles;
	private Dictionary<int, string> _lightStylePatterns;

	private void BuildLightmaps()
	{
		_lightStyles = CreateLightStyles();
		_surfaces = new Surface[_file.Faces.Length];

		var allocated = new List<int[]>();
		var pages = new List<byte[][]>();

		for ( var i = 0; i < _file.Faces.Length; i++ )
		{
			var face = _file.Faces[i];
			var (mins, width, height) = CalcSurfaceExtents( face );

			if ( !HasLightmap( face ) )
			{
				_surfaces[i] = new Surface( mins, width, height, 0, 0, 0 );
				continue;
			}

			if ( width > BlockWidth || height > BlockHeight )
				throw new InvalidDataException( $"Bad surface extents {width}/{height}" );

			var page = AllocBlock( allocated, width, height, out var x, out var y );
			while ( pages.Count <= page )
				pages.Add( NewLightmapPage() );

			_surfaces[i] = new Surface( mins, width, height, page, x, y );
			CopySamples( face, _surfaces[i], pages[page] );
		}

		if ( pages.Count == 0 )
			pages.Add( NewLightmapPage() );

		var atlasWidth = BlockWidth * LightmapColumns;
		var atlasHeight = BlockHeight * ((pages.Count + LightmapColumns - 1) / LightmapColumns);

		_lightmaps = new Texture[GoldSrc.Bsp.Face.MaxLightmaps];

		for ( var layer = 0; layer < _lightmaps.Length; layer++ )
		{
			var data = new byte[atlasWidth * atlasHeight * 4];

			for ( var pixel = 3; pixel < data.Length; pixel += 4 )
				data[pixel] = GoldSrc.Bsp.Face.NoStyle;

			for ( var page = 0; page < pages.Count; page++ )
			{
				var left = page % LightmapColumns * BlockWidth;
				var top = page / LightmapColumns * BlockHeight;

				for ( var y = 0; y < BlockHeight; y++ )
					Array.Copy( pages[page][layer], y * BlockWidth * 4, data, (((top + y) * atlasWidth) + left) * 4, BlockWidth * 4 );
			}

			_lightmaps[layer] = Texture.Create( atlasWidth, atlasHeight, ImageFormat.RGBA8888 )
				.WithData( data )
				.Finish();
		}
	}

	private (Vector2 Mins, int Width, int Height) CalcSurfaceExtents( in GoldSrc.Bsp.Face face )
	{
		var texInfo = _file.TexInfos[face.TexInfo];
		var mins = new Vector2( float.MaxValue );
		var maxs = new Vector2( float.MinValue );

		for ( var i = 0; i < face.NumEdges; i++ )
		{
			var texel = texInfo.Extent( _file.GetVertex( face, i ) );
			mins = Vector2.Min( mins, texel );
			maxs = Vector2.Max( maxs, texel );
		}

		var minS = MathF.Floor( mins.x / LuxelSize );
		var minT = MathF.Floor( mins.y / LuxelSize );
		var maxS = MathF.Ceiling( maxs.x / LuxelSize );
		var maxT = MathF.Ceiling( maxs.y / LuxelSize );

		return (new Vector2( minS, minT ) * LuxelSize, (int)(maxS - minS) + 1, (int)(maxT - minT) + 1);
	}

	private static int AllocBlock( List<int[]> allocated, int width, int height, out int x, out int y )
	{
		for ( var page = 0; ; page++ )
		{
			if ( page == allocated.Count )
				allocated.Add( new int[BlockWidth] );

			var best = BlockHeight;
			x = 0;
			y = 0;

			for ( var i = 0; i < BlockWidth - width; i++ )
			{
				var top = 0;
				int j;

				for ( j = 0; j < width; j++ )
				{
					if ( allocated[page][i + j] >= best )
						break;

					top = Math.Max( top, allocated[page][i + j] );
				}

				if ( j == width )
				{
					x = i;
					y = best = top;
				}
			}

			if ( best + height > BlockHeight )
				continue;

			for ( var i = 0; i < width; i++ )
				allocated[page][x + i] = best + height;

			return page;
		}
	}

	private void CopySamples( in GoldSrc.Bsp.Face face, in Surface surface, byte[][] page )
	{
		var fullbright = _file.Lighting.Length == 0;
		var size = surface.Width * surface.Height * 3;

		for ( var layer = 0; layer < GoldSrc.Bsp.Face.MaxLightmaps; layer++ )
		{
			var style = face.Styles[layer];
			var source = face.LightOffset + (layer * size);
			var hasSamples = style != GoldSrc.Bsp.Face.NoStyle && face.LightOffset >= 0 && source + size <= _file.Lighting.Length;

			if ( layer == 0 && fullbright )
				style = FullbrightStyle;
			else if ( !hasSamples )
				break;

			for ( var t = 0; t < surface.Height; t++ )
			{
				for ( var s = 0; s < surface.Width; s++ )
				{
					var sample = source + (((t * surface.Width) + s) * 3);
					var pixel = ((((surface.Y + t) * BlockWidth) + surface.X + s) * 4);

					if ( hasSamples )
					{
						page[layer][pixel + 0] = _file.Lighting[sample + 0];
						page[layer][pixel + 1] = _file.Lighting[sample + 1];
						page[layer][pixel + 2] = _file.Lighting[sample + 2];
					}

					page[layer][pixel + 3] = style;
				}
			}
		}
	}

	private static byte[][] NewLightmapPage()
	{
		var page = new byte[GoldSrc.Bsp.Face.MaxLightmaps][];

		for ( var i = 0; i < page.Length; i++ )
		{
			page[i] = new byte[BlockWidth * BlockHeight * 4];

			for ( var pixel = 3; pixel < page[i].Length; pixel += 4 )
				page[i][pixel] = GoldSrc.Bsp.Face.NoStyle;
		}

		return page;
	}

	private const int FirstSwitchableLightStyle = 32;
	private const int LightStartOff = 1;

	private int InitialLightStyleValue( int style )
	{
		return _lightStylePatterns.TryGetValue( style, out var pattern ) && pattern.Length > 0 ? (pattern[0] - 'a') * 22 : 256;
	}

	private Texture CreateLightStyles()
	{
		var patterns = new Dictionary<int, string>( DefaultLightStyles );

		foreach ( var entity in _file.Entities.Where( x => x.ClassName is "light" or "light_spot" ) )
		{
			var style = (int)entity.FloatForKey( "style" );
			if ( style < FirstSwitchableLightStyle || style >= MaxLightStyles || entity.ValueForKey( "targetname" ).Length == 0 )
				continue;

			var pattern = entity.ValueForKey( "pattern" );

			if ( ((int)entity.FloatForKey( "spawnflags" ) & LightStartOff) != 0 )
				patterns[style] = "a";
			else
				patterns[style] = pattern.Length > 0 ? pattern : "m";
		}

		_lightStylePatterns = patterns;

		var data = new byte[MaxStyleString * MaxLightStyles * 4];

		foreach ( var (style, pattern) in patterns )
		{
			var length = Math.Min( pattern.Length, MaxStyleString );

			for ( var frame = 0; frame < length; frame++ )
			{
				var pixel = ((style * MaxStyleString) + frame) * 4;

				data[pixel + 0] = (byte)(pattern[frame] - 'a');
				data[pixel + 1] = (byte)length;
			}
		}

		return Texture.Create( MaxStyleString, MaxLightStyles, ImageFormat.RGBA8888 )
			.WithData( data )
			.Finish();
	}
}
