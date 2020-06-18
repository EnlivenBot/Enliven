using CommandLine;

namespace Bot.Utilities {
    public class CmdOptions {
        [Option('o', "observer", Default = false, Required = false, HelpText = "This instance will not respond to commands or interact with the user")]
        public bool Observer { get; set; }
        
        [Option("token", Default = null, Required = false, HelpText = "Overrides token from config file")]
        public string BotToken { get; set; }
    }
}