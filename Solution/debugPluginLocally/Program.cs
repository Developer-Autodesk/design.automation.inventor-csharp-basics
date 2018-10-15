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
            // get project directory
            string projectdir = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;

            // get box.ipt absolute path
            string boxPath = System.IO.Path.Combine(projectdir, @"inputFiles\", "box.ipt");

            string boxPathCopy = System.IO.Path.Combine(projectdir, @"inputFiles\", "boxcopy.ipt");

            try
            {
                // delete an existing file
                System.IO.File.Delete(boxPathCopy);
            }
            catch (IOException e)
            {
                Console.WriteLine("The specified file is in use. It might be open by Inventor");
                return;
            }

            // create a copy
            System.IO.File.Copy(boxPath, boxPathCopy);

            // open box.ipt by Inventor
            Document doc = app.Documents.Open(boxPathCopy);

            // get params.json absolute path
            string paramsPath = System.IO.Path.Combine(projectdir, @"inputFiles\", "params.json");

            // create a name value map
            Inventor.NameValueMap map = app.TransientObjects.CreateNameValueMap();

            // add parameters into the map, do not change "_1". You may add more parameters "_2", "_3"...
            map.Add("_1", paramsPath);

            // create an instance of samplePlugin
            samplePlugin.SampleAutomation plugin = new samplePlugin.SampleAutomation(app);

            // run the plugin
            plugin.RunWithArguments(doc, map);
        }
    }
}
