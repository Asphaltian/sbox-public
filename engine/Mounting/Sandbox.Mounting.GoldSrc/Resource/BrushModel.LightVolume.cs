using System;

partial class BrushModel
{
	private const int IrradianceResolution = 8;
	private const int DistanceResolution = 16;
	private const int IrradianceChannels = 4;
	private const int DistanceChannels = 2;
	private const int RelocationChannels = 4;
	private const int MinProbes = 4;
	private const int MaxProbes = 128;
	private const int ProbeDensity = 8;
	private const int BakeVersion = 1;
	private const float BoundsMargin = 16f;
	private const int ProbeRays = 1024;
	private const float DistanceExponent = 50f;
	private const float MaxProbeValue = 65504f;
	private const int RelocationRays = 128;
	private const int RelocationSteps = 12;
	private const float RelocationBackfaceScale = 0.999f;
	private const float MinFrontfaceDistanceFactor = 0.2f;
	private const float MaxOffsetFactor = 0.45f;
	private const int ProbeChannels = 4;
	private const int ProbesPerRow = 1024;
	private const float LightTraceOffset = 8f;
	private const float LightTraceDistance = 2048f;
	private const float SkyTraceDistance = 8192f;
	private const int MaxLight = 255;

	private static readonly Vector3[] RelocationDirections = [.. Enumerable.Range( 0, RelocationRays ).Select( RelocationDirection )];
	private static readonly Vector3[] ProbeDirections = [.. Enumerable.Range( 0, ProbeRays ).Select( FibonacciDirection )];
	private static readonly (int Ray, float Weight)[][] DistanceWeights = BuildDistanceWeights();

	private BBox _lightVolumeBounds;
	private Texture _irradiance;
	private Texture _distance;
	private Texture _relocation;
	private Texture _irradianceProbes;

	private readonly record struct LightSample( int Face, int Offset );

	private static Vector3 RelocationDirection( int index )
	{
		var goldenRatio = (1f + MathF.Sqrt( 5f )) / 2f;
		var inclination = MathF.Acos( 1f - (2f * ((float)index / RelocationRays)) );
		var azimuth = MathF.PI * 2f * goldenRatio * index;

		return new Vector3( MathF.Sin( inclination ) * MathF.Cos( azimuth ), MathF.Sin( inclination ) * MathF.Sin( azimuth ), MathF.Cos( inclination ) );
	}

	private static Vector3 FibonacciDirection( int index )
	{
		const float goldenRatio = 1.6180339887498949f;

		var i = index + 0.5f;
		var phi = 2f * MathF.PI * goldenRatio * i;
		var cosTheta = 1f - (2f * (i / ProbeRays));
		var sinTheta = MathF.Sqrt( Math.Clamp( 1f - (cosTheta * cosTheta), 0f, 1f ) );

		return new Vector3( MathF.Cos( phi ) * sinTheta, MathF.Sin( phi ) * sinTheta, cosTheta );
	}

	private static Vector3 OctahedralDecode( int texel, int resolution )
	{
		var x = ((((texel % resolution) + 0.5f) / resolution) * 2f) - 1f;
		var y = ((((texel / resolution) + 0.5f) / resolution) * 2f) - 1f;
		var z = 1f - MathF.Abs( x ) - MathF.Abs( y );

		if ( z < 0f )
			(x, y) = ((1f - MathF.Abs( y )) * (x >= 0f ? 1f : -1f), (1f - MathF.Abs( x )) * (y >= 0f ? 1f : -1f));

		return new Vector3( x, y, z ).Normal;
	}

	private static (int Ray, float Weight)[][] BuildDistanceWeights()
	{
		var weights = new (int Ray, float Weight)[DistanceResolution * DistanceResolution][];

		for ( var texel = 0; texel < weights.Length; texel++ )
		{
			var direction = -OctahedralDecode( texel, DistanceResolution );
			var lobe = new List<(int Ray, float Weight)>();

			for ( var ray = 0; ray < ProbeRays; ray++ )
			{
				var weight = MathF.Pow( MathF.Max( Vector3.Dot( direction, ProbeDirections[ray] ), 0f ), DistanceExponent );

				if ( weight > 0f )
					lobe.Add( (ray, weight) );
			}

			var total = lobe.Sum( x => x.Weight );

			weights[texel] = [.. lobe.Select( x => (x.Ray, x.Weight / total) )];
		}

		return weights;
	}

