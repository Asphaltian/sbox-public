using System;
using System.Runtime.InteropServices;

class BrushModelLoader( BrushModel brushModel, int subModel ) : ResourceLoader<GameMount>
{
	protected override object Load() => brushModel.CreateSubModel( subModel, Path );
}

partial class BrushModel( GameMount host, string fullPath, string path, bool blueShift )
{
	private static readonly Sandbox.Diagnostics.Logger Log = new( "BrushModel" );

	public GoldSrc.Bsp.File File
	{
		get
		{
			Load();
			return _file;
		}
	}

	private GoldSrc.Bsp.File _file;
	private GoldSrc.MipTex[] _mipTextures;

	private readonly Dictionary<int, Texture> _textures = [];
	private readonly Dictionary<(string Frames, bool Turb, bool Tiled), Material> _materials = [];
	private readonly Dictionary<int, int[]> _frames = [];
	private readonly Dictionary<int, int[]> _alternateFrames = [];

	private static readonly int[] RandomTiling = [.. Enumerable.Range( 0, 20 * 20 ).Select( _ => Random.Shared.Next( 0x8000 ) )];

	[Flags]
	private enum SurfaceFlags
	{
		DrawSky = 4,
		DrawTurb = 0x10,
		DrawTiled = 0x20
	}

	private void Load()
	{
		if ( _file is not null )
			return;

		_file = new GoldSrc.Bsp.File( System.IO.File.ReadAllBytes( fullPath ), blueShift );

		var wadFiles = _file.Entities[0].ValueForKey( "wad" )
			.Split( ';', StringSplitOptions.RemoveEmptyEntries )
			.Select( x => Path.GetFileName( x.Replace( '\\', '/' ) ) )
			.ToArray();

		_mipTextures = [.. _file.Textures.Select( x => x is null || x.HasPixels ? x : FindTexture( x.Name, wadFiles ) )];

		FindTextureFrames();
		BuildLightmaps();
		BuildLightVolume();
	}

	private GoldSrc.MipTex FindTexture( string name, string[] wadFiles )
	{
		var mipTex = host.FindTexture( name, wadFiles );
		if ( mipTex is null )
			Log.Warning( $"{path}: texture '{name}' not found" );

		return mipTex;
	}

	public Model CreateSubModel( int index, string name )
	{
		var bspModel = File.Models[index];
		var meshes = new Dictionary<(string Frames, string AlternateFrames, bool Tiled), (int[] Frames, int[] AlternateFrames, List<MapVertex> Vertices, List<int> Indices)>();
		var turbMeshes = new Dictionary<int, (List<MapVertex> Vertices, List<int> Indices)>();
		var hasSky = false;
		var collisionVertices = new List<Vector3>();
		var collisionIndices = new List<int>();

		for ( var i = 0; i < bspModel.NumFaces; i++ )
		{
			var faceIndex = bspModel.FirstFace + i;
			var face = _file.Faces[faceIndex];
			var texInfo = _file.TexInfos[face.TexInfo];
			var flags = GetSurfaceFlags( face );

			if ( flags.HasFlag( SurfaceFlags.DrawTurb ) )
			{
				if ( !turbMeshes.TryGetValue( texInfo.MipTex, out var turbMesh ) )
					turbMeshes[texInfo.MipTex] = turbMesh = ([], []);

				AddTurbPolygon( turbMesh.Vertices, turbMesh.Indices, index, face, texInfo );
				continue;
			}

			AddPolygon( collisionVertices, collisionIndices, face );

			if ( flags.HasFlag( SurfaceFlags.DrawSky ) )
			{
				hasSky = true;
				continue;
			}

			var surface = _surfaces[faceIndex];
			var (frames, alternateFrames) = GetTextureFrames( texInfo.MipTex, surface );
			var key = (string.Join( ',', frames ), string.Join( ',', alternateFrames ), flags.HasFlag( SurfaceFlags.DrawTiled ));
			if ( !meshes.TryGetValue( key, out var mesh ) )
				meshes[key] = mesh = (frames, alternateFrames, [], []);

			AddPolygon( mesh.Vertices, mesh.Indices, face, texInfo, surface );
		}

		var builder = Model.Builder.WithName( name );

		foreach ( var (key, (frames, alternateFrames, vertices, indices)) in meshes )
			builder.AddMesh( CreateMesh( GetMaterial( frames, alternateFrames, key.Tiled ), vertices, indices ) );

		foreach ( var (texture, (vertices, indices)) in turbMeshes )
			builder.AddMesh( CreateMesh( GetTurbMaterial( texture ), vertices, indices ) );

		if ( hasSky )
			AddSkyBox( builder );

		if ( collisionIndices.Count > 0 )
		{
			if ( index == 0 )
				builder.AddCollisionMesh( collisionVertices, collisionIndices );
			else
				AddCollisionHulls( builder, bspModel );

			builder.AddTraceMesh( collisionVertices, collisionIndices );
		}

		return builder.Create();
	}

