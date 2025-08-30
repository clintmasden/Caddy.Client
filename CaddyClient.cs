using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Caddy.Client
{
    /// <summary>
    /// <see href="https://caddyserver.com/docs/api#api"/>
    /// </summary>
    public class CaddyClient
    {
        private readonly HttpClient _httpClient;

        public CaddyClient(string apiUrl, string username=null, string password=null)
        {
            // Ensure the apiUrl is the admin API endpoint (including the proper port).
            _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };

            if (!string.IsNullOrEmpty(username) && password != null)
            {
                // Set up basic authentication.
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            }

            // By default, accept JSON responses.
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        #region Endpoints (Generic Only)

        /// <summary>
        /// POST /load: uses a Caddyfile by default.
        /// </summary>
        public async Task<Result<T>> LoadConfiguration<T>(object config, string contentType = "text/caddyfile", CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>("/load", HttpMethod.Post, config, contentType, cancellationToken);
        }


        /// <summary>
        /// POST /stop: This is a fire‑and‑forget endpoint (an empty response is considered success).
        /// </summary>
        public async Task<Result<T>> Stop<T>(CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/stop");
                await _httpClient.SendAsync(request, cancellationToken);
                return Result<T>.Success(default);
            }
            catch (TaskCanceledException)
            {
                return Result<T>.Success(default);
            }
            catch (Exception ex)
            {
                return Result<T>.Fail(ex.Message);
            }

        }

        /// <summary>
        /// GET /config/[path]: uses JSON.
        /// </summary>
        public async Task<Result<T>> GetConfig<T>(string path = "", CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>($"/config/{path}", HttpMethod.Get, null, "application/json", cancellationToken);
        }

        /// <summary>
        /// POST /config/[path]: uses JSON.
        /// </summary>
        public async Task<Result<T>> SetConfig<T>(string path, object config, CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>($"/config/{path}", HttpMethod.Post, config, "application/json", cancellationToken);
        }

        /// <summary>
        /// PUT /config/[path]: uses JSON.
        /// </summary>
        public async Task<Result<T>> CreateConfig<T>(string path, object config, CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>($"/config/{path}", HttpMethod.Put, config, "application/json", cancellationToken);
        }

        /// <summary>
        /// PATCH /config/[path]: uses JSON.
        /// </summary>
        public async Task<Result<T>> UpdateConfig<T>(string path, object config, CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>($"/config/{path}", new HttpMethod("PATCH"), config, "application/json", cancellationToken);
        }

        /// <summary>
        /// DELETE /config/[path]: uses JSON.
        /// </summary>
        public async Task<Result<T>> DeleteConfig<T>(string path, CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>($"/config/{path}", HttpMethod.Delete, null, "application/json", cancellationToken);
        }

        /// <summary>
        /// POST /adapt: uses a Caddyfile by default.
        /// </summary>
        public async Task<Result<T>> AdaptConfig<T>(object config, string contentType = "text/caddyfile", CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>("/adapt", HttpMethod.Post, config, contentType, cancellationToken);
        }

        /// <summary>
        /// GET /pki/ca/<id>: uses JSON.
        /// </summary>
        public async Task<Result<T>> GetCAInfo<T>(string caId, CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>($"/pki/ca/{caId}", HttpMethod.Get, null, "application/json", cancellationToken);
        }

        /// <summary>
        /// GET /pki/ca/<id>/certificates: uses JSON.
        /// </summary>
        public async Task<Result<T>> GetCACertificates<T>(string caId, CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>($"/pki/ca/{caId}/certificates", HttpMethod.Get, null, "application/json", cancellationToken);
        }

        /// <summary>
        /// GET /reverse_proxy/upstreams: uses JSON.
        /// </summary>
        public async Task<Result<T>> GetReverseProxyUpstreams<T>(CancellationToken cancellationToken = default)
        {
            return await SendRequest<T>("/reverse_proxy/upstreams", HttpMethod.Get, null, "application/json", cancellationToken);
        }

        #endregion

        #region Internal Helper

        private async Task<Result<T>> SendRequest<T>(
            string endpoint,
            HttpMethod method,
            object content = null,
            string contentType = "application/json",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new HttpRequestMessage(method, endpoint);

                if (content != null)
                {
                    StringContent stringContent;
                    // For raw Caddyfile text, do not JSON‑serialize.
                    if (content is string s && contentType.Equals("text/caddyfile", StringComparison.OrdinalIgnoreCase))
                    {
                        stringContent = new StringContent(s, Encoding.UTF8, contentType);
                    }
                    else
                    {
                        var jsonContent = JsonConvert.SerializeObject(content);
                        stringContent = new StringContent(jsonContent, Encoding.UTF8, contentType);
                    }
                    request.Content = stringContent;
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

               var responseData = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(responseData))
                {
                    if (typeof(T) == typeof(string))
                    {
                        return Result<T>.Success((T)(object)"");
                    }
                    return Result<T>.Success(default);
                }

                if (typeof(T) == typeof(string))
                {
                    return Result<T>.Success((T)(object)responseData);
                }

                var data = JsonConvert.DeserializeObject<T>(responseData);
                return Result<T>.Success(data);
            }
            catch (Exception ex)
            {
                return Result<T>.Fail(ex.Message);
            }
        }

        #endregion
    }

    public class Result<T>
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; }
        public T Data { get; private set; }

        public static Result<T> Success(T data) => new Result<T> { IsSuccess = true, Data = data };
        public static Result<T> Fail(string errorMessage) => new Result<T> { IsSuccess = false, ErrorMessage = errorMessage };
    }
}
