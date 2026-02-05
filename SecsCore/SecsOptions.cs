namespace SecsCore;

public enum HsmsConnectionMode
{
    Active,  // 主动模式 (Host): 主动去连别人
    Passive  // 被动模式 (Equipment/Simulator): 监听端口，等人来连
}

public class SecsOptions
{
    // 基础配置
    public HsmsConnectionMode Mode { get; set; } = HsmsConnectionMode.Active;
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;

    // 身份配置
    public ushort DeviceId { get; set; } = 0;

    // 超时配置 (毫秒)
    public int T3 { get; set; } = 45000; // Reply Timeout
    public int T5 { get; set; } = 10000; // Connect Separation Timeout
    public int T6 { get; set; } = 5000;  // Control Transaction Timeout
    public int T7 { get; set; } = 10000; // Not Selected Timeout
    public int T8 { get; set; } = 5000;  // Network Intercharacter Timeout

    // 心跳配置
    public int LinkTestInterval { get; set; } = 60000;
}