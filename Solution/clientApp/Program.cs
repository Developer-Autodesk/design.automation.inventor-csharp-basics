/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Newtonsoft.Json.Linq;

namespace Autodesk.Inventor.IO.Sample
{
    class Credentials
    {
        // get your ConsumerKey/ConsumerSecret at http://forge.autodesk.com
        // store them in FORGE_CLIENT_ID and FORGE_CLIENT_SECRET environment variables
        public string ConsumerKey { get; set; }
        public string ConsumerSecret { get; set; }
    }

    class AppProperties
    {
        // Properties from the json config file located in the config folder
        public string AppId { get; set; }
        public string EngineName { get; set; }
        public string LocalAppPackage { get; set; } // Your local app package
        public string InputPath { get; set; }
        public string InputPartFile { get; set; }
        public string InputAssemblyZipFile { get; set; }
        public string AssemblyUnzipDir { get; set; }
        public string InputTopLevelAssembly { get; set; }
        public string OutputPartFile { get; set; }
        public string OutputImageFile { get; set; }
        public string OutputZipAssemblyFile { get; set; }
        public string PartAssemblyActivityId { get; set; }
        public string InventorIOBaseUrl { get; set; }
        public string ForgeDMBaseUrl { get; set; }
        public string ReqInputArgName { get; set; }
        public string OutputPartArgName { get; set; }
        public string OutputAssemblyArgName { get; set; }
        public string OutputImageArgName { get; set; }
        public string ErrorReport { get; set; }
        public string partReport { get; set; }
        public string assemblyReport { get; set; }
        public string ParamFile { get; set; }
        public string ParamArgName { get; set; }
    }

    class Program
    {
        static Credentials s_Creds;
        static AppProperties s_Config;
        static ForgeDaClient s_ForgeDaClient;
        static ForgeDmClient s_ForgeDmClient;
        static readonly string s_alias = "prod";
        static string s_nickname;

