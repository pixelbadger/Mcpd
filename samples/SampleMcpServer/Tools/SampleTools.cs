using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SampleMcpServer.Tools;

[McpServerToolType]
public static class SampleTools
{
    [McpServerTool, Description("Echoes the input back to the client.")]
    public static string Echo(string message) => message;

    [McpServerTool, Description("Returns the current UTC time.")]
    public static object Time() => new
    {
        utc = DateTimeOffset.UtcNow.ToString("o"),
        unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
}