	public void CreateLightVolume( GameObject parent )
	{
		Load();

		if ( _relocation is null )
			return;

		var volume = parent.AddComponent<IndirectLightVolume>();

		volume.Bounds = _lightVolumeBounds;
		volume.ProbeDensity = ProbeDensity;
		volume.BakeVersion = BakeVersion;
		volume.IrradianceTexture = _irradiance;
		volume.DistanceTexture = _distance;
		volume.RelocationTexture = _relocation;
	}

	private Texture CreateLightVolumeTexture( string name, ImageFormat format, Half[] data, int width, int height, int depth )
	{
		return Texture.CreateVolume( width, height, depth, format )
			.WithName( $"mount://{host.Ident}/{path}/{name}.vtex" )
			.WithData( System.Runtime.InteropServices.MemoryMarshal.AsBytes( data.AsSpan() ).ToArray() )
			.Finish();
	}

	private static int ProbeCount( float size )
	{
		return Math.Clamp( (int)MathF.Ceiling( size * (ProbeDensity / 1024f) ) + 1, MinProbes, MaxProbes );
	}

	private void BuildLightVolume()
	{
		if ( _file.Nodes.Length == 0 || _file.Lighting.Length == 0 )
			return;

		var bounds = new BBox( _file.Models[0].Mins, _file.Models[0].Maxs ).Grow( BoundsMargin );
		var counts = new Vector3Int( ProbeCount( bounds.Size.x ), ProbeCount( bounds.Size.y ), ProbeCount( bounds.Size.z ) );
		var spacing = bounds.Size / new Vector3( counts.x - 1, counts.y - 1, counts.z - 1 );

		var irradiance = new Half[counts.x * IrradianceResolution * counts.y * IrradianceResolution * counts.z * IrradianceChannels];
		var distance = new Half[counts.x * DistanceResolution * counts.y * DistanceResolution * counts.z * DistanceChannels];
		var relocation = new Half[counts.x * counts.y * counts.z * RelocationChannels];

		var probes = new float[counts.x * counts.y * counts.z][];
		var (skyColor, skyVector) = SkyLight();

		System.Threading.Tasks.Parallel.For( 0, counts.x * counts.y * counts.z, probe =>
		{
			var x = probe % counts.x;
			var y = probe / counts.x % counts.y;
			var z = probe / (counts.x * counts.y);

			var position = bounds.Mins + (new Vector3( x, y, z ) * spacing);
			var offset = RelocateProbe( position, spacing );
			var origin = position + offset;
			var reach = MathF.Max( 1f, spacing.Length + offset.Length );

			relocation[(probe * RelocationChannels) + 0] = (Half)offset.x;
			relocation[(probe * RelocationChannels) + 1] = (Half)offset.y;
			relocation[(probe * RelocationChannels) + 2] = (Half)offset.z;
			relocation[(probe * RelocationChannels) + 3] = (Half)(PointContents( origin ) == GoldSrc.Bsp.Leaf.ContentsSolid ? 0f : 1f);

			Span<Vector2> moments = stackalloc Vector2[DistanceResolution * DistanceResolution];
			Span<Vector2> depth = stackalloc Vector2[ProbeRays];

			var light = ProbeLight( origin, skyColor, skyVector, out var sample );

			WriteIrradianceTile( irradiance, GammaToLinear( light ), counts, x, y, z );

			for ( var ray = 0; ray < ProbeRays; ray++ )
			{
				var hit = TraceSurface( _file.Models[0].HeadNode[0], origin, origin + (ProbeDirections[ray] * reach), ProbeDirections[ray] );
				var length = hit.Face < 0 ? 1f : MathF.Min( (hit.Point - origin).Length / reach, 1f );

				depth[ray] = new Vector2( length, length * length );
			}

			for ( var texel = 0; texel < moments.Length; texel++ )
			{
				foreach ( var (ray, weight) in DistanceWeights[texel] )
					moments[texel] += depth[ray] * weight;
			}

			WriteDistanceTile( distance, moments, counts, x, y, z );

			if ( sample.Face >= 0 && _file.Faces[sample.Face].Styles.Any( style => style != 0 && style != GoldSrc.Bsp.Face.NoStyle ) )
				probes[probe] = [probe, sample.Face, sample.Offset, 1f];
		} );

		_lightVolumeBounds = bounds;
		_irradiance = CreateLightVolumeTexture( "irradiance", ImageFormat.RGBA16161616F, irradiance, counts.x * IrradianceResolution, counts.y * IrradianceResolution, counts.z );
		_distance = CreateLightVolumeTexture( "distance", ImageFormat.RG1616F, distance, counts.x * DistanceResolution, counts.y * DistanceResolution, counts.z );
		_relocation = CreateLightVolumeTexture( "relocation", ImageFormat.RGBA16161616F, relocation, counts.x, counts.y, counts.z );

		var records = probes.Where( x => x is not null ).SelectMany( x => x ).ToArray();
		if ( records.Length == 0 )
			return;

		var rows = ((records.Length / ProbeChannels) + ProbesPerRow - 1) / ProbesPerRow;

		Array.Resize( ref records, rows * ProbesPerRow * ProbeChannels );

		_irradianceProbes = Texture.Create( ProbesPerRow, rows, ImageFormat.RGBA32323232F )
			.WithName( $"mount://{host.Ident}/{path}/irradianceprobes.vtex" )
			.WithData( System.Runtime.InteropServices.MemoryMarshal.AsBytes( records.AsSpan() ).ToArray() )
			.Finish();
	}

