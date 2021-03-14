namespace Bot.Config.Emoji {
    public class ProgressEmojiList {
        public ProgressEmojiList() { }
        
        public ProgressEmojiList(string part0, string part2, string part4, string part6, string part8, string part10) {
            Part0 = part0;
            Part2 = part2;
            Part4 = part4;
            Part6 = part6;
            Part8 = part8;
            Part10 = part10;
        }

        public ProgressEmojiList(string part) : this(part, part, part, part, part, part) { }
        public ProgressEmojiList(string empty, string full) : this(empty, empty, empty, full, full, full) { }
        public ProgressEmojiList(string empty, string half, string full) : this(empty, empty, half, half, full, full) { }
        public string Part0 { get; set; } = null!;
        public string Part2 { get; set; } = null!;
        public string Part4 { get; set; } = null!;
        public string Part6 { get; set; } = null!;
        public string Part8 { get; set; } = null!;
        public string Part10 { get; set; } = null!;
    }
}