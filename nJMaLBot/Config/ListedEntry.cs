using System.Collections.Generic;

namespace Bot.Config {
    public class ListedEntry {
        public string Id { get; set; } = null!;
        public List<string> Data { get; set; } = new List<string>();
    }
}