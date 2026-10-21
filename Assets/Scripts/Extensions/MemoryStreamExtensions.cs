using System.IO;

public static class MemoryStreamExtensions
{
    public static bool IsAtEnd(this MemoryStream reader)
        => reader.Position >= reader.Length;    
}