	private (Vector3Int Color, Vector3 Vector) SkyLight()
	{
		var color = Vector3Int.Zero;
		var angles = Vector3.Zero;
		var vector = Vector3.Zero;

		foreach ( var entity in _file.Entities.Where( x => x.ClassName == "light_environment" ) )
		{
			angles = Vector3.Zero;

			foreach ( var (key, value) in entity.Pairs )
			{
				if ( key == "_light" )
				{
					var parts = value.Split( ' ', StringSplitOptions.RemoveEmptyEntries ).TakeWhile( x => int.TryParse( x, out _ ) ).Select( int.Parse ).ToArray();

					if ( parts.Length == 0 )
						continue;

					var red = parts[0];
					var green = parts.Length == 1 ? red : parts.ElementAtOrDefault( 1 );
					var blue = parts.Length == 1 ? red : parts.ElementAtOrDefault( 2 );

					if ( parts.Length >= 4 )
					{
						red = (int)(red * (parts[3] / 255.0));
						green = (int)(green * (parts[3] / 255.0));
						blue = (int)(blue * (parts[3] / 255.0));
					}

					color = new Vector3Int( SkyColor( red ), SkyColor( green ), SkyColor( blue ) );
				}
				else if ( key == "angles" )
				{
					angles = entity.VectorForKey( "angles" );
				}
				else if ( key == "angle" )
				{
					var angle = entity.FloatForKey( "angle" );

					angles = angle >= 0f ? new Vector3( angles.x, angle, angles.z ) : new Vector3( (int)angle == -1 ? -90f : 90f, 0f, 0f );
				}
				else if ( key == "pitch" )
				{
					angles.x = entity.FloatForKey( "pitch" );
				}
			}

			var pitch = angles.x * MathF.PI / 180f;
			var yaw = angles.y * MathF.PI / 180f;

			vector = new Vector3( MathF.Cos( pitch ) * MathF.Cos( yaw ), MathF.Cos( pitch ) * MathF.Sin( yaw ), MathF.Sin( pitch ) );
		}

		return (color, vector);
	}

	private static int SkyColor( int value )
	{
		return (int)(Math.Pow( value / 114.0, 0.6 ) * 264.0);
	}

	private Vector3 ProbeLight( Vector3 origin, Vector3Int skyColor, Vector3 skyVector, out LightSample sample )
	{
		var head = _file.Models[0].HeadNode[0];
		var start = new Vector3( origin.x, origin.y, origin.z + LightTraceOffset );
		var color = Vector3Int.Zero;

		sample = new LightSample( -1, -1 );

		if ( skyColor.x + skyColor.y + skyColor.z != 0 )
		{
			var surface = SurfaceAtPoint( head, start, origin - (skyVector * SkyTraceDistance) );

			if ( surface >= 0 && GetSurfaceFlags( _file.Faces[surface] ).HasFlag( SurfaceFlags.DrawSky ) )
				color = skyColor;
		}

		if ( color.x + color.y + color.z == 0 )
		{
			sample = LightPoint( head, start, start - new Vector3( 0f, 0f, LightTraceDistance ) );
			color = SampleLight( sample );
		}

		return LightColor( color );
	}

