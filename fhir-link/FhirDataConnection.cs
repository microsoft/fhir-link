namespace FhirLink
{
    internal class FhirDataConnection
    {
        public string Tenant { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string BaseUrl { get; set; }
        public string[] Scopes { get; set; }
    }
}
