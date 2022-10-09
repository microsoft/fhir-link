using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace FhirLink;

public class FhirLink
{
    private readonly string ENTITY_TYPE_TOKEN;
    private readonly string CONTAINER_NAME;
    private readonly string ACTIVE_FOLDER_NAME = "alwaysmerge";
    private readonly string ARCHIVE_FOLDER_NAME = "archive";

    private readonly CloudBlobClient _blobStorageClient;
    private readonly FhirClient _fhirClient;

    public FhirLink(CloudBlobClient blobStorageClient, FhirClient fhirClient)
    {
        _blobStorageClient = blobStorageClient;
        _fhirClient = fhirClient;

        CONTAINER_NAME = Environment.GetEnvironmentVariable("BlobStorageContainerName") ?? throw new Exception("Configuration BlobStorageContainerName invalid");
        ENTITY_TYPE_TOKEN = Environment.GetEnvironmentVariable("CustomerInsightsEntity") ?? throw new Exception("Configuration CustomerInsightsEntity invalid");
    }
     
    [FunctionName("BuildMergedPatientCsv")]
    public async Task RunAsync([TimerTrigger("%TimerTrigger%", RunOnStartup = true)] TimerInfo timer, ILogger log)
    {
        try
        {
            log.LogInformation($"Run started at {DateTime.UtcNow}");

            var q = new SearchParams().Where("link:missing=false").Select("id", "link"); 

            log.LogInformation("Querying FHIR for patients with links");

            // The cast here will get rid of non-patients (ideally would be better to filter with the SearchParams)
            var find = await _fhirClient.SearchAsync<Patient>(q);

            var patients = find.Entry.Select(ec => ec.Resource as Patient).ToList();

            var patientPairs = new Dictionary<(string, string), string>();

            // organize patients into unique patient pairs using dictionary
            foreach (var patient in patients)
            {
                // only get replaced-by links
                foreach (var link in patient.Link.Where(l => l.Type == Patient.LinkType.ReplacedBy))
                {
                    var linkedPatientId = link.Other.Reference.Split('/').LastOrDefault();

                    patientPairs.Add((patient.Id, linkedPatientId), null); // want a unique combo of id and link, don't need value
                }
            }

            var container = _blobStorageClient.GetContainerReference(CONTAINER_NAME);
            await container.CreateIfNotExistsAsync();

            var movedBlobCount = await MoveBlobs(container, ACTIVE_FOLDER_NAME, ARCHIVE_FOLDER_NAME);
            if(movedBlobCount > 0)
                log.LogInformation($"Moved {movedBlobCount} previous output {(movedBlobCount > 1 ? "files": "file")} from '{ACTIVE_FOLDER_NAME}' to '{ARCHIVE_FOLDER_NAME}'.");

            log.LogInformation("Building CSV of merged patients");
            var blobName = $"{ACTIVE_FOLDER_NAME}/merged_patients_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            var blob = container.GetBlockBlobReference(blobName);

            using CloudBlobStream stream = await blob.OpenWriteAsync();

            stream.Write(Encoding.Default.GetBytes("Entity1,Entity1Key,Entity2,Entity2Key\n"));

            foreach (var patientPair in patientPairs)
            {
                var lineStr = $"{ENTITY_TYPE_TOKEN},{patientPair.Key.Item1},{ENTITY_TYPE_TOKEN},{patientPair.Key.Item2}\n";

                stream.Write(Encoding.Default.GetBytes(lineStr));
            }

            stream.Flush();
            stream.Close();

            log.LogInformation($"Uploading {blobName} to data lake.");
        }
        catch (Exception exception)
        {
            log.LogError(exception.Message);
        }
        finally
        {
            log.LogInformation($"Run completed at {DateTime.UtcNow}");

            log.LogInformation($"Next run scheduled at {timer.Schedule.GetNextOccurrence(DateTime.UtcNow)}");
        }
    }

    private async Task<int> MoveBlobs(CloudBlobContainer container, string srcDirectory, string dstDirectory)
    {
        var blobCount = 0;

        foreach (var blob in await ListBlobsAsync(container, srcDirectory))
        {
            var srcPath = container.GetBlockBlobReference(blob.Name);
            var rawBlobName = blob.Name.Split("/").LastOrDefault();
            var dstPath = container.GetBlockBlobReference($"{dstDirectory}/{rawBlobName}");
            await dstPath.StartCopyAsync(srcPath);
            await srcPath.DeleteAsync();
            blobCount++;
        }
        return blobCount;
    }


    private async Task<IEnumerable<CloudBlockBlob>> ListBlobsAsync(CloudBlobContainer container, string directoryPath,
        BlobListingDetails listingDetails = BlobListingDetails.None, BlobRequestOptions options = null, bool recurse = false)
    {
        var foundBlobs = new List<CloudBlockBlob>();
        BlobResultSegment segment = null;
        var segmentResults = new List<IListBlobItem>();
        while (segment == null || segment.ContinuationToken != null)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                segment = await container.ListBlobsSegmentedAsync(
                    useFlatBlobListing: false,
                    blobListingDetails: listingDetails,
                    maxResults: null,
                    currentToken: segment?.ContinuationToken,
                    options: options,
                    operationContext: null,
                    prefix: null);
            }
            else
            {
                var dir = container.GetDirectoryReference(directoryPath);
                segment = await dir.ListBlobsSegmentedAsync(
                    useFlatBlobListing: false,
                    blobListingDetails: listingDetails,
                    maxResults: null,
                    currentToken: segment?.ContinuationToken,
                    options: options,
                    operationContext: null);
            }

            segmentResults.AddRange(segment.Results);
        }

        if (!segmentResults.Any())
            return foundBlobs;

        if (recurse)
        {
            foreach (var dir in segmentResults.OfType<CloudBlobDirectory>())
            {
                var subDir = dir.Uri.AbsolutePath.Substring($"/{container.Name}/".Length);
                foundBlobs.AddRange(await ListBlobsAsync(container, subDir, recurse: true));
            }
        }

        foundBlobs.AddRange(segmentResults.OfType<CloudBlockBlob>());
        return foundBlobs;
    }
}
