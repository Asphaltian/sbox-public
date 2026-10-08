using System;

partial class BrushModel
{
	private const int IrradianceResolution = 8;
	private const int DistanceResolution = 16;
	private const int MinProbes = 4;
	private const int MaxProbes = 40;
	private const int MaxProbeDensity = 8;
	private const int MaxProbeCount = 16384;
	private const float LightTraceDistance = 8192f;
	private const float ProbeDistance = 65504f;
	private const int RelocationSteps = 9;
	private const float RelocationRange = 0.45f;

	private static readonly Vector3[] RelocationOffsets = BuildRelocationOffsets();

	private static Vector3[] BuildRelocationOffsets()
	{
		var offsets = new List<Vector3>();

		for ( var z = 0; z < RelocationSteps; z++ )
		{
			for ( var y = 0; y < RelocationSteps; y++ )
			{
				for ( var x = 0; x < RelocationSteps; x++ )
					offsets.Add( ((new Vector3( x, y, z ) / (RelocationSteps - 1)) - 0.5f) * (RelocationRange * 2f) );
			}
		}

		return [.. offsets.Where( x => x != Vector3.Zero ).OrderBy( x => x.LengthSquared )];
	}

	private bool RelocateProbe( Vector3 position, Vector3 spacing, out Vector3 offset )
	{
		foreach ( var candidate in RelocationOffsets )
		{
			offset = candidate * spacing;

			if ( PointContents( position + offset ) != GoldSrc.Bsp.Leaf.ContentsSolid )
				return true;
		}

		offset = Vector3.Zero;

		return false;
	}

	private BBox _lightVolumeBounds;
	private int _lightVolumeDensity;
	private Texture _irradiance;
	private Texture _distance;
	private Texture _relocation;

	public void CreateLightVolume( GameObject parent )
	{
		Load();

		if ( _relocation is null )
			return;

		var volume = parent.AddComponent<IndirectLightVolume>();

		volume.Bounds = _lightVolumeBounds;
		volume.ProbeDensity = _lightVolumeDensity;
		volume.IrradianceTexture = _irradiance;
		volume.DistanceTexture = _distance;
		volume.RelocationTexture = _relocation;
	}

	private Texture CreateLightVolumeTexture( string name, Half[] data, int width, int height, int depth )
	{
		return Texture.CreateVolume( width, height, depth, ImageFormat.RGBA16161616F )
			.WithName( $"mount://{host.Ident}/{path}/{name}.vtex" )
			.WithData( System.Runtime.InteropServices.MemoryMarshal.AsBytes( data.AsSpan() ).ToArray() )
			.Finish();
	}

	private void BuildLightVolume()
	{
		if ( _file.Nodes.Length == 0 || _file.Lighting.Length == 0 )
			return;

		var world = _file.Models[0];
		var bounds = new BBox( world.Mins, world.Maxs );
		var density = MaxProbeDensity;
		var counts = ProbeCounts( bounds, density );

		while ( density > 1 && counts.x * counts.y * counts.z > MaxProbeCount )
			counts = ProbeCounts( bounds, --density );

		var spacing = new Vector3(
			bounds.Size.x / (counts.x - 1),
			bounds.Size.y / (counts.y - 1),
			bounds.Size.z / (counts.z - 1) );

		var irradiance = new Half[counts.x * IrradianceResolution * counts.y * IrradianceResolution * counts.z * 4];
		var distance = new Half[counts.x * DistanceResolution * counts.y * DistanceResolution * counts.z * 4];
		var relocation = new Half[counts.x * counts.y * counts.z * 4];

		Array.Fill( distance, (Half)ProbeDistance );

		for ( var z = 0; z < counts.z; z++ )
		{
			for ( var y = 0; y < counts.y; y++ )
			{
				for ( var x = 0; x < counts.x; x++ )
				{
					var position = bounds.Mins + (new Vector3( x, y, z ) * spacing);

					var probe = ((((z * counts.y) + y) * counts.x) + x) * 4;

					if ( PointContents( position ) == GoldSrc.Bsp.Leaf.ContentsSolid )
					{
						if ( !RelocateProbe( position, spacing, out var offset ) )
							continue;

						position += offset;

						relocation[probe + 0] = (Half)offset.x;
						relocation[probe + 1] = (Half)offset.y;
						relocation[probe + 2] = (Half)offset.z;
					}

					relocation[probe + 3] = (Half)1f;

					var light = LightPoint( position ) / (0.5f * MathF.PI);

					for ( var v = 0; v < IrradianceResolution; v++ )
					{
						var row = ((((z * counts.y * IrradianceResolution) + (y * IrradianceResolution) + v) * counts.x * IrradianceResolution) + (x * IrradianceResolution)) * 4;

						for ( var u = 0; u < IrradianceResolution; u++ )
						{
							irradiance[row + (u * 4) + 0] = (Half)light.x;
							irradiance[row + (u * 4) + 1] = (Half)light.y;
							irradiance[row + (u * 4) + 2] = (Half)light.z;
							irradiance[row + (u * 4) + 3] = (Half)1f;
						}
					}
				}
			}
		}

		_lightVolumeBounds = bounds;
		_lightVolumeDensity = density;
		_irradiance = CreateLightVolumeTexture( "irradiance", irradiance, counts.x * IrradianceResolution, counts.y * IrradianceResolution, counts.z );
		_distance = CreateLightVolumeTexture( "distance", distance, counts.x * DistanceResolution, counts.y * DistanceResolution, counts.z );
		_relocation = CreateLightVolumeTexture( "relocation", relocation, counts.x, counts.y, counts.z );
	}

