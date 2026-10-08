using System;

partial class ModelLoader
{
	private const float FullTurn = 360f;
	private const float MaxSwingLimit = 90f;
	private const float LockedCone = 5.729578f;
	private const float RotInertiaLimit = 0.05f;
	private const float MetresPerUnit = 0.0254f;

	private static readonly int[] HavokAxisOrder = [0, 2, 1];

	private static void AddJoints( ModelBuilder builder, RagdollRig rig )
	{
		var bind = rig.Model.Bind;

		for ( var child = 1; child < rig.Solids.Length; child++ )
		{
			var solid = rig.Solids[child];
			var parent = rig.ParentSolid( child );
			var parentBind = bind[rig.BodyBones[parent]];
			var parentSolid = bind[rig.SolidBones[parent]].Rotation;
			var childBind = bind[rig.SolidBones[child]];
			var rest = parentBind.Rotation.Inverse * parentSolid * (solid.Rest ?? parentSolid.Inverse * childBind.Rotation);
			var anchor = parentBind.PointToLocal( childBind.Position );
			var free = FreeAxes( solid );

			var (axis, range) = free == 0 ? (Vector3.Left, new Vector2( -LockedCone, LockedCone )) : Hinge( solid );

			if ( free < 2 )
				AddHinge( builder, parent, child, anchor, rest, axis, range );
			else
				AddRagdollJoint( builder, parent, child, anchor, rest, solid, solid.Axes ?? FindAxes( rig, parent, child, rest, anchor ) );
		}
	}

	private static int FreeAxes( in RagdollSolid solid )
	{
		return (solid.X.x != solid.X.y ? 1 : 0) + (solid.Y.x != solid.Y.y ? 1 : 0) + (solid.Z.x != solid.Z.y ? 1 : 0);
	}

	private static (Vector3 Axis, Vector2 Range) Hinge( in RagdollSolid solid )
	{
		if ( solid.X.x != solid.X.y )
			return (Vector3.Forward, solid.X);

		return solid.Y.x != solid.Y.y ? (Vector3.Left, solid.Y) : (Vector3.Up, solid.Z);
	}

	private static void AddHinge( ModelBuilder builder, int parent, int child, Vector3 anchor, Rotation rest, Vector3 axis, Vector2 range )
	{
		var frame = AxisFrame( axis );

		builder.AddHingeJoint( parent, child, new Transform( anchor, (rest * frame).Normal ), new Transform( Vector3.Zero, frame ) )
			.WithTwistLimit( range.x, range.y );
	}

	private static void AddRagdollJoint( ModelBuilder builder, int parent, int child, Vector3 anchor, Rotation rest, in RagdollSolid solid, RagdollAxes axes )
	{
		var planeIndex = 3 - axes.Twist - axes.Cone;
		var twistAxis = AxisVector( axes.Twist );
		var coneAxis = AxisVector( axes.Cone );
		var planeAxis = AxisVector( planeIndex );
		var twist = AxisLimit( solid, axes.Twist );
		var cone = AxisLimit( solid, axes.Cone );
		var plane = AxisLimit( solid, planeIndex );
		var frame = AxisFrame( twistAxis );
		var center = Rotation.FromAxis( coneAxis, (cone.x + cone.y) * 0.5f );
		var joint = builder.AddBallJoint( parent, child, new Transform( anchor, (rest * frame).Normal ), new Transform( Vector3.Zero, (center * frame).Normal ) );

		if ( cone.y - cone.x < FullTurn )
			joint.WithSwingLimit( (cone.y - cone.x) * 0.5f );

		if ( twist.y - twist.x < FullTurn )
			joint.WithTwistLimit( -twist.y, -twist.x );

		if ( plane.y - plane.x >= FullTurn )
			return;

		var above = AddPlaneLimit( builder, parent, child, anchor, rest * frame, coneAxis, MaxSwingLimit + plane.y );
		var below = AddPlaneLimit( builder, parent, child, anchor, rest * frame, -coneAxis, MaxSwingLimit - plane.x );

		if ( above && below )
			return;

		if ( Vector3.Dot( Vector3.Cross( twistAxis, planeAxis ), coneAxis ) > 0f )
			plane = new Vector2( -plane.y, -plane.x );

		frame = AxisFrame( planeAxis );

		builder.AddBallJoint( parent, child, new Transform( anchor, (rest * frame).Normal ), new Transform( Vector3.Zero, frame ) )
			.WithTwistLimit( plane.x, plane.y );
	}

