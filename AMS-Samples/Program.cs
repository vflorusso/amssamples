using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Newtonsoft.Json;

using Microsoft.WindowsAzure.MediaServices;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;


namespace AMS_Samples
{
    class Program
    {
        static string sampleVideoFilePath = @"C:\Azure\samplevideo.mp4";
        static string amsPresetPath = @"C:\Azure\AMSPresetFiles";
        static string configurationFile = @"C:\Azure\AMSSamples-config.json";
        static string sampleAssetName = "webeu-conf-mp4-Source - Adaptive 720p";
        static string amsAccountName = "";
        static string amsAccountKey = "";

        static string streamSmoothLegacy = "(format=fmp4-v20)";
        static string streamDash = "(format=mpd-time-csf)";
        static string streamHLSv4 = "(format=m3u8-aapl)";
        static string streamHLSv3 = "(format=m3u8-aapl-v3)";
        static string streamHDS = "(format=f4m-f4f)";

        static void Main(string[] args)
        {
            // read configuration
            string jsonConfig = File.ReadAllText(configurationFile);
            AMSSampleConfig config = JsonConvert.DeserializeObject<AMSSampleConfig>(jsonConfig);
            amsAccountName = config.msAccountName;
            amsAccountKey = config.msAccountKey;

            // create media context used trough the samples
            CloudMediaContext account = createAMSContext();

            // search an asset by name
            IAsset asset = GetAssetByName(account, sampleAssetName);
            IAssetFile videoFile = asset.AssetFiles.FirstOrDefault();
            //string storageSasUri = AMSHelpers.createStorageSasUrl(videoFile, config.stgAccountKey);

            // creating a media asset 
            //IAsset vodAsset = CreateAssetAndUploadSingleFile(account, AssetCreationOptions.None, sampleVideoFilePath);
            // create streaming locator
            string urlToStream = GetStreamingOriginLocatorEndPoint(account, asset);
            //Console.WriteLine(urlToStream + streamHLSv4);

            // enumerating assets and get streaming url
            //foreach (IAsset asset in account.Assets)
            //{
            //    Console.WriteLine("Asset name:{0} type: {1}", asset.Name, asset.AssetType);
            //    Console.WriteLine("\tFiles");
            //    foreach (IAssetFile assetFile in asset.AssetFiles)
            //    {
            //        Console.WriteLine("\tFilename:", assetFile.Name);
            //    }

            //}

            Console.ReadLine();
        }
        public static void createStreamingEndpoint( CloudMediaContext account)
        {
            //// Live streaming
            StreamingEndpointCreationOptions seOptions = new StreamingEndpointCreationOptions();
            IPRange streamingIPs = new IPRange() { Name = "any", Address = IPAddress.Any };

            seOptions.AccessControl = new StreamingEndpointAccessControl();
            seOptions.AccessControl.IPAllowList = new List<IPRange>() { streamingIPs };
            seOptions.CdnEnabled = true;
            seOptions.CustomHostNames = new List<string> { "loadbalanced.mydomain.com" };
            seOptions.Description = "Streaming Endpoint for Live channel";
            seOptions.Name = "Live01";
            seOptions.ScaleUnits = 1;
            IStreamingEndpoint endpoint = account.StreamingEndpoints.Create(seOptions);
            Task createStreamingEndpoint = account.StreamingEndpoints.CreateAsync(seOptions);

            while (!createStreamingEndpoint.IsCompleted)
            {
                Thread.Sleep(5000);
            }
            if (endpoint != null)
            {
                Task endpointStartTask = endpoint.StartAsync();
                while (!endpointStartTask.IsCompleted)
                {
                    Thread.Sleep(5000);
                }
            }
        }

