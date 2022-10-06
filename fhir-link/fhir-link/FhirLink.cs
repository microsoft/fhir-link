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

namespace fhirlink;

public class FhirLink
{
    private readonly CloudBlobClient _blogStorageClient;
    private readonly FhirClient _fhirClient;

    public FhirLink(CloudBlobClient blogStorageClient, FhirClient fhirClient)
    {
        _blogStorageClient = blogStorageClient;
        _fhirClient = fhirClient;
    }

    [FunctionName("BuildMergedPatientCsv")]
    public async Task RunAsync([TimerTrigger("0 0 */4 * * *", RunOnStartup = true)] TimerInfo timer, ILogger log)
    {
        try
        {
            log.LogInformation($"Run started at {DateTime.UtcNow}");

            var q = new SearchParams().Where("link:missing=false").Select("identifier", "link");

            log.LogInformation("Querying FHIR for patients with links");

            var find = await _fhirClient.SearchAsync(q);

            var patients = find.Entry.Select(ec => ec.Resource as Patient).ToList();

            // need to find correct structure for composite key of the ids or just unique list...?
            var patientPairs = new Dictionary<(string, string), string>();

            // organize patients into unique patient pairs using dictionary
            foreach (var patient in patients)
            {
                // only get replaces links
                foreach (var link in patient.Link.Where(l => l.Type == Patient.LinkType.Replaces))
                {
                    var replacedPatientId = link.Other.Reference.Split('/').LastOrDefault();

                    patientPairs.Add((patient.Id, replacedPatientId), null);
                }
            }

            log.LogInformation("Building CSV of merged patients");

            var container = _blogStorageClient.GetContainerReference("test");

            // todo: clean any illegal characters and settle on filename format
            var blogName = $"merged_patients_{DateTime.UtcNow.ToShortDateString}_{DateTime.UtcNow.ToShortTimeString}.csv";

            var blob = container.GetBlockBlobReference(blogName);

            using CloudBlobStream stream = await blob.OpenWriteAsync();

            stream.Write(Encoding.Default.GetBytes("Entity1,Entity1Key,Entity2,Entity2Key\n"));

            foreach (var patientPair in patientPairs)
            {
                var lineStr = $"AzureAPIforFHIR_Patient,{patientPair.Key.Item1},AzureAPIforFHIR_Patient,{patientPair.Key.Item2}\n";

                stream.Write(Encoding.Default.GetBytes(lineStr));
            }

            stream.Flush();
            stream.Close();

            log.LogInformation($"Uploading {blogName} to data lake.");
        }
        catch (Exception exception)
        {
            log.LogError(exception.Message);
        }
        finally
        {
            log.LogInformation($"Run completed at {DateTime.UtcNow}");

            log.LogInformation($"Next run scheduled at {timer.Schedule.GetNextOccurrence}");
        }
    }
}
