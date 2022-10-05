using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace fhirlink
{
    public class FhirLink
    {
        private readonly FhirClient _fhirClient;

        public FhirLink(FhirClient fhirClient)
        {
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

                // {{fhirurl}}/Patient?link:missing=false&_elements=identifier,link

                //var patients = await _fhirClient.GetAsync("/Patients");


                var find = await _fhirClient.SearchAsync(q);

                var patients = find.Entry.Select(ec => ec.Resource as Patient).ToList();

                // need to find correct structure for composite key of the ids or just unique list...?
                var patientPairs = new Dictionary<(string, string), string>();

                // organize patients into unique patient pairs using dictionary
                foreach (var patient in patients)
                {
                    // only get replaces links
                    foreach(var link in patient.Link.Where(l => l.Type == Patient.LinkType.Replaces))
                    {
                        var replacedPatientId = link.Other.Reference.Split('/').LastOrDefault();

                        patientPairs.Add((patient.Id, replacedPatientId), null);
                    }
                }

                log.LogInformation("Building CSV of merged patients");

                var filename = "merged_patients.csv";

                var filepath = "your_path.csv";
                using (StreamWriter writer = new(new FileStream(filepath,
                FileMode.Create, FileAccess.Write)))
                {
                    // foreach patient pair, write a line
                    writer.WriteLine("sep=,");
                    writer.WriteLine("Hello, Goodbye");
                }

                log.LogInformation($"Uploading {filename} to data lake.");
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
}
