namespace SecsCore;

public enum SecsFormat : byte
{
    List = 0x00,
    Binary = 0x20,    // 二进制流 (常用)
    Boolean = 0x24,
    ASCII = 0x40,
    JIS8 = 0x44,      // 极少用，通常视为 ASCII，这里先保留占位

    I8 = 0x60,        // long
    I1 = 0x64,        // sbyte
    I2 = 0x68,        // short
    I4 = 0x70,        // int

    F8 = 0x80,        // double
    F4 = 0x90,        // float

    U8 = 0xA0,        // ulong
    U1 = 0xA4,        // byte (注意与 Binary 的区别)
    U2 = 0xA8,        // ushort
    U4 = 0xB0         // uint
}
/// <summary>
/// HSMS 消息头中的 SType (Session Type) 定义
/// 参考: SEMI E37
/// </summary>
public enum HsmsType : byte
{
    DataMessage = 0,          // 普通 SECS-II 数据 (SxFy)
    SelectReq = 1,            // 握手请求
    SelectRsp = 2,            // 握手应答
    DeselectReq = 3,          // 断开请求
    DeselectRsp = 4,          // 断开应答
    LinktestReq = 5,          // 心跳请求
    LinktestRsp = 6,          // 心跳应答
    RejectReq = 7,            // 拒绝 (格式错误等)
    SeparateReq = 9           // 立即断开
}