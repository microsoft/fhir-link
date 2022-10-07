using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace FhirLink;

public class FhirLink
{
    private readonly string ENTITY_TYPE_TOKEN = "AzureAPIforFHIR_Patient";
    private readonly string CONTAINER_NAME;

    private readonly CloudBlobClient _blobStorageClient;
    private readonly FhirClient _fhirClient;

    public FhirLink(CloudBlobClient blobStorageClient, FhirClient fhirClient)
    {
        _blobStorageClient = blobStorageClient;
        _fhirClient = fhirClient;

        CONTAINER_NAME = Environment.GetEnvironmentVariable("BlobStorageContainerName") ?? "test";
    }

    [FunctionName("BuildMergedPatientCsv")]
    public async Task RunAsync([TimerTrigger("0 0 1 * * *", RunOnStartup = true)] TimerInfo timer, ILogger log)
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

            log.LogInformation("Building CSV of merged patients");

            var container = _blobStorageClient.GetContainerReference(CONTAINER_NAME);
            await container.CreateIfNotExistsAsync();

            // todo: Settle on filename format
            var blobName = $"merged_patients_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

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
}
