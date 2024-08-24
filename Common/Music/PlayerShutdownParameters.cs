namespace Common.Music;

public class PlayerShutdownParameters
{
    public static PlayerShutdownParameters Default => new();

    public bool SavePlaylist { get; set; } = true;
    public bool ShutdownDisplays { get; set; } = true;
    public bool RestartPlayer { get; set; }
}