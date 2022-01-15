using System.Collections.Generic;
using Common.Music;

namespace Common.Config {
    public class InstanceConfig : ConfigBase {
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
        public List<LavalinkNodeInfo> LavalinkNodes { get; set; } = new List<LavalinkNodeInfo>();
    }
}