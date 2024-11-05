using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Properties;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Resources;
using VideoPlayer.Tools;

namespace VideoPlayer.Services.Resources
{
    public class ResourceManager : IResourceManager
    {
        public ResourceManager(IEnvironment environment) 
        {
            Init();
            this.environment = environment;
        }
        private ConcurrentDictionary<string, ImageSource> images = new ConcurrentDictionary<string, ImageSource>();
        private readonly IEnvironment environment;

        private void Init()
        {
            images.AddOrUpdate(
                nameof(icons.abenteuer), 
                ImageSource.FromStream(() => new MemoryStream(icons.abenteuer32x32)), 
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.action),
                ImageSource.FromStream(() => new MemoryStream(icons.action32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.animation),
                ImageSource.FromStream(() => new MemoryStream(icons.animation32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.biografie),
                ImageSource.FromStream(() => new MemoryStream(icons.biografie32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.crime),
                ImageSource.FromStream(() => new MemoryStream(icons.crime32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.documentary),
                ImageSource.FromStream(() => new MemoryStream(icons.documentary32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.drama),
                ImageSource.FromStream(() => new MemoryStream(icons.drama32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.familie),
                ImageSource.FromStream(() => new MemoryStream(icons.familie32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.fantasy),
                ImageSource.FromStream(() => new MemoryStream(icons.fantasy32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.geschichte),
                ImageSource.FromStream(() => new MemoryStream(icons.geschichte32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.horror),
                ImageSource.FromStream(() => new MemoryStream(icons.horror32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.komodie),
                ImageSource.FromStream(() => new MemoryStream(icons.komodie32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.krieg),
                ImageSource.FromStream(() => new MemoryStream(icons.krieg32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.musik),
                ImageSource.FromStream(() => new MemoryStream(icons.musik32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.mystery),
                ImageSource.FromStream(() => new MemoryStream(icons.mystery32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.romanzen),
                ImageSource.FromStream(() => new MemoryStream(icons.romanzen32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.sciencefiction),
                ImageSource.FromStream(() => new MemoryStream(icons.sciencefiction32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.sport),
                ImageSource.FromStream(() => new MemoryStream(icons.sport32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.thriller),
                ImageSource.FromStream(() => new MemoryStream(icons.thriller32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.western),
                ImageSource.FromStream(() => new MemoryStream(icons.western32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.kinder),
                ImageSource.FromStream(() => new MemoryStream(icons.kinder32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.kurzfilm),
                ImageSource.FromStream(() => new MemoryStream(icons.kurzfilm32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.reality),
                ImageSource.FromStream(() => new MemoryStream(icons.reality32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.soap),
                ImageSource.FromStream(() => new MemoryStream(icons.soap32x32)),
                (name, value) => value);
            images.AddOrUpdate(
                nameof(icons.miniserien),
                ImageSource.FromStream(() => new MemoryStream(icons.miniserien32x32)),
                (name, value) => value);
        }

        public ImageSource GetGenreIcon(Genre genre)
        {
            var names = new string[] { genre.Name.ToLower() }
                .Concat(new string[] { genre.Name.ToLower().Replace(" ", "").Replace("ö", "o").Replace("ä", "a").Replace("ü", "u") })
                .Concat(genre.AlternateNames.SelectMany(n => new string[] { n.Name.ToLower() }
                    .Concat(new string[] { n.Name.ToLower().Replace(" ", "").Replace("ö", "o").Replace("ä", "a").Replace("ü", "u") })))
                ;
            foreach (var name in names)
                if (images.TryGetValue(name, out var icon2))
                    return icon2;
            return null;
        }

        private const string loadingVideoFileName = "loading.mp4";
        private string loadingVideoFilePath = string.Empty;
        protected string LoadingVideoFilePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(loadingVideoFilePath))
                {
                    string path = PathTools.Combine(environment.GetRootPath(), loadingVideoFileName);
                    if (File.Exists(path))
                        loadingVideoFilePath = path;
                    else
                    {
                        File.WriteAllBytes(path, videos.loading);
                        loadingVideoFilePath = path;
                    }
                }
                return loadingVideoFilePath;
            }
        }

        public CommunityToolkit.Maui.Views.MediaSource GetLoadingVideo()
        {
            return CommunityToolkit.Maui.Views.MediaSource.FromFile(LoadingVideoFilePath);            
        }
    }
}
