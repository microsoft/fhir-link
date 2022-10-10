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
            - Member: User, group or service principal - Azure Healthcare APIs app
2. In the Azure Portal, create a new Function App resource to deploy code into
    - Open the new Function App, and note the Name
    - Click the Get Publish Profile button to download the profile


# Deploy Code to Function App using GitHub workflow

1. Create your own Fork of this repo
2. Add the Function App Publish Profile as a Repo Secret
    - In your forked repo, go to Settings > Secrets > Actions
    - Add a New Repository Secret
        - name: fhir_link20221005124848_FFFF
        - value: paste publish profile from the downloaded file
3. Update the GitHub workflow with your deployment details
    - Open and edit the `fhir-link20221005124848.yml` workflow located in the .github/workflows directory
    - Update line 7 with your Function App Name
    - Ensure line 31 refers to the correct repo secret name (created in previous step)
    - Commit changes to your fork - this will automatically kick off the workflow to deploy into the Function App
4. Confirm the workflow run was successful

# Set App Settings in Configurations

1. Create or use an existing Key Vault to store the credentials
    - Open the App Registration created in previous steps
    - In the Secrets blade, create a new secret and store the value in a Key Vault secret named accordingly (such as FHIR-LINK-SECRET)
    - Copy the Application (client) ID from the App Registration, and store in another Key Value secret named accordingly (such as FHIR-LINK-CLIENTID)
2. Open the Function App in the Azure Portal, and create the following app settings in the Configuration blade: 

| Name  | Value   | Notes   |
|-------------- | -------------- | -------------- |
| BlobStorageConnectionString | Customer Insights Data Source | Can be copied from the Endpoints blade of the storage account |
| BlobStorageContainerName | Container name | This container should be dedicated to this function |
| FhirDataConnection:BaseUrl | `https://[FHIRServiceName].azurehealthcareapis.com/` | Can copy from FHIR Metadata endpoint in FHIR Service resource |
| FhirDataConnection:ClientId | `@Microsoft.KeyVault(SecretUri=[VaultURI]/secrets/[SecretName]/)` | Reference to this value in Key Vault |
| FhirDataConnection:ClientSecret | `@Microsoft.KeyVault(SecretUri=[VaultURI]/secrets/[SecretName]/)` | Reference to this value in Key Vault |
| FhirDataConnection:Tenant | Tenant ID of the FHIR Server | Can be copied from the FHIR Service on the Overview blade |
| CustomerInsightsEntity | name of the Entity that contains Patient Data from FHIR | Get from the Entities area of Customer Insight |
| TimerTrigger | cron expression for recurrence period | the period of time between runs of the Function |

## References: 

- KeyVault reference syntax https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references?tabs=azure-cli#reference-syntax
- Cron expression generator https://crontab.guru/



