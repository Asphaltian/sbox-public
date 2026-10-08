
class MaterialLoader( string texturePath, bool transparent ) : ResourceLoader<GameMount>
{
	protected override object Load()
	{
		var material = Material.Create( Path, "goldsrc" );

		material.SetFeature( "F_MASKED", transparent ? 1 : 0 );
		material.Set( "Color", Texture.Load( texturePath ) );

		return material;
	}
}