	private static Vector3Int ProbeCounts( BBox bounds, int density )
	{
		return new Vector3Int(
			ProbeCount( bounds.Size.x, density ),
			ProbeCount( bounds.Size.y, density ),
			ProbeCount( bounds.Size.z, density ) );
	}

	private static int ProbeCount( float size, int density )
	{
		return Math.Clamp( (int)MathF.Ceiling( size * (density / 1024f) ) + 1, MinProbes, MaxProbes );
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

	private Vector3 LightPoint( Vector3 point )
	{
		var (red, green, blue) = RecursiveLightPoint( _file.Models[0].HeadNode[0], point, point - new Vector3( 0f, 0f, LightTraceDistance ) );

		return new Vector3( LightScale( red ), LightScale( green ), LightScale( blue ) );
	}

	private static float LightScale( int value )
	{
		const float displayGamma = 2.2f;

		var index = MathF.Min( MathF.Floor( value / 64f ), 1023f );
		var screen = MathF.Floor( GoldSrc.TextureUpload.LightGammaTable( index ) / 4f ) / 255f;

		return MathF.Pow( screen * (128f / 192f) * 2f, displayGamma );
	}

	private (int Red, int Green, int Blue) RecursiveLightPoint( int nodeIndex, Vector3 start, Vector3 end )
	{
		if ( nodeIndex < 0 )
			return (0, 0, 0);

		var node = _file.Nodes[nodeIndex];
		var plane = _file.Planes[node.PlaneNum];

		var front = Vector3.Dot( start, plane.Normal ) - plane.Dist;
		var back = Vector3.Dot( end, plane.Normal ) - plane.Dist;
		var near = front < 0f ? node.Child1 : node.Child0;

		if ( (back < 0f) == (front < 0f) )
			return RecursiveLightPoint( near, start, end );

		var mid = start + ((end - start) * (front / (front - back)));

		var color = RecursiveLightPoint( near, start, mid );
		if ( color != (0, 0, 0) )
			return color;

		for ( var i = 0; i < node.NumFaces; i++ )
		{
			var index = node.FirstFace + i;
			var face = _file.Faces[index];

			if ( !HasLightmap( face ) )
				continue;

			var surface = _surfaces[index];
			var texel = _file.TexInfos[face.TexInfo].Project( mid );

			var s = (int)texel.x;
			var t = (int)texel.y;

			if ( s < surface.TextureMins.x || t < surface.TextureMins.y )
				continue;

			var ds = s - (int)surface.TextureMins.x;
			var dt = t - (int)surface.TextureMins.y;

			if ( ds > (surface.Width - 1) * LuxelSize || dt > (surface.Height - 1) * LuxelSize )
				continue;

			if ( face.LightOffset < 0 )
				return (0, 0, 0);

			var size = surface.Width * surface.Height * 3;
			var sample = face.LightOffset + ((((dt / LuxelSize) * surface.Width) + (ds / LuxelSize)) * 3);

			var red = 0;
			var green = 0;
			var blue = 0;

			for ( var layer = 0; layer < GoldSrc.Bsp.Face.MaxLightmaps && face.Styles[layer] != GoldSrc.Bsp.Face.NoStyle; layer++ )
			{
				var offset = sample + (layer * size);

				if ( offset + 2 >= _file.Lighting.Length )
					break;

				var scale = InitialLightStyleValue( face.Styles[layer] );

				red += _file.Lighting[offset + 0] * scale;
				green += _file.Lighting[offset + 1] * scale;
				blue += _file.Lighting[offset + 2] * scale;
			}

			return (Math.Max( red, 1 ), green, blue);
		}

		return RecursiveLightPoint( front < 0f ? node.Child0 : node.Child1, mid, end );
	}
}
