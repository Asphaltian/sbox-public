using System;
using System.Globalization;
using System.Text;

class VmtMaterialLoader( string gameDir, string fileName ) : ResourceLoader<GameMount>
{
    static readonly HashSet<string> SupportedShaders = new( StringComparer.OrdinalIgnoreCase )
    {
        "LightmappedGeneric",
        "UnlitGeneric",
        "VertexLitGeneric",
    };

    const uint FlagVertexColor = 0x10;
    const uint FlagVertexAlpha = 0x20;
    const uint FlagSelfIllum = 0x40;
    const uint FlagAlphaTest = 0x100;
    const uint FlagNoCull = 0x2000;
    const uint FlagBaseAlphaEnvMapMask = 0x100000;
    const uint FlagTranslucent = 0x200000;
    const uint FlagNormalMapAlphaEnvMapMask = 0x400000;
    const uint FlagHalfLambert = 0x8000000;

    protected override object Load()
    {
        var data = Host.GetFileBytes( gameDir, fileName );
        if ( data is null || data.Length == 0 )
            return null;

        var (shader, parameters) = ParseVmt( data );
        if ( shader is null || parameters is null )
            return null;

        if ( !SupportedShaders.Contains( shader ) )
            return null;

        shader = shader.ToLowerInvariant();
        if ( shader is null )
            return null;

        var material = Material.Create( Path, shader );
        if ( material is null )
            return null;

        bool selfIllum = HasFlag( parameters, "selfillum", FlagSelfIllum );
        bool alphaTest = HasFlag( parameters, "alphatest", FlagAlphaTest );
        bool translucent = HasFlag( parameters, "translucent", FlagTranslucent );
        bool noCull = HasFlag( parameters, "nocull", FlagNoCull );
        bool additive = GetBool( parameters, "additive" );
        bool phong = GetBool( parameters, "phong" );

        if ( alphaTest && translucent )
            translucent = false;

        if ( alphaTest )
            material.SetFeature( "F_ALPHA_TEST", 1 );

        if ( translucent || additive )
            material.SetFeature( "F_TRANSLUCENT", 1 );

        if ( noCull )
            material.SetFeature( "F_RENDER_BACKFACES", 1 );

        if ( selfIllum && shader != "unlitgeneric" )
            material.SetFeature( "F_SELF_ILLUM", 1 );

        SetTexture( material, parameters, "basetexture", "Color" );

        if ( SetTexture( material, parameters, "bumpmap", "Normal" ) && shader != "unlitgeneric" )
            material.SetFeature( "F_NORMAL_MAP", 1 );

        if ( alphaTest )
            material.Set( "g_flAlphaTestReference", GetFloat( parameters, "alphatestreference", 0.5f ) );

        if ( selfIllum && shader != "unlitgeneric" )
        {
            var tint = GetColor( parameters, "selfillumtint" );
            if ( tint.HasValue )
                material.Set( "g_vSelfIllumTint", tint.Value );
        }

        if ( selfIllum && shader != "unlitgeneric" && SetTexture( material, parameters, "selfillummask", "SelfIllumMask" ) )
            material.SetFeature( "F_SELF_ILLUM_MASK", 1 );

        var color = GetColor( parameters, "color" ) ?? GetColor( parameters, "color2" );
        if ( color.HasValue )
            material.Set( "g_vColor", color.Value );

        material.Set( "g_flAlpha", GetFloat( parameters, "alpha", 1.0f ) );

        if ( parameters.ContainsKey( "envmap" ) )
        {
            material.SetFeature( "F_ENVMAP", 1 );

            var envmapTint = GetColor( parameters, "envmaptint" );
            if ( envmapTint.HasValue )
                material.Set( "g_vEnvmapTint", envmapTint.Value );

            material.Set( "g_flEnvmapContrast", GetFloat( parameters, "envmapcontrast" ) );
            material.Set( "g_flEnvmapSaturation", GetFloat( parameters, "envmapsaturation", 1.0f ) );

            if ( SetTexture( material, parameters, "envmapmask", "EnvMapMask" ) )
                material.SetFeature( "F_ENVMAP_MASK", 1 );

            if ( GetBool( parameters, "basealphaenvmapmask" ) || HasFlag( parameters, "basealphaenvmapmask", FlagBaseAlphaEnvMapMask ) )
                material.Set( "g_nBaseAlphaEnvMapMask", 1 );

            if ( HasFlag( parameters, "normalmapalphaenvmapmask", FlagNormalMapAlphaEnvMapMask ) )
                material.Set( "g_nNormalMapAlphaEnvMapMask", 1 );

            if ( shader == "vertexlitgeneric" )
            {
                material.Set( "g_flEnvmapFresnel", GetFloat( parameters, "envmapfresnel" ) );
            }
        }

        if ( parameters.ContainsKey( "detail" ) )
        {
            if ( SetTexture( material, parameters, "detail", "Detail" ) )
            {
                material.SetFeature( "F_DETAIL", 1 );
                material.Set( "g_flDetailScale", GetFloat( parameters, "detailscale", 4.0f ) );
                material.Set( "g_flDetailBlendFactor", GetFloat( parameters, "detailblendfactor", 1.0f ) );
                material.Set( "g_nDetailBlendMode", GetInt( parameters, "detailblendmode" ) );

                var detailTint = GetColor( parameters, "detailtint" );
                if ( detailTint.HasValue )
                    material.Set( "g_vDetailTint", detailTint.Value );
            }
        }

        if ( shader == "lightmappedgeneric" )
        {
            if ( SetTexture( material, parameters, "basetexture2", "Color2" ) )
            {
                material.SetFeature( "F_BASETEXTURE2", 1 );
                material.Set( "g_flBlendAmount", GetFloat( parameters, "blendamount" ) );

                SetTexture( material, parameters, "bumpmap2", "Normal2" );

                if ( parameters.ContainsKey( "blendmodulatetexture" ) )
                {
                    if ( SetTexture( material, parameters, "blendmodulatetexture", "BlendModulationTexture" ) )
                        material.Set( "g_nFancyBlending", 1 );
                }
            }
        }

        if ( phong && shader == "vertexlitgeneric" )
        {
            material.SetFeature( "F_PHONG", 1 );

            if ( parameters.ContainsKey( "phongexponent" ) )
                material.Set( "g_flPhongExponent", GetFloat( parameters, "phongexponent", 5.0f ) );
            else if ( parameters.ContainsKey( "phongexponenttexture" ) )
                material.Set( "g_flPhongExponent", -1.0f );

            material.Set( "g_flPhongBoost", GetFloat( parameters, "phongboost", 1.0f ) );

            var fresnelRanges = GetVector3( parameters, "phongfresnelranges" );
            if ( fresnelRanges.HasValue )
                material.Set( "g_vPhongFresnelRanges", fresnelRanges.Value );

            var phongTint = GetColor( parameters, "phongtint" );
            if ( phongTint.HasValue )
                material.Set( "g_vPhongTint", phongTint.Value );

            if ( GetBool( parameters, "basemapalphaphongmask" ) )
                material.Set( "g_nBaseAlphaPhongMask", 1 );

            if ( GetBool( parameters, "phongalbedotint" ) )
                material.Set( "g_nPhongAlbedoTint", 1 );

            if ( GetBool( parameters, "invertphongmask" ) )
                material.Set( "g_nInvertPhongMask", 1 );

            if ( SetTexture( material, parameters, "phongexponenttexture", "PhongExponentTexture" ) )
                material.Set( "g_nHasPhongExponentTexture", 1 );

            if ( SetTexture( material, parameters, "phongwarptexture", "PhongWarpTexture" ) )
                material.SetFeature( "F_PHONG_WARP", 1 );

            if ( GetBool( parameters, "rimlight" ) )
            {
                material.SetFeature( "F_RIM_LIGHT", 1 );
                material.Set( "g_flRimLightExponent", GetFloat( parameters, "rimlightexponent", 4.0f ) );
                material.Set( "g_flRimLightBoost", GetFloat( parameters, "rimlightboost", 1.0f ) );

                if ( GetBool( parameters, "rimmask" ) )
                    material.Set( "g_nRimMask", 1 );
            }
        }

        if ( additive )
            material.SetFeature( "F_ADDITIVE_BLEND", 1 );

        if ( SetTexture( material, parameters, "lightwarptexture", "LightWarpTexture" ) && shader != "unlitgeneric" )
            material.SetFeature( "F_LIGHT_WARP", 1 );

        if ( shader == "lightmappedgeneric" && parameters.ContainsKey( "envmap" ) )
            material.Set( "g_flFresnelReflection", GetFloat( parameters, "fresnelreflection", 1.0f ) );

        if ( GetBool( parameters, "blendtintbybasealpha" ) && shader == "vertexlitgeneric" )
        {
            material.Set( "g_nBlendTintByBaseAlpha", 1 );
            material.Set( "g_flBlendTintColorOverBase", GetFloat( parameters, "blendtintcoloroverbase" ) );
        }

        if ( HasFlag( parameters, "vertexcolor", FlagVertexColor ) && shader == "unlitgeneric" )
            material.SetFeature( "F_VERTEX_COLOR", 1 );

        if ( HasFlag( parameters, "vertexalpha", FlagVertexAlpha ) && shader == "unlitgeneric" )
            material.SetFeature( "F_VERTEX_ALPHA", 1 );

        if ( HasFlag( parameters, "halflambert", FlagHalfLambert ) && shader == "vertexlitgeneric" )
            material.SetFeature( "F_HALF_LAMBERT", 1 );

        if ( GetBool( parameters, "selfillumfresnel" ) && shader == "vertexlitgeneric" )
        {
            if ( !selfIllum )
                material.SetFeature( "F_SELF_ILLUM", 1 );
            material.SetFeature( "F_SELFILLUM_FRESNEL", 1 );

            var fresnelMme = GetVector3( parameters, "selfillumfresnelminmaxexp" );
            if ( fresnelMme.HasValue )
                material.Set( "g_vSelfIllumFresnelMinMaxExp", fresnelMme.Value );
        }

        if ( GetBool( parameters, "ssbump" ) && shader == "lightmappedgeneric" )
            material.SetFeature( "F_SSBUMP", 1 );

        if ( GetBool( parameters, "nodiffusebumplighting" ) && shader == "lightmappedgeneric" )
            material.Set( "g_nNoDiffuseBumpLighting", 1 );

        if ( shader == "lightmappedgeneric" )
        {
            if ( GetBool( parameters, "basetexturenoenvmap" ) )
                material.Set( "g_nBaseTextureNoEnvmap", 1 );

            if ( GetBool( parameters, "basetexture2noenvmap" ) )
                material.Set( "g_nBaseTexture2NoEnvmap", 1 );

            if ( GetBool( parameters, "maskedblending" ) )
                material.SetFeature( "F_MASKED_BLENDING", 1 );
        }

        return material;
    }

