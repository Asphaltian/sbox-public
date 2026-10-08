using System;
using System.Text;

namespace GoldSrc.Bsp;

class File
{
	public const int Version = 30;
	public const int QuakeVersion = 29;

	public List<Entity> Entities { get; }
	public Plane[] Planes { get; }
	public Vector3[] Vertexes { get; }
	public TexInfo[] TexInfos { get; }
	public Face[] Faces { get; }
	public Node[] Nodes { get; }
	public Leaf[] Leafs { get; }
	public Edge[] Edges { get; }
	public int[] SurfEdges { get; }
	public Model[] Models { get; }

	public MipTex[] Textures { get; }

	public byte[] Lighting { get; }

	private readonly byte[] _data;
	private readonly Lump[] _lumps = new Lump[(int)LumpType.Count];

	public static int ReadNumModels( string path )
	{
		using var reader = new BinaryReader( System.IO.File.OpenRead( path ) );

		if ( reader.BaseStream.Length < sizeof( int ) + ((int)LumpType.Count * 8) || !IsSupported( reader.ReadInt32() ) )
			return 0;

		reader.BaseStream.Seek( (int)LumpType.Models * 8, SeekOrigin.Current );
		return new Lump( reader ).FileLength / Model.Size;
	}

	private static bool IsSupported( int version ) => version is Version or QuakeVersion;

	public File( byte[] data, bool blueShift )
	{
		_data = data;

		using var reader = new BinaryReader( new MemoryStream( data ) );

		var version = reader.ReadInt32();
		if ( !IsSupported( version ) )
			throw new InvalidDataException( $"BSP has wrong version number ({version} should be {Version})" );

		for ( var i = 0; i < _lumps.Length; i++ )
			_lumps[i] = new Lump( reader );

		if ( blueShift )
			(_lumps[(int)LumpType.Entities], _lumps[(int)LumpType.Planes]) = (_lumps[(int)LumpType.Planes], _lumps[(int)LumpType.Entities]);

		Planes = ReadLump( reader, LumpType.Planes, Plane.Size, r => new Plane( r ) );
		Vertexes = ReadLump( reader, LumpType.Vertexes, 12, r => new Vector3( r.ReadSingle(), r.ReadSingle(), r.ReadSingle() ) );
		TexInfos = ReadLump( reader, LumpType.TexInfo, TexInfo.Size, r => new TexInfo( r ) );
		Faces = ReadLump( reader, LumpType.Faces, Face.Size, r => new Face( r ) );
		Nodes = ReadLump( reader, LumpType.Nodes, Node.Size, r => new Node( r ) );
		Leafs = ReadLump( reader, LumpType.Leafs, Leaf.Size, r => new Leaf( r ) );
		Edges = ReadLump( reader, LumpType.Edges, Edge.Size, r => new Edge( r ) );
		SurfEdges = ReadLump( reader, LumpType.SurfEdges, sizeof( int ), r => r.ReadInt32() );
		Models = ReadLump( reader, LumpType.Models, Model.Size, r => new Model( r ) );

		Lighting = GetLump( LumpType.Lighting ).ToArray();
		Textures = ReadTextures();
		Entities = Entity.ParseEntities( Encoding.ASCII.GetString( GetLump( LumpType.Entities ) ).TrimEnd( '\0' ) );
	}

	public Vector3 GetVertex( in Face face, int index )
	{
		var surfEdge = SurfEdges[face.FirstEdge + index];
		return Vertexes[surfEdge >= 0 ? Edges[surfEdge].V0 : Edges[-surfEdge].V1];
	}

	private ReadOnlySpan<byte> GetLump( LumpType type )
	{
		var lump = _lumps[(int)type];
		if ( lump.FileOffset < 0 || lump.FileLength < 0 || lump.FileOffset + lump.FileLength > _data.Length )
			throw new InvalidDataException( $"BSP {type} lump is out of bounds" );

		return _data.AsSpan( lump.FileOffset, lump.FileLength );
	}

	private T[] ReadLump<T>( BinaryReader reader, LumpType type, int size, Func<BinaryReader, T> read )
	{
		var length = GetLump( type ).Length;
		if ( length % size != 0 )
			throw new InvalidDataException( $"BSP {type} lump has an odd size" );

		reader.BaseStream.Seek( _lumps[(int)type].FileOffset, SeekOrigin.Begin );

		var items = new T[length / size];
		for ( var i = 0; i < items.Length; i++ )
			items[i] = read( reader );

		return items;
	}

	private MipTex[] ReadTextures()
	{
		var lump = GetLump( LumpType.Textures );
		if ( lump.IsEmpty )
			return [];

		var textures = new MipTex[BitConverter.ToInt32( lump )];
		for ( var i = 0; i < textures.Length; i++ )
		{
			var offset = BitConverter.ToInt32( lump[(sizeof( int ) * (i + 1))..] );
			if ( offset >= 0 )
				textures[i] = new MipTex( lump[offset..] );
		}

		return textures;
	}
}
