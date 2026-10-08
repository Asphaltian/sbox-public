using System;

partial class BrushModel
{
	private const float HullEpsilon = 0.01f;
	private const float HullBoundsPadding = 1f;
	private const float HullMergeDistance = 0.05f;
	private const int MinHullVertices = 4;

	private static readonly (char Type, string Surface)[] TextureSurfaces =
	[
		('M', "metal"),
		('V', "metal.sheet"),
		('G', "metal.sheet"),
		('P', "metal.sheet"),
		('D', "dirt"),
		('T', "ceramic"),
		('S', "water"),
		('W', "wood"),
		('Y', "glass")
	];

	private readonly record struct HalfSpace( Vector3 Normal, float Dist );

	private byte SurfaceIndex( in GoldSrc.Bsp.TexInfo texInfo )
	{
		var type = host.FindTextureType( GetTextureName( texInfo.MipTex ) );

		return (byte)(Array.FindIndex( TextureSurfaces, x => x.Type == type ) + 1);
	}

	private static void AddCollisionMesh( ModelBuilder builder, List<Vector3> vertices, List<int> indices, List<byte> surfaces )
	{
		builder.AddSurface( null );

		foreach ( var (_, surface) in TextureSurfaces )
			builder.AddSurface( Sandbox.Surface.FindByName( surface ) );

		builder.AddCollisionMesh( vertices, indices, surfaces );
	}

	private void AddCollisionHulls( ModelBuilder builder, GoldSrc.Bsp.Model bspModel )
	{
		var mins = bspModel.Mins - HullBoundsPadding;
		var maxs = bspModel.Maxs + HullBoundsPadding;

		var planes = new List<HalfSpace>
		{
			new( new Vector3( 1f, 0f, 0f ), mins.x ),
			new( new Vector3( -1f, 0f, 0f ), -maxs.x ),
			new( new Vector3( 0f, 1f, 0f ), mins.y ),
			new( new Vector3( 0f, -1f, 0f ), -maxs.y ),
			new( new Vector3( 0f, 0f, 1f ), mins.z ),
			new( new Vector3( 0f, 0f, -1f ), -maxs.z )
		};

		AddCollisionHulls( builder, bspModel.HeadNode[0], planes );
	}

	private void AddCollisionHulls( ModelBuilder builder, int nodeIndex, List<HalfSpace> planes )
	{
		if ( nodeIndex < 0 )
		{
			if ( _file.Leafs[~nodeIndex].Contents != GoldSrc.Bsp.Leaf.ContentsSolid )
				return;

			var vertices = HullVertices( planes );

			if ( vertices.Count >= MinHullVertices )
				builder.AddCollisionHull( vertices );

			return;
		}

		var node = _file.Nodes[nodeIndex];
		var plane = _file.Planes[node.PlaneNum];

		planes.Add( new HalfSpace( plane.Normal, plane.Dist ) );
		AddCollisionHulls( builder, node.Child0, planes );

		planes[^1] = new HalfSpace( -plane.Normal, -plane.Dist );
		AddCollisionHulls( builder, node.Child1, planes );

		planes.RemoveAt( planes.Count - 1 );
	}

	private static List<Vector3> HullVertices( List<HalfSpace> planes )
	{
		var vertices = new List<Vector3>();

		for ( var i = 0; i < planes.Count - 2; i++ )
		{
			for ( var j = i + 1; j < planes.Count - 1; j++ )
			{
				var cross = Vector3.Cross( planes[i].Normal, planes[j].Normal );

				for ( var k = j + 1; k < planes.Count; k++ )
				{
					var determinant = Vector3.Dot( cross, planes[k].Normal );

					if ( MathF.Abs( determinant ) < HullEpsilon )
						continue;

					var point = ((Vector3.Cross( planes[j].Normal, planes[k].Normal ) * planes[i].Dist)
						+ (Vector3.Cross( planes[k].Normal, planes[i].Normal ) * planes[j].Dist)
						+ (cross * planes[k].Dist)) / determinant;

					if ( IsInside( planes, point ) && !vertices.Exists( x => x.Distance( point ) < HullMergeDistance ) )
						vertices.Add( point );
				}
			}
		}

		return vertices;
	}

	private static bool IsInside( List<HalfSpace> planes, Vector3 point )
	{
		foreach ( var plane in planes )
		{
			if ( Vector3.Dot( plane.Normal, point ) - plane.Dist < -HullEpsilon )
				return false;
		}

		return true;
	}
}
