﻿using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using Newtonsoft.Json;

namespace OneDrive.Api
{
    public abstract class ApiService
    {
        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IApplicationHost _applicationHost;

        private string UserAgent
        {
            get
            {
                var version = _applicationHost.ApplicationVersion.ToString();
                return string.Format("Emby/{0} +http://emby.media/", version);
            }
        }

        protected abstract string BaseUrl { get; }

        protected ApiService(IHttpClient httpClient, IJsonSerializer jsonSerializer, IApplicationHost applicationHost)
        {
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _applicationHost = applicationHost;
        }

        protected async Task<T> GetRequest<T>(string url, string accessToken, CancellationToken cancellationToken)
        {
            return await HandleException(async () =>
            {
                var httpRequest = PrepareHttpRequestOptions(url, accessToken, cancellationToken);
                var resultStream = await _httpClient.Get(httpRequest);
                return _jsonSerializer.DeserializeFromStream<T>(resultStream);
            });
        }

        protected async Task<Stream> GetRawRequest(string url, string accessToken, CancellationToken cancellationToken)
        {
            return await HandleException(async () =>
            {
                var httpRequest = PrepareHttpRequestOptions(url, accessToken, cancellationToken);
                return await _httpClient.Get(httpRequest);
            });
        }

        protected async Task<T> PostRequest<T>(string url, string accessToken, object data, CancellationToken cancellationToken)
        {
            var httpRequest = PrepareHttpRequestOptions(url, accessToken, cancellationToken);
            httpRequest.RequestContentType = "application/json";
            httpRequest.RequestContent = JsonConvert.SerializeObject(data);

            return await HandleException(async () =>
            {
                var result = await _httpClient.Post(httpRequest);
                return _jsonSerializer.DeserializeFromStream<T>(result.Content);
            });
        }

        protected async Task<T> PostRequest<T>(HttpRequestOptions httpRequest, CancellationToken cancellationToken)
        {
            return await HandleException(async () =>
            {
                var result1 = await _httpClient.Post(httpRequest);
                return _jsonSerializer.DeserializeFromStream<T>(result1.Content);
            });
        }

        protected async Task<T> PostRequest<T>(string url, string accessToken, string body, string contentType, CancellationToken cancellationToken)
        {
            var httpRequest = PrepareHttpRequestOptions(url, accessToken, cancellationToken);
            httpRequest.RequestContent = body;
            httpRequest.RequestContentType = contentType;

            return await HandleException(async () =>
            {
                var result = await _httpClient.Post(httpRequest);
                return _jsonSerializer.DeserializeFromStream<T>(result.Content);
            });
        }

        protected async Task<T> PutRequest<T>(HttpRequestOptions httpRequest, CancellationToken cancellationToken)
        {
            return await HandleException(async () =>
            {
                var result = await _httpClient.SendAsync(httpRequest, "PUT");
                return _jsonSerializer.DeserializeFromStream<T>(result.Content);
            });
        }

        protected async Task DeleteRequest(string url, string accessToken, CancellationToken cancellationToken)
        {
            await HandleException(async () =>
            {
                var httpRequest = PrepareHttpRequestOptions(url, accessToken, cancellationToken);
                await _httpClient.SendAsync(httpRequest, "DELETE");
            });
        }

        protected HttpRequestOptions PrepareHttpRequestOptions(string url, string accessToken, CancellationToken cancellationToken)
        {
            var httpRequestOptions = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = BaseUrl + url.TrimStart('/'),
                UserAgent = UserAgent
            };

            if (!string.IsNullOrEmpty(accessToken))
            {
                httpRequestOptions.RequestHeaders["Authorization"] = "Bearer " + accessToken;
            }

            return httpRequestOptions;
        }

        private async Task<T> HandleException<T>(Func<Task<T>> func)
        {
            try
            {
                return await func();
            }
            catch (HttpException ex)
            {
                var webException = ex.InnerException as WebException;
                if (webException != null)
                {
                    var stream = webException.Response.GetResponseStream();
                    var response = _jsonSerializer.DeserializeFromStream<OneDriveError>(stream);
                    throw new ApiException(response.error_description);
                }
                throw;
            }
        }

        private async Task HandleException(Func<Task> func)
        {
            try
            {
                await func();
            }
            catch (HttpException ex)
            {
                var webException = ex.InnerException as WebException;
                if (webException != null)
                {
                    var stream = webException.Response.GetResponseStream();
                    var response = _jsonSerializer.DeserializeFromStream<OneDriveError>(stream);
                    throw new ApiException(response.error_description);
                }
                throw;
            }
        }
    }
}
