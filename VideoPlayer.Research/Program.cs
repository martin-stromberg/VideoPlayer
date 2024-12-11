// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using VideoPlayer.Research.SourceReader;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.SourceReader;

Console.WriteLine("Hello, World!");

var sources = new SFTPMediaSource[] {
    new SFTPMediaSource()
    {
        Servername = "mstromberg.ddns.net",
        Port = 10022,
        Username = "Martin Stromberg",
        Password = "Hi1TvM!nav",
        RootPath = "/MedienServer/Filme"
    }
};
Parallel.ForEach(sources, (source) =>
{
    var Reader = new SFTPSourceReader(source);
    var folder = Reader.GetRoot();
    ReadFolder(Reader, folder);
});
Console.ReadKey();

void ReadFolder(SFTPSourceReader reader, SourceFolder folder, int level = 0)
{
    Console.WriteLine(folder.FullPath.PadLeft(folder.FullPath.Length + (level * 2)));
    var subFolders = reader.ReadFoldersAsync(folder).Result.ToList();
    var files = reader.ReadFilesAsync(folder).Result.ToList();
    foreach (var file in files)
    {
        Console.WriteLine(file.FullPath.PadLeft(file.FullPath.Length + (level * 2)));
        //var mediaItem = new MediaItem()
        //{
        //    Path = file.Path
        //};
        //var localFile = reader.Download(mediaItem, (p) => { });
        //localFile.Delete();
    }
    foreach (var subFolder in subFolders)
        ReadFolder(reader, subFolder);
}
