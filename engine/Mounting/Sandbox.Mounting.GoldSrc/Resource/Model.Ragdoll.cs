using System;

partial class ModelLoader
{
	private const float AngularDamping = 1.5f;
	private const float InertiaScale = 10f;
	private const float SwingLimit = 35f;
	private const float HingeLimit = 180f;
	private const float LockedRange = 1f;
	private const float MaxBoneRoll = 45f;
	private const int MaxSolids = 16;
	private const int DefaultSubModel = 0;
	private const string Flesh = "flesh";
	private const string HumanMonster = "hgrunt";
	private const string BipedPrefix = "Bip";
	private const string Biped = "Bip01";
	private const string BipedNeck = "Bip01 Neck";
	private const string BipedSpine = "Bip01 Spine";
	private const string PreferredSpine = "Bip01 Spine1";
	private const string Clavicle = "Arm";
	private const string Finger = "Finger";
	private const string Toe = "Toe";
	private const int FirstDeathActivity = 36;
	private const int LastDeathActivity = 39;
	private const int FirstDeathShotActivity = 66;
	private const int LastDeathShotActivity = 69;

	private static readonly Dictionary<string, string> BipedLimbs = new()
	{
		["Thigh"] = "Leg",
		["Calf"] = "Leg1",
		["Clavicle"] = Clavicle,
		["UpperArm"] = "Arm1",
		["Forearm"] = "Arm2"
	};

	private sealed class RagdollModel
	{
		public readonly GoldSrc.Mdl.File File;
		public readonly GoldSrc.Mdl.Bone[] Bones;
		public readonly Transform[] Bind;
		public readonly GoldSrc.Mdl.BoundingBox[] HitBoxes;
		public readonly List<(int Bone, Vector3 Position)> Vertices = [];
		public readonly BBox?[] Bounds;

		public RagdollModel( GoldSrc.Mdl.File file, GoldSrc.Mdl.Bone[] bones, Transform[] bind, GoldSrc.Mdl.BoundingBox[] hitBoxes )
		{
			File = file;
			Bones = bones;
			Bind = bind;
			HitBoxes = hitBoxes;
			Bounds = new BBox?[bones.Length];

			for ( var bodyPart = 0; bodyPart < file.NumBodyParts; bodyPart++ )
			{
				var vertices = new Vector3[file.GetModel( bodyPart, DefaultSubModel ).NumVerts];
				var vertexBones = new byte[vertices.Length];

				file.GetVertices( bodyPart, DefaultSubModel, vertices );
				file.GetBoneVertices( bodyPart, DefaultSubModel, vertexBones );

				for ( var i = 0; i < vertices.Length; i++ )
				{
					Vertices.Add( (vertexBones[i], vertices[i]) );
					Bounds[vertexBones[i]] = Bounds[vertexBones[i]] is { } box ? box.AddPoint( vertices[i] ) : new BBox( vertices[i], vertices[i] );
				}
			}
		}

		public IEnumerable<int> Ancestors( int bone )
		{
			for ( ; bone >= 0; bone = Bones[bone].Parent )
				yield return bone;
		}

		public int[] Owners( int[] solidBones )
		{
			var owners = new int[Bones.Length];

			Array.Fill( owners, -1 );

			for ( var i = 0; i < solidBones.Length; i++ )
				owners[solidBones[i]] = i;

			for ( var bone = 0; bone < Bones.Length; bone++ )
			{
				if ( owners[bone] < 0 )
					owners[bone] = ParentSolid( owners, bone );
			}

			return owners;
		}

		public int ParentSolid( int[] owners, int bone )
		{
			return Bones[bone].Parent >= 0 ? owners[Bones[bone].Parent] : 0;
		}

		public List<(int Bone, BBox Box)>[] Boxes( int[] solidBones )
		{
			var owners = Owners( solidBones );
			var boxes = new List<(int Bone, BBox Box)>[solidBones.Length];

			for ( var i = 0; i < boxes.Length; i++ )
				boxes[i] = [];

			foreach ( var hitBox in HitBoxes )
				boxes[owners[hitBox.Bone]].Add( (hitBox.Bone, new BBox( hitBox.Min, hitBox.Max )) );

			if ( Array.TrueForAll( boxes, x => x.Count != 0 ) )
				return boxes;

			var empty = Array.ConvertAll( boxes, x => x.Count == 0 );

			for ( var bone = 0; bone < Bones.Length; bone++ )
			{
				if ( empty[owners[bone]] && Bounds[bone] is { } box )
					boxes[owners[bone]].Add( (bone, box) );
			}

			return boxes;
		}

