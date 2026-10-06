namespace Visualizer.App.Audio;

public sealed class AudioDeviceInfo
{
    public AudioDeviceInfo(string id, string name, bool isDefault)
    {
        Id = id;
        Name = name;
        IsDefault = isDefault;
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsDefault { get; }

    public override string ToString() => IsDefault ? $"{Name} (Default)" : Name;
}
