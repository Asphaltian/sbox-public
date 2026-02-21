
class WavSoundLoader( string gameDir, string fileName ) : ResourceLoader<GameMount>
{
    protected override object Load()
    {
        var data = Host.GetFileBytes( gameDir, fileName );
        return data is null ? null : (object)SoundFile.FromWav( Path, data, false );
    }
}
