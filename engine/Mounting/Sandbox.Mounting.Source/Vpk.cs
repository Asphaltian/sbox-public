using System;
using System.Text;

namespace VpkLib;

public class VpkEntry
{
    public string FullPath;
    public uint Crc;
    public ushort PreloadBytes;
    public ushort ArchiveIndex;
    public uint EntryOffset;
    public uint EntryLength;
    public byte[] PreloadData;

    public int TotalSize => (int)(PreloadBytes + EntryLength);
}

public class VpkArchive : IDisposable
{
    const uint HeaderMarker = 0x55aa1234;
    const ushort EmbeddedInDirFile = 0x7fff;
    const ushort EntryTerminator = 0xffff;

    private readonly string _dirFilePath;
    private readonly Dictionary<string, VpkEntry> _entryLookup = new( StringComparer.OrdinalIgnoreCase );
    private readonly Dictionary<int, FileStream> _archiveStreams = [];

    public string BaseName { get; private set; }
    public string Directory { get; private set; }
    public int Version { get; private set; }
    public List<VpkEntry> Entries { get; private set; } = [];
    public bool IsValid { get; private set; }

    int DirectorySize;
    int EmbeddedDataOffset;

    public VpkArchive( string dirFilePath )
    {
        _dirFilePath = dirFilePath;
        Directory = Path.GetDirectoryName( dirFilePath ) ?? "";

        var fileName = Path.GetFileNameWithoutExtension( dirFilePath );
        BaseName = fileName.EndsWith( "_dir", StringComparison.OrdinalIgnoreCase )
            ? fileName[..^4]
            : fileName;

        try
        {
            ReadDirectory();
            IsValid = true;
        }
        catch
        {
            IsValid = false;
        }
    }

    private void ReadDirectory()
    {
        using var stream = File.OpenRead( _dirFilePath );
        using var reader = new BinaryReader( stream, Encoding.ASCII );

        var marker = reader.ReadUInt32();
        int headerSize;

        if ( marker == HeaderMarker )
        {
            Version = reader.ReadInt32();
            DirectorySize = reader.ReadInt32();

            if ( Version == 2 )
            {
                reader.ReadInt32(); // embedded chunk size
                reader.ReadInt32(); // chunk hashes size
                reader.ReadInt32(); // self hashes size
                reader.ReadInt32(); // signature size
                headerSize = 7 * 4;
            }
            else if ( Version == 1 )
            {
                headerSize = 3 * 4;
            }
            else
            {
                throw new InvalidDataException( $"Unknown VPK version: {Version}" );
            }
        }
        else
        {
            Version = 0;
            stream.Seek( 0, SeekOrigin.Begin );
            DirectorySize = (int)stream.Length;
            headerSize = 0;
        }

        EmbeddedDataOffset = headerSize + DirectorySize;

        ReadTree( reader );
    }

    private void ReadTree( BinaryReader reader )
    {
        while ( true )
        {
            var extension = ReadNullTerminatedString( reader );
            if ( string.IsNullOrEmpty( extension ) )
                break;

            while ( true )
            {
                var path = ReadNullTerminatedString( reader );
                if ( string.IsNullOrEmpty( path ) )
                    break;

                while ( true )
                {
                    var filename = ReadNullTerminatedString( reader );
                    if ( string.IsNullOrEmpty( filename ) )
                        break;

                    var entry = new VpkEntry
                    {
                        Crc = reader.ReadUInt32(),
                        PreloadBytes = reader.ReadUInt16(),
                        ArchiveIndex = reader.ReadUInt16(),
                        EntryOffset = reader.ReadUInt32(),
                        EntryLength = reader.ReadUInt32()
                    };

                    var terminator = reader.ReadUInt16();
                    if ( terminator != EntryTerminator )
                        throw new InvalidDataException( $"Expected entry terminator 0xFFFF, got 0x{terminator:X4}" );

                    if ( entry.PreloadBytes > 0 )
                        entry.PreloadData = reader.ReadBytes( entry.PreloadBytes );

                    entry.FullPath = path == " "
                        ? $"{filename}.{extension}"
                        : $"{path}/{filename}.{extension}";

                    Entries.Add( entry );
                    _entryLookup[entry.FullPath] = entry;
                }
            }
        }
    }

    private static string ReadNullTerminatedString( BinaryReader reader )
    {
        var sb = new StringBuilder();

        while ( true )
        {
            var b = reader.ReadByte();
            if ( b == 0 ) break;
            sb.Append( (char)b );
        }

        return sb.ToString();
    }

    public bool FileExists( string path )
    {
        return _entryLookup.ContainsKey( path );
    }

    public VpkEntry FindEntry( string path )
    {
        return _entryLookup.TryGetValue( path, out var entry ) ? entry : null;
    }

    public byte[] GetFileBytes( string path )
    {
        var entry = FindEntry( path );
        if ( entry is null )
            return null;

        var data = new byte[entry.TotalSize];
        var offset = 0;

        if ( entry.PreloadData is not null && entry.PreloadBytes > 0 )
        {
            Array.Copy( entry.PreloadData, 0, data, 0, entry.PreloadBytes );
            offset += entry.PreloadBytes;
        }

        if ( entry.EntryLength > 0 )
        {
            var stream = GetArchiveStream( entry.ArchiveIndex );
            if ( stream is null )
                return null;

            var seekOffset = entry.ArchiveIndex == EmbeddedInDirFile
                ? EmbeddedDataOffset + entry.EntryOffset
                : entry.EntryOffset;

            lock ( stream )
            {
                stream.Seek( seekOffset, SeekOrigin.Begin );
                stream.ReadExactly( data, offset, (int)entry.EntryLength );
            }
        }

        return data;
    }

    private FileStream GetArchiveStream( int archiveIndex )
    {
        if ( _archiveStreams.TryGetValue( archiveIndex, out var existing ) )
            return existing;

        var archivePath = archiveIndex == EmbeddedInDirFile
            ? _dirFilePath
            : Path.Combine( Directory, $"{BaseName}_{archiveIndex:D3}.vpk" );

        if ( !File.Exists( archivePath ) )
            return null;

        var stream = new FileStream( archivePath, FileMode.Open, FileAccess.Read, FileShare.Read );
        _archiveStreams[archiveIndex] = stream;
        return stream;
    }

    public void Dispose()
    {
        foreach ( var stream in _archiveStreams.Values )
            stream?.Dispose();

        _archiveStreams.Clear();
        GC.SuppressFinalize( this );
    }
}
