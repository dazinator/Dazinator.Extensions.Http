## Configuring Http Clients at runtime - solved

Microsoft provide `IHttpClientFactory` which is great.

However it only lets you configure named `HttpClients` once - when building your DI container on application startup. Once you use the http client for the first time, that configuration is locked in.

What if within your application, you want to allow the configuration for the `HttpClient` to be amended - for example the `BaseAddress` or the `Handler's that are active.

This library addresses this problem, not through allowing you to "mutate" any existing objects thats `IHttpClientFactory` knows about, but instead, allowing you to introduce newly named `HttpClient`s which will be lazily built on demand. You can therefore request a named http client with a name like "foo-v1" and then later, when you know you have new confiugration to apply, you can request "foo-v2" and at that point a new http client will be built and you can apply the latest configuration during that process.

Technically, although you only really need `Dazinator.Extensions.Options` as the key enabler to solve this problem - see that repo for details, this library builds upon the raw capability added there, to provide some additional capabilities, to make things more easily consumable, and easier to configure http clients, with concepts such as handlers etc.

## Usage

Import nuget package `Dazinator.Extensions.Http`

There are different usage patterns, starting simple then varying in sophistication.

### Simple

```cs
   services.AddHttpClient();
  
   services.ConfigureHttpClientFactory((sp, httpClientName, options) =>
   {
       // configure this named http client however you see fit
       options.HttpClientActions.Add(a =>
       {
           a.BaseAddress = new Uri($"http://{httpClientName}.localhost/");
       });
   });


   var sp = services.BuildServiceProvider();
   var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

   // Now get your http client, and version the name at runtime when your configuration is changed:
   using var httpClient = sut.CreateClient("foo-v1");
   Assert.Equal($"http://foo-v1.localhost/", httpClient.BaseAddress.ToString());

   // Configuration of the http client was changed somewhere.. use a new name.
   using var httpClient2 = sut.CreateClient("foo-v2");
   Assert.Equal($"http://foo-v2.localhost/", httpClient.BaseAddress.ToString());

```

### More advanced

Rather than configuring the `HttpClientFactoryOptions` directly, you can configure a "smarter" set of options provided by this library.
This "smarter" set of options is more focues on what you want to achieve. Based on this, it will configure the `HttpClientFactoryOptions` accordingly.


```cs
  services.AddHttpClient();
  services.ConfigureHttpClient((sp, name, options) =>
  {
      // load settings from some store using unique http client name (which can version)
      if (name.StartsWith("foo-"))
      {
          options.BaseAddress = $"http://{name}.localhost";
          options.EnableBypassInvalidCertificate = true;
          options.MaxResponseContentBufferSize = 2000;
          options.Timeout = TimeSpan.FromMinutes(2);
          // options.Handlers.Add(statusOkHandlerName);
      }
      if (name.StartsWith("bar-"))
      {
          options.BaseAddress = $"http://{name}.localhost";
          options.EnableBypassInvalidCertificate = true;
          options.MaxResponseContentBufferSize = 2000;
          options.Timeout = TimeSpan.FromMinutes(2);
         // options.Handlers.Add(statusNotFoundHandlerName);
      }
  });

