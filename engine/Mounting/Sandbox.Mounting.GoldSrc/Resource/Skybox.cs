using System;

class SkyboxSceneLoader : SceneLoader<GameMount>
{
	protected override void BuildScene()
	{
		var sky = new GameObject( true, "sky" );

		sky.AddComponent<ModelRenderer>().Model = Model.Load( System.IO.Path.ChangeExtension( Path, ".vmdl" ) );
	}
}

class SkyboxLoader( GameMount host, string name ) : ResourceLoader<GameMount>
{
	public const string Directory = "gfx/env";
	public const string FirstSide = "rt";

	private const float Width = 4096f * 0.57735f;
	private const float BoundsSize = 1048576f;
	private const byte DitherLimit = 0xfc;

	private static readonly string[] Sides = [FirstSide, "bk", "lf", "ft", "up", "dn"];
	private static readonly int[] TexOrder = [0, 2, 1, 3, 4, 5];

	private static readonly int[][] StToVec =
	[
		[3, -1, 2],
		[-3, 1, 2],
		[1, 3, 2],
		[-1, -3, 2],
		[-2, -1, 3],
		[2, -1, -3],
	];

	private static readonly Vector2[] Corners = [new( -1, -1 ), new( -1, 1 ), new( 1, 1 ), new( 1, -1 )];

	public static bool IsFirstSide( string path )
	{
		return path.EndsWith( $"{FirstSide}.tga", StringComparison.OrdinalIgnoreCase )
			&& path[..(path.LastIndexOf( '/' ) + 1)].EndsWith( $"{Directory}/", StringComparison.OrdinalIgnoreCase );
	}

	protected override object Load()
	{
		var builder = Model.Builder.WithName( Path );

		for ( var axis = 0; axis < StToVec.Length; axis++ )
		{
			var side = Sides[TexOrder[axis]];
			if ( host.FindFile( $"{Directory}/{name}{side}.tga" ) is not string file )
				continue;

			var (pixels, width, height) = TgaTextureLoader.Decode( System.IO.File.ReadAllBytes( file ) );
			if ( pixels is null )
				continue;

			for ( var i = 0; i < pixels.Length; i += 4 )
			{
				pixels[i + 0] = SkyGamma( pixels[i + 0] );
				pixels[i + 1] = SkyGamma( pixels[i + 1] );
				pixels[i + 2] = SkyGamma( pixels[i + 2] );
			}

			var material = Material.Create( $"{Path}/{side}.vmat", "goldsrc_sky", false );
			material.Set( "Color", Texture.Create( width, height ).WithData( pixels ).Finish() );

			var mesh = BrushModel.CreateMesh( material, [.. Corners.Select( x => MakeSkyVec( x.x, x.y, axis ) )], [0, 1, 2, 0, 2, 3] );
			mesh.Bounds = BBox.FromPositionAndSize( Vector3.Zero, BoundsSize );

			builder.AddMesh( mesh );
		}

		return builder.Create();
	}

	private static byte SkyGamma( byte value )
	{
		return value < DitherLimit ? GoldSrc.TextureUpload.TexGammaTable[GoldSrc.TextureUpload.Dither( value )] : value;
	}

	private static BrushModel.MapVertex MakeSkyVec( float s, float t, int axis )
	{
		var b = new Vector3( s * Width, t * Width, Width );
		var position = new Vector3();

		for ( var i = 0; i < 3; i++ )
		{
			var k = StToVec[axis][i];
			position[i] = k < 0 ? -b[-k - 1] : b[k - 1];
		}

		s = Math.Clamp( (s + 1f) * 0.5f, 1f / 512f, 511f / 512f );
		t = Math.Clamp( (t + 1f) * 0.5f, 1f / 512f, 511f / 512f );

		return new BrushModel.MapVertex { Position = position, TexCoord = new Vector2( s, 1f - t ) };
	}
}