		public float[] Volumes( int[] solidBones )
		{
			return Array.ConvertAll( Boxes( solidBones ), x => x.Sum( y => y.Box.Size.x * y.Box.Size.y * y.Box.Size.z ) );
		}
	}

	private sealed class RagdollRig
	{
		public readonly RagdollModel Model;
		public readonly RagdollSolid[] Solids;
		public readonly float InertiaScale;
		public readonly int[] SolidBones;
		public readonly int[] BodyBones;
		public readonly int[] Owners;
		public readonly List<(int Bone, BBox Box)>[] Boxes;

		public RagdollRig( RagdollModel model, in Ragdoll ragdoll, int[] solidBones )
		{
			Model = model;
			Solids = ragdoll.Solids;
			InertiaScale = ragdoll.InertiaScale;
			SolidBones = solidBones;
			Owners = model.Owners( solidBones );
			Boxes = model.Boxes( solidBones );
			BodyBones = (int[])solidBones.Clone();
			BodyBones[0] = RootBodyBone( model, solidBones[0] );
		}

		public int ParentSolid( int solid ) => Model.ParentSolid( Owners, SolidBones[solid] );
	}

	private static int RootBodyBone( RagdollModel model, int solidBone )
	{
		var geometry = Enumerable.Range( 0, model.Bones.Length ).Where( x => model.Bounds[x] is not null || Array.Exists( model.HitBoxes, y => y.Bone == x ) ).ToArray();
		var root = solidBone;

		foreach ( var ancestor in model.Ancestors( solidBone ).Skip( 1 ) )
		{
			if ( geometry.Any( x => model.Ancestors( x ).Contains( ancestor ) && !model.Ancestors( x ).Contains( root ) ) )
				root = ancestor;
		}

		return root;
	}

	private static bool FindRagdoll( string key, RagdollModel model, out Ragdoll ragdoll, out int[] solidBones )
	{
		if ( Ragdolls.TryGetValue( key, out ragdoll ) && FindSolids( model, ref ragdoll, out solidBones ) )
			return true;

		ragdoll = key.StartsWith( PlayerModel, StringComparison.OrdinalIgnoreCase ) ? Human : Ragdolls[HumanMonster];

		if ( FindSolids( model, ref ragdoll, out solidBones ) )
			return true;

		var biped = !Array.Exists( solidBones, x => x < 0 );

		if ( !biped && !model.File.Sequences.Any( IsDeathSequence ) )
			return false;

		solidBones = GenerateSolidBones( model );

		if ( solidBones.Length < 2 )
			return false;

		ragdoll = new Ragdoll( InertiaScale, GenerateSolids( model, solidBones ) );

		return true;
	}

	private static bool IsDeathSequence( GoldSrc.Mdl.SequenceDesc sequence )
	{
		return sequence.Activity is (>= FirstDeathActivity and <= LastDeathActivity) or (>= FirstDeathShotActivity and <= LastDeathShotActivity);
	}

	private static bool FindSolids( RagdollModel model, ref Ragdoll ragdoll, out int[] solidBones )
	{
		var authored = ragdoll.Solids;
		var bones = Array.ConvertAll( authored, x => Array.FindIndex( model.Bones, y => BipedName( y.Name ) == x.Bone ) );

		solidBones = bones;

		if ( Array.Exists( bones, x => x < 0 ) )
			return false;

		var owners = model.Owners( bones );

		if ( !SharesBoneAxes( model, authored, bones, owners ) )
			return false;

		var volumes = model.Volumes( bones );
		var kept = Enumerable.Range( 0, bones.Length ).Where( x => volumes[x] > 0f ).ToArray();

		if ( kept.Length == bones.Length )
			return true;

		if ( kept.Length < 2 )
			return false;

		var solids = Array.ConvertAll( kept, x => volumes[model.ParentSolid( owners, bones[x] )] > 0f ? authored[x] : authored[x] with { Rest = null } );

		solidBones = Array.ConvertAll( kept, x => bones[x] );
		ragdoll = ragdoll with { Solids = solids };

		return true;
	}