        public static void createChannelAndProgram(CloudMediaContext amsAccount, string channelName, string programName)
        {
            ChannelCreationOptions channelOptions = new ChannelCreationOptions();
            channelOptions.Name = channelName;
            channelOptions.Input = new ChannelInput();
            channelOptions.Encoding = new ChannelEncoding();
            channelOptions.Encoding.SystemPreset = "";
            channelOptions.EncodingType = ChannelEncodingType.Standard;
            channelOptions.Input.StreamingProtocol =  StreamingProtocol.FragmentedMP4;
            IPRange ipRange = new IPRange();
            ipRange.Name = "All";
            ipRange.Address = new System.Net.IPAddress(0);
            ipRange.SubnetPrefixLength = 0;
            channelOptions.Input.AccessControl = new ChannelAccessControl();
            channelOptions.Input.AccessControl.IPAllowList = new List<IPRange>() { new IPRange() { Name = "All", Address = IPAddress.Parse("0.0.0.0"), SubnetPrefixLength = 0 } };

            IChannel myChannel = amsAccount.Channels.Create(channelOptions);
            myChannel.StartAsync();
            while (myChannel.State != ChannelState.Running)
            {
                Thread.Sleep(10000);
            }
            IAsset asset = amsAccount.Assets.Create(programName, AssetCreationOptions.None);

            // Create a Program on the Channel. You can have multiple Programs that overlap or are sequential;
            // however each Program must have a unique name within your Media Services account.
            IProgram program = myChannel.Programs.Create(programName, TimeSpan.FromHours(3), asset.Id);
            program.StartAsync();
        
        }
        public static CloudMediaContext createAMSContext()
        {
            MediaServicesCredentials credentials = new MediaServicesCredentials(amsAccountName, amsAccountKey);
            CloudMediaContext mediaClient = new CloudMediaContext(credentials);
            return mediaClient;
        }

        public static void createStreamingEndpoint(CloudMediaContext amsAccount, string seName)
        {
            IStreamingEndpoint amsEndpoint = amsAccount.StreamingEndpoints.Create(seName, 1);
            amsEndpoint.StartAsync();
        }

        public static IList<IAsset> enumerateAssets(CloudMediaContext amsAccount)
        {

            IList<IAsset> amsAssets = new List<IAsset>();
            foreach (IAsset mediaAsset in amsAccount.Assets)
            {

                amsAssets.Add(mediaAsset);
            }
            return amsAssets;
        }

        static private IAsset CreateEmptyAsset(CloudMediaContext amsAccount, string assetName, AssetCreationOptions assetCreationOptions)
        {
            var asset = amsAccount.Assets.Create(assetName, assetCreationOptions);

            Console.WriteLine("Asset name: " + asset.Name);
            Console.WriteLine("Time created: " + asset.Created.Date.ToString());

            return asset;
        }

        static public IAsset CreateAssetAndUploadSingleFile(CloudMediaContext amsAccount, AssetCreationOptions assetCreationOptions, string singleFilePath)
        {
            var assetName = "UploadSingleFile_" + DateTime.UtcNow.ToString();
            var asset = CreateEmptyAsset(amsAccount, assetName, assetCreationOptions);

            var fileName = Path.GetFileName(singleFilePath);

            var assetFile = asset.AssetFiles.Create(fileName);

            Console.WriteLine("Created assetFile {0}", assetFile.Name);

            var accessPolicy = amsAccount.AccessPolicies.Create(assetName, TimeSpan.FromDays(3),
                                                                AccessPermissions.Write | AccessPermissions.List);

            var locator = amsAccount.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);

            Console.WriteLine("Upload {0}", assetFile.Name);

            assetFile.Upload(singleFilePath);
            Console.WriteLine("Done uploading of {0} using Upload()", assetFile.Name);

            locator.Delete();
            accessPolicy.Delete();

            return asset;
        }

