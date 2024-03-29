﻿using System;
using JetBrains.Annotations;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.RequestDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;

namespace TNRD.Zeepkist.GTR.SDK.Implementation;

[PublicAPI]
public class FavoritesApi : IFavoritesApi
{
    private readonly Sdk sdk;

    public FavoritesApi(Sdk sdk)
    {
        this.sdk = sdk;
    }

    public UniTask<Result<FavoritesGetAllResponseDTO>> Get(
        Action<FavoritesGetAllRequestDTOBuilder> builder
    )
    {
        FavoritesGetAllRequestDTOBuilder actualBuilder = new();
        builder?.Invoke(actualBuilder);
        FavoritesGetAllRequestDTO dto = actualBuilder.Build();

        return sdk.ApiClient.Get<FavoritesGetAllResponseDTO>($"favorites?{dto.ToQueryString()}", false);
    }

    public UniTask<Result<FavoriteResponseModel>> Get(int id)
    {
        return sdk.ApiClient.Get<FavoriteResponseModel>($"favorites/{id}");
    }

    public UniTask<Result<GenericIdResponseDTO>> Add(string level)
    {
        return Add(b => b.WithLevel(level));
    }

    public UniTask<Result<GenericIdResponseDTO>> Add(Action<FavoritesAddRequestDTOBuilder> builder)
    {
        FavoritesAddRequestDTOBuilder actualBuilder = new();
        builder?.Invoke(actualBuilder);
        FavoritesAddRequestDTO dto = actualBuilder.Build();

        return sdk.ApiClient.Post<GenericIdResponseDTO>("favorites", dto);
    }

    public UniTask<Result> Remove(int id)
    {
        return sdk.ApiClient.Delete($"favorites/{id}");
    }
}
