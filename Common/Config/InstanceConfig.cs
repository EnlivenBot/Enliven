using System.Collections.Generic;

namespace Common.Config;

public class InstanceConfig
{
    /// <summary>
    /// Instance bot name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Your Discord bot token
    /// </summary>
    public string BotToken { get; set; } = "Place your token here";

    /// <summary>
    /// Lavalink nodes credentials
    /// </summary>
    /// <example>
    /// {  
    ///      "Password": "youshallnotpass",  
    ///      "RestUri": "http://localhost:8080/",  
    ///      "WebSocketUri": "ws://localhost:8080/"
    ///      "Name": "Name will be displayed at player embed"
    ///  }
    /// </example>
    public List<LavalinkNodeInfo> LavalinkNodes { get; set; } = new();

    /// <summary>
    /// Allow manipulation modules for current bot instance
    /// </summary>
    /// <example>
    /// List of available modules
    /// !logging - disables logging
    /// </example>
    public List<string> Modules { get; set; } = new();
}

public static class InstanceConfigExtensions
{
    public static bool IsLoggingEnabled(this InstanceConfig config)
    {
        return !config.Modules.Contains("!logging");
    }
}