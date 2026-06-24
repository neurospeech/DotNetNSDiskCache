using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NSDiskCache;

public class DiskCacheOptions
{

    public string? Root { get; set; }

    public int KeepTTLSeconds { get; set; }

    public long MinSize { get; set; }

    public int MinAge { get; set; }

    public int MaxAge { get; set; }

}

public class BaseDiskCache
{

    public readonly DiskCacheOptions Options;
    private readonly string folder;
    private readonly Timer _timer;

    private bool isCleaning =false;

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
        if (options.MaxAge < 0)
        {
            throw new ArgumentException($"MaxAge cannot be less than zero");
        }
        if (this.Options.Root == null)
        {
            throw new ArgumentException($"Root cannot be empty and must not be shared by any other process");
        }
        Dir.EnsureDir(options.Root);
        this.folder = options.Root;
        _timer = new Timer((w) => Task.Run(Clean), null, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
    }

    public async Task<string> GetOrCreateAsync(string path, Func<string,Task>)

    private async Task Clean()
    {
        if (isCleaning)
        {
            return;
        }
        isCleaning = true;
        try
        {
            var tmp = this.Options.Root + "_deleted_" + DateTime.UtcNow.Ticks;
            System.IO.Directory.Move(this.folder, tmp);
            Dir.EnsureDir(this.folder);
            await Task.Delay(1000);
            System.IO.Directory.Delete(tmp, true);

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

}
