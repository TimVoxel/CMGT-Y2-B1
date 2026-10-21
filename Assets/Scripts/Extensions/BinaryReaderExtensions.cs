using System.IO;
using UnityEngine;

public static class BinaryReaderExtensions
{
    public static bool IsAtEnd(this BinaryReader reader)
        => reader.BaseStream.Position >= reader.BaseStream.Length;    

    public static Vector3 ReadVector3(this BinaryReader reader)
    {
        var x = reader.ReadSingle();
        var y = reader.ReadSingle();
        var z = reader.ReadSingle();
        return new Vector3(x, y, z);
    }

    public static Quaternion ReadQuaternion(this BinaryReader reader)
    {
        var x = reader.ReadSingle();
        var y = reader.ReadSingle();
        var z = reader.ReadSingle();
        var w = reader.ReadSingle();
        return new Quaternion(x, y, z, w);
    }
}