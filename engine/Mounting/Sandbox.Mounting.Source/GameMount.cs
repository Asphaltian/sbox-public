/// <summary>
/// A mounting implementation for Half-Life 2
/// </summary>
public abstract class GameMount : BaseGameMount
{
    public abstract long AppId { get; }

    public abstract IReadOnlyList<string> GameDirs { get; }

    protected virtual string GetVpkPathPrefix( string dir ) => null;

    string appDir;

    readonly List<VpkLib.VpkArchive> vpkArchives = [];

    protected override void Initialize( InitializeContext context )
    {
        if ( !context.IsAppInstalled( AppId ) )
            return;

        appDir = context.GetAppDirectory( AppId );
        IsInstalled = true;
    }

    protected override void Shutdown()
    {
        foreach ( var vpk in vpkArchives )
            vpk.Dispose();

        vpkArchives.Clear();
    }

    public byte[] GetFileBytes( string gameDir, string fileName )
    {
        foreach ( var vpk in vpkArchives )
        {
            var data = vpk.GetFileBytes( fileName );
            if ( data is not null )
                return data;
        }

        var loosePath = Path.Combine( appDir, gameDir, fileName );
        return File.Exists( loosePath ) ? File.ReadAllBytes( loosePath ) : null;
    }

    protected override Task Mount( MountContext context )
    {
        if ( string.IsNullOrEmpty( appDir ) || GameDirs is null || GameDirs.Count == 0 )
            return Task.CompletedTask;

        foreach ( var dir in GameDirs )
        {
            var root = Path.Combine( appDir, dir );
            if ( !System.IO.Directory.Exists( root ) )
                continue;

            MountVpks( context, dir, root );
            MountLooseFiles( context, dir, root );
        }

        IsMounted = true;
        return Task.CompletedTask;
    }

    void MountVpks( MountContext context, string dir, string root )
    {
        foreach ( var vpkPath in System.IO.Directory.GetFiles( root, "*_dir.vpk", SearchOption.TopDirectoryOnly ) )
        {
            try
            {
                var vpk = new VpkLib.VpkArchive( vpkPath );
                if ( !vpk.IsValid )
                {
                    vpk.Dispose();
                    continue;
                }

                var prefix = GetVpkPathPrefix( dir );
                if ( prefix is not null )
                    vpk.StripPathPrefix( prefix );

                vpkArchives.Add( vpk );

                foreach ( var entry in vpk.Entries )
                {
                    RegisterResource( context, dir, entry.FullPath );
                }
            }
            catch ( System.Exception ex )
            {
                Log.Warning( $"Failed to load VPK {vpkPath}: {ex.Message}" );
            }
        }
    }

    void MountLooseFiles( MountContext context, string dir, string root )
    {
        foreach ( var fullPath in System.IO.Directory.GetFiles( root, "*.*", SearchOption.AllDirectories ) )
        {
            var ext = Path.GetExtension( fullPath )?.ToLowerInvariant();
            if ( string.IsNullOrEmpty( ext ) || ext == ".vpk" )
                continue;

            var relativePath = Path.GetRelativePath( root, fullPath ).Replace( '\\', '/' );
            RegisterResource( context, dir, relativePath );
        }
    }

    void RegisterResource( MountContext context, string dir, string relativePath )
    {
        var ext = Path.GetExtension( relativePath )?.ToLowerInvariant();
        if ( string.IsNullOrEmpty( ext ) )
            return;

        var path = $"{dir}/{relativePath}";

        switch ( ext )
        {
            case ".wav": context.Add( ResourceType.Sound, path, new WavSoundLoader( dir, relativePath ) ); break;
            case ".vtf": context.Add( ResourceType.Texture, path, new VtfTextureLoader( dir, relativePath ) ); break;
            case ".vmt": context.Add( ResourceType.Material, path, new VmtMaterialLoader( dir, relativePath ) ); break;
        }
    }
}

