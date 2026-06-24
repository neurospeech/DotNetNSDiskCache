using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            var start = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long total = 0;

            var min = this.Options.MinAge;

            List<(DateTime time, string path, long size)>? all = null;
            long freeSize = 0;
            int deleted = 0;

            for (int i = this.Options.MaxAge; i >= min; i--)
            {
                var s = await this.GetFileSystemStatsAsync(this.Options.Root);
                freeSize = s.Bavail * s.Bsize;

                if (freeSize >= this.Options.MinSize)
                {
                    break;
                }

                all ??= await this.GetFileStatsAsync();

                try
                {
                    var keep = DateTime.Now.AddSeconds(-this.Options.KeepTTLSeconds * i);
                    var pending = new List<(DateTime time, string path, long size)>();

                    foreach (var file in all)
                    {
                        if (file.time < keep)
                        {
                            File.Delete(file.path);
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
                Console.WriteLine($"{this.Options.Root} ({deleted}/{all?.Count + deleted}) cleaned, {total.ToKMBString()} freed in {DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - start}ms.");
            }
            else
            {
                if (all?.Count > 0)
                {
                    if (this.Options.MinSize == long.MaxValue)
                    {
                        Console.WriteLine($"Cleaning {this.Options.Root} with entries ({all.Count}) for {DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - start}ms.");
                    }
                    else
                    {
                        Console.WriteLine($"Cleaning {this.Options.Root} with entries ({all.Count}) for {DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - start}ms as {freeSize.ToKMBString()} < {this.Options.MinSize.ToKMBString()}.");
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

    async Task GetFileStats()
    {
    }

    private async Task<List<(string path, long size, long time)>> GetFileStatsAsync()
    {
        var min = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - this.MinAge * this.KeepTTLSeconds * 1000;
        var files = new List<(string path, long size, long time)>();

        try
        {
            var directoryInfo = new DirectoryInfo(this.Root);
            var filesInfo = directoryInfo.GetFiles("*", SearchOption.AllDirectories);

            foreach (var file in filesInfo)
            {
                var time = file.CreationTimeUtc.Ticks / TimeSpan.TicksPerMillisecond;

                if (time > min)
                {
                    continue;
                }

                files.Add((path: file.FullName, size: file.Length, time: time));
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions appropriately
            Console.Error.WriteLine(ex);
        }

        return files;
    }

}
