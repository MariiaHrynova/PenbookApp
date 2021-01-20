using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;

namespace Penbook.Services.Ink
{
    public class InkCloudService
    {
        static string[] Scopes = { DriveService.Scope.DriveReadonly };
        static string ApplicationName = "Penbook";

        public async Task<UserCredential> Authenticate()
        {
            UserCredential credential;

            StorageFile storageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///credentials.json"));
            var stream = await storageFile.OpenStreamForReadAsync();
            //var handle = storageFile.CreateSafeFileHandle(options: FileOptions.RandomAccess);
            //var stream = new FileStream(handle, FileAccess.ReadWrite);
            
                    // The file token.json stores the user's access and refresh tokens, and is created
                    // automatically when the authorization flow completes for the first time.
                    string credPath = "token.json";
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        Scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true)).Result;
                
            return credential;
        }
        public async Task SaveOnCloudStorage(UserCredential credential)
        {
            var openPicker = new FileOpenPicker();
            StorageFile file = await openPicker.PickSingleFileAsync();

            DriveService driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            List<string> parents = new List<string>();

            if(file != null)
                await UploadAsync(driveService, parents, file);
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
