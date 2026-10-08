using System;

partial class BrushModel
{
	private const string SkyDirectory = "gfx/env";
	private const string DefaultSky = "desert";
	private const float DefaultMaxRange = 4096f;
	private const float SkyBoundsSize = 1048576f;
	private const byte DitherLimit = 0xfc;

	private static readonly string[] SkySides = ["rt", "bk", "lf", "ft", "up", "dn"];
	private static readonly int[] SkyTexOrder = [0, 2, 1, 3, 4, 5];

	private static readonly int[][] StToVec =
	[
		[3, -1, 2],
		[-3, 1, 2],
		[1, 3, 2],
		[-1, -3, 2],
		[-2, -1, 3],
		[2, -1, -3],
	];

	private static readonly Vector2[] SkyCorners = [new( -1, -1 ), new( -1, 1 ), new( 1, 1 ), new( 1, -1 )];

	private void AddSkyBox( ModelBuilder builder )
	{
		var worldspawn = _file.Entities[0];

		var name = worldspawn.ValueForKey( "skyname" );
		if ( name.Length == 0 )
			name = ConsoleSystem.GetValue( "sv_skyname", DefaultSky );

		if ( name.Length == 0 || FindSkySide( name, SkySides[0] ) is null )
			name = DefaultSky;

		var maxRange = worldspawn.FloatForKey( "MaxRange" );
		var width = (maxRange > 0f ? maxRange : DefaultMaxRange) * 0.57735f;

		for ( var axis = 0; axis < StToVec.Length; axis++ )
		{
			var side = SkySides[SkyTexOrder[axis]];
			if ( FindSkySide( name, side ) is not string file )
				continue;

			using var bitmap = Bitmap.CreateFromTgaBytes( System.IO.File.ReadAllBytes( file ) );

			var material = Material.Create( $"mount://{host.Ident}/{path}/{name}{side}.vmat", "goldsrc_sky", false );
			material.Set( "Color", CreateSkyTexture( bitmap ) );

			var vertices = SkyCorners.Select( x => MakeSkyVec( x.x, x.y, axis, width ) ).ToList();

			var mesh = CreateMesh( material, vertices, [0, 1, 2, 0, 2, 3] );
			mesh.Bounds = BBox.FromPositionAndSize( Vector3.Zero, SkyBoundsSize );

			builder.AddMesh( mesh );
		}
	}

	private static Texture CreateSkyTexture( Bitmap bitmap )
	{
		var pixels = bitmap.GetPixels32();
		var data = new byte[pixels.Length * 4];

		for ( var i = 0; i < pixels.Length; i++ )
		{
			data[(i * 4) + 0] = SkyGamma( pixels[i].r );
			data[(i * 4) + 1] = SkyGamma( pixels[i].g );
			data[(i * 4) + 2] = SkyGamma( pixels[i].b );
			data[(i * 4) + 3] = pixels[i].a;
		}

		return Texture.Create( bitmap.Width, bitmap.Height )
			.WithData( data )
			.Finish();
	}

	private static byte SkyGamma( byte value )
	{
		return value < DitherLimit ? GoldSrc.TextureUpload.TexGammaTable[GoldSrc.TextureUpload.Dither( value )] : value;
	}

	private static MapVertex MakeSkyVec( float s, float t, int axis, float width )
	{
		var b = new Vector3( s * width, t * width, width );
		var position = new Vector3();

		for ( var i = 0; i < 3; i++ )
		{
			var k = StToVec[axis][i];
			position[i] = k < 0 ? -b[-k - 1] : b[k - 1];
		}

		s = Math.Clamp( (s + 1f) * 0.5f, 1f / 512f, 511f / 512f );
		t = Math.Clamp( (t + 1f) * 0.5f, 1f / 512f, 511f / 512f );

		return new MapVertex { Position = position, TexCoord = new Vector2( s, 1f - t ) };
	}

	private string FindSkySide( string name, string side )
	{
		return host.FindFile( $"{SkyDirectory}/{name}{side}.tga" );
	}
}
