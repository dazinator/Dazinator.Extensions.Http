## Usage

You can configure specific named http clients to use basic authentication.

Create your classes that inject `HttpClient` - example:

```csharp

    public class FooClient
    {
        private readonly HttpClient _httpClient;

        public FooClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task Get()
        {
            var response = await _httpClient.GetAsync("https://www.foo.com");
        }
    }

    public class BarClient
    {
        private readonly HttpClient _httpClient;

        public BarClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task Get()
        {
            await _httpClient.GetAsync("https://www.bar.com");
        }
    }
```

Add the following to your config file to configure basic auth for each of these:

```csharp

"HttpClient": {
    "FooClient": {
      "Handlers": [
        "BasicAuth",
      ],
      "BasicAuth": {
        "Username":"foo@foo.com",
        "Password":"p@ssword"
      }
    },
    "BarClient": {
     "Handlers": [
       "BasicAuth",
     ],
     "BasicAuth": {
       "Username":"bar@bar.com",
       "Password":"p@ss"
     }
    }    
   }
}

```

Now on startup, register the BasicAuthHandler with the handler registry.

```csharp
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var registry = services.ConfigureHttpClientHandlerRegistry(config, (reg) =>
            {
                reg.RegisterBasicAuthHandler();
            });       

```

Finally, add the typed `HttpClient`s, calling `AddFromConfiguration` to add handlers based on the config above.      

```csharp 

   services.AddHttpClient<FooClient>()
           .SetupFromHttpClientOptions(); 
   
   services.AddHttpClient<BarClient>()
           .SetupFromHttpClientOptions();
  
   var sp = services.BuildServiceProvider();

```

You can now consume `FooClient` and `BarClient` from DI, and they will use Basic Authentication as configured.

## Protecting password

You might not like the idea of configuring the password as plain text in the json config.
You can take control of this by implementing your own custom options class to provide the password or decrypt it appropriately.

Create a class that implements `IBasicAuthenticationHandlerOptions`:

```

        public class CustomOptions : IBasicAuthenticationHandlerOptions
        {
            public string Username { get; set; }

            public bool AllowHttp { get; set; }

            public string ProtectedPassword { get; set; }

            public string GetPassword()
            {
                var unprotected = String.Concat(ProtectedPassword.Reverse());
                return unprotected;
            }
        }


```

You must implement the `GetPassword()` method, in here I am "decrypting" another property which would be set in the config:

```json
{
"HttpClient": {
    "FooClient": {
      "Handlers": [
        "BasicAuth",
      ],
      "BasicAuth": {
        "Username":"foo@foo.com",
        "ProtectedPassword":"ABC"
      }
    }  
   }
}

```

You must now provide the type of your custom options when configuring the handler in the following places:

```csharp
            var registry = services.ConfigureHttpClientHandlerRegistry(config, (reg) =>
            {
                reg.RegisterBasicAuthHandler<CustomOptions>();
            });          
```