public class AgeOfChivalryMount : GameMount
{
    public override string Ident => "ageofchivalry";
    public override string Title => "Age of Chivalry";
    public override long AppId => 17510;
    public override IReadOnlyList<string> GameDirs => ["vpks", "ageofchivalry"];
    protected override string GetVpkPathPrefix( string dir ) => dir == "vpks" ? "hl2" : null;
}

public class AlienSwarmMount : GameMount
{
    public override string Ident => "swarm";
    public override string Title => "Alien Swarm";
    public override long AppId => 630;
    public override IReadOnlyList<string> GameDirs => ["swarm_base", "swarm"];
}

public class BladeSymphonyMount : GameMount
{
    public override string Ident => "berimbau";
    public override string Title => "Blade Symphony";
    public override long AppId => 225600;
    public override IReadOnlyList<string> GameDirs => ["berimbau"];
}

public class CounterStrikeSourceMount : GameMount
{
    public override string Ident => "cstrikesource";
    public override string Title => "Counter-Strike: Source";
    public override long AppId => 240;
    public override IReadOnlyList<string> GameDirs => ["hl2", "cstrike"];
}

public class DIPRIPWarmUpMount : GameMount
{
    public override string Ident => "diprip";
    public override string Title => "D.I.P.R.I.P. Warm Up";
    public override long AppId => 17530;
    public override IReadOnlyList<string> GameDirs => ["vpks", "diprip"];
    protected override string GetVpkPathPrefix( string dir ) => dir == "vpks" ? "hl2" : null;
}

public class DayOfDefeatSourceMount : GameMount
{
    public override string Ident => "dodsource";
    public override string Title => "Day of Defeat: Source";
    public override long AppId => 300;
    public override IReadOnlyList<string> GameDirs => ["hl2", "dod"];
}

public class DinoDDayMount : GameMount
{
    public override string Ident => "dinodday";
    public override string Title => "Dino D-Day";
    public override long AppId => 70000;
    public override IReadOnlyList<string> GameDirs => ["dinodday"];
}

public class DystopiaMount : GameMount
{
    public override string Ident => "dystopia";
    public override string Title => "Dystopia";
    public override long AppId => 17580;
    public override IReadOnlyList<string> GameDirs => ["core", "dystopia"];
}

public class FistfulOfFragsMount : GameMount
{
    public override string Ident => "fof";
    public override string Title => "Fistful of Frags";
    public override long AppId => 265630;
    public override IReadOnlyList<string> GameDirs => ["sdk/hl2", "fof"];
}

public class GarrysModMount : GameMount
{
    public override string Ident => "garrysmod";
    public override string Title => "Garry's Mod";
    public override long AppId => 4000;
    public override IReadOnlyList<string> GameDirs => ["sourceengine", "garrysmod"];
}

public class GStringMount : GameMount
{
    public override string Ident => "gstring";
    public override string Title => "G String";
    public override long AppId => 1224600;
    public override IReadOnlyList<string> GameDirs => ["core", "gstringv2", "patch"];
}

public class HalfLife2Mount : GameMount
{
    public override string Ident => "hl2";
    public override string Title => "Half-Life 2";
    public override long AppId => 220;
    public override IReadOnlyList<string> GameDirs => ["hl2", "lostcoast", "episodic", "ep2", "hl2_complete"];
}

public class HalfLife2DeathmatchMount : GameMount
{
    public override string Ident => "hl2mp";
    public override string Title => "Half-Life 2: Deathmatch";
    public override long AppId => 320;
    public override IReadOnlyList<string> GameDirs => ["hl2", "hl2mp", "hl2_complete"];
}

public class HalfLifeDeathmatchSourceMount : GameMount
{
    public override string Ident => "hl1mp";
    public override string Title => "Half-Life Deathmatch: Source";
    public override long AppId => 360;
    public override IReadOnlyList<string> GameDirs => ["hl2", "hl1", "hl1mp"];
}