        static IAsset CreateEncodingJob(CloudMediaContext amsAccount, IAsset asset)
        {
            // Declare a new job.
            IJob job = amsAccount.Jobs.Create("My encoding job");
            // Get a media processor reference, and pass to it the name of the 
            // processor to use for the specific task.
            IMediaProcessor processor = AMSHelpers.GetLatestMediaProcessorByName(amsAccount, "Media Encoder Standard");

            // Create a task with the encoding details, using a string preset.
            // String presets for Media Encoder Standard are listed here: https://msdn.microsoft.com/library/azure/mt269960.aspx
            ITask task = job.Tasks.AddNew("My encoding task",
                processor,
                "H264 Multiple Bitrate 720p",
                TaskOptions.ProtectedConfiguration);

            // Specify the input asset to be encoded.
            task.InputAssets.Add(asset);
            // Add an output asset to contain the results of the job. 
            // This output is specified as AssetCreationOptions.None, which 
            // means the output asset is not encrypted. 
            task.OutputAssets.AddNew("Output asset",
                AssetCreationOptions.None);
            // Use the following event handler to check job progress.  
            job.StateChanged += new
                    EventHandler<JobStateChangedEventArgs>(StateChanged);

            // Launch the job.
            job.Submit();

            // Optionally log job details. This displays basic job details
            // to the console and saves them to a JobDetails-{JobId}.txt file 
            // in your output folder.
            LogJobDetails(amsAccount, job.Id);

            // Check job execution and wait for job to finish. 
            CancellationTokenSource cts = new CancellationTokenSource();

            Task progressJobTask = job.GetExecutionProgressTask(cts.Token);
            while (!(progressJobTask.IsCompleted||progressJobTask.IsFaulted|| progressJobTask.IsCanceled))
            {
                foreach (ITask jobtask in job.Tasks)
                {
                    Console.WriteLine(string.Format("Name: {0} - Progress: {1}"), jobtask.Name, jobtask.Progress.ToString());
                }
                Thread.Sleep(5000);
            }
            //progressJobTask.Wait();

            // **********
            // Optional code.  Code after this point is not required for 
            // an encoding job, but shows how to access the assets that 
            // are the output of a job, either by creating URLs to the 
            // asset on the server, or by downloading. 
            // **********

            // Get an updated job reference.
            job = GetJob(amsAccount, job.Id);
            

            // If job state is Error the event handling 
            // method for job progress should log errors.  Here we check 
            // for error state and exit if needed.
            if (job.State == JobState.Error)
            {
                Console.WriteLine("\nExiting method due to job error.");
                return null;
            }        

            return job.OutputMediaAssets.First();

        }

        static IJob GetJob(CloudMediaContext amsAccount, string jobId)
        {
            // Use a Linq select query to get an updated 
            // reference by Id. 
            var jobInstance =
                from j in amsAccount.Jobs
                where j.Id == jobId
                select j;
            // Return the job reference as an Ijob. 
            IJob job = jobInstance.FirstOrDefault();

            return job;
        }

        static IAsset GetAsset(CloudMediaContext amsAccount, string assetId)
        {
            // Use a LINQ Select query to get an asset.
            var assetInstance =
                from a in amsAccount.Assets
                where a.Id == assetId
                select a;
            // Reference the asset as an IAsset.
            IAsset asset = assetInstance.FirstOrDefault();

            return asset;
        }
        static IAsset GetAssetByName(CloudMediaContext amsAccount, string assetName)
        {
            // Use a LINQ Select query to get an asset.
            var assetInstance =
                from a in amsAccount.Assets
                where a.Name == assetName
                select a;
            // Reference the asset as an IAsset.
            IAsset asset = assetInstance.FirstOrDefault();

            return asset;
        }


        private static void StateChanged(object sender, JobStateChangedEventArgs e)
        {
            Console.WriteLine("Job state changed event:");
            Console.WriteLine("  Previous state: " + e.PreviousState);
            Console.WriteLine("  Current state: " + e.CurrentState);
            CloudMediaContext amsAccount = createAMSContext();
            switch (e.CurrentState)
            {
                case JobState.Finished:
                    Console.WriteLine();
                    Console.WriteLine("********************");
                    Console.WriteLine("Job is finished.");
                    Console.WriteLine("Please wait while local tasks or downloads complete...");
                    Console.WriteLine("********************");
                    Console.WriteLine();
                    Console.WriteLine();
                    break;
                case JobState.Canceling:
                case JobState.Queued:
                case JobState.Scheduled:
                case JobState.Processing:
                    Console.WriteLine("Please wait...\n");
                    break;
                case JobState.Canceled:
                case JobState.Error:
                    // Cast sender as a job.
                    IJob job = (IJob)sender;
                    // Display or log error details as needed.
                    LogJobStop(amsAccount, job.Id);
                    break;
                default:
                    break;
            }
        }

