/// <summary>
/// A mounting implementation for Half-Life 1
/// </summary>
public abstract class GameMount : BaseGameMount
{
	public abstract long AppId { get; }

	public override long? SteamAppId => AppId;

	public abstract IReadOnlyList<string> GameDirs { get; }

	const string BaseGameDir = "valve";
	const string BlueShiftGameDir = "bshift";
	const string PlatformDir = "platform";
	const string DecalWadName = "decals";

	string appDir;

	readonly Dictionary<string, Wad> _wads = new( System.StringComparer.OrdinalIgnoreCase );

	protected override void Initialize( InitializeContext context )
	{
		if ( !context.IsAppInstalled( AppId ) )
			return;

		appDir = context.GetAppDirectory( AppId );
		IsInstalled = true;
	}

	internal GoldSrc.MipTex FindTexture( string name, IEnumerable<string> wadFiles )
	{
		foreach ( var wadFile in wadFiles )
		{
			var data = FindWad( wadFile )?.GetLumpData( name );
			if ( data is not null )
				return new GoldSrc.MipTex( data );
		}

		return null;
	}

	Wad FindWad( string fileName )
	{
		if ( _wads.TryGetValue( fileName, out var wad ) )
			return wad;

		if ( FindFile( fileName ) is string path )
		{
			wad = new Wad();
			wad.LoadWadFile( path );
		}

		return _wads[fileName] = wad;
	}

	internal string FindFile( string path )
	{
		return GameDirs.Append( BaseGameDir )
			.Select( dir => Path.Combine( appDir, dir, path ) )
			.FirstOrDefault( File.Exists );
	}

	protected override void Shutdown()
	{
		_wads.Clear();
	}

	protected override Task Mount( MountContext context )
	{
		if ( string.IsNullOrEmpty( appDir ) || GameDirs is null || GameDirs.Count == 0 )
			return Task.CompletedTask;

		var optionalDirs = GameDirs
			.Where( dir => !dir.EndsWith( "_hd", System.StringComparison.Ordinal ) )
			.SelectMany( dir => new[] { $"{dir}_lv", $"{dir}_addon", $"{dir}_downloads" } );

		foreach ( var dir in GameDirs.Concat( optionalDirs ).Append( PlatformDir ) )
		{
			var root = Path.Combine( appDir, dir );
			if ( !System.IO.Directory.Exists( root ) )
				continue;

			foreach ( var fullPath in System.IO.Directory.GetFiles( root, "*.*", SearchOption.AllDirectories ) )
			{
				var ext = Path.GetExtension( fullPath )?.ToLowerInvariant();
				if ( string.IsNullOrEmpty( ext ) )
					continue;

				var path = Path.GetRelativePath( appDir, fullPath ).Replace( '\\', '/' );

				if ( ext == ".wad" )
				{
					try
					{
						var wad = new Wad();
						wad.LoadWadFile( fullPath );
						var wadName = Path.GetFileNameWithoutExtension( path );
						var isDecals = wadName.Equals( DecalWadName, System.StringComparison.OrdinalIgnoreCase );

						if ( isDecals )
							context.Add( ResourceType.Binary, path, new BinaryLoader( fullPath ) );

						for ( var i = 0; isDecals && i < wad.Lumps.Count; i++ )
							context.Add( ResourceType.Texture, $"{dir}/decals/{i}", new DecalTextureLoader( wad, wad.Lumps[i].Name ) );

						foreach ( var lump in wad.Lumps )
						{
							if ( lump.Type == 66 )
							{
								context.Add( ResourceType.Binary, $"{path}/{lump.Name.ToLowerInvariant()}", new WadLumpLoader( wad, lump.Name ) );
								context.Add( ResourceType.Texture, $"{dir}/{wadName}/{lump.Name.ToLowerInvariant()}", new PicTextureLoader( wad, lump.Name ) );
							}

							if ( lump.Type != 67 ) continue;

							context.Add( ResourceType.Binary, $"{path}/{lump.Name.ToLowerInvariant()}", new WadLumpLoader( wad, lump.Name ) );

							var texture = new WadTextureLoader( wad, lump.Name );
							context.Add( ResourceType.Texture, $"{dir}/textures/{wadName}/{lump.Name}", texture );
							context.Add( ResourceType.Material, $"{dir}/materials/{wadName}/{lump.Name}", new MaterialLoader( texture.Path, lump.Name.StartsWith( '{' ) ) );
						}
					}
					catch ( System.Exception ex )
					{
						Log.Warning( $"Failed to load WAD {fullPath}: {ex.Message}" );
					}

					continue;
				}

				if ( ext == ".mdl" )
				{
					context.Add( ResourceType.Binary, path, new BinaryLoader( fullPath ) );

					using var stream = new FileStream( fullPath, FileMode.Open, FileAccess.Read );
					using var reader = new BinaryReader( stream );

					if ( reader.ReadInt32() != 0x54534449 || reader.ReadInt32() != 10 )
						continue;

					stream.Seek( 204, SeekOrigin.Begin );
					if ( reader.ReadInt32() <= 0 )
						continue;

					context.Add( ResourceType.Model, path, new ModelLoader( fullPath ) );
				}
				else if ( ext == ".spr" )
				{
					context.Add( ResourceType.Binary, path, new BinaryLoader( fullPath ) );

					using var stream = new FileStream( fullPath, FileMode.Open, FileAccess.Read );
					using var reader = new BinaryReader( stream );

					if ( reader.ReadInt32() != Sprite.Ident || reader.ReadInt32() != Sprite.Version )
						continue;

					stream.Seek( Sprite.NumFramesOffset, SeekOrigin.Begin );

					var sprite = new Sprite( fullPath );
					var numFrames = reader.ReadInt32();

					for ( var i = 0; i < numFrames; i++ )
					{
						context.Add( ResourceType.Texture, $"{path}/{i}", new SpriteTextureLoader( sprite, i ) );
						context.Add( ResourceType.Material, $"{path}/{i}", new SpriteMaterialLoader( sprite, i ) );
					}
				}
				else if ( ext == ".wav" )
				{
					context.Add( ResourceType.Binary, path, new BinaryLoader( fullPath ) );
					context.Add( ResourceType.Sound, path, new WavSoundLoader( fullPath ) );
				}
				else if ( ext == ".mp3" )
				{
					context.Add( ResourceType.Sound, path, new Mp3SoundLoader( fullPath ) );
				}
				else if ( ext is ".lmp" or ".nod" or ".bmp" )
				{
					context.Add( ResourceType.Binary, path, new BinaryLoader( fullPath ) );
				}
				else if ( ext == ".tga" )
				{
					context.Add( ResourceType.Binary, path, new BinaryLoader( fullPath ) );
					context.Add( ResourceType.Texture, path, new TgaTextureLoader( fullPath ) );
				}
				else if ( ext is ".txt" or ".cfg" or ".sc" or ".res" or ".gam" or ".lst" or ".scr" or ".inf" or ".vdf" )
				{
					context.Add( ResourceType.Text, path, new TextLoader( fullPath ) );
				}
				else if ( ext == ".bsp" )
				{
					var brushModel = new BrushModel( this, fullPath, path, GameDirs.Contains( BlueShiftGameDir ) );
					var numSubModels = GoldSrc.Bsp.File.ReadNumModels( fullPath );

					context.Add( ResourceType.Binary, path, new BinaryLoader( fullPath ) );
					context.Add( ResourceType.Scene, path, new MapLoader( brushModel ) );
					context.Add( ResourceType.Model, path, new BrushModelLoader( brushModel, 0 ) { Flags = ResourceFlags.DeveloperOnly } );

					for ( var i = 1; i < numSubModels; i++ )
						context.Add( ResourceType.Model, $"{path}/{i}", new BrushModelLoader( brushModel, i ) { Flags = ResourceFlags.DeveloperOnly } );
				}
			}
		}

		IsMounted = true;
		return Task.CompletedTask;
	}
}

