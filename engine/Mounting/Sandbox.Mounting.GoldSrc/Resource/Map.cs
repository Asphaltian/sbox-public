class MapLoader( BrushModel brushModel ) : SceneLoader<GameMount>
{
	protected override void BuildScene()
	{
		var entities = brushModel.File.Entities;
		var model = Model.Load( System.IO.Path.ChangeExtension( Path, ".vmdl" ) );

		var world = new GameObject( true, entities[0].ClassName );
		world.AddComponent<ModelRenderer>().Model = model;

		var collider = world.AddComponent<ModelCollider>();
		collider.Model = model;
		collider.Static = true;

		brushModel.CreateLightVolume( world );

		foreach ( var entity in entities.Where( x => x.ClassName == "info_player_start" ) )
		{
			var go = new GameObject( true, entity.ClassName );
			go.WorldPosition = entity.VectorForKey( "origin" );
			go.WorldRotation = Rotation.FromYaw( entity.ValueForKey( "angle" ).Length > 0 ? entity.FloatForKey( "angle" ) : entity.VectorForKey( "angles" ).y );
			go.AddComponent<SpawnPoint>();
		}
	}
}

class BinaryLoader( string fullPath ) : ResourceLoader<GameMount>
{
	protected override object Load() => File.ReadAllBytes( fullPath );
}

class TextLoader( string fullPath ) : ResourceLoader<GameMount>
{
	protected override object Load() => File.ReadAllText( fullPath );
}
