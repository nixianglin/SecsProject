namespace SecsCore;

public class SecsMessage
{
    public SecsHeader Header { get; set; } // 允许修改 SystemBytes
    public SecsItem? Body { get; }

    // 构造普通数据消息
    public SecsMessage(byte stream, byte function, bool replyExpected, SecsItem? body = null)
    {
        Header = new SecsHeader
        {
            Stream = stream,
            Function = function,
            StreamOrWaitBit = replyExpected,
            SType = HsmsType.DataMessage
        };
        Body = body;
    }

    // 构造内部使用的 (接收到的) 消息
    internal SecsMessage(SecsHeader header, SecsItem? body)
    {
        Header = header;
        Body = body;
    }

    public override string ToString() => $"S{Header.Stream}F{Header.Function}";
}