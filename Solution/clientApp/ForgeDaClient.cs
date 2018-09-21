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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;

namespace Autodesk.Inventor.IO.Sample
{

    public abstract class ForgeClient
    {
        private RestClient Client { get; set; } = null;

        public string BaseUrl => Client.BaseUrl.ToString();

        public string Authorization => ((ForgeAuthenticator)Client.Authenticator).Token.GetHeader();

        protected string AccessToken => ((ForgeAuthenticator)Client.Authenticator).Token.AccessToken;

        protected ForgeClient(string baseURL, string clientId, string clientSecret, string authScope)
        {
            Uri baseUri = new Uri(baseURL);
            string domain = baseUri.GetLeftPart(UriPartial.Authority);
            bool isLocal = baseUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

            Client = new RestClient(baseURL)
            {
                Authenticator = isLocal ? null : new ForgeAuthenticator(domain, clientId, clientSecret, authScope)
            };
        }
        public async Task<ForgeRestResponse> Request(string path, string payload, Method method)
        {
            RestRequest request = new RestRequest(path, method);
            request.AddParameter("application/json", payload, ParameterType.RequestBody);
            IRestResponse response = await Client.ExecuteTaskAsync(request);
            return new ForgeRestResponse(response);
        }
    }


    public class ForgeDaClient : ForgeClient
    {
        public ForgeDaClient(string baseURL, string clientId, string clientSecret, string authScope = "code:all") :
            base(baseURL, clientId, clientSecret, authScope)
        {
        }

