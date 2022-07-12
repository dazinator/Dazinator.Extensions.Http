namespace Dazinator.Extensions.Http.Tests.Impl
{
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Options;

    public class VersioningHttpClientOptionsFactory : IOptionsFactory<HttpClientFactoryOptions>
    {
        private readonly IOptionsFactory<HttpClientFactoryOptions> _innerFactory;
        private readonly Func<string, HttpClientFactoryOptions> _createVersionedHttpClientOptions;
        private readonly HashSet<string> _names = new();
        private readonly ConcurrentDictionary<string, HttpClientFactoryOptions> _optionsCache = new();

        public VersioningHttpClientOptionsFactory(
            IEnumerable<IConfigureOptions<HttpClientFactoryOptions>> setups,
            IOptionsFactory<HttpClientFactoryOptions> innerFactory,
            Func<string, HttpClientFactoryOptions> createVersionedHttpClientOptions)
        {
            _innerFactory = innerFactory;
            _createVersionedHttpClientOptions = createVersionedHttpClientOptions;
            foreach (var item in setups)
            {
                if (item is ConfigureNamedOptions<HttpClientFactoryOptions> namedSetup)
                {
                    _ = _names.Add(namedSetup.Name);
                }
            }
        }

        public HttpClientFactoryOptions GetOptions(string name)
        {
            // we use a convention
            // versioned clients will be named "foo-#{versionstamp}"
            // if a versioned name is supplied (i.e ends with -#{whatever} and we don't find an options with that version name,
            // then we build a new one.

            // if name supplies matches a name registered using named options (i.e on startup) then fallback to default implementation, i.e don't use
            // the functionality provided by this library for dealing with http client name versioning.
            if (_names.Contains(name))
            {
                return _innerFactory.Create(name);
            }

            // otherwise treat the name as a versioning http client name
            var versionTag = name.Split('-').LastOrDefault();
            if (string.IsNullOrWhiteSpace(versionTag))
            {
                // could log a warning - as this doesn't appear to be a name that was either configured on startup, or that uses a version tag..
                // so fallback to the default implementation in this case - for http clinet names not registered on startup the default factory just uses default options.
                return _innerFactory.Create(versionTag);
            }

            return _optionsCache.GetOrAdd(name, (key) => _createVersionedHttpClientOptions(key));
        }

        public HttpClientFactoryOptions Create(string name) => GetOptions(name);
    }
}