	private static Mesh CreateMesh( Material material, List<MapVertex> vertices, List<int> indices )
	{
		var mesh = new Mesh( material );
		mesh.CreateVertexBuffer( vertices.Count, vertices );
		mesh.CreateIndexBuffer( indices.Count, indices );
		mesh.Bounds = BBox.FromPoints( vertices.Select( x => x.Position ) );

		return mesh;
	}

	private SurfaceFlags GetSurfaceFlags( in GoldSrc.Bsp.Face face )
	{
		var texInfo = _file.TexInfos[face.TexInfo];
		var name = _mipTextures[texInfo.MipTex]?.Name ?? NoTextureName;

		if ( name.StartsWith( "sky", StringComparison.Ordinal ) )
			return SurfaceFlags.DrawSky | SurfaceFlags.DrawTiled;

		if ( name.StartsWith( "scroll", StringComparison.Ordinal ) )
			return SurfaceFlags.DrawTiled;

		if ( name.StartsWith( '!' )
			|| name.StartsWith( "laser", StringComparison.OrdinalIgnoreCase )
			|| name.StartsWith( "water", StringComparison.OrdinalIgnoreCase ) )
			return SurfaceFlags.DrawTurb;

		return IsSpecial( texInfo ) ? SurfaceFlags.DrawTiled : 0;
	}

	private bool IsSpecial( in GoldSrc.Bsp.TexInfo texInfo )
	{
		return _mipTextures[texInfo.MipTex] is not null && texInfo.Flags.HasFlag( GoldSrc.Bsp.TexInfoFlags.Special );
	}

	private bool HasLightmap( in GoldSrc.Bsp.Face face )
	{
		var flags = GetSurfaceFlags( face );
		if ( flags.HasFlag( SurfaceFlags.DrawSky ) || flags.HasFlag( SurfaceFlags.DrawTurb ) )
			return false;

		return !flags.HasFlag( SurfaceFlags.DrawTiled ) || !IsSpecial( _file.TexInfos[face.TexInfo] );
	}

	private void AddPolygon( List<Vector3> vertices, List<int> indices, in GoldSrc.Bsp.Face face )
	{
		var first = vertices.Count;

		for ( var i = 0; i < face.NumEdges; i++ )
			vertices.Add( _file.GetVertex( face, i ) );

		AddPolygonIndices( indices, first, face.NumEdges );
	}

	private void AddPolygon( List<MapVertex> vertices, List<int> indices, in GoldSrc.Bsp.Face face, in GoldSrc.Bsp.TexInfo texInfo, in Surface surface )
	{
		var first = vertices.Count;
		var plane = _file.Planes[face.PlaneNum];
		var normal = face.Side == 0 ? plane.Normal : -plane.Normal;
		var mipTex = _mipTextures[texInfo.MipTex];
		var textureSize = mipTex is null ? new Vector2( NoTextureSize ) : new Vector2( mipTex.Width, mipTex.Height );

		for ( var i = 0; i < face.NumEdges; i++ )
		{
			var position = _file.GetVertex( face, i );
			var texel = texInfo.Project( position );

			vertices.Add( new MapVertex
			{
				Position = position,
				Normal = normal,
				TexCoord = texel / textureSize,
				LightmapCoord = surface.GetLightmapCoord( texel ),
				LightmapBlock = surface.LightmapBlock
			} );
		}

		AddPolygonIndices( indices, first, face.NumEdges );
	}

