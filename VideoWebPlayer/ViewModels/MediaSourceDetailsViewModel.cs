using Microsoft.AspNetCore.Components.Authorization;
using VideoWebPlayer.Client;
using ApiModels = VideoWebPlayer.Controllers.Models;
using ClientModels = VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.ViewModels;

internal sealed class MediaSourceDetailsViewModel
{
    private readonly VideoWebPlayerClient _client;
    private readonly AuthenticationStateProvider _authStateProvider;

    public MediaSourceDetailsViewModel(VideoWebPlayerClient client, AuthenticationStateProvider authStateProvider)
    {
        _client = client;
        _authStateProvider = authStateProvider;
    }

    public ClientModels.DtoMediaSource? MediaSource { get; private set; }
    public ClientModels.SourceGenresDto SourceGenres { get; private set; } = new();

    public List<ApiModels.MediaEntryDto> Entries { get; } = new();
    public int Page { get; private set; }
    public int PageSize { get; } = 30;

    public bool IsLoading { get; private set; } = true;
    public bool? IsAuthenticated { get; private set; }

    public string SearchText { get; set; } = "";
    public long? SelectedGenreId { get; private set; }

    public string AuthorizationToken => _client.AuthorizationToken;

    public async Task InitializeAsync(long sourceId)
    {
        IsLoading = true;
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            IsAuthenticated = user?.Identity?.IsAuthenticated == true;

            MediaSource = await _client.RequestSourceAsync(sourceId);
            if (MediaSource is not null)
            {
                SourceGenres = await _client.RequestSourceGenresAsync(MediaSource.Id)
                    ?? new ClientModels.SourceGenresDto();
            }
            else
            {
                SourceGenres = new ClientModels.SourceGenresDto();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ResetEntries()
    {
        Page = 0;
        Entries.Clear();
    }

    public void SetGenre(long? genreId) => SelectedGenreId = genreId;

    public async Task<IReadOnlyList<ApiModels.MediaEntryDto>> LoadNextPageAsync(long sourceId)
    {
        if (IsLoading)
            return Array.Empty<ApiModels.MediaEntryDto>();

        IsLoading = true;
        try
        {
            var newEntries = await _client.RequestSourceItems(
                sourceId,
                Page,
                PageSize,
                SearchText,
                SelectedGenreId ?? 0);

            if (newEntries?.Any() == true)
            {
                Entries.AddRange(newEntries);
                Page++;
                return newEntries;
            }

            return Array.Empty<ApiModels.MediaEntryDto>();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
