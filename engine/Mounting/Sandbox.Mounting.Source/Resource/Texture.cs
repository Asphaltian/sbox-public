using System;

class VtfTextureLoader( string gameDir, string fileName ) : ResourceLoader<GameMount>
{
    protected override object Load()
    {
        var data = Host.GetFileBytes( gameDir, fileName );
        return data is null || data.Length < 64 ? null : (object)VtfReader.CreateTexture( data );
    }
}

file static class VtfReader
{
    const uint VtfSignature = 0x00465456; // "VTF\0"
    const uint FlagEnvironmentMap = 0x4000;
    const uint ImageResource = 0x30;

    static readonly HashSet<int> SupportedFormats =
    [
        0, 1, 2, 3, 4, 5, 6, 8, 11, 12, 13, 14, 15, 16, 17, 18, 19, 21,
        24, 25, 27, 28, 29, 36, 37
    ];

    static int RemapFormat( int rawFormat )
    {
        return rawFormat switch
        {
            20 => 13,
            37 => 36,
            38 => 37,
            _ => rawFormat
        };
    }

    public static Texture CreateTexture( byte[] data )
    {
        using var ms = new MemoryStream( data );
        using var reader = new BinaryReader( ms );

        if ( reader.ReadUInt32() != VtfSignature )
            return null;

        var versionMajor = reader.ReadInt32();
        var versionMinor = reader.ReadInt32();
        var headerSize = reader.ReadInt32();

        if ( versionMajor != 7 )
            return null;

        var width = (int)reader.ReadUInt16();
        var height = (int)reader.ReadUInt16();
        var flags = reader.ReadUInt32();
        var numFrames = (int)reader.ReadUInt16();
        reader.ReadUInt16(); // startFrame

        ms.Seek( 20, SeekOrigin.Current ); // padding + reflectivity (VectorAligned)
        ms.Seek( 4, SeekOrigin.Current ); // bumpScale

        var rawFormat = RemapFormat( reader.ReadInt32() );
        var numMipLevels = (int)reader.ReadByte();
        var lowResFormat = RemapFormat( reader.ReadInt32() );
        var lowResWidth = (int)reader.ReadByte();
        var lowResHeight = (int)reader.ReadByte();

        int depth = 1;
        if ( versionMinor >= 2 )
            depth = Math.Max( 1, (int)reader.ReadUInt16() );

        if ( width <= 0 || height <= 0 || numMipLevels <= 0 )
            return null;

        if ( depth > 1 )
            return null;

        if ( !SupportedFormats.Contains( rawFormat ) )
            return null;

        var imageFormat = (ImageFormat)rawFormat;

        var imageDataOffset = FindImageDataOffset( reader, ms, versionMinor, headerSize, lowResWidth, lowResHeight, lowResFormat );
        if ( imageDataOffset < 0 || imageDataOffset >= data.Length )
            return null;

        var mipSizes = new int[numMipLevels];
        for ( int mip = 0; mip < numMipLevels; mip++ )
        {
            var mipW = Math.Max( 1, width >> mip );
            var mipH = Math.Max( 1, height >> mip );
            mipSizes[mip] = ComputeImageSize( mipW, mipH, imageFormat );
        }

        var totalPerFace = mipSizes.Sum();
        bool isCubemap = (flags & FlagEnvironmentMap) != 0;

        if ( isCubemap )
            return CreateCubemap( ms, data, imageDataOffset, width, height, imageFormat, numMipLevels, numFrames, mipSizes, totalPerFace, versionMinor );

        return CreateTexture2D( ms, data, imageDataOffset, width, height, imageFormat, numMipLevels, numFrames, mipSizes, totalPerFace );
    }

    static int FindImageDataOffset( BinaryReader reader, MemoryStream ms, int versionMinor, int headerSize, int lowResWidth, int lowResHeight, int lowResFormat )
    {
        if ( versionMinor >= 3 )
        {
            ms.Seek( 3, SeekOrigin.Current ); // pad4
            var numResources = reader.ReadInt32();

            ms.Seek( 0x50, SeekOrigin.Begin );

            for ( int i = 0; i < numResources; i++ )
            {
                var resType = reader.ReadUInt32();
                var resData = reader.ReadUInt32();

                if ( (resType & 0x00FFFFFF) == ImageResource )
                    return (int)resData;
            }

            return -1;
        }

        var lowResSize = (lowResWidth > 0 && lowResHeight > 0 && lowResFormat >= 0)
            ? ComputeImageSize( lowResWidth, lowResHeight, (ImageFormat)lowResFormat )
            : 0;

        return headerSize + lowResSize;
    }

    static Texture CreateTexture2D( MemoryStream ms, byte[] data, int imageDataOffset, int width, int height, ImageFormat imageFormat, int numMipLevels, int numFrames, int[] mipSizes, int totalPerFace )
    {
        var result = new byte[totalPerFace];

        if ( numFrames <= 1 )
        {
            if ( imageDataOffset + totalPerFace > data.Length )
                return null;

            Array.Copy( data, imageDataOffset, result, 0, totalPerFace );
        }
        else
        {
            int writeOffset = 0;
            ms.Seek( imageDataOffset, SeekOrigin.Begin );

            for ( int mip = numMipLevels - 1; mip >= 0; mip-- )
            {
                var mipSize = mipSizes[mip];
                ms.ReadExactly( result, writeOffset, mipSize );
                writeOffset += mipSize;
                ms.Seek( (long)mipSize * (numFrames - 1), SeekOrigin.Current );
            }
        }

        return Texture.Create( width, height, imageFormat )
            .WithData( result )
            .WithMips( numMipLevels )
            .WithStaticUsage()
            .Finish();
    }

    static Texture CreateCubemap( MemoryStream ms, byte[] data, int imageDataOffset, int width, int height, ImageFormat imageFormat, int numMipLevels, int numFrames, int[] mipSizes, int totalPerFace, int versionMinor )
    {
        var result = new byte[totalPerFace * 6];
        int faceCount = DetectFaceCount( data, imageDataOffset, numMipLevels, numFrames, mipSizes, versionMinor );

        ms.Seek( imageDataOffset, SeekOrigin.Begin );

        int writeOffset = 0;

        for ( int mip = numMipLevels - 1; mip >= 0; mip-- )
        {
            var mipSize = mipSizes[mip];

            for ( int frame = 0; frame < numFrames; frame++ )
            {
                for ( int face = 0; face < faceCount; face++ )
                {
                    if ( frame == 0 && face < 6 )
                    {
                        ms.ReadExactly( result, writeOffset, mipSize );
                        writeOffset += mipSize;
                    }
                    else
                    {
                        ms.Seek( mipSize, SeekOrigin.Current );
                    }
                }
            }
        }

        return Texture.CreateCube( width, height, imageFormat )
            .WithData( result )
            .WithMips( numMipLevels )
            .WithStaticUsage()
            .Finish();
    }

    static int DetectFaceCount( byte[] data, int imageDataOffset, int numMipLevels, int numFrames, int[] mipSizes, int versionMinor )
    {
        if ( versionMinor < 1 )
            return 6;

        long totalWith7 = 0;
        for ( int mip = 0; mip < numMipLevels; mip++ )
            totalWith7 += (long)mipSizes[mip] * numFrames * 7;

        if ( imageDataOffset + totalWith7 <= data.Length )
            return 7;

        return 6;
    }

    static int ComputeImageSize( int width, int height, ImageFormat format )
    {
        switch ( format )
        {
            case ImageFormat.DXT1:
            case ImageFormat.ATI1N:
                {
                    var bw = Math.Max( 1, (width + 3) / 4 );
                    var bh = Math.Max( 1, (height + 3) / 4 );
                    return bw * bh * 8;
                }

            case ImageFormat.DXT3:
            case ImageFormat.DXT5:
            case ImageFormat.ATI2N:
                {
                    var bw = Math.Max( 1, (width + 3) / 4 );
                    var bh = Math.Max( 1, (height + 3) / 4 );
                    return bw * bh * 16;
                }

            case ImageFormat.RGBA8888:
            case ImageFormat.ABGR8888:
            case ImageFormat.ARGB8888:
            case ImageFormat.BGRA8888:
            case ImageFormat.BGRX8888:
            case ImageFormat.R32F:
                return width * height * 4;

            case ImageFormat.RGB888:
            case ImageFormat.BGR888:
                return width * height * 3;

            case ImageFormat.RGB565:
            case ImageFormat.BGR565:
            case ImageFormat.BGRA4444:
            case ImageFormat.BGRX5551:
            case ImageFormat.BGRA5551:
            case ImageFormat.IA88:
                return width * height * 2;

            case ImageFormat.I8:
            case ImageFormat.A8:
                return width * height;

            case ImageFormat.RGBA16161616F:
            case ImageFormat.RGBA16161616:
                return width * height * 8;

            case ImageFormat.RGBA32323232F:
                return width * height * 16;

            case ImageFormat.RGB323232F:
                return width * height * 12;

            default:
                return width * height * 4;
        }
    }
}