	private static bool SharesBoneAxes( RagdollModel model, RagdollSolid[] solids, int[] bones, int[] owners )
	{
		for ( var i = 1; i < solids.Length; i++ )
		{
			if ( solids[i].Rest is not { } rest )
				continue;

			var parent = model.Bind[bones[model.ParentSolid( owners, bones[i] )]].Rotation;
			var roll = rest.Inverse * (parent.Inverse * model.Bind[bones[i]].Rotation);
			var angle = MathF.Abs( 2f * MathF.Atan2( roll.x, roll.w ) ) * (180f / MathF.PI);

			if ( MathF.Min( angle, 360f - angle ) > MaxBoneRoll )
				return false;
		}

		return true;
	}

	private static string SolidName( string bone )
	{
		if ( bone.Length <= Biped.Length || !bone.StartsWith( BipedPrefix, StringComparison.Ordinal ) )
			return bone;

		var numbered = char.IsAsciiDigit( bone[BipedPrefix.Length] ) && char.IsAsciiDigit( bone[BipedPrefix.Length + 1] );

		return numbered ? string.Concat( Biped, bone.AsSpan( Biped.Length ) ) : bone;
	}

	private static string BipedName( string bone )
	{
		var name = SolidName( bone );

		if ( !BipedLimb( name, out var limb ) || !BipedLimbs.TryGetValue( limb, out var alias ) )
			return name;

		return string.Concat( name.AsSpan( 0, name.Length - limb.Length ), alias );
	}

	private static bool BipedLimb( string name, out string limb )
	{
		var side = Biped.Length + 1;

		limb = null;

		if ( name.Length <= side + 2 || !name.StartsWith( Biped, StringComparison.Ordinal ) || name[side] is not ('L' or 'R') || name[side + 1] != ' ' )
			return false;

		limb = name[(side + 2)..];

		return true;
	}

	private static bool IsMinorBone( string bone )
	{
		var name = BipedName( bone );

		if ( name == Biped || name == BipedNeck )
			return true;

		return BipedLimb( name, out var limb ) && (limb == Clavicle || limb.StartsWith( Finger, StringComparison.Ordinal ) || limb.StartsWith( Toe, StringComparison.Ordinal ));
	}

	private static bool IsSpineBone( string bone )
	{
		var name = SolidName( bone );

		if ( !name.StartsWith( BipedSpine, StringComparison.Ordinal ) )
			return false;

		return name.Length == BipedSpine.Length || (name.Length == BipedSpine.Length + 1 && char.IsAsciiDigit( name[^1] ));
	}

	private static int[] GenerateSolidBones( RagdollModel model )
	{
		var bones = model.Bones;
		var animated = AnimatedBones( model );
		var pivots = new SortedSet<int>();

		foreach ( var hitBox in model.HitBoxes )
		{
			var bone = hitBox.Bone;

			while ( (!animated[bone] || IsMinorBone( bones[bone].Name )) && bones[bone].Parent >= 0 )
				bone = bones[bone].Parent;

			if ( !IsMinorBone( bones[bone].Name ) )
				pivots.Add( bone );
		}

		var spines = pivots.Where( x => IsSpineBone( bones[x].Name ) ).ToArray();

		if ( spines.Length > 1 )
		{
			var torso = Array.FindIndex( spines, x => SolidName( bones[x].Name ) == PreferredSpine );

			pivots.ExceptWith( spines );
			pivots.Add( spines[Math.Max( torso, 0 )] );
		}

		var solidBones = AddRootSolid( model, [.. pivots] );

		while ( solidBones.Count > MaxSolids )
			solidBones.RemoveAt( SmallestLeaf( model, [.. solidBones] ) );

		return [.. solidBones];
	}

	private static List<int> AddRootSolid( RagdollModel model, int[] pivots )
	{
		var roots = Array.FindAll( pivots, x => !model.Ancestors( x ).Skip( 1 ).Any( pivots.Contains ) );

		if ( roots.Length < 2 )
			return [.. pivots];

		var common = model.Ancestors( roots[0] ).Where( x => roots.All( y => model.Ancestors( y ).Contains( x ) ) ).DefaultIfEmpty( -1 ).First();

		if ( common < 0 )
			return [.. pivots];

		var rooted = pivots.Append( common ).Order().ToArray();

		return model.Volumes( rooted )[0] > 0f ? [.. rooted] : [.. pivots];
	}

