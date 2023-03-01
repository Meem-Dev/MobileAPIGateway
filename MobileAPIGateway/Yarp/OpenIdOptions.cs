namespace MobileAPIGateway.Yarp
{
    public class OpenIdOptions
    {
        internal const string SectionName = "Oidc";

        public string[] Scopes { get; init; } = Array.Empty<string>();
        public bool? EnableRefreshToken { get; init; } = true;
        public string Authority { get; init; } = default!;
        public string ClientId { get; init; } = default!;
        public string ClientSecret { get; init; } = default!;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(Authority))
                throw new ArgumentException($"{nameof(Authority)} is empty");

            if (string.IsNullOrWhiteSpace(ClientId))
                throw new ArgumentException($"{nameof(ClientId)} is empty");

            if (string.IsNullOrWhiteSpace(ClientSecret))
                throw new ArgumentException($"{nameof(ClientSecret)} is empty");
        }
    }
}
