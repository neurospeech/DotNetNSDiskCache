using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NSDiskCache;

internal class Dir
{

    internal static void EnsureDir(string folder)
    {
        var d = new DirectoryInfo(folder);
        EnsureDir(d);        
    }

    static void EnsureDir(DirectoryInfo d)
    {
        if (d.Exists)
        {
            return;
        }
        if (d.Parent ==null)
        {
            throw new InvalidOperationException("Cannot generate directory at root");
        }
        EnsureDir(d.Parent);
        d.Create();
    }
        

}
