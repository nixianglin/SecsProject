using SecsCore.Items;

namespace SecsCore;

public static class Sml
{
    // List & String
    public static SecsItem L(params SecsItem[] items) => new ItemL(items);
    public static SecsItem A(string value) => new ItemA(value);

    // Binary
    public static SecsItem B(params byte[] value) => new ItemBinary(value);
    public static SecsItem Boolean(bool value) => new ItemBoolean(value);

    // Unsigned Integers
    public static SecsItem U1(params byte[] value) => new ItemU1(value);
    public static SecsItem U2(params ushort[] value) => new ItemU2(value);
    public static SecsItem U4(params uint[] value) => new ItemU4(value);
    public static SecsItem U8(params ulong[] value) => new ItemU8(value);

    // Signed Integers
    public static SecsItem I1(params sbyte[] value) => new ItemI1(value);
    public static SecsItem I2(params short[] value) => new ItemI2(value);
    public static SecsItem I4(params int[] value) => new ItemI4(value);
    public static SecsItem I8(params long[] value) => new ItemI8(value);

    // Floating Point
    public static SecsItem F4(params float[] value) => new ItemF4(value);
    public static SecsItem F8(params double[] value) => new ItemF8(value);
}