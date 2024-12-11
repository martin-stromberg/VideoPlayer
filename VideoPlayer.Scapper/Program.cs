// See https://aka.ms/new-console-template for more information

Console.WriteLine("Hello, World!");

var rootPath = "Y:\\Serien";
foreach (var folder in Directory.GetDirectories(rootPath))
    ScrapTVShow(folder);

void ScrapTVShow(string folder)
{
    var showName = Path.GetFileName(folder);
}