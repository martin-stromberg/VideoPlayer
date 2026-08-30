using Microsoft.AspNetCore.Components.Authorization;
using VideoWebPlayer.Client;
using ApiModels = VideoWebPlayer.Controllers.Models;
using ClientModels = VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.ViewModels;

internal sealed class MediaSourceDetailsViewModel
{
    private readonly VideoWebPlayerClient _client;
    private readonly AuthenticationStateProvider _authStateProvider;
    private int _stateVersion;

    public MediaSourceDetailsViewModel(VideoWebPlayerClient client, AuthenticationStateProvider authStateProvider)
    {
        _client = client;
        _authStateProvider = authStateProvider;
    }

    public long? ActiveSourceId { get; private set; }
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
        var version = ++_stateVersion;
        ActiveSourceId = sourceId;
        MediaSource = null;
        SourceGenres = new ClientModels.SourceGenresDto();
        ResetSourceState();
        IsLoading = true;
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (version != _stateVersion)
                return;

            var user = authState.User;
            IsAuthenticated = user?.Identity?.IsAuthenticated == true;

            var mediaSource = await _client.RequestSourceAsync(sourceId);
            if (version != _stateVersion)
                return;

            MediaSource = mediaSource;
            if (mediaSource is not null)
            {
                var sourceGenres = await _client.RequestSourceGenresAsync(mediaSource.Id)
                    ?? new ClientModels.SourceGenresDto();
                if (version != _stateVersion)
                    return;

                SourceGenres = sourceGenres;
            }
            else
            {
                SourceGenres = new ClientModels.SourceGenresDto();
            }
        }
        finally
        {
            if (version == _stateVersion)
                IsLoading = false;
        }
    }

    public void ResetEntries()
    {
        _stateVersion++;
        ResetEntryState();
    }

    public void SetGenre(long? genreId) => SelectedGenreId = genreId;

    public async Task<IReadOnlyList<ApiModels.MediaEntryDto>> LoadNextPageAsync(long sourceId)
    {
        if (IsLoading || ActiveSourceId != sourceId)
            return Array.Empty<ApiModels.MediaEntryDto>();

        var version = _stateVersion;
        var page = Page;
        var searchText = SearchText;
        var selectedGenreId = SelectedGenreId;

        IsLoading = true;
        try
        {
            var newEntries = await _client.RequestSourceItems(
                sourceId,
                page,
                PageSize,
                searchText,
                selectedGenreId ?? 0);

            if (version != _stateVersion || ActiveSourceId != sourceId)
                return Array.Empty<ApiModels.MediaEntryDto>();

            if (newEntries?.Any() == true)
            {
                Entries.AddRange(newEntries);
                Page = page + 1;
                return newEntries;
            }

            return Array.Empty<ApiModels.MediaEntryDto>();
        }
        finally
        {
            if (version == _stateVersion)
                IsLoading = false;
        }
    }

    private void ResetSourceState()
    {
        SearchText = "";
        SelectedGenreId = null;
        ResetEntryState();
    }

    private void ResetEntryState()
    {
        IsLoading = false;
        Page = 0;
        Entries.Clear();
    }
}