```

Note: You are able to fulfill common requirements. For example - you can `EnableBypassInvalidCertificate` if your client is talking to a server with an invalid SSL cert. Behind the scenes this is more work to do if configuring the `HttpClientFactoryOptions` directly so it relieves you from this.


## Super advanced - configuring handlers.

Suppose you want to create a re-usable `DelegatingMessageHandler` that can be configured / enabled for various http clients in your application.

Create the handler. 
Here is an example generic handler that simply invokes invokes whatever Func you supply in the constructor. 
It also gets passed in the http client name, and an `IOptionsMontitor<TOptions>` so it can have its own options that vary from http client name to http client name.


```cs

    public class DelegatingHandlerWithOptions<TOptions> : DelegatingHandler
    {
        private readonly string _httpClientName;
        private readonly IOptionsMonitor<TOptions> _optionsMontitor;
        private readonly Func<HttpRequestMessage, TOptions, CancellationToken, Task<HttpResponseMessage>> _sendAsync;


        public DelegatingHandlerWithOptions(string httpClientName, IOptionsMonitor<TOptions> optionsMontitor, Func<HttpRequestMessage, TOptions, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _httpClientName = httpClientName;
            _optionsMontitor = optionsMontitor;
            _sendAsync = sendAsync;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var handlerOptonsForNamedHttpClient = _optionsMontitor.Get(_httpClientName);
            return await _sendAsync.Invoke(request, handlerOptonsForNamedHttpClient, cancellationToken);

        }
    }

    public class StatusHandlerOptions
    {
        public System.Net.HttpStatusCode StatusCode { get; set; }
    }



```

Now you can register this handler with the handler registry, and then configure it on a http client like so:

```cs
   services.AddHttpClient();

   // lazily configure different options for the handler, configured based on http client name.
   services.Configure<StatusHandlerOptions>((sp, name, options) =>
   {
       if (name.StartsWith("foo-"))
       {
           options.StatusCode = System.Net.HttpStatusCode.OK;
       }
       if (name.StartsWith("bar-"))
       {
           options.StatusCode = System.Net.HttpStatusCode.NotFound;
       }
   });

   // Rgister the handler with the handler registry - using the name "status-handler":-
   var handlerRegistry = services.ConfigureHttpClientHandlerRegistry((registry) =>
       registry.RegisterHandler<DelegatingHandlerWithOptions<StatusHandlerOptions>>("status-handler", (r) =>
           r.Factory = (sp, httpClientName) =>
           {
               var optionsMontior = sp.GetRequiredService<IOptionsMonitor<StatusHandlerOptions>>();
               return new DelegatingHandlerWithOptions<StatusHandlerOptions>(httpClientName, optionsMontior, (request, handlerOptions, cancelToken) =>
                                       {
                                           var result = new HttpResponseMessage(handlerOptions.StatusCode);
                                           return Task.FromResult(result);
                                       });
           }));


   // Configures HttpClientOptions on demand when a distinct httpclient name is requested.
   services.ConfigureHttpClient((sp, name, options) =>
   {
       if (name.StartsWith("foo-"))
       {
           options.BaseAddress = $"http://{name}.localhost";
           options.EnableBypassInvalidCertificate = true;
           options.MaxResponseContentBufferSize = 2000;
           options.Timeout = TimeSpan.FromMinutes(2);
           // Both clients have the same handler "status-handler" added.
           // But the handler implementation also has its own optios per named http client - allowing it to behave differently per client.
           options.Handlers.Add("status-handler");
       }
       if (name.StartsWith("bar-"))
       {
           options.BaseAddress = $"http://{name}.localhost";
           options.EnableBypassInvalidCertificate = true;
           options.MaxResponseContentBufferSize = 2000;
           options.Timeout = TimeSpan.FromMinutes(2);
           // Both clients have the same handler "status-handler" added.
           // But the handler implementation also has its own optios per named http client - allowing it to behave differently per client.
           options.Handlers.Add("status-handler");
       }
   });
 ;

 using var fooClient = sut.CreateClient("foo-v1");
 using var barClient = sut.CreateClient("bar-v1");

 var fooResponse = await fooClient.GetAsync("/foo");
 var barResponse = await barClient.GetAsync("/bar");

 Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
 Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);

```

In the scenario above:-

1. The handler I have implemented, has it's own `Options` to control it's behaviour. Its not mandatory that you do this, however i've found it's commonly needed, so showing how it can be done.
2. The handler not only has one set of options, but also has access to it's `http client name' it's being requested for. Therefore it can obtain it's options for the specific http client name - which are also built on demand as required. This allows the same handler to be configured per http client. Again this is not mandatory, but just a useful capability worth demonstrating.