public class HalfLifeMount : GameMount
{
	public override string Ident => "hl1";
	public override string Title => "Half-Life";
	public override long AppId => 70;
	public override IReadOnlyList<string> GameDirs => ["valve_hd", "valve"];
}

public class OpposingForceMount : GameMount
{
	public override string Ident => "opfor";
	public override string Title => "Half-Life: Opposing Force";
	public override long AppId => 50;
	public override IReadOnlyList<string> GameDirs => ["gearbox_hd", "gearbox"];
}

public class BlueShiftMount : GameMount
{
	public override string Ident => "bshift";
	public override string Title => "Half-Life: Blue Shift";
	public override long AppId => 130;
	public override IReadOnlyList<string> GameDirs => ["bshift_hd", "bshift"];
}

public class CounterStrikeMount : GameMount
{
	public override string Ident => "cstrike";
	public override string Title => "Counter-Strike";
	public override long AppId => 10;
	public override IReadOnlyList<string> GameDirs => ["cstrike_hd", "cstrike"];
}

public class ConditionZeroMount : GameMount
{
	public override string Ident => "czero";
	public override string Title => "Counter-Strike: Condition Zero";
	public override long AppId => 80;
	public override IReadOnlyList<string> GameDirs => ["czero_hd", "czero"];
}

public class RicochetMount : GameMount
{
	public override string Ident => "ricochet";
	public override string Title => "Ricochet";
	public override long AppId => 60;
	public override IReadOnlyList<string> GameDirs => ["ricochet_hd", "ricochet"];
}

public class TeamFortressClassicMount : GameMount
{
	public override string Ident => "tfc";
	public override string Title => "Team Fortress Classic";
	public override long AppId => 20;
	public override IReadOnlyList<string> GameDirs => ["tfc_hd", "tfc"];
}

public class DeathmatchClassicMount : GameMount
{
	public override string Ident => "dmc";
	public override string Title => "Deathmatch Classic";
	public override long AppId => 40;
	public override IReadOnlyList<string> GameDirs => ["dmc_hd", "dmc"];
}

public class DayOfDefeatMount : GameMount
{
	public override string Ident => "dod";
	public override string Title => "Day of Defeat";
	public override long AppId => 30;
	public override IReadOnlyList<string> GameDirs => ["dod_hd", "dod"];
}

public class SvenCoopMount : GameMount
{
	public override string Ident => "svencoop";
	public override string Title => "Sven Co-op";
	public override long AppId => 225840;
	public override IReadOnlyList<string> GameDirs => ["svencoop"];
}

public class CryOfFearMount : GameMount
{
	public override string Ident => "cof";
	public override string Title => "Cry of Fear";
	public override long AppId => 223710;
	public override IReadOnlyList<string> GameDirs => ["cryoffear"];
}
