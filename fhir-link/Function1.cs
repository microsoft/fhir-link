using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace fhir_link
{
    public class Function1
    {
        [FunctionName("Function1")]
        public void Run([TimerTrigger("0 0 */4 * * *")]TimerInfo myTimer, ILogger log)
        {
            //log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            /*todo:
             * setup fhir connector class
             * query fhir for all records with links
             * convert links in the returned JOSN files to csv template
             * cleanup fhir records
             * write csv to datalakes
             */
        }
    }
}
