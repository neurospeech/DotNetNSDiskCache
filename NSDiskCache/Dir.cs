namespace NeuroSpeech.NSDiskCache;

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
