namespace Doorbell.Api.Demo;

public sealed class DemoOptions
{
    public string SampleImageRelativePath { get; set; } =
        "wwwroot/demo/media/sample-snapshot.jpg";

    public string SoundRelativePath { get; set; } =
        "wwwroot/demo/media/chime.mp3";

    public string SnapshotResolution { get; set; } = "640x480";

    public int SnapshotTimeoutSeconds { get; set; } = 8;

    public int MaxAttempts { get; set; } = 3;
}