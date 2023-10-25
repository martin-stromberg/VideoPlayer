using System.Text;
using System.Xml;

namespace VideoPlayer.Services.Mediathek
{
    public class MediathekInfoFile
    {

        public enum VideoType
        {

            Movie,
            TVShow

        }

        public VideoType Type { get; private set; }

        public string Station { get; private set; }

        public string Name { get; private set; }

        public string Title { get; private set; }

        public int SeasonNo { get; private set; }

        public int EpisodeNo { get; private set; }

        public string Plot { get; internal set; }

        internal async Task<bool> LoadAsync(string fileContent)
        {
            string[] lines = fileContent.Replace("\r\n", "\r").Split('\r');
            string firstLine = lines.FirstOrDefault();
            if (firstLine.Replace(" ", string.Empty).StartsWith("Sender:ZDF"))
                return await LoadMediathekInfoAsync(lines);
            else if (firstLine.Replace(" ", string.Empty).StartsWith("Sender:ARD"))
                return await LoadMediathekInfoAsync(lines);
            else
                return false;
        }

        private async Task<bool> LoadMediathekInfoAsync(string[] lines)
        {
            foreach (var line in lines)
                await ProcessMediathekLineAsync(line);
            switch (Name)
            {
                case "Filme":
                    Name = CorrectName(Title);
                    Title = Name;
                    Type = VideoType.Movie;
                    return !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Name);
                default:
                    Type = VideoType.TVShow;
                    if (!string.IsNullOrWhiteSpace(Plot) && Plot.StartsWith(Name))
                        Plot = Plot.Remove(0, Name.Length).TrimStart(' ', '-');
                    return SeasonNo != 0 && EpisodeNo != 0 && !string.IsNullOrWhiteSpace(Title)
                        && !string.IsNullOrWhiteSpace(Name);
            }
        }

        private string CorrectName(string title)
        {
            return title.Replace("Filme:", string.Empty).Trim().Replace("| ARD Mediathek", string.Empty).Trim();
        }

        private enum CurrentInfo
        {

            None,
            Website

        }

        private CurrentInfo CurrentLineInfo = CurrentInfo.None;

        private async Task ProcessMediathekLineAsync(string line)
        {
            switch (CurrentLineInfo)
            {
                case CurrentInfo.None:
                    var parts = line.Split(':');
                    switch (parts[0])
                    {
                        case "Sender":
                            Station = parts[1].Trim();
                            break;
                        case "Thema":
                            Name = parts[1].Trim();
                            break;
                        case "Titel":
                            Title = parts[1].Trim();
                            break;
                        case "Website":
                            CurrentLineInfo = CurrentInfo.Website;
                            break;
                    }
                    break;
                case CurrentInfo.Website:
                    CurrentLineInfo = CurrentInfo.None;
                    using (HttpClient client = new HttpClient())
                        try
                        {
                            var html = await client.GetStringAsync(line);
                            var bytes = Encoding.UTF8.GetBytes(html);
                            html = Encoding.Default.GetString(bytes);
                            await LoadInfoFromZDFWebsiteAsync(html);
                            await LoadInfoFromARDWebsite(html);
                        }
                        catch { }
                    break;
            }
        }

        private async Task LoadInfoFromARDWebsite(string html)
        {
            var head = html.Remove(0, html.IndexOf("<head"));
            head = html.Remove(head.IndexOf("</head>") + "</head>".Length - 1);

            var body = html.Remove(0, html.IndexOf("<body"));
            body = html.Remove(body.IndexOf("</body>") + "</body>".Length - 1);

            using (HttpClient client = new HttpClient())
                try
                {
                    var name = findTag(head, "meta", "property", "name", "content");
                    if (string.IsNullOrWhiteSpace(name))
                        throw new ArgumentNullException(nameof(name));
                    var description = findTag(head, "meta", "name", "description", "content");
                    if (string.IsNullOrWhiteSpace(description))
                        throw new ArgumentNullException(nameof(name));

                    SeasonNo = 0;
                    EpisodeNo = 0;
                    Plot = description;
                    Title = name;
                }
                catch { }
        }

        private string findTag(
            string source,
            string tagName,
            string proprtyName,
            string proprtyValue,
            string returnPropertyName)
        {
            XmlDocument XmlDoc = new XmlDocument();
            var tag = $"<{tagName}";
            while (source.Contains(tag))
            {
                source = source.Remove(0, source.IndexOf(tag));
                var starttag = source.Remove(source.IndexOf(">") + 1);
                try
                {
                    if (starttag.EndsWith("/>"))
                        XmlDoc.LoadXml(starttag);
                    else
                    {
                        var content = getTagContent(source, $"<{tagName}", $"</{tagName}>");
                        XmlDoc.LoadXml($"{starttag}{content}</{tagName}>");
                    }
                    if (XmlDoc.DocumentElement.GetAttribute(proprtyName) == proprtyValue)
                    {
                        if (string.IsNullOrWhiteSpace(returnPropertyName))
                            return XmlDoc.DocumentElement.InnerText.Trim().Replace("  ", " ");
                        else
                            return XmlDoc.DocumentElement.GetAttribute(returnPropertyName);
                    }
                }
                catch { }
                source = source.Remove(0, source.IndexOf(">") + 1);
            }
            return string.Empty;
        }

        private object getTagContent(string source, string startTag, string endTag)
        {
            var content = string.Empty;
            source = source.Remove(0, source.IndexOf(">") + 1);
            int endTagCount = 1;
            while (endTagCount > 0)
            {
                string contentPart = string.Empty;
                int offsetStart = source.IndexOf(startTag);
                int offsetEnd = source.IndexOf(endTag);
                if (offsetEnd == -1)
                    return string.Empty;
                if (offsetStart < offsetEnd && offsetStart >= 0)
                {
                    contentPart = source.Substring(0, offsetStart + startTag.Length);
                    endTagCount += 1;
                }
                else
                {
                    contentPart = source.Substring(0, offsetEnd);
                    endTagCount -= 1;
                    if (endTagCount > 0)
                        contentPart += endTag;
                }
                source = source.Remove(0, contentPart.Length);
                content += contentPart;
            }
            return content;
        }

        private async Task LoadInfoFromZDFWebsiteAsync(string html)
        {
            var head = html.Remove(0, html.IndexOf("<head"));
            head = html.Remove(head.IndexOf("</head>") + "</head>".Length - 1);

            var body = html.Remove(0, html.IndexOf("<body"));
            body = html.Remove(body.IndexOf("</body>") + "</body>".Length - 1);
            using (HttpClient client = new HttpClient())
                try
                {
                    var description = findTag(head, "meta", "name", "description", "content");
                    if (string.IsNullOrWhiteSpace(description))
                        throw new ArgumentNullException(nameof(description));
                    var title = findTag(head, "meta", "name", "twitter:title", "content");
                    if (string.IsNullOrWhiteSpace(title))
                        title = findTag(head, "meta", "name", "og:title", "content");
                    if (string.IsNullOrWhiteSpace(title))
                        findTag(body, "h1", "class", "big-headline", string.Empty);
                    if (string.IsNullOrWhiteSpace(title))
                        throw new ArgumentNullException(nameof(title));
                    var episodeInfo = findTag(body, "span", "class", "teaser-cat", string.Empty).Trim().Split(',');
                    var season = episodeInfo[0].Split(' ').Last();
                    var episode = episodeInfo[1].Split(' ').Last();

                    SeasonNo = Convert.ToInt32(season);
                    EpisodeNo = Convert.ToInt32(episode);
                    Plot = description;
                    Title = title;
                }
                catch (Exception ex) { }
        }

    }
}