public class HalfLifeSourceMount : GameMount
{
    public override string Ident => "hl1source";
    public override string Title => "Half-Life: Source";
    public override long AppId => 280;
    public override IReadOnlyList<string> GameDirs => ["hl2", "hl1", "hl1_hd"];
}

public class INFRAMount : GameMount
{
    public override string Ident => "infra";
    public override string Title => "INFRA";
    public override long AppId => 251110;
    public override IReadOnlyList<string> GameDirs => ["infra"];
}

public class InsurgencyMount : GameMount
{
    public override string Ident => "insurgency";
    public override string Title => "INSURGENCY: Modern Infantry Combat";
    public override long AppId => 17700;
    public override IReadOnlyList<string> GameDirs => ["vpks", "insurgency"];
    protected override string GetVpkPathPrefix( string dir ) => dir == "vpks" ? "hl2" : null;
}

public class KlausVeensTreasonMount : GameMount
{
    public override string Ident => "treason";
    public override string Title => "Klaus Veen's Treason";
    public override long AppId => 1786950;
    public override IReadOnlyList<string> GameDirs => ["hl2", "hl2mp", "treason"];
}

public class Left4DeadMount : GameMount
{
    public override string Ident => "left4dead";
    public override string Title => "Left 4 Dead";
    public override long AppId => 500;
    public override IReadOnlyList<string> GameDirs => ["left4dead", "left4dead_dlc3"];
}

public class Left4Dead2Mount : GameMount
{
    public override string Ident => "left4dead2";
    public override string Title => "Left 4 Dead 2";
    public override long AppId => 550;
    public override IReadOnlyList<string> GameDirs => ["left4dead2", "left4dead2_dlc1", "left4dead2_dlc2", "left4dead2_dlc3", "update"];
}

public class NuclearDawnMount : GameMount
{
    public override string Ident => "nucleardawn";
    public override string Title => "Nuclear Dawn";
    public override long AppId => 17710;
    public override IReadOnlyList<string> GameDirs => ["base", "nucleardawn"];
}

public class PiratesVikingsAndKnightsIIMount : GameMount
{
    public override string Ident => "pvkii";
    public override string Title => "Pirates Vikings & Knights II";
    public override long AppId => 17570;
    public override IReadOnlyList<string> GameDirs => ["sdkbase_pvkii/hl2", "pvkii"];
}

public class PortalMount : GameMount
{
    public override string Ident => "portal";
    public override string Title => "Portal";
    public override long AppId => 400;
    public override IReadOnlyList<string> GameDirs => ["hl2", "portal"];
}

public class Portal2Mount : GameMount
{
    public override string Ident => "portal2";
    public override string Title => "Portal 2";
    public override long AppId => 620;
    public override IReadOnlyList<string> GameDirs => ["portal2", "portal2_dlc1", "portal2_dlc2"];
}

public class TeamFortress2Mount : GameMount
{
    public override string Ident => "tf2";
    public override string Title => "Team Fortress 2";
    public override long AppId => 440;
    public override IReadOnlyList<string> GameDirs => ["hl2", "tf"];
}

public class TheStanleyParableMount : GameMount
{
    public override string Ident => "thestanleyparable";
    public override string Title => "The Stanley Parable";
    public override long AppId => 221910;
    public override IReadOnlyList<string> GameDirs => ["thestanleyparable"];
}

public class ZenoClashModelsMount : GameMount
{
    public override string Ident => "zenoclashmodels";
    public override string Title => "Zeno Clash (Model Pack)";
    public override long AppId => 22208;
    public override IReadOnlyList<string> GameDirs => ["Zeno_Clash"];
}

public class ZombiePanicSourceMount : GameMount
{
    public override string Ident => "zps";
    public override string Title => "Zombie Panic! Source";
    public override long AppId => 17500;
    public override IReadOnlyList<string> GameDirs => ["base", "vpks", "zps"];
}
