namespace GoldSrc.Bsp;

enum LumpType
{
	Entities,
	Planes,
	Textures,
	Vertexes,
	Visibility,
	Nodes,
	TexInfo,
	Faces,
	Lighting,
	ClipNodes,
	Leafs,
	MarkSurfaces,
	Edges,
	SurfEdges,
	Models,

	Count
}

readonly struct Lump( BinaryReader reader )
{
	public readonly int FileOffset = reader.ReadInt32();
	public readonly int FileLength = reader.ReadInt32();
}

readonly struct Plane( BinaryReader reader )
{
	public const int Size = 20;
	public const int TypeZ = 2;

	public readonly Vector3 Normal = new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	public readonly float Dist = reader.ReadSingle();
	public readonly int Type = reader.ReadInt32();
}

readonly struct TexInfo( BinaryReader reader )
{
	public const int Size = 40;

	public readonly Vector3 S = new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	public readonly float OffsetS = reader.ReadSingle();
	public readonly Vector3 T = new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	public readonly float OffsetT = reader.ReadSingle();
	public readonly int MipTex = reader.ReadInt32();
	public readonly TexInfoFlags Flags = (TexInfoFlags)reader.ReadInt32();

	public Vector2 Project( Vector3 point )
	{
		return new Vector2( Vector3.Dot( point, S ) + OffsetS, Vector3.Dot( point, T ) + OffsetT );
	}

	public Vector2 Extent( Vector3 point )
	{
		return new Vector2(
			(float)(((double)point.y * S.y) + ((double)point.x * S.x) + ((double)point.z * S.z) + OffsetS),
			(float)(((double)point.y * T.y) + ((double)point.x * T.x) + ((double)point.z * T.z) + OffsetT) );
	}
}

[System.Flags]
enum TexInfoFlags
{
	Special = 1
}

readonly struct Node( BinaryReader reader )
{
	public const int Size = 24;

	public readonly int PlaneNum = reader.ReadInt32();
	public readonly short Child0 = reader.ReadInt16();
	public readonly short Child1 = reader.ReadInt16();
	public readonly short[] Bounds = [reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()];
	public readonly ushort FirstFace = reader.ReadUInt16();
	public readonly ushort NumFaces = reader.ReadUInt16();
}

readonly struct Leaf( BinaryReader reader )
{
	public const int Size = 28;
	public const int ContentsSolid = -2;

	public readonly int Contents = reader.ReadInt32();
	public readonly byte[] Remainder = reader.ReadBytes( Size - sizeof( int ) );
}

readonly struct Edge( BinaryReader reader )
{
	public const int Size = 4;

	public readonly ushort V0 = reader.ReadUInt16();
	public readonly ushort V1 = reader.ReadUInt16();
}

readonly struct Face( BinaryReader reader )
{
	public const int Size = 20;
	public const int MaxLightmaps = 4;

	public const byte NoStyle = 255;

	public readonly short PlaneNum = reader.ReadInt16();
	public readonly short Side = reader.ReadInt16();
	public readonly int FirstEdge = reader.ReadInt32();
	public readonly short NumEdges = reader.ReadInt16();
	public readonly short TexInfo = reader.ReadInt16();
	public readonly byte[] Styles = reader.ReadBytes( MaxLightmaps );

	public readonly int LightOffset = reader.ReadInt32();
}

readonly struct Model( BinaryReader reader )
{
	public const int Size = 64;
	public const int MaxHulls = 4;

	public readonly Vector3 Mins = new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	public readonly Vector3 Maxs = new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	public readonly Vector3 Origin = new( reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle() );
	public readonly int[] HeadNode = [reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()];
	public readonly int VisLeafs = reader.ReadInt32();
	public readonly int FirstFace = reader.ReadInt32();
	public readonly int NumFaces = reader.ReadInt32();
}
