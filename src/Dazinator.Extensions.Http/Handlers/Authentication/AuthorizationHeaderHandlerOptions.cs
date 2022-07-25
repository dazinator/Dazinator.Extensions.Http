namespace Dazinator.Extensions.Http.Handlers.Authentication
{
    public class AuthorizationHeaderHandlerOptions : IAuthorizationHeaderHandlerOptions
    {

        /// <summary>
        /// Allow transmission of these credetnails unsecurely over http.
        /// </summary>
        public bool AllowHttp { get; set; }

        /// <summary>
        /// The name of the authorization scheme. This vaue is used to construct the authorisation header submitted with the request. Example header:
        ///    Authorization: {SchemeName} {credential}
        /// </summary>
        public string SchemeName { get; set; }

        /// <summary>
        /// If a request failes with a http 401 (unauthorized), the handler can be configured to re-attempt the request upto a maximum number of attempts specified here. 
        /// Each attempt results in a fresh credential and authorize header being appended to the request so this might reslove issues where tokens expire and new ones need to be reproduced tec.
        /// </summary>
        public int? MaxRetryUnauthorizedRequestCount { get; set; }
    }
}