	private static void AddPolygonIndices( List<int> indices, int first, int count )
	{
		for ( var i = 1; i < count - 1; i++ )
		{
			indices.Add( first );
			indices.Add( first + i + 1 );
			indices.Add( first + i );
		}
	}

	private Material GetMaterial( int[] frames, int[] alternateFrames, bool tiled )
	{
		var textureIndex = frames[0];
		var name = GetTextureName( textureIndex );

		_frames.TryGetValue( textureIndex, out var chain );
		_alternateFrames.TryGetValue( textureIndex, out var alternateChain );

		if ( (chain is not null && frames != chain) || (alternateChain is not null && alternateFrames != alternateChain) )
			name = $"{name}_{string.Join( '-', frames )}_{string.Join( '-', alternateFrames )}";

		var key = (name, false, tiled);
		if ( _materials.TryGetValue( key, out var material ) )
			return material;

		material = Material.Create( $"mount://{host.Ident}/{path}/{name}_{(tiled ? 1 : 0)}.vmat", "goldsrc_lightmapped", false );
		material.Set( "LightStyles", _lightStyles );
		material.Set( "g_flDrawTiled", tiled ? 1f : 0f );

		for ( var i = 0; i < _lightmaps.Length; i++ )
			material.Set( $"Lightmap{i}", _lightmaps[i] );

		if ( frames.Length > 1 || alternateFrames.Length > 0 )
		{
			material.SetFeature( "F_ANIMATED", 1 );
			material.Set( "g_flFrameCount", (float)frames.Length );
			material.Set( "g_flAlternateFrameCount", (float)alternateFrames.Length );

			for ( var i = 0; i < alternateFrames.Length; i++ )
				material.Set( $"AlternateColor{i}", GetTexture( alternateFrames[i] ) );
		}

		for ( var i = 0; i < frames.Length; i++ )
			material.Set( $"Color{i}", GetTexture( frames[i] ) );

		return _materials[key] = material;
	}

	private Material GetTurbMaterial( int textureIndex )
	{
		var key = (GetTextureName( textureIndex ), true, false);
		if ( _materials.TryGetValue( key, out var material ) )
			return material;

		material = Material.Create( $"mount://{host.Ident}/{path}/{GetTextureName( textureIndex )}.vmat", "goldsrc_turb", false );
		material.Set( "Color", GetTexture( textureIndex ) );

		return _materials[key] = material;
	}

	private string GetTextureName( int index ) => _mipTextures[index]?.Name ?? NoTextureName;

	private int[] TextureAnimation( int baseTexture, int[] frames, in Surface surface )
	{
		var mipTex = _mipTextures[baseTexture];
		if ( !mipTex.Name.StartsWith( '-' ) )
			return frames;

		var s = (uint)((int)surface.TextureMins.x + (mipTex.Width << 16)) / (uint)mipTex.Width;
		var t = (uint)((int)surface.TextureMins.y + (mipTex.Height << 16)) / (uint)mipTex.Height;

		return [frames[RandomTiling[(t % 20) + ((s % 20) * 20)] % frames.Length]];
	}

	private (int[] Frames, int[] AlternateFrames) GetTextureFrames( int textureIndex, in Surface surface )
	{
		if ( !_frames.TryGetValue( textureIndex, out var frames ) )
			return ([textureIndex], []);

		return (TextureAnimation( textureIndex, frames, surface ), _alternateFrames.TryGetValue( textureIndex, out var alternateFrames )
			? TextureAnimation( alternateFrames[0], alternateFrames, surface )
			: []);
	}

	private const int MaxAnimationFrames = 10;

