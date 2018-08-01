using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace RecognizeSpeech
{
    class Program
    {
        private static async Task ProcessAsync()
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;
            string sourceFile = null;
            string destinationFile = null;

            // Retrieve the connection string for use with the application. The storage connection string is stored
            // in an environment variable on the machine running the application called storageconnectionstring.
            // If the environment variable is created after the application is launched in a console or with Visual
            // Studio, the shell needs to be closed and reloaded to take the environment variable into account.
            string storageConnectionString = Environment.GetEnvironmentVariable("storageconnectionstring");

            // Check whether the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Create a container called 'quickstartblobs' and append a GUID value to it to make the name unique. 
                    cloudBlobContainer = cloudBlobClient.GetContainerReference("quickstartblobs" + Guid.NewGuid().ToString());
                    await cloudBlobContainer.CreateAsync();
                    Console.WriteLine("Created container '{0}'", cloudBlobContainer.Name);
                    Console.WriteLine();

                    // Set the permissions so the blobs are public. 
                    BlobContainerPermissions permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    await cloudBlobContainer.SetPermissionsAsync(permissions);

                    // Create a file in your local MyDocuments folder to upload to a blob.
                    string localPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string localFileName = "QuickStart_" + Guid.NewGuid().ToString() + ".txt";
                    sourceFile = Path.Combine(localPath, localFileName);
                    // Write text to the file.
                    File.WriteAllText(sourceFile, "Hello, World!");

                    Console.WriteLine("Temp file = {0}", sourceFile);
                    Console.WriteLine("Uploading to Blob storage as blob '{0}'", localFileName);
                    Console.WriteLine();

                    // Get a reference to the blob address, then upload the file to the blob.
                    // Use the value of localFileName for the blob name.
                    CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(localFileName);
                    await cloudBlockBlob.UploadFromFileAsync(sourceFile);

                    // List the blobs in the container.
                    Console.WriteLine("Listing blobs in container.");
                    BlobContinuationToken blobContinuationToken = null;
                    do
                    {
                        var results = await cloudBlobContainer.ListBlobsSegmentedAsync(null, blobContinuationToken);
                        // Get the value of the continuation token returned by the listing call.
                        blobContinuationToken = results.ContinuationToken;
                        foreach (IListBlobItem item in results.Results)
                        {
                            Console.WriteLine(item.Uri);
                        }
                    } while (blobContinuationToken != null); // Loop while the continuation token is not null.
                    Console.WriteLine();

                    // Download the blob to a local file, using the reference created earlier. 
                    // Append the string "_DOWNLOADED" before the .txt extension so that you can see both files in MyDocuments.
                    destinationFile = sourceFile.Replace(".txt", "_DOWNLOADED.txt");
                    Console.WriteLine("Downloading blob to {0}", destinationFile);
                    Console.WriteLine();
                    await cloudBlockBlob.DownloadToFileAsync(destinationFile, FileMode.Create);
                }
                catch (StorageException ex)
                {
                    Console.WriteLine("Error returned from the service: {0}", ex.Message);
                }
                finally
                {
                    Console.WriteLine("Press any key to delete the sample files and example container.");
                    Console.ReadLine();
                    // Clean up resources. This includes the container and the two temp files.
                    Console.WriteLine("Deleting the container and any blobs it contains");
                    if (cloudBlobContainer != null)
                    {
                        await cloudBlobContainer.DeleteIfExistsAsync();
                    }
                    Console.WriteLine("Deleting the local source file and local downloaded files");
                    Console.WriteLine();
                    File.Delete(sourceFile);
                    File.Delete(destinationFile);
                }
            }
            else
            {
                Console.WriteLine(
                    "A connection string has not been defined in the system environment variables. " +
                    "Add a environment variable named 'storageconnectionstring' with your storage " +
                    "connection string as a value.");
            }
        }
        public static async Task RecognizeSpeechFromFileAsync()
        {
            // Creates an instance of a speech factory with specified subscription key and service region.
            // Replace with your own subscription key and service region (e.g., "westus").
            var factory = SpeechFactory.FromSubscription("f93eb671db224bc68f499e8fe734a084", "northeurope");

            var stopRecognition = new TaskCompletionSource<int>();

            // Creates a speech recognizer using file as audio input.
            // Replace with your own audio file name.
            using (var recognizer = factory.CreateSpeechRecognizerWithFileInput(@"Denis-hackathon.wav"))
            {
                // Subscribes to events.
                recognizer.IntermediateResultReceived += (s, e) => {
                    Console.WriteLine($"\n    Partial result: {e.Result.Text}.");
                };

                recognizer.FinalResultReceived += (s, e) => {
                    var result = e.Result;
                    Console.WriteLine($"Recognition status: {result.RecognitionStatus.ToString()}");
                    switch (result.RecognitionStatus)
                    {
                        case RecognitionStatus.Recognized:
                            Console.WriteLine($"\n    Final result: Text: {result.Text}, Offset: {result.OffsetInTicks}, Duration: {result.Duration}.");
                            break;
                        case RecognitionStatus.InitialSilenceTimeout:
                            Console.WriteLine("The start of the audio stream contains only silence, and the service timed out waiting for speech.\n");
                            break;
                        case RecognitionStatus.InitialBabbleTimeout:
                            Console.WriteLine("The start of the audio stream contains only noise, and the service timed out waiting for speech.\n");
                            break;
                        case RecognitionStatus.NoMatch:
                            Console.WriteLine("The speech was detected in the audio stream, but no words from the target language were matched. Possible reasons could be wrong setting of the target language or wrong format of audio stream.\n");
                            break;
                        case RecognitionStatus.Canceled:
                            Console.WriteLine($"There was an error, reason: {result.RecognitionFailureReason}");
                            break;
                    }
                };

                recognizer.RecognitionErrorRaised += (s, e) => {
                    Console.WriteLine($"\n    An error occurred. Status: {e.Status.ToString()}, FailureReason: {e.FailureReason}");
                    stopRecognition.TrySetResult(0);
                };

                recognizer.OnSessionEvent += (s, e) => {
                    Console.WriteLine($"\n    Session event. Event: {e.EventType.ToString()}.");
                    // Stops recognition when session stop is detected.
                    if (e.EventType == SessionEventType.SessionStoppedEvent)
                    {
                        Console.WriteLine($"\nStop recognition.");
                        stopRecognition.TrySetResult(0);
                    }
                };

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                // Waits for completion.
                // Use Task.WaitAny to keep the task rooted.
                Task.WaitAny(new[] { stopRecognition.Task });

                // Stops recognition.
                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
        }
        public static async Task ContinuousRecognitionWithFileAsync()
        {
            // <recognitionContinuousWithFile>
            // Creates an instance of a speech factory with specified subscription key and service region.
            // Replace with your own subscription key and service region (e.g., "westus").
            var factory = SpeechFactory.FromSubscription("f93eb671db224bc68f499e8fe734a084", "northeurope");

            var stopRecognition = new TaskCompletionSource<int>();

            // Creates a speech recognizer using file as audio input.
            // Replace with your own audio file name.
            string finalTextResult = " ";
            using (var recognizer = factory.CreateSpeechRecognizerWithFileInput(@"C:\\Denis-hackathon.wav"))
            {
                // Subscribes to events.
                recognizer.IntermediateResultReceived += (s, e) => {
                    Console.WriteLine($"\n    Partial result: {e.Result.Text}.");
                };

                recognizer.FinalResultReceived += (s, e) => {
                    var result = e.Result;
                   
                    Console.WriteLine($"Recognition status: {result.RecognitionStatus.ToString()}");
                    switch (result.RecognitionStatus)
                    {
                        case RecognitionStatus.Recognized:
                            Console.WriteLine($"\n    Final result: Text: {result.Text}, Offset: {result.OffsetInTicks}, Duration: {result.Duration}.");
                            finalTextResult = finalTextResult + result.Text;
                            break;
                        case RecognitionStatus.InitialSilenceTimeout:
                            Console.WriteLine("The start of the audio stream contains only silence, and the service timed out waiting for speech.\n");
                            break;
                        case RecognitionStatus.InitialBabbleTimeout:
                            Console.WriteLine("The start of the audio stream contains only noise, and the service timed out waiting for speech.\n");
                            break;
                        case RecognitionStatus.NoMatch:
                            Console.WriteLine("The speech was detected in the audio stream, but no words from the target language were matched. Possible reasons could be wrong setting of the target language or wrong format of audio stream.\n");
                            break;
                        case RecognitionStatus.Canceled:
                            Console.WriteLine($"There was an error, reason: {result.RecognitionFailureReason}");
                            break;
                    }
                    //Console.WriteLine(finalTextResult);
                };

                recognizer.RecognitionErrorRaised += (s, e) => {
                    Console.WriteLine($"\n    An error occurred. Status: {e.Status.ToString()}, FailureReason: {e.FailureReason}");
                    stopRecognition.TrySetResult(0);
                };

                recognizer.OnSessionEvent += (s, e) => {
                    Console.WriteLine($"\n    Session event. Event: {e.EventType.ToString()}.");
                    // Stops recognition when session stop is detected.
                    if (e.EventType == SessionEventType.SessionStoppedEvent)
                    {
                        Console.WriteLine($"\nStop recognition.");
                        stopRecognition.TrySetResult(0);
                    }
                };

                // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
                
                // Waits for completion.
                // Use Task.WaitAny to keep the task rooted.
                Task.WaitAny(new[] { stopRecognition.Task });

                // Stops recognition.
                await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            // </recognitionContinuousWithFile>
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;
            string sourceFile = null;
            string destinationFile = null;
            string storageConnectionString = Environment.GetEnvironmentVariable("storageconnectionstring");
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Create a container called 'quickstartblobs' and append a GUID value to it to make the name unique. 
                    // cloudBlobContainer = cloudBlobClient.GetContainerReference("quickstartblobs" + Guid.NewGuid().ToString());
                    cloudBlobContainer = cloudBlobClient.GetContainerReference("quickstartblobs");
                    // await cloudBlobContainer.CreateAsync();
                    //Console.WriteLine("Created container '{0}'", cloudBlobContainer.Name);
                    Console.WriteLine();

                    // Set the permissions so the blobs are public. 
                    BlobContainerPermissions permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    await cloudBlobContainer.SetPermissionsAsync(permissions);

                    // Create a file in your local MyDocuments folder to upload to a blob.
                    string localPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string localFileName = "QuickStart_" + Guid.NewGuid().ToString() + ".txt";
                    sourceFile = Path.Combine(localPath, localFileName);
                    // Write text to the file.
                    File.WriteAllText(sourceFile, finalTextResult);

                    Console.WriteLine("Temp file = {0}", sourceFile);
                    Console.WriteLine("Uploading to Blob storage as blob '{0}'", localFileName);
                    Console.WriteLine();

                    // Get a reference to the blob address, then upload the file to the blob.
                    // Use the value of localFileName for the blob name.
                    CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(localFileName);
                    await cloudBlockBlob.UploadFromFileAsync(sourceFile);

                    // List the blobs in the container.
                    Console.WriteLine("Listing blobs in container.");
                    BlobContinuationToken blobContinuationToken = null;
                    do
                    {
                        var results = await cloudBlobContainer.ListBlobsSegmentedAsync(null, blobContinuationToken);
                        // Get the value of the continuation token returned by the listing call.
                        blobContinuationToken = results.ContinuationToken;
                        foreach (IListBlobItem item in results.Results)
                        {
                            Console.WriteLine(item.Uri);
                        }
                    } while (blobContinuationToken != null); // Loop while the continuation token is not null.
                    Console.WriteLine();

                    // Download the blob to a local file, using the reference created earlier. 
                    // Append the string "_DOWNLOADED" before the .txt extension so that you can see both files in MyDocuments.
                    destinationFile = sourceFile.Replace(".txt", "_DOWNLOADED.txt");
                    Console.WriteLine("Downloading blob to {0}", destinationFile);
                    Console.WriteLine();
                    await cloudBlockBlob.DownloadToFileAsync(destinationFile, FileMode.Create);
                }
                catch (StorageException ex)
                {
                    Console.WriteLine("Error returned from the service: {0}", ex.Message);
                }
                finally
                {
                    /*Console.WriteLine("Press any key to delete the sample files and example container.");
                    Console.ReadLine();
                    // Clean up resources. This includes the container and the two temp files.
                    Console.WriteLine("Deleting the container and any blobs it contains");
                    if (cloudBlobContainer != null)
                    {
                        await cloudBlobContainer.DeleteIfExistsAsync();
                    }
                    Console.WriteLine("Deleting the local source file and local downloaded files");
                    Console.WriteLine();
                    File.Delete(sourceFile);
                    File.Delete(destinationFile);*/
                }
            }
            else
            {
                Console.WriteLine(
                    "A connection string has not been defined in the system environment variables. " +
                    "Add a environment variable named 'storageconnectionstring' with your storage " +
                    "connection string as a value.");
            }
        
    
}
        public static async Task RecognizeSpeechAsync()
        {
            // Creates an instance of a speech factory with specified
            // subscription key and service region. Replace with your own subscription key
            // and service region (e.g., "westus").
            var factory = SpeechFactory.FromSubscription("f93eb671db224bc68f499e8fe734a084", "northeurope");

            // Creates a speech recognizer.
            using (var recognizer = factory.CreateSpeechRecognizer())
            {
                Console.WriteLine("Say something...");
                

                // Performs recognition.
                // RecognizeAsync() returns when the first utterance has been recognized, so it is suitable 
                // only for single shot recognition like command or query. For long-running recognition, use
                // StartContinuousRecognitionAsync() instead.
                var result = await recognizer.RecognizeAsync();

                // Checks result.
                if (result.RecognitionStatus != RecognitionStatus.Recognized)
                {
                    Console.WriteLine($"Recognition status: {result.RecognitionStatus.ToString()}");
                    if (result.RecognitionStatus == RecognitionStatus.Canceled)
                    {
                        Console.WriteLine($"There was an error, reason: {result.RecognitionFailureReason}");
                    }
                    else
                    {
                        Console.WriteLine("No speech could be recognized.\n");
                    }
                }
                else
                {
                    Console.WriteLine($"We recognized: {result.Text}");
                    /*try
                     {
                         StreamWriter sw = new StreamWriter("C:\\Test1.txt", true, Encoding.ASCII);
                         sw.Write(result.Text);
                         sw.Close();
                     }
                     catch(Exception e)
                     {
                         Console.WriteLine("Exception: " + e.Message);
                     }
                     finally
                     {
                         Console.WriteLine("Executing finally block.");
                     }*/
                    //string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    // using (StreamWriter outputFile = new StreamWriter(Path.Combine(mydocpath, "WriteLines.txt"), true))
                    // {
                    //   outputFile.WriteLine("Fourth Line";
                    //}
                    CloudStorageAccount storageAccount = null;
                    CloudBlobContainer cloudBlobContainer = null;
                    string sourceFile = null;
                    string destinationFile = null;
                    string storageConnectionString = Environment.GetEnvironmentVariable("storageconnectionstring");
                    if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
                    {
                        try
                        {
                            // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                            // Create a container called 'quickstartblobs' and append a GUID value to it to make the name unique. 
                            // cloudBlobContainer = cloudBlobClient.GetContainerReference("quickstartblobs" + Guid.NewGuid().ToString());
                            cloudBlobContainer = cloudBlobClient.GetContainerReference("quickstartblobs");
                           // await cloudBlobContainer.CreateAsync();
                            //Console.WriteLine("Created container '{0}'", cloudBlobContainer.Name);
                            Console.WriteLine();

                            // Set the permissions so the blobs are public. 
                            BlobContainerPermissions permissions = new BlobContainerPermissions
                            {
                                PublicAccess = BlobContainerPublicAccessType.Blob
                            };
                            await cloudBlobContainer.SetPermissionsAsync(permissions);

                            // Create a file in your local MyDocuments folder to upload to a blob.
                            string localPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            string localFileName = "QuickStart_" + Guid.NewGuid().ToString() + ".txt";
                            sourceFile = Path.Combine(localPath, localFileName);
                            // Write text to the file.
                            File.WriteAllText(sourceFile, result.Text);

                            Console.WriteLine("Temp file = {0}", sourceFile);
                            Console.WriteLine("Uploading to Blob storage as blob '{0}'", localFileName);
                            Console.WriteLine();

                            // Get a reference to the blob address, then upload the file to the blob.
                            // Use the value of localFileName for the blob name.
                            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(localFileName);
                            await cloudBlockBlob.UploadFromFileAsync(sourceFile);

                            // List the blobs in the container.
                            Console.WriteLine("Listing blobs in container.");
                            BlobContinuationToken blobContinuationToken = null;
                            do
                            {
                                var results = await cloudBlobContainer.ListBlobsSegmentedAsync(null, blobContinuationToken);
                                // Get the value of the continuation token returned by the listing call.
                                blobContinuationToken = results.ContinuationToken;
                                foreach (IListBlobItem item in results.Results)
                                {
                                    Console.WriteLine(item.Uri);
                                }
                            } while (blobContinuationToken != null); // Loop while the continuation token is not null.
                            Console.WriteLine();

                            // Download the blob to a local file, using the reference created earlier. 
                            // Append the string "_DOWNLOADED" before the .txt extension so that you can see both files in MyDocuments.
                            destinationFile = sourceFile.Replace(".txt", "_DOWNLOADED.txt");
                            Console.WriteLine("Downloading blob to {0}", destinationFile);
                            Console.WriteLine();
                            await cloudBlockBlob.DownloadToFileAsync(destinationFile, FileMode.Create);
                        }
                        catch (StorageException ex)
                        {
                            Console.WriteLine("Error returned from the service: {0}", ex.Message);
                        }
                        finally
                        {
                            /*Console.WriteLine("Press any key to delete the sample files and example container.");
                            Console.ReadLine();
                            // Clean up resources. This includes the container and the two temp files.
                            Console.WriteLine("Deleting the container and any blobs it contains");
                            if (cloudBlobContainer != null)
                            {
                                await cloudBlobContainer.DeleteIfExistsAsync();
                            }
                            Console.WriteLine("Deleting the local source file and local downloaded files");
                            Console.WriteLine();
                            File.Delete(sourceFile);
                            File.Delete(destinationFile);*/
                        }
                    }
                    else
                    {
                        Console.WriteLine(
                            "A connection string has not been defined in the system environment variables. " +
                            "Add a environment variable named 'storageconnectionstring' with your storage " +
                            "connection string as a value.");
                    }
                }
            }

        }
        static void Main(string[] args)
        {
            //RecognizeSpeechAsync().Wait();
            ContinuousRecognitionWithFileAsync().Wait();
            Console.WriteLine("Please press a key to continue.");
            Console.ReadLine();
        }
    }
}