	private static bool AddPlaneLimit( ModelBuilder builder, int parent, int child, Vector3 anchor, Rotation parentFrame, Vector3 normal, float limit )
	{
		if ( limit > MaxSwingLimit )
			return false;

		builder.AddBallJoint( parent, child, new Transform( anchor, parentFrame.Normal ), new Transform( Vector3.Zero, AxisFrame( normal ) ) )
			.WithSwingLimit( limit );

		return true;
	}

	private static RagdollAxes FindAxes( RagdollRig rig, int parent, int child, Rotation rest, Vector3 anchor )
	{
		var solid = rig.Solids[child];
		var inertia = BoxInertia( rig, child, out var childCenter, out var childMass ) * (childMass * rig.InertiaScale * MetresPerUnit * MetresPerUnit);

		inertia = Vector3.Max( inertia, new Vector3( inertia.Length * RotInertiaLimit ) );

		BoxInertia( rig, parent, out var parentCenter, out var parentMass );

		var childArm = -childCenter * MetresPerUnit;
		var parentArm = (anchor - parentCenter) * MetresPerUnit;
		var twist = 0;
		var best = float.NegativeInfinity;

		foreach ( var axis in HavokAxisOrder )
		{
			var childAxis = AxisVector( axis );
			var parentAxis = rest * childAxis;
			var response = (Vector3.Cross( childArm, childAxis ).LengthSquared / childMass)
				+ Vector3.Dot( childAxis * childAxis, Vector3.One / inertia )
				+ Vector3.Dot( parentAxis * parentAxis, Vector3.One / inertia )
				+ (Vector3.Cross( parentArm, parentAxis ).LengthSquared / parentMass);

			if ( response <= best )
				continue;

			best = response;
			twist = axis;
		}

		var order = Array.IndexOf( HavokAxisOrder, twist );
		var plane = HavokAxisOrder[(order + 1) % 3];
		var cone = HavokAxisOrder[(order + 2) % 3];

		return AxisRange( solid, cone ) < AxisRange( solid, plane ) ? new RagdollAxes( twist, plane ) : new RagdollAxes( twist, cone );
	}

	private static Vector3 BoxInertia( RagdollRig rig, int solid, out Vector3 center, out float mass )
	{
		var bind = rig.Model.Bind;
		var bodyBind = bind[rig.BodyBones[solid]];
		var boxes = rig.Boxes[solid];
		var volume = boxes.Sum( x => x.Box.Size.x * x.Box.Size.y * x.Box.Size.z );
		var inertia = Vector3.Zero;

		center = Vector3.Zero;
		mass = volume * MetresPerUnit * MetresPerUnit * MetresPerUnit * Surface.FindByName( rig.Solids[solid].Surface ).Density;

		foreach ( var (bone, box) in boxes )
			center += bodyBind.ToLocal( bind[bone] ).PointToWorld( box.Center ) * (box.Size.x * box.Size.y * box.Size.z / volume);

		foreach ( var (bone, box) in boxes )
		{
			var local = bodyBind.ToLocal( bind[bone] );
			var size = box.Size * box.Size;
			var moments = new Vector3( size.y + size.z, size.x + size.z, size.x + size.y ) / 12f;
			var arm = local.PointToWorld( box.Center ) - center;
			var share = box.Size.x * box.Size.y * box.Size.z / volume;

			for ( var axis = 0; axis < 3; axis++ )
			{
				var direction = local.Rotation.Inverse * AxisVector( axis );
				var offset = arm.LengthSquared - (arm[axis] * arm[axis]);

				inertia[axis] += (Vector3.Dot( direction * direction, moments ) + offset) * share;
			}
		}

		return inertia;
	}

	private static Vector3 AxisVector( int axis ) => axis == 0 ? Vector3.Forward : axis == 1 ? Vector3.Left : Vector3.Down;

	private static Vector2 AxisLimit( in RagdollSolid solid, int axis )
	{
		return axis == 0 ? new Vector2( -solid.X.y, -solid.X.x ) : axis == 1 ? new Vector2( -solid.Y.y, -solid.Y.x ) : solid.Z;
	}

	private static float AxisRange( in RagdollSolid solid, int axis )
	{
		var limit = AxisLimit( solid, axis );

		return limit.y - limit.x;
	}

	private static Rotation AxisFrame( Vector3 axis )
	{
		return Vector3.Dot( Vector3.Up, axis ) < -0.999f ? Rotation.FromAxis( Vector3.Forward, 180f ) : RotationBetween( Vector3.Up, axis );
	}

	private static Rotation RotationBetween( Vector3 from, Vector3 to )
	{
		var axis = Vector3.Cross( from, to );

		return new Rotation { x = axis.x, y = axis.y, z = axis.z, w = 1f + Vector3.Dot( from, to ) }.Normal;
	}
}
