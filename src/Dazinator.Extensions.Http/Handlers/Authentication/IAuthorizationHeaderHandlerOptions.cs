namespace Dazinator.Extensions.Http.Handlers.Authentication
{
    public interface IAuthorizationHeaderHandlerOptions
    {

        /// <summary>
        /// Allow transmission of these credetnails unsecurely over http.
        /// </summary>
        bool AllowHttp { get; }

        /// <summary>
        /// The name of the authorization scheme. This vaue is used to construct the authorisation header submitted with the request. Example header:
        ///    Authorization: {SchemeName} {credential}
        /// </summary>
        string SchemeName { get; set; }

        /// <summary>
        /// The maximum number of times a request should automatically be retried on authentication failure (i.e http status code 401). 
        /// </summary>
        public int? MaxRetryUnauthorizedRequestCount { get; set; }

    }
}
