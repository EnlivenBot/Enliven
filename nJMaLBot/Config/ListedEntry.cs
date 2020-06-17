using System.Collections.Generic;

namespace Bot.Config {
    public class ListedEntry {
        public string Id { get; set; }
        public List<string> Data { get; set; } = new List<string>();
    }
}