	private void FindTextureFrames()
	{
		var groups = new Dictionary<string, (int[] Frames, int[] AlternateFrames)>();
		var counts = new Dictionary<string, (int Max, int AlternateMax)>();

		for ( var i = 0; i < _mipTextures.Length; i++ )
		{
			var name = _mipTextures[i]?.Name;
			if ( string.IsNullOrEmpty( name ) || (name[0] != '+' && name[0] != '-') )
				continue;

			var frame = name.Length > 1 ? name[1] : (char)0;
			if ( frame is >= 'a' and <= 'z' )
				frame = (char)(frame - ('a' - 'A'));

			var group = name.Length > 2 ? name[2..] : "";
			if ( !groups.TryGetValue( group, out var frames ) )
			{
				groups[group] = frames = (new int[MaxAnimationFrames], new int[MaxAnimationFrames]);

				Array.Fill( frames.Frames, -1 );
				Array.Fill( frames.AlternateFrames, -1 );
			}

			counts.TryGetValue( group, out var count );

			if ( frame is >= '0' and <= '9' )
			{
				frames.Frames[frame - '0'] = i;
				count.Max = Math.Max( count.Max, frame - '0' + 1 );
			}
			else if ( frame is >= 'A' and <= 'J' )
			{
				frames.AlternateFrames[frame - 'A'] = i;
				count.AlternateMax = Math.Max( count.AlternateMax, frame - 'A' + 1 );
			}
			else
			{
				throw new InvalidDataException( $"Bad animating texture {name}" );
			}

			counts[group] = count;
		}

		foreach ( var (group, (frames, alternateFrames)) in groups )
		{
			var (max, alternateMax) = counts[group];
			var chain = frames[..max];
			var alternateChain = alternateFrames[..alternateMax];

			for ( var i = 0; i < max; i++ )
			{
				if ( chain[i] < 0 )
					throw new InvalidDataException( $"Missing frame {i} of {group}" );
			}

			for ( var i = 0; i < alternateMax; i++ )
			{
				if ( alternateChain[i] < 0 )
					throw new InvalidDataException( $"Missing frame {i} of {group}" );
			}

			foreach ( var texture in chain )
			{
				_frames[texture] = chain;

				if ( alternateMax != 0 )
					_alternateFrames[texture] = alternateChain;
			}

			foreach ( var texture in alternateChain )
			{
				_frames[texture] = alternateChain;

				if ( max != 0 )
					_alternateFrames[texture] = chain;
			}
		}
	}

	private const string NoTextureName = "notexture";
	private const int NoTextureSize = 16;

	private static readonly Color32 NoTextureColor = new( 255, 0, 255 );

	private Texture GetTexture( int index )
	{
		if ( _textures.TryGetValue( index, out var texture ) )
			return texture;

		var mipTex = _mipTextures[index];
		var pixels = mipTex?.GetPixels() ?? GetNoTexturePixels();
		var size = mipTex is null ? new Vector2Int( NoTextureSize ) : new Vector2Int( mipTex.Width, mipTex.Height );

		GoldSrc.TextureUpload.ApplyGamma( pixels, size.x, size.y, mipTex is not null && mipTex.IsTransparent ? GoldSrc.TextureType.Alpha : GoldSrc.TextureType.Opaque );

		return _textures[index] = GoldSrc.TextureUpload.CreateTexture( pixels, size.x, size.y );
	}

	private static byte[] GetNoTexturePixels()
	{
		var pixels = new byte[NoTextureSize * NoTextureSize * 4];
		var half = NoTextureSize / 2;

		for ( var y = 0; y < NoTextureSize; y++ )
		{
			for ( var x = 0; x < NoTextureSize; x++ )
			{
				var pixel = ((y * NoTextureSize) + x) * 4;
				var color = (x < half) != (y < half) ? Color32.Black : NoTextureColor;

				pixels[pixel + 0] = color.r;
				pixels[pixel + 1] = color.g;
				pixels[pixel + 2] = color.b;
				pixels[pixel + 3] = 255;
			}
		}

		return pixels;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct MapVertex
	{
		[VertexLayout.Position]
		public Vector3 Position;

		[VertexLayout.Normal]
		public Vector3 Normal;

		[VertexLayout.TexCoord( 0 )]
		public Vector2 TexCoord;

		[VertexLayout.TexCoord( 1 )]
		public Vector2 LightmapCoord;

		[VertexLayout.Color]
		public Color32 LightmapBlock;
	}
}
