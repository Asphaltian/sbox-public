using System;

partial class ModelLoader
{
	private const float LinearDamping = 0.01f;
	private const float MinimumHullVolume = 1f;
	private const int ShieldHitBox = 21;
	private const string ModelDirectory = "/models/";
	private const string PlayerModel = "player";

	private static readonly string[] ShieldGames = ["cstrike", "cstrike_beta", "czero", "terror"];

	private void AddPhysics( ModelBuilder builder, GoldSrc.Mdl.File mdlFile, GoldSrc.Mdl.Bone[] bones, List<ModelBuilder.Bone> skeleton, Transform[] bind )
	{
		var key = RagdollKey( fullPath );
		var model = new RagdollModel( mdlFile, bones, bind, HitBoxes( mdlFile, key ) );

		if ( !FindRagdoll( key, model, out var ragdoll, out var solidBones ) )
		{
			foreach ( var hitBox in model.HitBoxes )
				builder.AddCollisionBox( (hitBox.Max - hitBox.Min) * 0.5f, bind[hitBox.Bone].PointToWorld( (hitBox.Min + hitBox.Max) * 0.5f ), bind[hitBox.Bone].Rotation );

			return;
		}

		var rig = new RagdollRig( model, ragdoll, solidBones );

		AddBodies( builder, skeleton, rig );
		AddJoints( builder, rig );
	}

	private static string RagdollKey( string fullPath )
	{
		var path = System.IO.Path.ChangeExtension( fullPath, null ).Replace( '\\', '/' );
		var models = path.LastIndexOf( ModelDirectory, StringComparison.OrdinalIgnoreCase );

		return models < 0 ? System.IO.Path.GetFileName( path ) : path[(models + ModelDirectory.Length)..];
	}

	private GoldSrc.Mdl.BoundingBox[] HitBoxes( GoldSrc.Mdl.File mdlFile, string key )
	{
		var hitBoxes = mdlFile.HitBoxes.ToArray();

		if ( !key.StartsWith( PlayerModel, StringComparison.OrdinalIgnoreCase ) || !ShieldGames.Contains( Host.GameDirs[^1], StringComparer.OrdinalIgnoreCase ) )
			return hitBoxes;

		return [.. hitBoxes.Where( ( _, i ) => i != ShieldHitBox ).Take( hitBoxes.Length - 1 )];
	}

	private static void AddBodies( ModelBuilder builder, List<ModelBuilder.Bone> skeleton, RagdollRig rig )
	{
		var bind = rig.Model.Bind;
		var vertices = SolidVertices( rig );

		for ( var i = 0; i < rig.Solids.Length; i++ )
		{
			var bodyBind = bind[rig.BodyBones[i]];
			var body = builder.AddBody( 0f, Surface.FindByName( rig.Solids[i].Surface ), skeleton[rig.BodyBones[i]].Name )
				.SetBindPose( bodyBind )
				.SetDamping( LinearDamping, rig.Solids[i].AngularDamping )
				.SetInertiaScale( rig.InertiaScale );

			var hull = SupportPoints( vertices[i] );

			if ( SpansVolume( hull ) )
			{
				body.AddHull( hull );
				continue;
			}

			foreach ( var (bone, box) in rig.Boxes[i] )
			{
				var local = bodyBind.ToLocal( bind[bone] );

				body.AddBox( box.Size * 0.5f, new Transform( local.PointToWorld( box.Center ), local.Rotation ) );
			}
		}
	}

	private static List<Vector3>[] SolidVertices( RagdollRig rig )
	{
		var bind = rig.Model.Bind;
		var solids = new List<Vector3>[rig.Solids.Length];

		for ( var i = 0; i < solids.Length; i++ )
			solids[i] = [];

		foreach ( var (bone, position) in rig.Model.Vertices )
		{
			var solid = rig.Owners[bone];

			solids[solid].Add( bind[rig.BodyBones[solid]].PointToLocal( bind[bone].PointToWorld( position ) ) );
		}

		for ( var child = 1; child < solids.Length; child++ )
		{
			var parent = rig.ParentSolid( child );
			var pivot = bind[rig.SolidBones[child]].Position;

			solids[child].Add( bind[rig.BodyBones[child]].PointToLocal( pivot ) );
			solids[parent].Add( bind[rig.BodyBones[parent]].PointToLocal( pivot ) );
		}

		return solids;
	}

	private static Vector3[] SupportPoints( List<Vector3> points )
	{
		var support = new HashSet<Vector3>();

		if ( points.Count == 0 )
			return [];

		for ( var x = -1; x <= 1; x++ )
		{
			for ( var y = -1; y <= 1; y++ )
			{
				for ( var z = -1; z <= 1; z++ )
				{
					var direction = new Vector3( x, y, z );

					if ( direction != Vector3.Zero )
						support.Add( points.MaxBy( p => Vector3.Dot( p, direction ) ) );
				}
			}
		}

		return [.. support];
	}

	private static bool SpansVolume( Vector3[] points )
	{
		if ( points.Length < 4 )
			return false;

		var origin = points[0];
		var edge = points.MaxBy( x => (x - origin).LengthSquared ) - origin;
		var normal = points.Select( x => Vector3.Cross( edge, x - origin ) ).MaxBy( x => x.LengthSquared );

		return points.Max( x => MathF.Abs( Vector3.Dot( normal, x - origin ) ) ) > MinimumHullVolume;
	}
}
