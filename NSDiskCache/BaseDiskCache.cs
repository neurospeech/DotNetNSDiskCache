using System.Security.Cryptography;

namespace NeuroSpeech.NSDiskCache;

public class DiskCacheOptions
{

    public string? Root { get; set; }

    public int KeepTTLSeconds { get; set; }

    public long MinSize { get; set; }

    public int MinAge { get; set; }

    public int MaxAge { get; set; }

    public DiskCacheOptions()
    {
        this.KeepTTLSeconds = 3600;
        this.MinSize = long.MaxValue;
        this.MinAge = 1;
        this.MaxAge = 4;
    }

}

public class BaseDiskCache
{

    public readonly DiskCacheOptions Options;
    private readonly string folder;
    private readonly Timer _timer;

    private bool isCleaning = false;

    public BaseDiskCache(DiskCacheOptions? options = null)
    {
        Options = options = new DiskCacheOptions() {
            Root  = Path.Join(Path.GetTempPath(), "disk-cache"),
            KeepTTLSeconds = 3600,
            MinSize = long.MaxValue,
            MaxAge = 1,
            MinAge = 1,
        };
        if(options.KeepTTLSeconds < 15*60)
        {
            throw new ArgumentException($"KeepTTLSeconds cannot be less than 15 minutes");
        }
        if (options.MinSize < 1024*1024)
        {
            throw new ArgumentException($"MinSize cannot be less than 1GB");
        }
        if (options.MinAge < 0)
        {
            throw new ArgumentException($"MinAge cannot be less than zero");
        }
        if (options.MaxAge < this.Options.MinAge)
        {
            throw new ArgumentException($"MaxAge cannot be less than MinAge");
        }
        if (this.Options.Root == null)
        {
            throw new ArgumentException($"Root cannot be empty and must not be shared by any other process");
        }
        Dir.EnsureDir(options.Root);
        this.folder = options.Root;
        _timer = new Timer((w) => Task.Run(Clean), null, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
    }

    public async Task<TempFile> GetOrCreateAsync(string path, Func<TempFile,Task> taskFactory) {
        // lets create an atomic way

        var diskPath = new TempFile(GetDiskPath(path), false);
        if (diskPath.Exists)
        {
            diskPath.UpdateTime();
            return this.Link(diskPath);
        }

        using var _lock = await LockFile.LockAsync($"disk-cache:{this.folder}:{path}");
        diskPath = new TempFile(GetDiskPath(path), false);
        if (diskPath.Exists)
        {
            diskPath.UpdateTime();
            return this.Link(diskPath);
        }

        using var tempFile = new TempFile(GetDiskPath(Guid.NewGuid().ToString("N")));
        await taskFactory(tempFile);

        // this is atomic
        tempFile.Move(diskPath.FullPath);
        return this.Link(diskPath);
    }

    private TempFile Link(TempFile source)
    {
        using var tempFile = new TempFile(GetDiskPath(Guid.NewGuid().ToString("N")));
        source.Link(tempFile);
        return tempFile;
    }

    private string GetDiskPath(string path)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(path);
        Span<byte> hashDestination = stackalloc byte[SHA512.HashSizeInBytes];
        SHA512.HashData(buffer, hashDestination);

        // 2. Encodes and strips trailing padding automatically
        return Path.Join(this.folder, path + "." + Convert.ToHexString(hashDestination) + ".dat");
    }

    private async Task Clean()
    {
        if (isCleaning)
        {
            return;
        }
        isCleaning = true;

        var minAge = this.Options.MinAge;
        var maxAge = this.Options.MaxAge;

        try
        {
            var start = DateTime.UtcNow;
            long total = 0;

            var min = this.Options.MinAge;

            List<(DateTime time, FileInfo file, long size)>? all = null;
            long freeSize = 0;
            int deleted = 0;

            for (int i = this.Options.MaxAge; i >= min; i--)
            {

                var rootDir = new DirectoryInfo(this.Options.Root);
                var drive = new DriveInfo(rootDir.Root.FullName);

                if (freeSize >= drive.TotalFreeSpace)
                {
                    break;
                }

                await Task.Delay(100);

                all ??= this.GetFileStatsAsync();

                try
                {
                    var keep = DateTime.UtcNow.AddSeconds(-this.Options.KeepTTLSeconds * i);
                    var pending = new List<(DateTime time, FileInfo file, long size)>();

                    foreach (var file in all)
                    {
                        if (file.time < keep)
                        {
                            DeletePath(file.file);
                            deleted++;
                            total += file.size;
                            continue;
                        }
                        pending.Add(file);
                    }

                    all = pending;

                    if (all.Count == 0)
                    {
                        break;
                    }
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error);
                }
            }

            if (total > 0)
            {
                Console.WriteLine($"{this.Options.Root} ({deleted}/{all?.Count + deleted}) cleaned, {total.ToKMBString()} freed in {(DateTime.UtcNow - start).TotalMilliseconds}ms.");
            }
            else
            {
                if (all?.Count > 0)
                {
                    if (this.Options.MinSize == long.MaxValue)
                    {
                        Console.WriteLine($"Cleaning {this.Options.Root} with entries ({all.Count}) for {(DateTime.UtcNow - start).TotalMilliseconds}ms.");
                    }
                    else
                    {
                        Console.WriteLine($"Cleaning {this.Options.Root} with entries ({all.Count}) for {(DateTime.UtcNow - start).TotalMilliseconds}ms as {freeSize.ToKMBString()} < {this.Options.MinSize.ToKMBString()}.");
                    }
                }
            }

        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
        }
        finally
        {
            isCleaning = false;
        }
    }

    private void DeletePath(FileInfo file)
    {
        var parent = file.Directory;
        file.Delete();
        for(;;)
        {
            if (parent == null)
            {
                break;
            }
            if (parent.FullName == this.folder)
            {
                break;
            }
            var hasAny = parent.EnumerateFileSystemInfos().Any();
            if (hasAny)
            {
                break;
            }
            parent.Delete();
            parent = parent.Parent;
        }
    }

    private List<(DateTime time, FileInfo file, long size)> GetFileStatsAsync()
    {
        var min = DateTime.Now - (this.Options.MinAge * TimeSpan.FromSeconds(this.Options.KeepTTLSeconds));
        var files = new List<(DateTime, FileInfo file, long size)>();

        var directoryInfo = new DirectoryInfo(this.Options.Root);
        try
        {

            foreach (var entry in directoryInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if (entry is not FileInfo file)
                {
                    continue;
                }
                var time = file.LastWriteTimeUtc;

                if (time > min)
                {
                    continue;
                }

                files.Add((time, file, file.Length));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return files;

    }

}