    bool SetTexture( Material material, Dictionary<string, string> parameters, string vmtParam, string shaderParam )
    {
        if ( !parameters.TryGetValue( vmtParam, out var texturePath ) || string.IsNullOrWhiteSpace( texturePath ) )
            return false;

        texturePath = texturePath.Replace( '\\', '/' ).Trim( '/' );

        var texture = ResolveTexture( texturePath );
        if ( texture is null || texture.IsError )
            return false;

        material.Set( shaderParam, texture );
        return true;
    }

    Texture ResolveTexture( string path )
    {
        var ident = Host.Ident;

        foreach ( var dir in Host.GameDirs )
        {
            var texture = Texture.Load( $"mount://{ident}/{dir}/materials/{path}.vtf.vtex", false );
            if ( texture is not null && !texture.IsError )
                return texture;
        }

        return null;
    }

    static bool HasFlag( Dictionary<string, string> parameters, string paramName, uint flagBit )
    {
        return GetBool( parameters, paramName ) || (parameters.TryGetValue( "flags", out var flagsStr ) && uint.TryParse( flagsStr, out var flags ) && (flags & flagBit) != 0);
    }

    static bool GetBool( Dictionary<string, string> parameters, string key )
    {
        return parameters.TryGetValue( key, out var value ) && value.Trim() is "1" or "true";
    }

