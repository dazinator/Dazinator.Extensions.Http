namespace Dazinator.Extensions.Http
{
    /// <summary>
    /// Encapsulates options that can be applied to an <see cref="HttpClient"/>
    /// </summary>
    public class HttpClientOptions
    {
        public bool EnableBypassInvalidCertificate { get; set; } = false;

        /// <summary>
        /// <see cref="HttpClient.BaseAddress"/>
        /// </summary>
        public string? BaseAddress { get; set; }
        /// <summary>
        /// <see cref="HttpClient.Timeout"/>
        /// </summary>
        public TimeSpan? Timeout { get; set; }
        /// <summary>
        /// <see cref="HttpClient.MaxResponseContentBufferSize"/>
        /// </summary>
        public long? MaxResponseContentBufferSize { get; set; }
        public List<string> Handlers { get; set; } = new List<string>();

        /// <summary>
        /// Apply these options to an <see cref="HttpClient"/>
        /// </summary>
        /// <param name="httpClient"></param>
        public virtual void Apply(HttpClient httpClient)
        {
            if (!string.IsNullOrWhiteSpace(BaseAddress))
            {
                httpClient.BaseAddress = new Uri(BaseAddress);
            }

            if (Timeout != null)
            {
                httpClient.Timeout = Timeout.Value;
            }

            if (MaxResponseContentBufferSize != null)
            {
                httpClient.MaxResponseContentBufferSize = MaxResponseContentBufferSize.Value;
            }
        }

    }
}
