namespace Common.Music {
    public class PlayerSnapshotParameters {
        public bool SavePlaylist { get; set; } = true;
    }

    public class PlayerShutdownParameters : PlayerSnapshotParameters {
        public static PlayerShutdownParameters Default => new();

        public bool ShutdownDisplays { get; set; } = true;
    }
}