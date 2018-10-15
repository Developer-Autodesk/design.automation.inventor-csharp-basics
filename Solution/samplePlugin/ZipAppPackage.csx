#r "System.IO.Compression.ZipFile"

public class ZipAppPackage
{
    string ZipSourceDir = null;
    string ZipDestDir = null;
    string ZipFile = null;

    public ZipAppPackage()
    {
        string[] args = Environment.GetCommandLineArgs();

        if (args.Length != 5)
        {
            Console.WriteLine("Incorrect number of command line arguments");
            PrintUsage();
        }

        ZipSourceDir = args[2];
        ZipDestDir = args[3];
        ZipFile = args[4];

        if (!System.IO.Directory.Exists(ZipSourceDir))
        {
            Console.WriteLine("ZipSourceDir not found: " + ZipSourceDir);
            PrintUsage();
        }

        RemoveOldZip();
        CreateZipAppPackage();
    }

    void PrintUsage()
    {
        Console.WriteLine("usage: csi.exe $(SolutionDir)\\samplePlugin\\ZipApp.csx <zipSourceDir> <zipDestDir> <zipFile>");
        Environment.Exit(1);
    }

    void RemoveOldZip()
    {
        string file = Path.Combine(ZipDestDir, ZipFile);

        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }

    void CreateZipAppPackage()
    {
        if (!System.IO.Directory.Exists(ZipDestDir))
        {
            System.IO.Directory.CreateDirectory(ZipDestDir);
        }

        string file = Path.Combine(ZipDestDir, ZipFile);

        try
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(ZipSourceDir, file);
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception when zipping " + file + "\n" + e.ToString());
        }
    }
}

ZipAppPackage zip = new ZipAppPackage();