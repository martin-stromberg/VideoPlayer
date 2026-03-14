using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using WebPlayerApi.Models;
using WebPlayerApi.Service.Data.SFtp;

namespace WebPlayerApi.Services
{
    public interface IMediaCache
    {
        void Save(string name, MediaItem[] mediaItems);
        IEnumerable<MediaItem> Load(string name);
    }
    public class MediaCache : IMediaCache
    {
        public void Save(string name, MediaItem[] mediaItems)
        {
            FileInfo saveFile = new FileInfo($"{name}.cache");
            File.WriteAllText(saveFile.FullName, JsonSerializer.Serialize(mediaItems));
        }
        public IEnumerable<MediaItem> Load(string name)
        {
            FileInfo saveFile = new FileInfo($"{name}.cache");
            if (!saveFile.Exists)
                return new List<MediaItem>();
            var json = File.ReadAllText(saveFile.FullName);
            return JsonSerializer.Deserialize<MediaItem[]>(json).ToList();
        }
    }
    public class MediaService : IMediaService
    {
        private readonly IMediaCache cache;
        private readonly ILogger<IPCheck> logger;

        public MediaService(IMediaCache cache, ISourceService mediaService, ILogger<IPCheck> logger)
        {
            this.cache = cache;
            this.logger = logger;
        }

        


        private Dictionary<string, List<MediaItem>> _AllMediaItems = new Dictionary<string, List<MediaItem>>();
        private Dictionary<string, bool> _MediaItemsLoading = new Dictionary<string, bool>();

        

        

        

        

        

        



        public PagedResult<MediaItemDto> GetMediaItems(string directoryName, int offset, int count)
        {
            //var dir = _configuredDirectories.FirstOrDefault(d => d.Name.Equals(directoryName, StringComparison.OrdinalIgnoreCase));
            //if (dir == null)
            //  return new PagedResult<MediaItemDto>();

            //if (!_AllMediaItems.ContainsKey(dir.Name))
            //{
            //    LoadMediaItemsAsync(dir);
            //    return new PagedResult<MediaItemDto>()
            //    {
            //        Loading = true
            //    };
            //} else if (_MediaItemsLoading[dir.Name])
            //    return new PagedResult<MediaItemDto>()
            //    {
            //        Loading = true
            //    };
            //else if (!_AllMediaItems[dir.Name].Any())
            //{
            //    LoadMediaItemsAsync(dir);
            //    return new PagedResult<MediaItemDto>()
            //    {
            //        Loading = true
            //    };
            //}

            //var paged = _AllMediaItems[dir.Name]
            //    .Skip(offset)
            //    .Take(count)
            //   .Select(m => new MediaItemDto
            //   {
            //       Id = m.Id,
            //       Title = m.Title,
            //       Type = m.Type,
            //       FilePath = m.FilePath,
            //       ImagePaths = m.ImagePaths,
            //       PictureBase64 = m.Picture is null?string.Empty: Convert.ToBase64String(m.Picture)
            //   })
            //   .ToList();
            //return new PagedResult<MediaItemDto>
            //{
            //    Page = offset,
            //    PageSize = count,
            //    Items = paged,
            //    TotalCount = _AllMediaItems[dir.Name].Count
            //};
            throw new NotImplementedException();
        }


        public CardResult<MediaItemDetailsDto> GetMediaItem(string id)
        {
            foreach (var list in _AllMediaItems)
            {
                var item = list.Value.FirstOrDefault(i => i.Id == id);
                if (item is null)
                    continue;

                return new CardResult<MediaItemDetailsDto>()
                {
                    Item = new MediaItemDetailsDto()
                    {

                        FilePath = item.FilePath,
                        Id = item.Id,
                        ImagePaths = item.ImagePaths,
                        Title = item.Title,
                        Type = item.Type,
                        ReleaseDate = item.ReleaseDate,
                        PictureBase64 = item.Picture is null ? string.Empty : Convert.ToBase64String(item.Picture),
                        Children = item.Children?.Select(i => new MediaItemDto()
                        {
                            FilePath = i.FilePath,
                            Id = i.Id,
                            ImagePaths = i.ImagePaths,
                            Title = i.Title,
                            Type = i.Type,
                            Plot = i.Plot,
                            PictureBase64 = i.Picture is null ? string.Empty : Convert.ToBase64String(i.Picture),
                            ReleaseDate = i.ReleaseDate
                        }).ToArray()
                    },
                    Loading = false
                };
            }
            return null;
        }

        
        
        
        
        
        
        


        public Stream GetMediaStream(string parentId, string id)
        {
            //foreach (var list in _AllMediaItems)
            //{
            //    var parentItem = list.Value.FirstOrDefault(i => i.Id == parentId);
            //    if (parentItem is null)
            //        continue;
            //    var mediaItem = parentItem.Children.FirstOrDefault(i => i.Id == id);
            //    if (mediaItem is null)
            //        continue;

            //    var reader = CreateReader(parentItem.Source);
            //    return reader.ReadStream(mediaItem);                
            //}
            //return null;
            throw new NotImplementedException();
        }

        public async Task ReloadAsync(string source = "")
        {
            //foreach (var dir in GetConfiguredDirectories()
            //    .Where(dir => dir.Name == source || string.IsNullOrWhiteSpace(source)))
            //{
            //    LoadMediaItemsAsync(dir, true);
            //}
            throw new NotImplementedException();
        }
    }

}
