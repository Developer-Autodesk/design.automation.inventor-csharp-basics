using System;
using Inventor;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace debugPluginLocally
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var inv = new InventorConnector()) {
                InventorServer server = inv.GetInventorServer();

                try
                {
                    Console.WriteLine("Running locally...");
                    // run the plugin
                    DebugSamplePlugin(server);
                }
                catch(Exception e)
                {
                    string message = $"Exception: {e.Message}";
                    if (e.InnerException != null)
                        message += $"{System.Environment.NewLine}    Inner exception: {e.InnerException.Message}";

                    Console.WriteLine(message);
                }
                finally
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        Console.WriteLine("Press any key to exit. All documents will be closed.");
                        Console.ReadKey();
                    }
                }
            }
        }

        /// <summary>
        /// Opens box.ipt and runs samplePlugin
        /// </summary>
        /// <param name="app"></param>
        private static void DebugSamplePlugin(InventorServer app)
        {
            // get solution directory
            string solutiondir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName;
            string inputFilesDir = System.IO.Path.Combine(solutiondir, @"clientApp\inputFiles\");
            string inputFilesLocalDir = System.IO.Path.Combine(solutiondir, @"debugPluginLocally\inputFiles\");

            // get box.ipt absolute path
            string boxPath = System.IO.Path.Combine(inputFilesDir, "box.ipt");
            string boxPathCopy = System.IO.Path.Combine(inputFilesLocalDir, "boxcopy.ipt");

            try
            {
                // delete an existing file
                System.IO.File.Delete(boxPathCopy);
            }
            catch (IOException)
            {
                Console.WriteLine("The specified file is in use. It might be open by Inventor");
                return;
            }

            // create a copy
            System.IO.File.Copy(boxPath, boxPathCopy);

            // open box.ipt by Inventor
            Document doc = app.Documents.Open(boxPathCopy);

            // get paramsSmall.json absolute path
            string paramsPathSmall = System.IO.Path.Combine(inputFilesDir, "paramsSmall.json");

            // get paramsLarge.json absolute path
            string paramsPathLarge = System.IO.Path.Combine(inputFilesDir, "paramsLarge.json");

            // create a name value map
            Inventor.NameValueMap map = app.TransientObjects.CreateNameValueMap();

            // add parameters into the map, do not change "_1". You may add more parameters "_2", "_3"...
            map.Add("_1", paramsPathSmall);
            map.Add("_2", paramsPathLarge);

            // create an instance of samplePlugin
            samplePlugin.SampleAutomation plugin = new samplePlugin.SampleAutomation(app);

            // run the plugin
            plugin.RunWithArguments(doc, map);
        }
    }
}