        static void DownloadToDocs(string url, string localFile)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(0, 10, 0); // 10min timeout for large file downloads
                    StreamContent content = (StreamContent)client.GetAsync(url).Result.Content;
                    string fname = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), localFile);
                    Console.WriteLine("Downloading to {0}.", fname);

                    using (FileStream output = File.Create(fname))
                    {
                        content.ReadAsStreamAsync().Result.CopyTo(output);
                        output.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error downloading to documents folder: " + e.Message);
            }
        }

        static readonly string[] _postKeysV3 = { "key", "success_action_status", "success_action_redirect",  "content-type", "policy",
                                                 "x-amz-signature", "x-amz-credential", "x-amz-algorithm",
                                                 "x-amz-date", "x-amz-server-side-encryption", "x-amz-security-token" };

        static async Task<bool> UploadFileAsync(JToken formData, string url, string filePath)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    MultipartFormDataContent form = new MultipartFormDataContent();

                    Array.ForEach(_postKeysV3, key => form.Add(new StringContent(formData[key]?.ToString()), key));
                    form.Add(new ByteArrayContent(File.ReadAllBytes(filePath)), "file");

                    var responseMessage = await httpClient.PostAsync(url, form);
                    return responseMessage.StatusCode == HttpStatusCode.OK;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Upload error: " + e.Message);
                return false;
            }
        }

        private static async Task<bool> EnsureBucketExists(string bucketId)
        {
            Console.WriteLine($"Checking if bucket {bucketId} exists...");

            // Check to see if the app exists
            ForgeRestResponse response = await s_ForgeDmClient.GetBuckets();

            string items = response.GetResponseContentProperty("items");
            bool bucketExists = items.Contains(bucketId);
            
            if (!bucketExists)
            {
                dynamic payload = new JObject();
                payload.bucketKey = bucketId;
                payload.policyKey = $"persistent"; // see https://forge.autodesk.com/en/docs/data/v2/overview/retention-policy/ for options
                Console.WriteLine($"Creating bucket {bucketId}...");
                response = await s_ForgeDmClient.CreateBucket(payload.ToString());
                if (response.ReportIfError("Exception when creating bucket."))
                    return false;
            }
            return true;
        }

        private static async Task<bool> EnsureInputExists(string file)
        {
            Console.WriteLine($"Checking if input {file} is in bucket {getInputBucketKey()}...");
            string localPath = "../../" + s_Config.InputPath + "/" + file;
            if (!File.Exists(localPath))
            {
                Console.WriteLine($"Cannot read local input file: {localPath}");
                return false;
            }

            ForgeRestResponse response = await s_ForgeDmClient.GetBucketObjectDetails(getInputBucketKey(), file);
            if (!response.IsSuccessStatusCode())
            {
                if (response.Status == HttpStatusCode.Forbidden)
                {
                    response.ReportError("InputBucketId and OutputBucketId in config.json must be unique (not created by another forge application)");
                    return false;
                }
                else if (response.Status == HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Uploading input {localPath}...");
                    HttpResponseMessage message = await s_ForgeDmClient.UploadBucketObject(localPath, getInputBucketKey(), file);
                    if (!message.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Exception uploading input: {response.Status} {response.ResponseContent}");
                        return false;
                    }
                }
            }
            else
            {
                Console.WriteLine("Found existing input");
            }

            return true;
        }

        /// <summary>
        /// Create the app (or new version of it) that will be used with both the assembly and part activities and workItems
        /// </summary>
        /// <returns>true if successful, false otherwise</returns>
        private static async Task<bool> CreateApp()
        {
            Console.WriteLine($"Checking if app {s_Config.AppId} exists...");

            // Check to see if the app exists
            ForgeRestResponse response = await s_ForgeDaClient.GetAppBundles();
            string appsData = response.GetResponseContentProperty("data");
            string appName = $"{s_nickname}.{s_Config.AppId}+{s_alias}";
            bool appExists = appsData.Contains(appName);

            dynamic payload = new JObject();
            payload.engine = s_Config.EngineName;

            if (!appExists)
            {
                Console.WriteLine($"Creating app {s_Config.AppId}...");
                payload.id = s_Config.AppId;
                response = await s_ForgeDaClient.PostAppBundle(payload.ToString());
                if (response.ReportIfError("Exception creating app."))
                    return false;
            }
            else
            {
                Console.WriteLine($"Creating new version for app {s_Config.AppId}...");
                response = await s_ForgeDaClient.PostAppBundleVersion(s_Config.AppId, payload.ToString());
                if (response.ReportIfError("Exception creating app version."))
                    return false;
            }

            JObject responseObj = JObject.Parse(response.ResponseContent);
            JToken uploadParams = responseObj["uploadParameters"];
            if (uploadParams == null)
            {
                Console.WriteLine($"App {s_Config.AppId} does not provide upload data.");
                return false;
            }

            string uploadUrl = uploadParams["endpointURL"]?.ToString();
            string version = responseObj["version"]?.ToString();

            Console.WriteLine($"Checking if \"{s_alias}\" alias exists for app {s_Config.AppId}");
            response = await s_ForgeDaClient.GetAppBundleAlias(s_Config.AppId, s_alias);
            if (!response.IsSuccessStatusCode() && !(response.Status == HttpStatusCode.NotFound))
            {
                response.ReportError("Exception getting app alias.");
                return false;
            }
            payload = new JObject();
            payload.version = version.ToString();
            if (response.Status == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Creating new \"{s_alias}\" alias for app {s_Config.AppId}");
                payload.id = s_alias;
                response = await s_ForgeDaClient.PostAppBundleAlias(s_Config.AppId, payload.ToString());
                if (response.ReportIfError("Exception creating app alias."))
                    return false;
            }
            else
            {
                Console.WriteLine($"Updating \"{s_alias}\" alias for app {s_Config.AppId}");
                response = await s_ForgeDaClient.PatchAppBundleAlias(s_Config.AppId, s_alias, payload.ToString());
                if (response.ReportIfError("Exception updating app alias."))
                    return false;
            }

            string absolutePath = Path.GetFullPath("../../" + s_Config.LocalAppPackage);
            Console.WriteLine($"Uploading zip file " + absolutePath + "...");
            bool uploadSuccessful = await UploadFileAsync(uploadParams["formData"], uploadUrl, absolutePath);
            if (!uploadSuccessful)
            {
                Console.WriteLine("Failed to upload zip file.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Create an activity (or new version of it)
        /// </summary>
        /// <returns>true if successful, false otherwise</returns>
        private static async Task<bool> CreateActivity(string id, string commandLineParameters, JObject parameters)
        {
            Console.WriteLine($"Checking if activity {id} exists...");

            ForgeRestResponse response = await s_ForgeDaClient.GetActivities();
            string activities = response.GetResponseContentProperty("data");
            bool activityExists = activities.Contains(id);


            dynamic payload = new JObject();
            payload.engine = s_Config.EngineName;
            payload.apps = new JArray($"{s_nickname}.{s_Config.AppId}+{s_alias}");
            payload.commandLine = $"$(engine.path)\\InventorCoreConsole.exe /i $(args[{s_Config.ReqInputArgName}].path) /al $(apps[{s_Config.AppId}].path) $(args[{s_Config.ParamArgName}].path)";
            payload.settings = new JObject();
            payload.parameters = parameters;

            if (!activityExists)
            {
                Console.WriteLine($"Creating activity {id}...");
                payload.id = id;
                response = await s_ForgeDaClient.PostActivity(payload.ToString());
                if (response.ReportIfError("Exception creating activity."))
                    return false;
            }
            else
            {
                Console.WriteLine($"Creating new version for activity {id}...");
                response = await s_ForgeDaClient.PostActivityVersion(id, payload.ToString());
                if (response.ReportIfError("Exception creating activity version."))
                    return false;
            }

            string version = response.GetResponseContentProperty("version");

            Console.WriteLine($"Checking if \"{s_alias}\" alias exists for activity {id}");
            response = await s_ForgeDaClient.GetActivityAlias(id, s_alias);
            if (!response.IsSuccessStatusCode() && !(response.Status == HttpStatusCode.NotFound))
            {
                response.ReportError("Exception getting activity alias.");
                return false;
            }

            payload = new JObject();
            payload.version = version;
            if (response.Status == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Creating new \"{s_alias}\" alias for activity {id}");
                payload.id = s_alias;
                response = await s_ForgeDaClient.PostActivityAlias(id, payload.ToString());
                if (response.ReportIfError("Exception creating activity alias."))
                    return false;
            }
            else
            {
                Console.WriteLine($"Updating \"{s_alias}\" alias for activity {id}");
                response = await s_ForgeDaClient.PatchActivityAlias(id, s_alias, payload.ToString());
                if (response.ReportIfError("Exception updating activity alias."))
                    return false;
            }

            return true;
        }

        private static async Task<bool> CreatePartAssemblyActivity()
        {
            // create json for input and output parameters
            JObject parameters = new JObject(
                new JProperty(s_Config.ReqInputArgName, new JObject(
                    new JProperty("verb", "get"))),
                new JProperty($"{s_Config.ParamArgName}", new JObject(
                    new JProperty("localName", s_Config.ParamFile),
                    new JProperty("verb", "get"))),
                new JProperty(s_Config.OutputPartArgName, new JObject(
                    new JProperty("zip", false),
                    new JProperty("ondemand", false),
                    new JProperty("optional", true),
                    new JProperty("localName", s_Config.OutputPartFile),
                    new JProperty("verb", "post"))),
                 new JProperty(s_Config.OutputAssemblyArgName, new JObject(
                    new JProperty("zip", false),
                    new JProperty("ondemand", false),
                    new JProperty("optional", true),
                    new JProperty("localName", s_Config.OutputZipAssemblyFile),
                    new JProperty("verb", "post")
                )),
                new JProperty(s_Config.OutputImageArgName, new JObject(
                    new JProperty("zip", false),
                    new JProperty("ondemand", false),
                    new JProperty("optional", true),
                    new JProperty("localName", s_Config.OutputImageFile),
                    new JProperty("verb", "post")
                ))
            );
            return await CreateActivity(s_Config.PartAssemblyActivityId, $"{s_Config.ParamFile} {s_Config.OutputPartFile}", parameters);
        }

        private static async Task<string> CreatePartWorkItem()
        {
            Console.WriteLine("Creating part work item...");
            ForgeRestResponse response = await s_ForgeDmClient.CreateSignedUrl(getInputBucketKey(), s_Config.InputPartFile);
            if (response.ReportIfError("Exception creating signed url for work item input."))
                return null;

            string inputSignedUrl = response.GetResponseContentProperty("signedUrl");

            JObject payload = new JObject(
                new JProperty("activityId", $"{s_nickname}.{s_Config.PartAssemblyActivityId}+{s_alias}"),
                // Must match the input parameter in activity
                new JProperty("arguments", new JObject(
                    new JProperty(s_Config.ReqInputArgName, new JObject(
                        new JProperty("url", inputSignedUrl)
                    )),
                    // This shows passing parameters and values into the plug-in
                    new JProperty($"{s_Config.ParamArgName}", new JObject(
                        new JProperty("url", "data:application/json,{\"height\":\"16 in\", \"width\":\"10 in\"}")
                    )),
                    // must match the output parameter in activity
                    new JProperty(s_Config.OutputPartArgName, new JObject(
                        new JProperty("url", s_Config.ForgeDMBaseUrl + "buckets/" + getOutputBucketKey() + "/objects/" + s_Config.OutputPartFile),
                        new JProperty("verb", "put"),
                        new JProperty("headers", new JObject(
                            new JProperty("Authorization", s_ForgeDmClient.Authorization),
                            new JProperty("Content-type", "application/octet-stream")
                        ))
                    )),
                    new JProperty(s_Config.OutputImageArgName, new JObject(
                        new JProperty("url", s_Config.ForgeDMBaseUrl + "buckets/" + getOutputBucketKey() + "/objects/" + s_Config.OutputImageFile),
                        new JProperty("verb", "put"),
                        new JProperty("headers", new JObject(
                            new JProperty("Authorization", s_ForgeDmClient.Authorization),
                            new JProperty("Content-type", "application/octet-stream")
                        ))
                    ))
                ))
            );

            response = await s_ForgeDaClient.PostWorkItem(payload.ToString());
            if (response.ReportIfError("Exception creating work item."))
                return null;

            string workItemId = response.GetResponseContentProperty("id");
            if (string.IsNullOrEmpty(workItemId))
            {
                Console.WriteLine("Failed to post work item");
                return null;
            }

            return workItemId;
        }

        private static async Task<string> CreateAssemblyWorkItem()
        {
            Console.WriteLine("Creating assembly work item...");
            ForgeRestResponse response = await s_ForgeDmClient.CreateSignedUrl(getInputBucketKey(), s_Config.InputAssemblyZipFile);
            if (response.ReportIfError("Exception creating signed url for work item input."))
                return null;

            string inputSignedUrl = response.GetResponseContentProperty("signedUrl");

            JObject payload = new JObject(
                new JProperty("activityId", $"{s_nickname}.{s_Config.PartAssemblyActivityId}+{s_alias}"),
                new JProperty("arguments", new JObject(
                    new JProperty(s_Config.ReqInputArgName, new JObject(
                        new JProperty("url", inputSignedUrl),
                        new JProperty("zip", false),
                        new JProperty("pathInZip", s_Config.InputTopLevelAssembly),
                        new JProperty("localName", "Assy")
                    )),
                    // This shows passing parameters and values into the plug-in
                    new JProperty($"{s_Config.ParamArgName}", new JObject(
                        new JProperty("url", "data:application/json,{\"handleOffset\":\"9 in\", \"height\":\"16 in\"}")
                    )),
                    // must match the output parameter in activity
                    new JProperty(s_Config.OutputAssemblyArgName, new JObject(
                        new JProperty("url", s_Config.ForgeDMBaseUrl + "buckets/" + getOutputBucketKey() + "/objects/" + s_Config.OutputZipAssemblyFile),
                        new JProperty("verb", "put"),
                        new JProperty("headers", new JObject(
                            new JProperty("Authorization", s_ForgeDmClient.Authorization),
                            new JProperty("Content-type", "application/octet-stream")
                        ))
                    ))
                ))
            );

            response = await s_ForgeDaClient.PostWorkItem(payload.ToString());
            if (response.ReportIfError("Exception creating work item."))
                return null;

            string workItemId = response.GetResponseContentProperty("id");
            if (string.IsNullOrEmpty(workItemId))
            {
                Console.WriteLine("Failed to post work item");
                return null;
            }

            return workItemId;
        }

        static void LoadConfig()
        {
            // read the config file from disk
            String configContent = File.ReadAllText("config.json");

            JavaScriptSerializer configSer = new JavaScriptSerializer();

            // deserialize appProperties
            s_Config = configSer.Deserialize<AppProperties>(configContent);

            // read the security context from enviroment variables
            s_Creds = new Credentials();
            s_Creds.ConsumerKey = System.Environment.GetEnvironmentVariable("FORGE_CLIENT_ID");
            s_Creds.ConsumerSecret = System.Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET");
            if (s_Creds.ConsumerKey == null || s_Creds.ConsumerSecret == null)
            {
                Console.WriteLine("Enviroment variables with Forge credentials are not defined !!!");
                throw new Exception();
            }
        }

        static void Main(string[] args)
        {
            Task t = MainAsync(args);
            t.Wait();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                LoadConfig();

                // Create DesignAutomation object for making REST calls to the DesignAutomation APIs
                s_ForgeDaClient = new ForgeDaClient(s_Config.InventorIOBaseUrl, s_Creds.ConsumerKey, s_Creds.ConsumerSecret);

                // Get the user's nickname for querying if apps and activities exist. If no nickname is set, the forge app id will be returned
                ForgeRestResponse response = await s_ForgeDaClient.GetNickname();
                if (!response.ReportIfError("Error retrieving nickname for user."))
                {
                    string content = response.ResponseContent;
                    s_nickname = content.Replace("\"", String.Empty);
                }
                else
                    return;

                // Create Forge DM object for making REST calls to the Forge Data Management APIs
                s_ForgeDmClient = new ForgeDmClient(s_Config.ForgeDMBaseUrl, s_Creds.ConsumerKey, s_Creds.ConsumerSecret);

                // Create the input bucket for input files if necessary
                if (!await EnsureBucketExists(getInputBucketKey()))
                {
                    return;
                }

                // Upload part input if necessary
                if (!await EnsureInputExists(s_Config.InputPartFile))
                {
                    return;
                }

                // Upload assembly input if necessary
                if (!await EnsureInputExists(s_Config.InputAssemblyZipFile))
                {
                    return;
                }

                // Create the output bucket for result files if necessary
                if (!await EnsureBucketExists(getOutputBucketKey()))
                {
                    return;
                }

                if (!await CreateApp())
                {
                    return;
                }

                // Setup a part & assembly activity to show how to interact with an Inventor Part file and assembly zip file
                if (!await CreatePartAssemblyActivity())
                {
                    return;
                }

                // Create a part activity work item
                string workItemId = await CreatePartWorkItem();
                if (workItemId == null)
                {
                    return;
                }

                // Wait for the result of the work item
                string status;
                do
                {
                    Console.WriteLine("Sleeping for 2 sec...");
                    Thread.Sleep(2000);
                    response = await s_ForgeDaClient.GetWorkItem(workItemId);
                    if (response.ReportIfError("Exception getting work item status."))
                        return;
                    status = response.GetResponseContentProperty("status");
                    Console.WriteLine($"Work item status: {status}");
                }
                while (status == "pending" || status == "inprogress");

                string reportUrl = response.GetResponseContentProperty("reportUrl");
                if (status != "success")
                {
                    Console.WriteLine("Work item failed. Writing report log to: " + s_Config.ErrorReport);
                    DownloadToDocs(reportUrl, s_Config.ErrorReport);
                    return;
                }

                Console.WriteLine("Writing report log to: " + s_Config.partReport);
                DownloadToDocs(reportUrl, s_Config.partReport);
                response = await s_ForgeDmClient.CreateSignedUrl(getOutputBucketKey(), s_Config.OutputPartFile);
                string outputDownloadUrl = response.GetResponseContentProperty("signedUrl");
                DownloadToDocs(outputDownloadUrl, s_Config.OutputPartFile);

                // Create an assembly activity work item
                workItemId = await CreateAssemblyWorkItem();
                if (workItemId == null)
                {
                    return;
                }

                // Wait for the result of the work item
                do
                {
                    Console.WriteLine("Sleeping for 2 sec...");
                    Thread.Sleep(2000);
                    response = await s_ForgeDaClient.GetWorkItem(workItemId);
                    if (response.ReportIfError("Exception getting work item status."))
                        return;
                    status = response.GetResponseContentProperty("status");
                    Console.WriteLine($"Work item status: {status}");
                }
                while (status == "pending" || status == "inprogress");

                reportUrl = response.GetResponseContentProperty("reportUrl");
                if (status != "success")
                {
                    Console.WriteLine("Work item failed. Writing report log to: " + s_Config.ErrorReport);
                    DownloadToDocs(reportUrl, s_Config.ErrorReport);
                    return;
                }

                Console.WriteLine("Writing report log to: " + s_Config.assemblyReport);
                DownloadToDocs(reportUrl, s_Config.assemblyReport);
                response = await s_ForgeDmClient.CreateSignedUrl(getOutputBucketKey(), s_Config.OutputZipAssemblyFile);
                outputDownloadUrl = response.GetResponseContentProperty("signedUrl");
                DownloadToDocs(outputDownloadUrl, s_Config.OutputZipAssemblyFile);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.ToString()}");
            }
            finally
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

	    private static string getInputBucketKey()
	    {
		    return s_Creds.ConsumerKey.ToLower() + "input";
	    }

	    private static string getOutputBucketKey()
	    {
		    return s_Creds.ConsumerKey.ToLower() + "output";
	    }
	}
}
