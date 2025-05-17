// See https://aka.ms/new-console-template for more information

using VideoPlayer.Scapper;

//ScrapLoewenzahn();
//ScrapJahresrueckblick();
//Einstein();
Pefferkoerner();

void ScrapJahresrueckblick()
{
    string rootPath = "\\\\ds1522plus\\MedienServer\\Serien\\Jahresrückblick";
    var rootFolder = new DirectoryInfo(rootPath);
    if (!rootFolder.Exists)
        return;
    foreach (var file in rootFolder.GetFiles("*.mp4"))
    {
        var fileName = file.Name
            .Replace("Jahresrückblick-Jahresrückblick", "Jahresrückblick");
        for (int year = 1950; year < DateTime.Now.Year; year++)
        {
            var found = (fileName.Contains($" {year}") || fileName.Contains($"S{year}E"));
            if (!found) continue;            

            var filesToRename = rootFolder.GetFiles($"{Path.GetFileNameWithoutExtension(file.Name)}.*");
            var nfoExists = filesToRename.Any(f => f.Extension.ToLower() == ".nfo");
            int EpisodeNo = 1;
            foreach (var fileToRename in filesToRename)
            {
                var EpisodeName = EpisodeNo.ToString().PadLeft(2, '0');
                fileName = $"{fileToRename.DirectoryName}\\Jahresrückblick S{year}E{EpisodeName}{fileToRename.Extension}";
                if (fileName != fileToRename.FullName)
                {
                    while(File.Exists(fileName))
                    {
                        EpisodeNo++;
                        EpisodeName = EpisodeNo.ToString().PadLeft(2, '0');
                        fileName = $"{fileToRename.DirectoryName}\\Jahresrückblick S{year}E{EpisodeName}{fileToRename.Extension}";
                    }
                    File.Move(fileToRename.FullName, fileName);
                }

                if (!nfoExists)
                {
                    fileName = $"{Path.ChangeExtension(fileName, ".nfo")}";
                    File.WriteAllText(fileName, $@"<?xml version=""1.0"" encoding=""utf-8""?>
<episodedetails>
  <title>Jahresrückblick {year}{((EpisodeNo > 1)?$" ({EpisodeNo})":"")}</title>
  <season>{year}</season>
  <episode>{EpisodeNo}</episode>
</episodedetails>", System.Text.Encoding.UTF8);
                    nfoExists = true;
                }
            }
            break;
        }
    }
}

void ScrapLoewenzahn()
{
    var episodes = new Fernsehserien_DE().LoadEpisodes("https://www.fernsehserien.de/loewenzahn/episodenguide")
    .ToArray();

    var rootPath = "\\\\ds1522plus\\MedienServer\\Kinder\\Serien\\Loewenzahn";
    var rootFolder = new DirectoryInfo(rootPath);
    if (rootFolder.Exists)
        foreach (var folder in rootFolder.GetDirectories("Staffel *"))
        {
            var seasonNo = int.Parse(folder.Name.Replace("Staffel ", ""));
            var seasonEpisodes = episodes.Where(e => e.SeasonNo == seasonNo).ToArray();
            foreach (var file in folder.GetFiles("*.mp4").ToArray())
            {
                var fileName = CorrectName(file.Name);
                var foundEpisode = seasonEpisodes.FirstOrDefault(e => fileName.Contains(CorrectName(e.Name)));
                if (foundEpisode is null)
                    foundEpisode = episodes.FirstOrDefault(e => fileName.Contains(CorrectName(e.Name)));
                if (foundEpisode is null)
                    continue;
                if (foundEpisode.SeasonNo == 0)
                    continue;
                var newFileName = $"Löwenzahn S{foundEpisode.SeasonNo.ToString().PadLeft(2, '0')}E{foundEpisode.EpisodeNo.ToString().PadLeft(2, '0')} - {CorrectName(foundEpisode.Name)}{file.Extension}";
                if (newFileName == file.Name)
                    continue;
                var filesToRename = folder.GetFiles($"{Path.GetFileNameWithoutExtension(file.Name)}.*")
                    .Concat(folder.GetFiles($"{Path.GetFileNameWithoutExtension(file.Name)}-thumb.*"));
                foreach (var fileToRename in filesToRename)
                {
                    newFileName = $"Löwenzahn S{foundEpisode.SeasonNo.ToString().PadLeft(2, '0')}E{foundEpisode.EpisodeNo.ToString().PadLeft(2, '0')} - {CorrectName(foundEpisode.Name)}{fileToRename.Extension}";
                    if (seasonNo != foundEpisode.SeasonNo)
                    {
                        newFileName = $"{fileToRename.Directory.Parent.FullName}\\Staffel {foundEpisode.SeasonNo.ToString().PadLeft(2, '0')}\\{newFileName}";
                    }
                    else
                    {
                        newFileName = $"{fileToRename.DirectoryName}\\{newFileName}";
                    }
                    if (fileToRename.Name.EndsWith($"-thumb{fileToRename.Extension}"))
                        newFileName = $"{Path.GetDirectoryName(newFileName)}\\{Path.GetFileNameWithoutExtension(newFileName)}-thumb{fileToRename.Extension}";

                    switch (fileToRename.Extension)
                    {
                        case ".nfo":
                            fileToRename.Delete();
                            break;
                        default:
                            File.Move(fileToRename.FullName, newFileName);
                            break;
                    }
                }
            }
        }
}

void Einstein()
{
    string rootPath = "\\\\ds1522plus\\MedienServer\\Kinder\\Serien\\Schloss Einstein";
    string newPath = $"{rootPath}\\neu";
    var rootFolder = new DirectoryInfo(rootPath);
    if (!rootFolder.Exists)
        return;
    var newFOlder = new DirectoryInfo(newPath);
    if (!newFOlder.Exists)
        return;

    var episodes = new Fernsehserien_DE().LoadEpisodes("https://www.fernsehserien.de/schloss-einstein/episodenguide")
        .OrderBy(e => e.SeasonNo)
        .ThenBy(e => e.EpisodeNo)
        .ToArray();
    foreach (var file in newFOlder.GetFiles("*.mp4"))
        try
        {
            var nameParts = Path.GetFileNameWithoutExtension(file.Name).Split('-').Select(p => p.Trim())
                .Where(p => !p.StartsWith("S") || p.Skip(3).FirstOrDefault() != 'E')
                .Where(p => p != "Schloss Einstein")
                .Select(p => p.EndsWith(". Folge") ? p.Replace(". Folge", "") : p.StartsWith("Folge ") ? p.Replace("Folge ", "") : p)
                .ToArray();
            if (!int.TryParse(nameParts.Where(p => int.TryParse(p, out _)).FirstOrDefault(), out var episodeNo))
                episodeNo = int.Parse(nameParts.Where(p => int.TryParse(p.Split('.').First(), out _)).FirstOrDefault().Split('.').First());
            nameParts = nameParts.Where(p => p != episodeNo.ToString())
                .Select(p => string.Join('.', p.Split('.').Where(e => e != episodeNo.ToString())).Trim())
                .ToArray();

            var episode = episodes.FirstOrDefault(e => e.Name == $"Folge {episodeNo}");
            if (episode is null)
                episode = episodes.FirstOrDefault(e => e.TotalEpisodeNo == episodeNo);
            if (episode is null)
                continue;
            nameParts = new string[] {
            "Schloss Einstein",
            $"S{episode.SeasonNo.ToString().PadLeft(2, '0')}E{episode.EpisodeNo.ToString().PadLeft(2, '0')}",
            episode.Name
        }.Concat(nameParts).ToArray();

            var episodeName = string.Join(" - ", nameParts);
            string destPath = $"{rootPath}\\Staffel {episode.SeasonNo}\\{episodeName}{file.Extension}";
            var destFile = new FileInfo(destPath);
            if (destFile.Exists)
                continue;
            if (!destFile.Directory.Exists)
                destFile.Directory.Create();
            file.MoveTo(destFile.FullName);
        }
        catch { }
}
void Pefferkoerner()
{
    string rootPath = "\\\\ds1522plus\\MedienServer\\Kinder\\Serien\\Die Pfefferkörner";
    string newPath = $"{rootPath}\\neu";
    var rootFolder = new DirectoryInfo(rootPath);
    if (!rootFolder.Exists)
        return;
    var newFOlder = new DirectoryInfo(newPath);
    if (!newFOlder.Exists)
        return;

    var episodes = new Fernsehserien_DE().LoadEpisodes("https://www.fernsehserien.de/die-pfefferkoerner/episodenguide")
        .Where(e => e.SeasonNo != 0)
        .OrderBy(e => e.SeasonNo)
        .ThenBy(e => e.EpisodeNo)
        .ToArray();
    foreach (var file in newFOlder.GetFiles("*.mp4"))
        try
        {
            var nameParts = Path.GetFileNameWithoutExtension(file.Name).Split('-').Select(p => p.Trim())
                .Where(p => !p.StartsWith("S") || p.Skip(3).FirstOrDefault() != 'E')
                .Where(p => p != "Die Pfefferkörner")
                .Select(p => p.EndsWith(". Folge") ? p.Replace(". Folge", "") : p.StartsWith("Folge ") ? p.Replace("Folge ", "") : p)
                .ToArray();
            if (!int.TryParse(nameParts.Where(p => int.TryParse(p.Split('.').First(), out _)).FirstOrDefault().Split('.').FirstOrDefault(), out var episodeNo))
                if (!int.TryParse(nameParts.Where(p => int.TryParse(p, out _)).FirstOrDefault(), out episodeNo))
                    episodeNo = int.Parse(nameParts.Where(p => int.TryParse(p.Split('.').First(), out _)).FirstOrDefault().Split('.').First());
            nameParts = nameParts.Where(p => p != episodeNo.ToString())
                .Select(p => string.Join('.', p.Split('.').Where(e => e != episodeNo.ToString())).Trim())
                .ToArray();

            var episode = episodes.FirstOrDefault(e => e.Name == $"Folge {episodeNo}");
            if (episode is null)
                episode = episodes.FirstOrDefault(e => e.TotalEpisodeNo == episodeNo);
            if (episode is null)
                continue;
            nameParts = new string[] {
            "Die Pfefferkörner",
            $"S{episode.SeasonNo.ToString().PadLeft(2, '0')}E{episode.EpisodeNo.ToString().PadLeft(2, '0')}",
            episode.Name
        }.Concat(nameParts).ToArray();

            var episodeName = string.Join(" - ", nameParts);
            string destPath = $"{rootPath}\\Staffel {episode.SeasonNo}\\{episodeName}{file.Extension}";
            var destFile = new FileInfo(destPath);
            if (destFile.Exists)
                continue;
            if (!destFile.Directory.Exists)
                destFile.Directory.Create();
            file.MoveTo(destFile.FullName);
        }
        catch { }
}

string CorrectName(string name)
{
    return name.ToLower()
        .Replace("?", "")
        .Replace("–", "-")
        .Replace("’", "")
        .Replace("'", "")
        .Replace(":", "")
        .Replace("–", "")
        .Replace("–", "");
}