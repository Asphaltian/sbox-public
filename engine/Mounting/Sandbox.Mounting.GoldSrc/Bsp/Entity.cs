using System;
using System.Globalization;

namespace GoldSrc.Bsp;

class Entity
{
	public IReadOnlyList<KeyValuePair<string, string>> Pairs => _pairs;

	public string ClassName => ValueForKey( "classname" );

	private readonly List<KeyValuePair<string, string>> _pairs = [];

	public string ValueForKey( string key )
	{
		foreach ( var pair in _pairs )
		{
			if ( pair.Key.Equals( key, StringComparison.Ordinal ) )
				return pair.Value;
		}

		return string.Empty;
	}

	public float FloatForKey( string key )
	{
		return ParseFloat( ValueForKey( key ) );
	}

	public Vector3 VectorForKey( string key )
	{
		var parts = ValueForKey( key ).Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length < 3 )
			return Vector3.Zero;

		return new Vector3( ParseFloat( parts[0] ), ParseFloat( parts[1] ), ParseFloat( parts[2] ) );
	}

	private static float ParseFloat( string value )
	{
		return float.TryParse( value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result ) ? result : 0f;
	}

	public static List<Entity> ParseEntities( string data )
	{
		var entities = new List<Entity>();
		var position = 0;

		while ( NextToken( data, ref position ) is string token )
		{
			if ( token != "{" )
				throw new InvalidDataException( $"ParseEntities: found '{token}' when expecting {{" );

			var entity = new Entity();

			while ( true )
			{
				var key = NextToken( data, ref position ) ?? throw new InvalidDataException( "ParseEntities: EOF without closing brace" );
				if ( key == "}" )
					break;

				var value = NextToken( data, ref position ) ?? throw new InvalidDataException( "ParseEntities: EOF without closing brace" );
				if ( value == "}" )
					throw new InvalidDataException( "ParseEntities: closing brace without data" );

				entity._pairs.Add( new( key, value ) );
			}

			entities.Add( entity );
		}

		return entities;
	}

	private static string NextToken( string data, ref int position )
	{
		while ( position < data.Length && data[position] <= ' ' )
			position++;

		if ( position >= data.Length )
			return null;

		if ( data[position] == '"' )
		{
			var start = ++position;
			while ( position < data.Length && data[position] != '"' )
				position++;

			var end = position++;
			return data[start..end];
		}

		var first = position;
		while ( position < data.Length && data[position] > ' ' )
			position++;

		return data[first..position];
	}
}