        public async Task<ForgeRestResponse> GetNickname()
        {
            string path = $"forgeapps/me";
            return await Request(path, string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> GetAppBundlesVersions(string id)
        {
            string path = $"appbundles/{id}/versions";
            return await Request(path, string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> GetAppBundles()
        {
            string path = $"appbundles";
            return await Request(path, string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> PostAppBundle(string payload)
        {
            return await Request("appbundles", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> PostAppBundleVersion(string id, string payload)
        {
            return await Request($"appbundles/{id}/versions", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> GetActivityVersions(string id)
        {
            string path = $"activities/{id}/versions";
            return await Request(path, string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> GetActivities()
        {
            string path = $"activities";
            return await Request(path, string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> GetActivityAlias(string id, string alias)
        {
            return await Request($"activities/{id}/aliases/{alias}", string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> PostActivity(string payload)
        {
            return await Request("activities", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> PostActivityVersion(string id, string payload)
        {
            return await Request($"activities/{id}/versions", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> PostActivityAlias(string id, string payload)
        {
            return await Request($"activities/{id}/aliases", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> PatchActivityAlias(string id, string alias, string payload)
        {
            return await Request($"activities/{id}/aliases/{alias}", payload, Method.PATCH);
        }

        public async Task<ForgeRestResponse> GetWorkItem(string id)
        {
            var path = $"workitems/{id}";
            return await Request(path, string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> PostWorkItem(string payload)
        {
            return await Request("workitems", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> GetAppBundleAlias(string id, string alias)
        {
            return await Request($"appbundles/{id}/aliases/{alias}", string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> PostAppBundleAlias(string id, string payload)
        {
            return await Request($"appbundles/{id}/aliases", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> PatchAppBundleAlias(string id, string alias, string payload)
        {
            return await Request($"appbundles/{id}/aliases/{alias}", payload, Method.PATCH);
        }
    }

    public class ForgeDmClient : ForgeClient
    {
        public ForgeDmClient(string baseURL, string clientId, string clientSecret,
            string authScope = "bucket:create bucket:read data:read data:write data:create") :
            base(baseURL, clientId, clientSecret, authScope)
        {
        }

        public async Task<ForgeRestResponse> CreateBucket(string payload)
        {
            return await Request($"buckets", payload, Method.POST);
        }

        public async Task<ForgeRestResponse> GetBuckets()
        {
            return await Request($"buckets", string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> GetBucketObjects(string bucketKey)
        {
            return await Request($"buckets/{bucketKey}/objects", string.Empty, Method.GET);
        }

        public async Task<ForgeRestResponse> GetBucketObjectDetails(string bucketKey, string objectName)
        {
            return await Request($"buckets/{bucketKey}/objects/{objectName}/details", string.Empty, Method.GET);
        }

        public async Task<HttpResponseMessage> UploadBucketObject(string filePath, string bucketName, string fileName)
        {
            string url = $"{BaseUrl}/buckets/{bucketName}/objects/{fileName}";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
                using (FileStream stream = File.OpenRead(filePath))
                {
                    HttpResponseMessage response = await client.PutAsync(url, new StreamContent(stream));
                    return response;
                }
            }
        }

        public async Task<ForgeRestResponse> CreateSignedUrl(string bucketKey, string objectName)
        {
            dynamic payload = new JObject();
            payload.minutesExpiration = 45;
            payload.singleUse = true;
            return await Request($"buckets/{bucketKey}/objects/{objectName}/signed", payload.ToString(), Method.POST);
        }
    }

    public class ForgeAuthenticator : IAuthenticator
    {
        public class ForgeToken
        {
            [JsonProperty("token_type")]
            public string TokenType { get; set; } = string.Empty;

            [JsonProperty("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonProperty("expires_in")]
            public double ExpiresIn { get; set; } = 0.0;

            public DateTime Created { get; } = DateTime.Now;

            public bool IsValid()
            {
                return (Created + TimeSpan.FromSeconds(ExpiresIn - 5.0)) > DateTime.Now;
            }

            public string GetHeader()
            {
                return TokenType + " " + AccessToken;
            }
        }

        private string Url { get; }
        private string Key { get; }
        private string Secret { get; }
        private string AuthScope { get; }

        public ForgeToken Token = new ForgeToken();

        public ForgeAuthenticator(string url, string key, string secret, string authScope)
        {
            Url = url;
            Key = key;
            Secret = secret;
            AuthScope = authScope;
        }

        public void Authenticate(IRestClient client, IRestRequest request)
        {
            if (!request.Parameters.Any(p => p.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
            {
                request.AddParameter("Authorization", GetTokenHeader(), ParameterType.HttpHeader);
            }
        }

        public string GetTokenHeader()
        {
            if (Token.IsValid())
                return Token.GetHeader();

            var client = new RestClient(Url);

            RestRequest request = new RestRequest("authentication/v1/authenticate", Method.POST);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("client_id", Key);
            request.AddParameter("client_secret", Secret);
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("scope", AuthScope);

            IRestResponse response = client.Execute(request);
            Token = JsonConvert.DeserializeObject<ForgeToken>(response.Content);

            Debug.Assert(Token.IsValid());
            return Token.GetHeader();
        }
    }

    public class ForgeRestResponse
    {
        public HttpStatusCode Status => Response.StatusCode;
        public string ResponseContent => Response.Content;

        private IRestResponse Response;
        public ForgeRestResponse(IRestResponse response)
        {
            Response = response;
        }
        public bool IsSuccessStatusCode()
        {
            return ((int)Status >= 200) && ((int)Status <= 299);
        }

        public string GetResponseContentProperty(string key)
        {
            try
            {
                Dictionary<string, object> content = JsonConvert.DeserializeObject<Dictionary<string, object>>(ResponseContent);
                return content[key]?.ToString();
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }

        public void ReportError(string strErrorMessage)
        {
            Console.WriteLine($"Error reported: {strErrorMessage}");
            Console.WriteLine($"Response status: {Status}");
            Console.WriteLine($"Response details: {ResponseContent}");
        }

        public bool ReportIfError(string strErrorMessage)
        {
            bool bError = !IsSuccessStatusCode();
            if (bError)
                ReportError(strErrorMessage);

            return bError;
        }
    }
}