	private static Vector3 LightColor( Vector3Int color )
	{
		var max = Math.Max( Math.Max( color.x, color.y ), color.z );
		var light = GoldSrc.TextureUpload.LightGammaTable( Math.Clamp( max, 1, MaxLight ) * 4 ) / 1023f;

		return max == 0 ? new Vector3( light ) : new Vector3( color.x, color.y, color.z ) * (light / max);
	}

	private Vector3Int SampleLight( LightSample sample )
	{
		if ( sample.Face < 0 )
			return Vector3Int.Zero;

		var face = _file.Faces[sample.Face];
		var size = _surfaces[sample.Face].Width * _surfaces[sample.Face].Height * 3;

		var red = 0;
		var green = 0;
		var blue = 0;

		for ( var layer = 0; layer < GoldSrc.Bsp.Face.MaxLightmaps && face.Styles[layer] != GoldSrc.Bsp.Face.NoStyle; layer++ )
		{
			var offset = sample.Offset + (layer * size);

			if ( offset + 2 >= _file.Lighting.Length )
				break;

			var scale = InitialLightStyleValue( face.Styles[layer] );

			red += _file.Lighting[offset + 0] * scale;
			green += _file.Lighting[offset + 1] * scale;
			blue += _file.Lighting[offset + 2] * scale;
		}

		red >>= 8;
		green >>= 8;
		blue >>= 8;

		if ( red == 0 )
			red = 1;

		return new Vector3Int( Math.Min( red, MaxLight ), Math.Min( green, MaxLight ), Math.Min( blue, MaxLight ) );
	}

	private bool SurfaceContains( int faceIndex, Vector3 point, out int s, out int t )
	{
		var surface = _surfaces[faceIndex];
		var texel = _file.TexInfos[_file.Faces[faceIndex].TexInfo].Project( point );

		s = (int)texel.x - (int)surface.TextureMins.x;
		t = (int)texel.y - (int)surface.TextureMins.y;

		return s >= 0 && t >= 0 && s <= (surface.Width - 1) * LuxelSize && t <= (surface.Height - 1) * LuxelSize;
	}

	private int SurfaceAtPoint( int nodeIndex, Vector3 start, Vector3 end )
	{
		if ( nodeIndex < 0 )
			return -1;

		var node = _file.Nodes[nodeIndex];
		var plane = _file.Planes[node.PlaneNum];

		var front = Vector3.Dot( start, plane.Normal ) - plane.Dist;
		var back = Vector3.Dot( end, plane.Normal ) - plane.Dist;

		if ( (back < 0f) == (front < 0f) )
			return SurfaceAtPoint( front < 0f ? node.Child1 : node.Child0, start, end );

		var mid = start + ((end - start) * (front / (front - back)));
		var face = SurfaceAtPoint( front < 0f ? node.Child1 : node.Child0, start, mid );

		if ( face >= 0 )
			return face;

		for ( var i = 0; i < node.NumFaces; i++ )
		{
			if ( SurfaceContains( node.FirstFace + i, mid, out _, out _ ) )
				return node.FirstFace + i;
		}

		return SurfaceAtPoint( front < 0f ? node.Child0 : node.Child1, mid, end );
	}

	private LightSample LightPoint( int nodeIndex, Vector3 start, Vector3 end )
	{
		if ( nodeIndex < 0 )
			return new LightSample( -1, -1 );

		var node = _file.Nodes[nodeIndex];
		var plane = _file.Planes[node.PlaneNum];

		var front = Vector3.Dot( start, plane.Normal ) - plane.Dist;
		var back = Vector3.Dot( end, plane.Normal ) - plane.Dist;

		if ( (back < 0f) == (front < 0f) )
			return LightPoint( front < 0f ? node.Child1 : node.Child0, start, end );

		var mid = start + ((end - start) * (front / (front - back)));
		var sample = LightPoint( front < 0f ? node.Child1 : node.Child0, start, mid );

		if ( sample.Face >= 0 )
			return sample;

		for ( var i = 0; i < node.NumFaces; i++ )
		{
			var index = node.FirstFace + i;
			var face = _file.Faces[index];

			if ( (GetSurfaceFlags( face ) & (SurfaceFlags.DrawTiled | SurfaceFlags.DrawTurb)) != 0 || !SurfaceContains( index, mid, out var s, out var t ) )
				continue;

			if ( face.LightOffset < 0 )
				return new LightSample( -1, -1 );

			return new LightSample( index, face.LightOffset + ((((t / LuxelSize) * _surfaces[index].Width) + (s / LuxelSize)) * 3) );
		}

		return LightPoint( front < 0f ? node.Child0 : node.Child1, mid, end );
	}

