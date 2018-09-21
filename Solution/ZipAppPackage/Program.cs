using System;
using System.IO;

namespace ZipAppPackage
{
    class Program
    {

        static string ZipSourceDir = null;
        static string ZipDestDir = null;
        static string ZipFile = null;

        static void RemoveOldZip()
        { 
            string file = ZipDestDir + "/" + ZipFile;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        static void ZipAppPackage()
        {
            if (!Directory.Exists(ZipDestDir))
            {
                Directory.CreateDirectory(ZipDestDir);
            }
            string file = ZipDestDir + "/" + ZipFile;
            try
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(ZipSourceDir, file);
            }
            catch (Exception e)
            {
                Console.WriteLine("Expeption zipping " + file + "\n" + e.ToString());
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("usage: ZipAppPackage.exe <zipSourceDir> <zipDestDir> <zipFile>");
            Environment.Exit(1);
        }

        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Incorrect number of command line arguments");
                PrintUsage();
            }

            ZipSourceDir = args[0];
            ZipDestDir = args[1];
            ZipFile = args[2];

            if (!System.IO.Directory.Exists(ZipSourceDir))
            {
                Console.WriteLine("ZipSourceDir not found: " + ZipSourceDir);
                PrintUsage();
            }

            RemoveOldZip();
            ZipAppPackage();
        }
    }
}
