﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.FluentResults;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.SDK.Client;

internal abstract class ClientBase
{
    private const int MAX_ATTEMPT_COUNT = 3;

    private readonly Sdk sdk;
    private readonly HttpClient httpClient;

    protected bool LogRequestUrl { get; set; }
    protected bool LogResponseOutput { get; set; }

    protected ClientBase(Sdk sdk, string baseAddress)
    {
        this.sdk = sdk;

        httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseAddress);
    }

    private async UniTask<Result> RefreshAuth()
    {
        Result refreshResult = await sdk.UsersApi.Refresh(sdk.UsersApi.ModVersion);
        if (refreshResult.IsSuccess)
            return Result.Ok();

        Result loginResult = await sdk.UsersApi.Login(sdk.UsersApi.ModVersion, false);
        if (loginResult.IsSuccess)
            return Result.Ok();

        return Result.Merge(refreshResult, loginResult);
    }

    private void LogUrl(HttpRequestMessage msg)
    {
        if (LogRequestUrl)
            Debug.Log($"[GTR][{msg.Method.Method}] {msg.RequestUri}");
    }

    private void LogContent(HttpRequestMessage msg, string content)
    {
        if (LogResponseOutput)
            Debug.Log($"[GTR][{msg.Method.Method}] {msg.RequestUri}\nRESPONSE:\n{content}");
    }

    public async UniTask<Result> Get(
        string requestUri,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        Result result = null;

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            result = await GetInternal(requestUri, addAuth, allowRefresh, ct);
            if (result.IsSuccess)
                break;

            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), cancellationToken: ct);
        }

        return result;
    }

    private async UniTask<Result> GetInternal(
        string requestUri,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        LogUrl(requestMessage);

        if (addAuth)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sdk.UsersApi.AccessToken);

        HttpResponseMessage response = null;

        try
        {
            response = await httpClient.SendAsync(requestMessage, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await GetWithRefresh(requestUri, addAuth, allowRefresh, response, ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e))
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        return Result.Ok();
    }

    private async Task<Result> GetWithRefresh(
        string requestUri,
        bool addAuth,
        bool allowRefresh,
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (!allowRefresh)
        {
            return Result.Fail("Unauthorized")
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            Result refreshAuthResult = await RefreshAuth();
            if (!refreshAuthResult.IsSuccess)
                continue;

            Result result = await Get(requestUri, addAuth, false, ct);
            if (result.IsSuccess)
                return result;
        }

        return Result.Fail("Unauthorized")
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    public async UniTask<Result<TResponse>> Get<TResponse>(
        string requestUri,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        Result<TResponse> result = null;

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            result = await GetInternal<TResponse>(requestUri, addAuth, allowRefresh, ct);
            if (result.IsSuccess)
                break;

            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), cancellationToken: ct);
        }

        return result;
    }

    private async UniTask<Result<TResponse>> GetInternal<TResponse>(
        string requestUri,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        LogUrl(requestMessage);

        if (addAuth)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sdk.UsersApi.AccessToken);

        HttpResponseMessage response = null;

        try
        {
            response = await httpClient.SendAsync(requestMessage, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await GetWithRefresh<TResponse>(requestUri, addAuth, allowRefresh, response, ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e))
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        try
        {
            string responseJson = await response.Content.ReadAsStringAsync();
            LogContent(requestMessage, responseJson);
            return JsonConvert.DeserializeObject<TResponse>(responseJson);
        }
        catch (Exception e)
        {
            return new ExceptionalError(e);
        }
    }

    private async Task<Result<TResponse>> GetWithRefresh<TResponse>(
        string requestUri,
        bool addAuth,
        bool allowRefresh,
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (!allowRefresh)
        {
            return Result.Fail("Unauthorized")
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            Result refreshAuthResult = await RefreshAuth();
            if (!refreshAuthResult.IsSuccess)
                continue;

            Result<TResponse> result = await Get<TResponse>(requestUri, addAuth, false, ct);
            if (result.IsSuccess)
                return result;
        }

        return Result.Fail("Unauthorized")
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    public async UniTask<Result> Post(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        Result result = null;

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            result = await PostInternal(requestUri, data, addAuth, allowRefresh, ct);
            if (result.IsSuccess)
                break;

            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), cancellationToken: ct);
        }

        return result;
    }

    private async UniTask<Result> PostInternal(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        string requestJson = JsonConvert.SerializeObject(data);
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        LogUrl(requestMessage);

        if (addAuth)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sdk.UsersApi.AccessToken);

        HttpResponseMessage response = null;

        try
        {
            response = await httpClient.SendAsync(requestMessage, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await PostWithRefresh(requestUri, data, addAuth, allowRefresh, response, ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e))
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        return Result.Ok()
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    private async Task<Result> PostWithRefresh(
        string requestUri,
        object data,
        bool addAuth,
        bool allowRefresh,
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (!allowRefresh)
        {
            return Result.Fail("Unauthorized")
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            Result refreshAuthResult = await RefreshAuth();
            if (!refreshAuthResult.IsSuccess)
                continue;

            Result result = await Post(requestUri, data, addAuth, false, ct);
            if (result.IsSuccess)
                return result;
        }

        return Result.Fail("Unauthorized")
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    public async UniTask<Result<TResponse>> Post<TResponse>(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        bool allowedToFail = false,
        CancellationToken ct = default
    )
    {
        Result<TResponse> result = null;

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            result = await PostInternal<TResponse>(requestUri, data, addAuth, allowRefresh, allowedToFail, ct);
            if (result.IsSuccess || allowedToFail)
                break;

            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), cancellationToken: ct);
        }

        return result;
    }

    private async UniTask<Result<TResponse>> PostInternal<TResponse>(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        bool allowedToFail = false,
        CancellationToken ct = default
    )
    {
        string requestJson = JsonConvert.SerializeObject(data);
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        LogUrl(requestMessage);

        if (addAuth)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sdk.UsersApi.AccessToken);

        HttpResponseMessage response = null;

        try
        {
            response = await httpClient.SendAsync(requestMessage, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await PostWithRefresh<TResponse>(requestUri,
                data,
                addAuth,
                allowRefresh,
                allowedToFail,
                response,
                ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e))
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        try
        {
            string responseJson = await response.Content.ReadAsStringAsync();
            LogContent(requestMessage, responseJson);
            return JsonConvert.DeserializeObject<TResponse>(responseJson);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    private async Task<Result<TResponse>> PostWithRefresh<TResponse>(
        string requestUri,
        object data,
        bool addAuth,
        bool allowRefresh,
        bool allowedToFail,
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (!allowRefresh)
        {
            return Result.Fail("Unauthorized")
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            Result refreshAuthResult = await RefreshAuth();
            if (!refreshAuthResult.IsSuccess)
                continue;

            Result<TResponse> result = await Post<TResponse>(requestUri, data, addAuth, false, allowedToFail, ct);
            if (result.IsSuccess)
                return result;
        }

        return Result.Fail("Unauthorized")
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    public async UniTask<Result> Patch(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        Result result = null;

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            result = await PatchInternal(requestUri, data, addAuth, allowRefresh, ct);
            if (result.IsSuccess)
                break;

            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), cancellationToken: ct);
        }

        return result;
    }

    private async UniTask<Result> PatchInternal(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        string requestJson = JsonConvert.SerializeObject(data);
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri);
        requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        LogUrl(requestMessage);

        if (addAuth)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sdk.UsersApi.AccessToken);

        HttpResponseMessage response = null;

        try
        {
            response = await httpClient.SendAsync(requestMessage, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await PatchWithRefresh(requestUri, data, addAuth, allowRefresh, response, ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e))
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        return Result.Ok()
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    private async Task<Result> PatchWithRefresh(
        string requestUri,
        object data,
        bool addAuth,
        bool allowRefresh,
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (!allowRefresh)
        {
            return Result.Fail("Unauthorized")
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            Result refreshAuthResult = await RefreshAuth();
            if (!refreshAuthResult.IsSuccess)
                continue;

            Result result = await Patch(requestUri, data, addAuth, false, ct);
            if (result.IsSuccess)
                return result;
        }

        return Result.Fail("Unauthorized")
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    public async UniTask<Result> Delete(
        string requestUri,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        Result result = null;

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            result = await DeleteInternal(requestUri, addAuth, allowRefresh, ct);
            if (result.IsSuccess)
                break;

            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), cancellationToken: ct);
        }

        return result;
    }

    private async UniTask<Result> DeleteInternal(
        string requestUri,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        LogUrl(requestMessage);

        if (addAuth)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sdk.UsersApi.AccessToken);

        HttpResponseMessage response = null;

        try
        {
            response = await httpClient.SendAsync(requestMessage, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await DeleteWithRefresh(requestUri, addAuth, allowRefresh, response, ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e))
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        return Result.Ok()
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    private async Task<Result> DeleteWithRefresh(
        string requestUri,
        bool addAuth,
        bool allowRefresh,
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (!allowRefresh)
        {
            return Result.Fail("Unauthorized")
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            Result refreshAuthResult = await RefreshAuth();
            if (!refreshAuthResult.IsSuccess)
                continue;

            Result result = await Delete(requestUri, addAuth, false, ct);
            if (result.IsSuccess)
                return result;
        }

        return Result.Fail("Unauthorized")
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    public async UniTask<Result> Delete(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        Result result = null;

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            result = await DeleteInternal(requestUri, data, addAuth, allowRefresh, ct);
            if (result.IsSuccess)
                break;

            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(i + 1, 2)), cancellationToken: ct);
        }

        return result;
    }

    private async UniTask<Result> DeleteInternal(
        string requestUri,
        object data,
        bool addAuth = true,
        bool allowRefresh = true,
        CancellationToken ct = default
    )
    {
        string requestJson = JsonConvert.SerializeObject(data);
        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        LogUrl(requestMessage);

        if (addAuth)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sdk.UsersApi.AccessToken);

        HttpResponseMessage response = null;

        try
        {
            response = await httpClient.SendAsync(requestMessage, ct);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return await DeleteWithRefresh(requestUri, data, addAuth, allowRefresh, response, ct);
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e))
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        return Result.Ok()
            .WithReason(new StatusCodeReason(response.StatusCode));
    }

    private async Task<Result> DeleteWithRefresh(
        string requestUri,
        object data,
        bool addAuth,
        bool allowRefresh,
        HttpResponseMessage response,
        CancellationToken ct
    )
    {
        if (!allowRefresh)
        {
            return Result.Fail("Unauthorized")
                .WithReason(new StatusCodeReason(response.StatusCode));
        }

        for (int i = 0; i < MAX_ATTEMPT_COUNT; i++)
        {
            Result refreshAuthResult = await RefreshAuth();
            if (!refreshAuthResult.IsSuccess)
                continue;

            Result result = await Delete(requestUri, data, addAuth, false, ct);
            if (result.IsSuccess)
                return result;
        }

        return Result.Fail("Unauthorized")
            .WithReason(new StatusCodeReason(response.StatusCode));
    }
}
