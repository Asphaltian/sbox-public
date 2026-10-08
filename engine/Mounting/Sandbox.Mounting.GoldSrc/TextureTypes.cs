using System;

namespace GoldSrc;

static class TextureTypes
{
	public const string FileName = "sound/materials.txt";
	public const char Concrete = 'C';

	private const int MaxTextures = 512;
	private const int MaxNameLength = 12;

	public static Dictionary<string, char> Load( string path )
	{
		var types = new Dictionary<string, char>( StringComparer.OrdinalIgnoreCase );
		if ( path is null )
			return types;

		var lines = System.IO.File.ReadAllText( path ).Split( '\n' );

		for ( var n = 0; n < lines.Length && types.Count < MaxTextures; n++ )
		{
			var line = lines[n];
			var i = 0;

			while ( i < line.Length && char.IsWhiteSpace( line[i] ) )
				i++;

			if ( i >= line.Length || !char.IsAsciiLetter( line[i] ) )
				continue;

			var type = char.ToUpperInvariant( line[i++] );

			while ( i < line.Length && char.IsWhiteSpace( line[i] ) )
				i++;

			if ( i >= line.Length )
				continue;

			var j = i;

			while ( j < line.Length && !char.IsWhiteSpace( line[j] ) )
				j++;

			if ( j >= line.Length && n == lines.Length - 1 )
				continue;

			types.TryAdd( line[i..Math.Min( j, i + MaxNameLength )], type );
		}

		return types;
	}

	public static char Find( Dictionary<string, char> types, string name )
	{
		if ( name.Length > 1 && name[0] is '-' or '+' )
			name = name[2..];

		if ( name.Length > 0 && name[0] is '{' or '!' or '~' or ' ' )
			name = name[1..];

		if ( name.Length > MaxNameLength )
			name = name[..MaxNameLength];

		return types.GetValueOrDefault( name, Concrete );
	}
}