    static float GetFloat( Dictionary<string, string> parameters, string key, float defaultValue = 0f )
    {
        return parameters.TryGetValue( key, out var value ) && float.TryParse( value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result )
            ? result
            : defaultValue;
    }

    static int GetInt( Dictionary<string, string> parameters, string key, int defaultValue = 0 )
    {
        return parameters.TryGetValue( key, out var value ) && int.TryParse( value.Trim(), out var result ) ? result : defaultValue;
    }

    static Vector3? GetColor( Dictionary<string, string> parameters, string key )
    {
        if ( !parameters.TryGetValue( key, out var value ) )
            return null;

        value = value.Trim();

        bool isBracket = value.StartsWith( '[' ) && value.EndsWith( ']' );
        bool isBrace = value.StartsWith( '{' ) && value.EndsWith( '}' );

        if ( !isBracket && !isBrace )
            return null;

        var inner = value[1..^1].Trim();
        var parts = inner.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
        if ( parts.Length < 3 )
            return null;

        if ( !float.TryParse( parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r ) ) return null;
        if ( !float.TryParse( parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g ) ) return null;
        if ( !float.TryParse( parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b ) ) return null;

        if ( isBrace )
        {
            r /= 255f;
            g /= 255f;
            b /= 255f;
        }

        return new Vector3( r, g, b );
    }