	private static int SmallestLeaf( RagdollModel model, int[] solidBones )
	{
		var owners = model.Owners( solidBones );
		var volumes = model.Volumes( solidBones );
		var parents = new bool[solidBones.Length];

		for ( var i = 1; i < solidBones.Length; i++ )
			parents[model.ParentSolid( owners, solidBones[i] )] = true;

		return Enumerable.Range( 1, solidBones.Length - 1 )
			.Where( x => !parents[x] )
			.OrderBy( x => SolidName( model.Bones[solidBones[x]].Name ).StartsWith( Biped, StringComparison.Ordinal ) )
			.ThenBy( x => volumes[x] )
			.First();
	}

	private static bool[] AnimatedBones( RagdollModel model )
	{
		var bones = model.Bones;
		var bind = model.Bind;
		var animated = new bool[bones.Length];
		var rest = new Rotation[bones.Length];

		for ( var bone = 0; bone < bones.Length; bone++ )
			rest[bone] = bones[bone].Parent >= 0 ? bind[bones[bone].Parent].Rotation.Inverse * bind[bone].Rotation : bind[bone].Rotation;

		foreach ( var blends in model.File.BlendTransforms )
		{
			foreach ( var blend in blends )
			{
				for ( var i = 0; i < blend.Transforms.Length; i++ )
				{
					var bone = i % bones.Length;

					if ( animated[bone] )
						continue;

					var angles = JointAngles( rest[bone].Inverse * blend.Transforms[i].Rotation );

					animated[bone] = MathF.Abs( angles.x ) > LockedRange || MathF.Abs( angles.y ) > LockedRange || MathF.Abs( angles.z ) > LockedRange;
				}
			}
		}

		return animated;
	}

	private static RagdollSolid[] GenerateSolids( RagdollModel model, int[] solidBones )
	{
		var bones = model.Bones;
		var bind = model.Bind;
		var owners = model.Owners( solidBones );
		var parents = new int[solidBones.Length];
		var rest = new Rotation[solidBones.Length];
		var mins = new Vector3[solidBones.Length];
		var maxs = new Vector3[solidBones.Length];
		var world = new Rotation[bones.Length];

		for ( var i = 1; i < solidBones.Length; i++ )
		{
			parents[i] = solidBones[model.ParentSolid( owners, solidBones[i] )];
			rest[i] = bind[parents[i]].Rotation.Inverse * bind[solidBones[i]].Rotation;
		}

		foreach ( var blends in model.File.BlendTransforms )
		{
			foreach ( var blend in blends )
			{
				for ( var frame = 0; frame < blend.Transforms.Length; frame += bones.Length )
				{
					for ( var bone = 0; bone < bones.Length; bone++ )
					{
						var local = blend.Transforms[frame + bone].Rotation;

						world[bone] = bones[bone].Parent >= 0 ? world[bones[bone].Parent] * local : local;
					}

					for ( var i = 1; i < solidBones.Length; i++ )
					{
						var angles = JointAngles( rest[i].Inverse * (world[parents[i]].Inverse * world[solidBones[i]]) );

						mins[i] = Vector3.Min( mins[i], angles );
						maxs[i] = Vector3.Max( maxs[i], angles );
					}
				}
			}
		}

		var solids = new RagdollSolid[solidBones.Length];

		for ( var i = 0; i < solids.Length; i++ )
		{
			var range = maxs[i] - mins[i];
			var free = (range.x > LockedRange ? 1 : 0) + (range.y > LockedRange ? 1 : 0) + (range.z > LockedRange ? 1 : 0);
			var limit = free > 1 ? SwingLimit : HingeLimit;

			solids[i] = new RagdollSolid( bones[solidBones[i]].Name, Flesh, AngularDamping,
				JointLimit( mins[i].x, maxs[i].x, limit ), JointLimit( mins[i].y, maxs[i].y, limit ), JointLimit( mins[i].z, maxs[i].z, limit ), null, null );
		}

		return solids;
	}

	private static Vector2 JointLimit( float min, float max, float limit )
	{
		return max - min > LockedRange ? new Vector2( MathF.Max( min, -limit ), MathF.Min( max, limit ) ) : default;
	}

	private static Vector3 JointAngles( Rotation rotation )
	{
		var x = rotation.x;
		var y = rotation.y;
		var z = rotation.z;
		var w = rotation.w;

		return new Vector3(
			MathF.Atan2( 2f * ((y * z) + (w * x)), 1f - (2f * ((x * x) + (y * y))) ),
			-MathF.Asin( Math.Clamp( 2f * ((x * z) - (w * y)), -1f, 1f ) ),
			MathF.Atan2( 2f * ((x * y) + (w * z)), 1f - (2f * ((y * y) + (z * z))) ) ) * (180f / MathF.PI);
	}
}
