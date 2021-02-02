using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Penbook.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;

namespace Penbook.Services.Ink
{
    public class InkCloudService
    {
        static string ApplicationName = "Penbook";
        static string clientID = "1053877635062-nisi5mcgg6d6fiknttstkt95ovpa8unr.apps.googleusercontent.com";
        static string clientSecret = "bZcZJ8RY7wabOZHiKbJ6iK9C";
        static string redirectURI = "urn:ietf:wg:oauth:2.0:oob";
        static string authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        static string scope = "https://www.googleapis.com/auth/drive.appdata";

        private string GenerateRandomBase64Url()
        {
            Guid g = Guid.NewGuid();
            string GuidString = Convert.ToBase64String(g.ToByteArray());
            GuidString = GuidString.Replace("=", "");
            GuidString = GuidString.Replace("+", "");

            return GuidString;
        }
        public async Task Authenticate()
        {
            string state = GenerateRandomBase64Url();
            string code_verifier = GenerateRandomBase64Url();            

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["state"] = state;
            localSettings.Values["code_verifier"] = code_verifier;

            string authorizationRequest = string.Format($@"{authorizationEndpoint}?response_type=code&scope={Uri.EscapeDataString(scope)}&redirect_uri={Uri.EscapeDataString(redirectURI)}&client_id={clientID}&state={state}");

            Uri startURI = new Uri(authorizationRequest);
            Uri endURI = new Uri(redirectURI);
            string result = string.Empty;
            try
            {
                WebAuthenticationResult webAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, startURI, endURI);
                switch (webAuthenticationResult.ResponseStatus)
                {
                    // Successful authentication.  
                    case WebAuthenticationStatus.Success:
                        result = webAuthenticationResult.ResponseData.ToString();
                        break;
                    // HTTP error.  
                    case WebAuthenticationStatus.ErrorHttp:
                        result = webAuthenticationResult.ResponseErrorDetail.ToString();
                        break;
                    default:
                        result = webAuthenticationResult.ResponseData.ToString();
                        break;
                }

            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
        }

        public async Task SaveOnCloudStorage()
        {
            await Authenticate();

            UserCredential credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                           new ClientSecrets
                           {
                               ClientId = clientID,
                               ClientSecret = clientSecret
                           },
                           new string[]{
                               DriveService.Scope.Drive,
                               DriveService.Scope.DriveFile
                           },
                           Environment.UserName,
                           CancellationToken.None,
                           new AppDataFileStore(Windows.ApplicationModel.Package.Current.InstalledLocation.Path)).Result;
                      
            //Once consent is recieved, your token will be stored locally on the AppData directory, so that next time you wont be prompted for consent. 

            DriveService service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "MyAppName",
            });
            service.HttpClient.Timeout = TimeSpan.FromMinutes(100);

            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            // Dropdown of file types the user can save the file as
            savePicker.FileTypeChoices.Add("GIF", new List<string>() { ".gif" });

            StorageFile file = await savePicker.PickSaveFileAsync();

             await UploadAsync(service, new string[] { }, file);
        }
        private static async Task<Google.Apis.Drive.v3.Data.File> UploadAsync(DriveService driveService, IList<string> parents, StorageFile file)
        {
            // Prepare the JSON metadata
            string json = "{\"name\":\"" + file.Name + "\"";
            if (parents.Count > 0)
            {
                json += ", \"parents\": [";
                foreach (string parent in parents)
                {
                    json += "\"" + parent + "\", ";
                }
                json = json.Remove(json.Length - 2) + "]";
            }
            json += "}";
            Debug.WriteLine(json);

            Google.Apis.Drive.v3.Data.File uploadedFile = null;
            try
            {
                BasicProperties prop = await file.GetBasicPropertiesAsync();
                ulong fileSize = prop.Size;

                // Step 1: Start a resumable session
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create("https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable");
                httpRequest.Headers["Content-Type"] = "application /json; charset=UTF-8";
                httpRequest.Headers["Content-Length"] = json.Length.ToString();
                httpRequest.Headers["X-Upload-Content-Type"] = file.ContentType;
                httpRequest.Headers["X-Upload-Content-Length"] = fileSize.ToString();
                httpRequest.Headers["Authorization"] = "Bearer " + ((UserCredential)driveService.HttpClientInitializer).Token.AccessToken;
                httpRequest.Method = "POST";

                using (System.IO.Stream requestStream = await httpRequest.GetRequestStreamAsync())
                using (System.IO.StreamWriter streamWriter = new System.IO.StreamWriter(requestStream))
                {
                    streamWriter.Write(json);
                }

                // Step 2: Save the resumable session URI
                HttpWebResponse httpResponse = (HttpWebResponse)(await httpRequest.GetResponseAsync());
                if (httpResponse.StatusCode != HttpStatusCode.OK)
                    return null;

                // Step 3: Upload the file
                httpRequest = (HttpWebRequest)WebRequest.Create("https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&upload_id=" + httpResponse.Headers["x-guploader-uploadid"]);
                httpRequest.Headers["Content-Type"] = file.ContentType;
                httpRequest.Headers["Content-Length"] = fileSize.ToString();
                httpRequest.Method = "PUT";

                using (System.IO.Stream requestStream = await httpRequest.GetRequestStreamAsync())
                using (System.IO.FileStream fileStream = new System.IO.FileStream(file.Path, FileMode.Open))
                {
                    await fileStream.CopyToAsync(requestStream);
                }

                httpResponse = (HttpWebResponse)(await httpRequest.GetResponseAsync());
                if (httpResponse.StatusCode != HttpStatusCode.OK)
                    return null;

                // Try to retrieve the file from Google
                FilesResource.ListRequest request = driveService.Files.List();
                if (parents.Count > 0)
                    request.Q += "'" + parents[0] + "' in parents and ";
                request.Q += "name = '" + file.Name + "'";
                FileList result = request.Execute();
                if (result.Files.Count > 0)
                    uploadedFile = result.Files[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return uploadedFile;
        }
    }
}
