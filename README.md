# FHIR Link

> FHIR Link is an open source function to help enable tighter integration between Azure Health Data Services FHIR Service (or Azure API for FHIR) and Dynamics 365 Customer Insights.

## Scenario

Different EHR systems handle merge and unmerge patient records differently. When ingesting patient data into FHIR, one common way to represent merged patients is with patient links. 

Customer Insights (D365) unifies disparate records into a single dataset that provides a unified view of that data. For instance, from FHIR and CRM systems in order to build patient segments for targeted outreach. Patients merged in FHIR through patient.link, need to be respected as a part of the unification process in Customer Insights. 

## Solution 

Our solution is an Azure Function App that sits between the FHIR Service and Customer Insights, providing a list of merged patients to Customer Insights via Azure Data Lake for processing:
- queries FHIR (Azure Health Data Services or Azure API for FHIR) for patients merged together using patient.link (get patients with Link attribute). 
- takes FHIR ids of those patients linked with the replaced-by link type, and writes it to a template CSV file readable by Customer Insights.
- the resulting CSV file is placed in an `alwaysmerge` directory
- with each run, previous files are moved from the `alwaysmerge` directory to an `archive` directory

Customer Insights can be configured to connect to this Azure Data Lake container as a data source, to be used in the Unify process as list of patients that will Always Match.

Instructions to deploy and configure this Function App can be found in [setup.md](./docs/setup.md).

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Community Discord
If you are interested in projects like this or connecting with the health and life sciences developer community, please join our Discord server at https://aka.ms/HLS-Discord. We're a technology agnostic community seeking to share and collaborate on all things related to developing healthcare solutions. For in-depth questions specific to this project, please use the "Discussions" tab on GitHub. We welcome your thoughts and feedback.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
