using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NSDiskCache;

public class TempFolder : IDisposable
{

    static long id = 0;
    public string folder { get; private set; }

    public TempFolder(string? suffix, string? root = null)
    {
        root ??= Path.Join( Path.GetTempPath(), DateTime.UtcNow.Ticks.ToString());
        if (id == 0)
        {
            id = DateTime.UtcNow.Ticks;
        }
        Exception? last = null;
        for (int i = 0; i < 100; i++)
        {
            var folder = Path.Join(root, suffix?.Length > 0 ? $"tf-{suffix}-{id++}" : $"tf-{id++}");
            if(System.IO.Directory.Exists(folder))
            {
                continue;
            }
            try
            {
                Dir.EnsureDir(folder);
                this.folder = folder;
            } catch (Exception ex)
            {
                last = ex;
                continue;
            }
        }
        if (last != null)
        {
            throw last;
        }
        throw new InvalidOperationException("Could not create temp folder");
    }

    public void Dispose()
    {
        if (this.folder != null)
        {
            Directory.Delete(this.folder, true);
            this.folder = null!;
        }
    }
}
