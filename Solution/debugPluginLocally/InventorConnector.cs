using System;
using System.Runtime.InteropServices;

using Inventor;

namespace debugPluginLocally
{
    class InventorConnector : IDisposable
    {
        Application _Instance;
        bool _CreatedByUs;
        const string PROG_ID = "Inventor.Application";

        public InventorConnector()
        {
        }

        public InventorServer GetInventorServer()
        {
            Connect();
            return _Instance as InventorServer;
        }

        private void Connect()
        {
            if (_Instance == null)
            {
                _Instance = TryConnectToRunningInstance();
                if (_Instance == null)
                {
                    _Instance = TryCreateInstance();
                    _CreatedByUs = _Instance != null;
                }
                if (_Instance == null)
                    throw new ApplicationException("Could not connect to Inventor.");
            }
        }

        private static Application TryCreateInstance()
        {
            Console.WriteLine("Trying to create instance of Inventor...");
            Application app = null;
            try
            {
                Type type = Type.GetTypeFromProgID(PROG_ID);
                app = Activator.CreateInstance(type) as Application;
                Console.WriteLine($"Connected to Inventor {app.SoftwareVersion.DisplayName}");

                // show Inventor UI
                app.Visible = true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"No running Inventor instance... ({e.Message})");
            }
            return app;
        }

        private static Application TryConnectToRunningInstance()
        {
            Console.WriteLine("Trying to connect to Inventor...");
            Application app = null;
            try
            {
                app = Marshal.GetActiveObject(PROG_ID) as Application;
                Console.WriteLine($"Connected to Inventor {app.SoftwareVersion.DisplayName}");
            }
            catch /*(Exception e)*/
            {
                //Console.WriteLine($"Could not connect to running Inventor Instance... ({e.Message})");
            }
            return app;
        }

        public void Dispose()
        {
            if (_Instance != null)
            {
                Console.WriteLine("Closing all documents...");
                _Instance.Documents.CloseAll(UnreferencedOnly: false);

                if (_CreatedByUs)
                {
                    // Uncomment to close the Inventor instance
                    //_Instance.Quit();
                }

                Console.WriteLine("Detaching from Inventor...");
                Marshal.ReleaseComObject(_Instance);
                _Instance = null;
            }
        }
    }
}