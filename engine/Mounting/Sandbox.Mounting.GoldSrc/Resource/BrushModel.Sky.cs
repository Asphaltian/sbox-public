partial class BrushModel
{
	private const string DefaultSky = "desert";

	public void CreateSky()
	{
		Load();

		if ( !_file.Faces.Any( x => GetSurfaceFlags( x ).HasFlag( SurfaceFlags.DrawSky ) ) )
			return;

		if ( (host.FindSky( _file.Entities[0].ValueForKey( "skyname" ) ) ?? host.FindSky( DefaultSky )) is not string path )
			return;

		var sky = new GameObject( true, "sky" );

		sky.AddComponent<Skybox3D>().SkyboxScene = SceneFile.Load( path );
	}
}
