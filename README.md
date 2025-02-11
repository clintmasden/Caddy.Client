# caddy.client

A .NET Standard/C# implementation of the Monday.com API.

## Important Notice

I welcome contributions in the form of pull requests (PRs) for any breaking changes or modifications. However, please ensure that all PRs include a comprehensive sample demonstrating the proposed changes. Upon verification and confirmation of the sample, a new version will be released.

## Resources

| Name       | Resources                                       |
|------------|-------------------------------------------------|
| APIs       | [API Documentation](https://caddyserver.com/docs/api#api) |

## Getting Started

```csharp

using System.Threading.Tasks;
using Xunit;
using Caddy.Client;

namespace Caddy.Client.Tests
{
    public class CaddyClientTests
    {
        private readonly CaddyClient _client;
        // Use an absolute JSON pointer for a server within the HTTP app.
        private const string TestPath = "apps/http/servers/test";

        // The default Caddyfile used for load and adapt operations.
        private readonly string _defaultCaddyFile = @"{
    auto_https off

    admin :9081 {
    }
}

:9080 {
    encode gzip zstd

    log {
        output file ""./logs/caddy.log"" {
            roll_size 10mb
            roll_keep 5
            roll_keep_for 720h
        }
        format console
    }

    handle_path /dummy* {
        reverse_proxy 127.0.0.1:8001
    }

    handle {
        basic_auth {
            admin $2a$12$5Qr2roJvKSfoI4zKsz4i/u6rtlq2w9YjfAIJ6Zqzu0fRiXxtiEO82
        }
        reverse_proxy localhost:9081
    }
}";

        public CaddyClientTests()
        {
            // Ensure the API URL points to your Caddy admin endpoint.
            var apiUrl = "https://caddy.com";
            var username = "admin";
            var password = "admin"; // Replace with the actual password if needed.
            _client = new CaddyClient(apiUrl, username, password);
        }

        [Fact]
        public async Task LoadConfiguration_ShouldSucceed()
        {
            var result = await _client.LoadConfiguration<string>(_defaultCaddyFile);
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

       //[Fact(Skip = "Manual test: run only when needed")]
        [Fact]
        public async Task Stop_ShouldSucceed()
        {
            // For /stop, an empty response is acceptable.
            var result = await _client.Stop<string>();
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }

        [Fact]
        public async Task GetConfig_ShouldReturnConfig()
        {
            // Get the entire configuration.
            var result = await _client.GetConfig<string>("");
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task AdaptConfig_ShouldReturnAdaptedConfig()
        {
            var result = await _client.AdaptConfig<string>(_defaultCaddyFile, "text/caddyfile");
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.Data);
            Assert.Contains("\"apps\"", result.Data);
        }

        [Fact]
        public async Task CreateUpdateDeleteConfig_ShouldSucceed()
        {
            // 1. CREATE: Create a new server configuration at "apps/http/servers/test".
            var createConfig = new
            {
                listen = new[] { ":9001" },
                routes = new object[]
                {
                    new
                    {
                        handle = new object[]
                        {
                            new { handler = "static_response", body = "create" }
                        }
                    }
                }
            };

            var createResult = await _client.CreateConfig<string>(TestPath, createConfig);
            Assert.True(createResult.IsSuccess, createResult.ErrorMessage);

            var getAfterCreate = await _client.GetConfig<string>(TestPath);
            Assert.True(getAfterCreate.IsSuccess, getAfterCreate.ErrorMessage);
            Assert.Contains("9001", getAfterCreate.Data);
            Assert.Contains("create", getAfterCreate.Data);

            // 2. UPDATE: Update the configuration so that it returns "update".
            var updateConfig = new
            {
                listen = new[] { ":9001" },
                routes = new object[]
                {
                    new
                    {
                        handle = new object[]
                        {
                            new { handler = "static_response", body = "update" }
                        }
                    }
                }
            };

            var updateResult = await _client.UpdateConfig<string>(TestPath, updateConfig);
            Assert.True(updateResult.IsSuccess, updateResult.ErrorMessage);

            var getAfterUpdate = await _client.GetConfig<string>(TestPath);
            Assert.True(getAfterUpdate.IsSuccess, getAfterUpdate.ErrorMessage);
            Assert.Contains("update", getAfterUpdate.Data);

            // 3. DELETE: Delete the configuration.
            var deleteResult = await _client.DeleteConfig<string>(TestPath);
            Assert.True(deleteResult.IsSuccess, deleteResult.ErrorMessage);

            var getAfterDelete = await _client.GetConfig<string>(TestPath);
            // Expect the pointer to return "null" (or empty) when the config no longer exists.
            Assert.Contains("null", getAfterDelete.Data);
        }

        [Fact]
        public async Task GetCAInfo_ShouldReturnCAInfo()
        {
            var caId = "local";
            var result = await _client.GetCAInfo<string>(caId);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task GetCACertificates_ShouldReturnCertificates()
        {
            var caId = "local";
            var result = await _client.GetCACertificates<string>(caId);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task GetReverseProxyUpstreams_ShouldReturnStatus()
        {
            var result = await _client.GetReverseProxyUpstreams<string>();
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.NotNull(result.Data);
        }
    }
}

```