        private static void LogJobStop(CloudMediaContext amsAccount, string jobId)
        {
            StringBuilder builder = new StringBuilder();
            IJob job = GetJob(amsAccount, jobId);

            builder.AppendLine("\nThe job stopped due to cancellation or an error.");
            builder.AppendLine("***************************");
            builder.AppendLine("Job ID: " + job.Id);
            builder.AppendLine("Job Name: " + job.Name);
            builder.AppendLine("Job State: " + job.State.ToString());
            builder.AppendLine("Job started (server UTC time): " + job.StartTime.ToString());
            // Log job errors if they exist.  
            if (job.State == JobState.Error)
            {
                builder.Append("Error Details: \n");
                foreach (ITask task in job.Tasks)
                {
                    foreach (ErrorDetail detail in task.ErrorDetails)
                    {
                        builder.AppendLine("  Task Id: " + task.Id);
                        builder.AppendLine("    Error Code: " + detail.Code);
                        builder.AppendLine("    Error Message: " + detail.Message + "\n");
                    }
                }
            }
            builder.AppendLine("***************************\n");
            // Write the output to a local file and to the console. The template 
            // for an error output file is:  JobStop-{JobId}.txt
            Console.Write(builder.ToString());
        }

        private static void LogJobDetails(CloudMediaContext amsAccount, string jobId)
        {
            StringBuilder builder = new StringBuilder();
            IJob job = GetJob(amsAccount, jobId);

            builder.AppendLine("\nJob ID: " + job.Id);
            builder.AppendLine("Job Name: " + job.Name);
            builder.AppendLine("Job submitted (client UTC time): " + DateTime.UtcNow.ToString());
            Console.Write(builder.ToString());
        }

        private static string JobIdAsFileName(string jobID)
        {
            return jobID.Replace(":", "_");
        }

        private static string GetStreamingOriginLocatorEndPoint(CloudMediaContext amsAccount, IAsset assetToStream)
        {

            // Get a reference to the streaming manifest file from the  
            // collection of files in the asset. 
            var theManifest =
                                from f in assetToStream.AssetFiles
                                where f.Name.EndsWith(".ism")
                                select f;

            // Cast the reference to a true IAssetFile type. 
            IAssetFile manifestFile = theManifest.First();
            IAccessPolicy policy = null;

            // Create a 30-day readonly access policy. 
            policy = amsAccount.AccessPolicies.Create("Streaming policy",
                TimeSpan.FromDays(30),
                AccessPermissions.Read);

            // Create an OnDemandOrigin locator to the asset. 
            ILocator originLocator = createStreamingLocatorIfNotExists(assetToStream, policy, amsAccount);


            // Display some useful values based on the locator.
            Console.WriteLine("Streaming asset base path on origin: ");
            Console.WriteLine(originLocator.Path);
            Console.WriteLine();

            // Create a full URL to the manifest file. Use this for playback
            // in streaming media clients. 
            // get a streaming URL for any StreamingEndpoint running
            Dictionary<string, string> streamingUrlsList = new Dictionary<string, string>();
            foreach (var endpoint in amsAccount.StreamingEndpoints)
            {
                string path = originLocator.Path;
                if (endpoint.Name != "default") path = originLocator.Path.Insert(originLocator.Path.IndexOf("//")+2, endpoint.Name + "-");
                streamingUrlsList.Add(endpoint.Name, path + manifestFile.Name + "/manifest");
            }
            string urlForClientStreaming = streamingUrlsList.FirstOrDefault().Value;
            Console.WriteLine("URL to manifest for client streaming: ");
            Console.WriteLine(urlForClientStreaming);
            Console.WriteLine();

            // Display the ID of the origin locator, the access policy, and the asset.
            Console.WriteLine("Origin locator Id: " + originLocator.Id);
            Console.WriteLine("Access policy Id: " + policy.Id);
            Console.WriteLine("Streaming asset Id: " + assetToStream.Id);

            // Return the locator. 
            return urlForClientStreaming;
        }

        private static ILocator createStreamingLocatorIfNotExists(IAsset asset, IAccessPolicy policy, CloudMediaContext amsAccount)
        {
            ILocator streamingLocator = null;

            streamingLocator = amsAccount.Locators.Where(loc => loc.Type == LocatorType.OnDemandOrigin && loc.AssetId == asset.Id).FirstOrDefault(); 
            if(streamingLocator == null)
            {
                streamingLocator = amsAccount.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset,
                                                                               policy,
                                                                               DateTime.UtcNow.AddMinutes(-5));
            }
            return streamingLocator;
        }

    }

}
