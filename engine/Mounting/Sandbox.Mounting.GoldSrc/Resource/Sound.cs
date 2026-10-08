
class WavSoundLoader( string fullPath ) : ResourceLoader<GameMount>
{
	protected override object Load() => SoundFile.FromWav( Path, File.ReadAllBytes( fullPath ) );
}

class Mp3SoundLoader( string fullPath ) : ResourceLoader<GameMount>
{
	protected override object Load() => SoundFile.FromMp3( Path, File.ReadAllBytes( fullPath ) );
}
