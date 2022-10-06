﻿using fhirlink;
using Hl7.Fhir.Rest;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Net.Http.Headers;

[assembly: FunctionsStartup(typeof(Startup))]
namespace fhirlink;

// overkill for non-durable function?
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
        // todo: offer keyvault alternative via presence of keyvault environment variable.. log decision to console (don't az fx have direct integration w/ keyvault you'd take advantage of for this?)
        var fhirDataConnection = new FhirDataConnection
        {
            Tenant = Environment.GetEnvironmentVariable("FhirDataConnection:Tenant"),
            ClientId = Environment.GetEnvironmentVariable("FhirDataConnection:ClientId"),
            ClientSecret = Environment.GetEnvironmentVariable("FhirDataConnection:ClientSecret"),
            BaseUrl = Environment.GetEnvironmentVariable("FhirDataConnection:BaseUrl"),
            Scopes = Environment.GetEnvironmentVariable("FhirDataConnection:Scopes").Split(',')
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

            var app = ConfidentialClientApplicationBuilder.Create(fhirDataConnection.ClientId)
                .WithClientSecret(fhirDataConnection.ClientSecret)
                .Build();

            var tokenResult = app.AcquireTokenForClient(fhirDataConnection.Scopes)
            .WithAuthority(AzureCloudInstance.AzurePublic, fhirDataConnection.Tenant)
            .ExecuteAsync().Result;

            var client = new FhirClient(fhirDataConnection.BaseUrl, settings);

            client.RequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

            return client;
        });
    }
}