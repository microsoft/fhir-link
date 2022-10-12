using Azure.Identity;
using FhirLink;
using Hl7.Fhir.Rest;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Linq;
using System.Net.Http.Headers;

[assembly: FunctionsStartup(typeof(Startup))]
namespace FhirLink;

public class Startup : FunctionsStartup
{
    public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
    {
        FunctionsHostBuilderContext context = builder.GetContext();

        builder.ConfigurationBuilder
            .SetBasePath(context.ApplicationRootPath)
            .AddEnvironmentVariables();
    }

    public override void Configure(IFunctionsHostBuilder builder)
    {
        var baseFhirUrl = Environment.GetEnvironmentVariable("FhirDataConnection:BaseUrl");
        // todo: offer keyvault alternative via presence of keyvault environment variable.. log decision to console (don't az fx have direct integration w/ keyvault you'd take advantage of for this?)
        var fhirDataConnection = new FhirDataConnection
        {
            Tenant = Environment.GetEnvironmentVariable("FhirDataConnection:Tenant"),
            BaseUrl = baseFhirUrl,
            Scopes = ($"{baseFhirUrl}{(baseFhirUrl.Last() == '/' ? ".default" : "/.default")}").Split(','),
        };

        var blobStrageConnStr = Environment.GetEnvironmentVariable("BlobStorageConnectionString");

        builder.Services.AddScoped(options =>
        {
            var storageAccount = CloudStorageAccount.Parse(blobStrageConnStr);

            return storageAccount.CreateCloudBlobClient();
        });

        builder.Services.AddScoped(options =>
        {
            var settings = new FhirClientSettings
            {
                PreferredFormat = ResourceFormat.Json,
                PreferredReturn = Prefer.ReturnMinimal
            };

            var creds = new DefaultAzureCredential();
            var fhirToken = creds.GetToken(new Azure.Core.TokenRequestContext(fhirDataConnection.Scopes, tenantId: fhirDataConnection.Tenant));
            var client = new FhirClient(fhirDataConnection.BaseUrl, settings);

            client.RequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fhirToken.Token);

            return client;
        });
    }
}