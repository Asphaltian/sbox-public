using System;

partial class BrushModel
{
	private const float SubdivideSize = 64f;

	private const float TurbAlwaysDrawn = -1f;

	private void AddTurbPolygon( List<MapVertex> vertices, List<int> indices, int subModel, in GoldSrc.Bsp.Face face, GoldSrc.Bsp.TexInfo texInfo )
	{
		var plane = _file.Planes[face.PlaneNum];
		var normal = face.Side == 0 ? plane.Normal : -plane.Normal;
		var test = new Vector2( plane.Dist, subModel == 0 ? TurbAlwaysDrawn : plane.Type == GoldSrc.Bsp.Plane.TypeZ ? 1f : 0f );

		var polygon = new List<Vector3>( face.NumEdges );
		for ( var i = 0; i < face.NumEdges; i++ )
			polygon.Add( _file.GetVertex( face, i ) );

		SubdividePolygon( polygon, emit =>
		{
			var first = vertices.Count;

			foreach ( var position in emit )
			{
				vertices.Add( new MapVertex
				{
					Position = position,
					Normal = normal,
					TexCoord = new Vector2( Vector3.Dot( position, texInfo.S ), Vector3.Dot( position, texInfo.T ) ),
					LightmapCoord = test
				} );
			}

			AddPolygonIndices( indices, first, emit.Count );
		} );
	}

	private static void SubdividePolygon( List<Vector3> polygon, Action<List<Vector3>> emit )
	{
		var bounds = BBox.FromPoints( polygon );

		for ( var axis = 0; axis < 3; axis++ )
		{
			var m = (bounds.Mins[axis] + bounds.Maxs[axis]) * 0.5f;
			m = SubdivideSize * MathF.Floor( (m / SubdivideSize) + 0.5f );

			if ( bounds.Maxs[axis] - m < 8f || m - bounds.Mins[axis] < 8f )
				continue;

			var front = new List<Vector3>();
			var back = new List<Vector3>();

			for ( var i = 0; i < polygon.Count; i++ )
			{
				var a = polygon[i];
				var b = polygon[(i + 1) % polygon.Count];
				var distA = a[axis] - m;
				var distB = b[axis] - m;

				if ( distA >= 0f )
					front.Add( a );

				if ( distA <= 0f )
					back.Add( a );

				if ( distA == 0f || distB == 0f || (distA > 0f) == (distB > 0f) )
					continue;

				var split = a + ((b - a) * (distA / (distA - distB)));
				front.Add( split );
				back.Add( split );
			}

			SubdividePolygon( front, emit );
			SubdividePolygon( back, emit );
			return;
		}

		emit( polygon );
	}
}