	private static int TileTexel( Vector3Int counts, int resolution, int x, int y, int z, int u, int v )
	{
		return (((z * counts.y * resolution) + (y * resolution) + v) * counts.x * resolution) + (x * resolution) + u;
	}

	private static void WriteIrradianceTile( Half[] data, Vector3 radiance, Vector3Int counts, int x, int y, int z )
	{
		for ( var v = 0; v < IrradianceResolution; v++ )
		{
			for ( var u = 0; u < IrradianceResolution; u++ )
			{
				var texel = TileTexel( counts, IrradianceResolution, x, y, z, u, v ) * IrradianceChannels;

				data[texel + 0] = (Half)MathF.Min( radiance.x, MaxProbeValue );
				data[texel + 1] = (Half)MathF.Min( radiance.y, MaxProbeValue );
				data[texel + 2] = (Half)MathF.Min( radiance.z, MaxProbeValue );
				data[texel + 3] = (Half)1f;
			}
		}
	}

	private static void WriteDistanceTile( Half[] data, Span<Vector2> moments, Vector3Int counts, int x, int y, int z )
	{
		const int last = DistanceResolution - 1;

		for ( var v = 0; v < DistanceResolution; v++ )
		{
			for ( var u = 0; u < DistanceResolution; u++ )
			{
				var edgeU = u == 0 || u == last;
				var edgeV = v == 0 || v == last;
				var value = moments[(v * DistanceResolution) + u];

				if ( edgeU && edgeV )
					value = (moments[0] + moments[last] + moments[last * DistanceResolution] + moments[(last * DistanceResolution) + last]) * 0.25f;
				else if ( edgeU || edgeV )
					value = (value + moments[((edgeU ? last - v : v) * DistanceResolution) + (edgeV ? last - u : u)]) * 0.5f;

				var texel = TileTexel( counts, DistanceResolution, x, y, z, u, v ) * DistanceChannels;

				data[texel + 0] = (Half)value.x;
				data[texel + 1] = (Half)MathF.Sqrt( MathF.Max( value.y - (value.x * value.x), 0f ) );
			}
		}
	}

	private Vector3 RelocateProbe( Vector3 position, Vector3 spacing )
	{
		var head = _file.Models[0].HeadNode[0];
		var minSpacing = MathF.Min( spacing.x, MathF.Min( spacing.y, spacing.z ) );
		var minFrontfaceDistance = minSpacing * MinFrontfaceDistanceFactor;
		var offset = Vector3.Zero;

		for ( var step = 0; step < RelocationSteps; step++ )
		{
			var origin = position + offset;
			var inside = PointContents( origin ) == GoldSrc.Bsp.Leaf.ContentsSolid;

			var closest = -1;
			var farthest = -1;
			var closestDistance = float.MaxValue;
			var farthestDistance = 0f;

			for ( var ray = 0; ray < RelocationRays; ray++ )
			{
				var hit = TraceContents( head, 0f, spacing.Length, origin, RelocationDirections[ray], !inside );
				if ( hit < 0f )
					continue;

				if ( hit < closestDistance )
				{
					closestDistance = inside ? hit * RelocationBackfaceScale : hit;
					closest = ray;
				}

				if ( hit > farthestDistance )
				{
					farthestDistance = hit;
					farthest = ray;
				}
			}

			Vector3 candidate;

			if ( inside && closest >= 0 )
				candidate = offset + (RelocationDirections[closest] * (closestDistance + (minFrontfaceDistance * 0.5f)));
			else if ( !inside && closestDistance < minFrontfaceDistance && farthest >= 0 && farthest != closest && Vector3.Dot( RelocationDirections[closest], RelocationDirections[farthest] ) <= 0f )
				candidate = offset + (RelocationDirections[farthest] * MathF.Min( farthestDistance, minSpacing ));
			else if ( !inside && closestDistance > minFrontfaceDistance && offset.LengthSquared > 0f )
				candidate = offset - (offset.Normal * MathF.Min( closestDistance - minFrontfaceDistance, offset.Length ));
			else
				break;

			if ( (candidate / spacing).Length > MaxOffsetFactor )
				break;

			offset = candidate;
		}

		return offset;
	}