    static Vector3? GetVector3( Dictionary<string, string> parameters, string key )
    {
        if ( !parameters.TryGetValue( key, out var value ) )
            return null;

        value = value.Trim();
        if ( value.StartsWith( '[' ) && value.EndsWith( ']' ) )
            value = value[1..^1].Trim();

        var parts = value.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
        return parts.Length < 3
            ? null
            : !float.TryParse( parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x )
            ? null
            : !float.TryParse( parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y )
            ? null
            : !float.TryParse( parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z ) ? null : new Vector3( x, y, z );
    }

    static (string Shader, Dictionary<string, string> Parameters) ParseVmt( byte[] data )
    {
        var text = Encoding.UTF8.GetString( data );
        var pos = 0;

        SkipWhitespaceAndComments( text, ref pos );

        var shader = ReadToken( text, ref pos );
        if ( string.IsNullOrEmpty( shader ) )
            return (null, null);

        SkipWhitespaceAndComments( text, ref pos );

        if ( pos >= text.Length || text[pos] != '{' )
            return (null, null);

        pos++;

        var parameters = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
        ParseBlock( text, ref pos, parameters );

        return (shader, parameters);
    }

    static void ParseBlock( string text, ref int pos, Dictionary<string, string> parameters )
    {
        while ( pos < text.Length )
        {
            SkipWhitespaceAndComments( text, ref pos );

            if ( pos >= text.Length )
                break;

            if ( text[pos] == '}' )
            {
                pos++;
                break;
            }

            var key = ReadToken( text, ref pos );
            if ( string.IsNullOrEmpty( key ) )
                break;

            SkipWhitespaceAndComments( text, ref pos );

            if ( pos >= text.Length )
                break;

            if ( text[pos] == '{' )
            {
                pos++;
                SkipBlock( text, ref pos );
                continue;
            }

            var value = ReadToken( text, ref pos );
            if ( value is null )
                break;

            if ( key.StartsWith( '$' ) )
                key = key[1..];

            parameters.TryAdd( key, value );
        }
    }

    static void SkipBlock( string text, ref int pos )
    {
        int depth = 1;
        while ( pos < text.Length && depth > 0 )
        {
            if ( text[pos] == '{' ) depth++;
            else if ( text[pos] == '}' ) depth--;
            pos++;
        }
    }

    static void SkipWhitespaceAndComments( string text, ref int pos )
    {
        while ( pos < text.Length )
        {
            if ( char.IsWhiteSpace( text[pos] ) )
            {
                pos++;
                continue;
            }

            if ( pos + 1 < text.Length && text[pos] == '/' && text[pos + 1] == '/' )
            {
                while ( pos < text.Length && text[pos] != '\n' )
                    pos++;
                continue;
            }

            break;
        }
    }

    static string ReadToken( string text, ref int pos )
    {
        if ( pos >= text.Length )
            return null;

        if ( text[pos] == '"' )
        {
            pos++;
            var start = pos;
            while ( pos < text.Length && text[pos] != '"' )
            {
                if ( text[pos] == '\\' && pos + 1 < text.Length )
                    pos++;
                pos++;
            }
            var token = text[start..pos];
            if ( pos < text.Length ) pos++;
            return token;
        }

        {
            var start = pos;
            while ( pos < text.Length && !char.IsWhiteSpace( text[pos] ) && text[pos] != '{' && text[pos] != '}' && text[pos] != '"' )
                pos++;
            return pos > start ? text[start..pos] : null;
        }
    }
}
