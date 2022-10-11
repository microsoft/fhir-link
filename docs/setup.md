# Prerequisites

- Azure access required: 
    - Application Administrator
    - Privileged Role Administrator
    - User Administrator
- Azure FHIR Service deployed and configured
    - Azure Health Data Services or Azure API for FHIR
- Existing Storage Account where Customer Insights connects to for data ingestion
    - If not already feeding data into Customer Insights from an ADLS storage account, see documentation on required setup: https://learn.microsoft.com/en-us/dynamics365/customer-insights/connect-common-data-model
    - Create a new container for a new Data Source in Customer Insights

# Setup Azure Resources

1. Create App Registration for authentication to FHIR
    - Option 1: via Powershell or Cloud Shell. Script available here: https://gist.github.com/pjirsa/774e2d80ce6d161db45d60893b0a39f3
    - Option 2: via the Azure Portal
        - Browse into App Registrations, and create new with single tenant access. No redirect URI is necessary.
        - Browse into your deployed Azure FHIR Service resource, and add a Role Assignment in Access control (IAM)
            - Role: FHIR Data Reader
            - Member: User, group or service principal - App Registration just created
2. In the Azure Portal, create a new Function App resource to deploy code into
    - Function App properties:
        - Publish: Code
        - Runtime stack: .NET
    - Once complete, go to the new resource
    - Click the Get Publish Profile button to download the profile


# Deploy Code to Function App using GitHub workflow

1. Create your own Fork of this repo
2. Add the Function App Publish Profile as a Repo Secret
    - In your forked repo, go to Settings > Secrets > Actions
    - Add a New Repository Secret
        - name: fhir_link20221005124848_FFFF
        - value: paste publish profile from the downloaded file
3. Go to the Actions tab, and enable workflows in this forked repository
4. Update the GitHub workflow with your deployment details
    - Open and edit the `fhir-link20221005124848.yml` workflow located in the .github/workflows directory
    - Update line 7 with your Function App Name
    - Ensure line 31 refers to the correct repo secret name (created in previous step)
    - Commit changes to your forked repo
5. Go to the Actions tab, and confirm the workflow run was successful

# Set App Settings in Configurations

1. Open the Function App in the Azure Portal, and create the following app settings in the Configuration blade, setting values with the details of your environment. 

> You can use this template [configuration.md](./samples/configuration.md) for quick addition of these app settings via the Advanced editor. It includes a sample cron expression to run every 5 minutes for testing purposes.

| Name  | Description   | Example   |
|-------------- | -------------- | -------------- |
| BlobStorageConnectionString | Connection string from the Access Keys blade of the storage account | `DefaultEndpointsProtocol=[https];AccountName=[storageAccountName];AccountKey=[storageAccountKey];EndpointSuffix=[storageAccountEndpointSuffix]` |
| BlobStorageContainerName | The name of the container in the Storage Account. This container should be dedicated to this function. | |
| FhirDataConnection:BaseUrl |  Base URL of the Azure FHIR Service. Copy from the FHIR Metadata endpoint in FHIR Service resource (remove metadata). | `https://[FHIRServiceName].azurehealthcareapis.com/` |
| FhirDataConnection:ClientId | Application (client) ID of the App Registration created in previous steps (copy from Overview blade) | |
| FhirDataConnection:ClientSecret | App Registration secret value, generated in the Certificates & Secrets blade of the App Registration  | |
| FhirDataConnection:Tenant | The tenant ID where the FHIR Service is deployed. This can be found in Azure Active Directory | |
| CustomerInsightsEntity | The name of the Entity in Customer Insights containing patients from the FHIR Service | |
| TimerTrigger | In Cron, the desired recurrence period. This example will run every 4 hours starting at 3am. | `0 0 3/4? * * *` | 
2. Save all configurations, and validate the function runs successfully.

> NOTE: Once working configurations are validated, it is highly recommended that you store connection string and authentication values in a key vault.

3. Create or use an existing Key Vault to store sensitive configurations.
4. Update the app settings to reference these key value secrets according to documentation: https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references?tabs=azure-cli#reference-syntax