	private int PointContents( Vector3 point )
	{
		var node = _file.Models[0].HeadNode[0];

		while ( node >= 0 )
		{
			var plane = _file.Planes[_file.Nodes[node].PlaneNum];

			node = Vector3.Dot( point, plane.Normal ) - plane.Dist > 0f ? _file.Nodes[node].Child0 : _file.Nodes[node].Child1;
		}

		return _file.Leafs[-1 - node].Contents;
	}

	private float TraceContents( int nodeIndex, float near, float far, Vector3 start, Vector3 direction, bool solid )
	{
		if ( nodeIndex < 0 )
			return (_file.Leafs[-1 - nodeIndex].Contents == GoldSrc.Bsp.Leaf.ContentsSolid) == solid ? near : -1f;

		var node = _file.Nodes[nodeIndex];
		var plane = _file.Planes[node.PlaneNum];

		var front = Vector3.Dot( start + (direction * near), plane.Normal ) - plane.Dist;
		var back = Vector3.Dot( start + (direction * far), plane.Normal ) - plane.Dist;

		if ( (back < 0f) == (front < 0f) )
			return TraceContents( front < 0f ? node.Child1 : node.Child0, near, far, start, direction, solid );

		var mid = near + ((far - near) * (front / (front - back)));
		var hit = TraceContents( front < 0f ? node.Child1 : node.Child0, near, mid, start, direction, solid );

		return hit >= 0f ? hit : TraceContents( front < 0f ? node.Child0 : node.Child1, mid, far, start, direction, solid );
	}

	private (int Face, Vector3 Point) TraceSurface( int nodeIndex, Vector3 start, Vector3 end, Vector3 direction )
	{
		if ( nodeIndex < 0 )
			return (-1, default);

		var node = _file.Nodes[nodeIndex];
		var plane = _file.Planes[node.PlaneNum];

		var front = Vector3.Dot( start, plane.Normal ) - plane.Dist;
		var back = Vector3.Dot( end, plane.Normal ) - plane.Dist;

		if ( (back < 0f) == (front < 0f) )
			return TraceSurface( front < 0f ? node.Child1 : node.Child0, start, end, direction );

		var mid = start + ((end - start) * (front / (front - back)));
		var hit = TraceSurface( front < 0f ? node.Child1 : node.Child0, start, mid, direction );

		if ( hit.Face >= 0 )
			return hit;

		for ( var i = 0; i < node.NumFaces; i++ )
		{
			var face = _file.Faces[node.FirstFace + i];
			var normal = face.Side == 0 ? plane.Normal : -plane.Normal;

			if ( Vector3.Dot( direction, normal ) < 0f && FaceContains( face, normal, mid ) )
				return (node.FirstFace + i, mid);
		}

		return TraceSurface( front < 0f ? node.Child0 : node.Child1, mid, end, direction );
	}

	private bool FaceContains( in GoldSrc.Bsp.Face face, Vector3 normal, Vector3 point )
	{
		var previous = _file.GetVertex( face, face.NumEdges - 1 );
		var winding = 0f;

		for ( var i = 0; i < face.NumEdges; i++ )
		{
			var vertex = _file.GetVertex( face, i );
			var side = Vector3.Dot( Vector3.Cross( vertex - previous, point - previous ), normal );

			if ( side * winding < 0f )
				return false;

			if ( side != 0f )
				winding = side;

			previous = vertex;
		}

		return true;
	}

	private static Vector3 GammaToLinear( Vector3 color )
	{
		return new Vector3( GammaToLinear( color.x ), GammaToLinear( color.y ), GammaToLinear( color.z ) );
	}

	private static float GammaToLinear( float value )
	{
		value = Math.Clamp( value, 0f, 1f );

		return value < 0.04045f ? value / 12.92f : MathF.Pow( (value + 0.055f) / 1.055f, 2.4f );
	}
}
