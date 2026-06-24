namespace NeuroSpeech.NSDiskCache;

public sealed class TempFile : IDisposable
{
    public readonly string FullPath;
    private readonly bool deleteOnExit;
    private readonly FileInfo info;

    public string Name => info.Name;

    public string Ext => info.Extension;

    public DateTime LastWrite => Exists ? info.LastWriteTime : DateTime.MinValue;

    public bool Exists
    {
        get
        {
            info.Refresh();
            return info.Exists;
        }
    }

    public long Size
    {
        get
        {
            info.Refresh();
            return info.Length;
        }
    }

    public TempFile(string path, bool deleteOnExit = true)
    {
        this.FullPath = path;
        this.deleteOnExit = deleteOnExit;
        this.info = new FileInfo(path);
    }

    public async Task CopyFrom(Stream stream)
    {
        using var fs = this.info.OpenWrite();
        await stream.CopyToAsync(fs);
    }

    public async Task CopyTo(Stream stream)
    {
        using var fs = this.info.OpenRead();
        await stream.CopyToAsync(fs);
    }

    public void Move(string destPath)
    {
        System.IO.File.Move(FullPath, destPath);
    }

    public void Link(string destPath)
    {
        System.IO.File.CreateSymbolicLink(destPath, this.FullPath);
    }

    public void Link(TempFile destPath)
    {
        System.IO.File.CreateSymbolicLink(destPath.FullPath, this.FullPath);
    }

    public void Delete()
    {
        info.Delete();
    }

    public void Dispose()
    {
        if (deleteOnExit && this.Exists)
        {
            info.Delete();
        }
    }

    internal void UpdateTime()
    {
        System.IO.File.SetLastWriteTimeUtc(this.FullPath, DateTime.UtcNow);
    }
}
