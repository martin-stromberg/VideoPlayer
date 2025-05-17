using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace VideoPlayer.Scapper
{
    public static class Extensins
    {
        public static string Remove(this string str, string textToFind, bool removeBefore)
        {
            var offset = str.IndexOf(textToFind);
            if (offset == -1) return string.Empty;
            if (removeBefore)
                str = str.Remove(0, offset);
            else
                str = str.Remove(offset + textToFind.Length);
            return str;
        }

    }
    public class EpisodeInfo
    {
        public int SeasonNo { get; set; }
        public int EpisodeNo { get; set; }
        public string Name { get; set; }
        public int TotalEpisodeNo { get; set; }
    }
    public  class Fernsehserien_DE
    {
        public IEnumerable<EpisodeInfo> LoadEpisodes(string uri)
        {
            XmlDocument xmlDocument = new XmlDocument();
            HttpClient client = new HttpClient();
            var response = client.GetAsync(uri).Result;
            var responseContent = response.Content.ReadAsStringAsync().Result;
            responseContent = responseContent.Remove("<section ", true);
            while (responseContent.Length > 0)
            {
                var section = responseContent.Remove("</section>", false);
                responseContent = responseContent.Remove(0, section.Length);
                               
                section = section.Remove("<a ", true);
                while (section.Length > 0)
                    try
                    {
                        var link = section.Remove("</a>", false);
                        var cell = section.Remove("role=\"cell\"", true);
                        section = section.Remove(0, link.Length);
                        var title = link.Remove("title=\"", true);
                        if (string.IsNullOrWhiteSpace(title))
                            continue;
                        title = title.Remove(0, "title=\"".Length).Remove("\"", false).Trim('"');
                        if (string.IsNullOrWhiteSpace(title))
                            continue;                        
                        var episodeCounter = 0;
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            cell = cell.Remove(0, "role=\"cell\"".Length);
                            cell = cell.Remove("role=\"cell\"", true);
                            cell = cell.Remove(0, "role=\"cell\"".Length);
                            var episodeCounterText = cell.Remove(">", true).Remove(0, 1).Remove("<", false);
                            episodeCounterText = episodeCounterText.Remove(episodeCounterText.Length - 1);
                            if (!int.TryParse(episodeCounterText, out episodeCounter))
                                episodeCounter = 0;
                        }
                        string pattern = @"(?<zahl>\d+)\.(?<zahl2>\d+)\s(?<bezeichnung>.+)";
                        Match match = Regex.Match(title, pattern);
                        if (!match.Success)
                            continue;
                        var seasonNo = int.Parse(match.Groups["zahl"].Value);
                        var episodeNo = int.Parse(match.Groups["zahl2"].Value);
                        title = match.Groups["bezeichnung"].Value;
                        yield return new EpisodeInfo()
                        {
                            TotalEpisodeNo = episodeCounter,
                            SeasonNo = seasonNo,
                            EpisodeNo = episodeNo,
                            Name = title
                        };
                    }
                    finally
                    {
                        section = section.Remove("<a ", true);
                    }
                responseContent = responseContent.Remove("<section ", true);
            }
        }
    }
}
