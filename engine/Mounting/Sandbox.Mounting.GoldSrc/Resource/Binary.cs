class BinaryLoader( string fullPath ) : ResourceLoader<GameMount>
{
	protected override object Load() => File.ReadAllBytes( fullPath );
}

class TextLoader( string fullPath ) : ResourceLoader<GameMount>
{
	protected override object Load() => File.ReadAllText( fullPath );
}

class WadLumpLoader( Wad wad, string lumpName ) : ResourceLoader<GameMount>
{
	protected override object Load() => wad.GetLumpData( lumpName );
}
