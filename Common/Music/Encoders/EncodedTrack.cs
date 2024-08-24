namespace Common.Music.Encoders;

public class EncodedTrack
{
    public EncodedTrack(int encoderId, byte[] data)
    {
        EncoderId = encoderId;
        Data = data;
    }

    public int EncoderId { get; set; }
    public byte[] Data { get; set; }
}