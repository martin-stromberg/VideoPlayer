using System.Text;
using System.Xml;

namespace VideoPlayer.Services.Mediathek
{
    public class MediathekInfoFile
    {

        public enum VideoType
        {
            Unknown,
            Movie,
            TVShow

        }

        public VideoType Type { get; private set; }

        public string Station { get; private set; }

        public string Name { get; private set; }

        public string Title { get; private set; }
        public string ImageURL { get; private set; }

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
            Type = VideoType.Unknown;
            foreach (var line in lines)
                await ProcessMediathekLineAsync(line);
            switch(Type)
            {
                case VideoType.Movie:
                    Name = CorrectName(Title);
                    Title = Name;
                    Type = VideoType.Movie;
                    return !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Name);
                case VideoType.TVShow:
                    Type = VideoType.TVShow;
                    if (!string.IsNullOrWhiteSpace(Plot) && Plot.StartsWith(Name))
                        Plot = Plot.Remove(0, Name.Length).TrimStart(' ', '-');
                    return SeasonNo != 0 && EpisodeNo != 0 && !string.IsNullOrWhiteSpace(Title)
                        && !string.IsNullOrWhiteSpace(Name);
                default:
                    return false;
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
                            LoadInfoFromZDFWebsiteAsync(html);
                            LoadInfoFromARDWebsite(html);
                        }
                        catch { }
                    break;
            }
        }

        private void LoadInfoFromARDWebsite(string html)
        {
            var head = html.Remove(0, html.IndexOf("<head"));
            head = head.Remove(head.IndexOf("</head>") + "</head>".Length - 1);

            var body = html.Remove(0, html.IndexOf("<body"));
            body = body.Remove(body.IndexOf("</body>") + "</body>".Length - 1);

            try
            {
                var name = findTag(head, "meta", "property", "name", "content");
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentNullException(nameof(name));
                var description = findTag(head, "meta", "name", "description", "content");
                if (string.IsNullOrWhiteSpace(description))
                    throw new ArgumentNullException(nameof(description));
                description = description.Split('|').LastOrDefault()?.Trim();
                if (string.IsNullOrWhiteSpace(description))
                    throw new ArgumentNullException(nameof(description));
                name = name.Split('|').FirstOrDefault()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentNullException(nameof(name));
                var blacklist = new string[] { "Video" };
                description = string.Join("\r\n", description.Split('|').Where(d => !blacklist.Contains(d.Trim())));

                ImageURL = findTag(head, "meta", "property", "og:image", "content");
                if (!string.IsNullOrWhiteSpace(ImageURL))
                {
                    ImageURL = ImageURL.Substring(ImageURL.IndexOf("url=") + "url=".Length);
                    ImageURL = Uri.UnescapeDataString(ImageURL);
                    ImageURL = ImageURL.Replace("{width}", "500");
                }

                SeasonNo = 0;
                EpisodeNo = 0;
                Plot = description;
                Title = name;
                Type = VideoType.Movie;
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
                catch 
                {
                    var currTag = starttag;
                    var offset = currTag.IndexOf($"{proprtyName}=\"");
                    if (offset >= 0)
                    {
                        currTag = currTag.Substring(offset + $"{proprtyName}=\"".Length);
                        offset = currTag.IndexOf('"');
                        currTag = currTag.Substring (0, offset);
                        if (currTag == proprtyValue && !string.IsNullOrWhiteSpace(returnPropertyName))
                        {
                            currTag = starttag;
                            offset = currTag.IndexOf($"{returnPropertyName}=\"");
                            if (offset >= 0)
                            {
                                currTag = currTag.Substring(offset + $"{returnPropertyName}=\"".Length);
                                offset = currTag.IndexOf('"');
                                currTag = currTag.Substring(0, offset);
                                return currTag;
                            }
                        }
                    }
                }
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

        private void LoadInfoFromZDFWebsiteAsync(string html)
        {
            var head = html.Remove(0, html.IndexOf("<head"));
            head = head.Remove(head.IndexOf("</head>") + "</head>".Length - 1);

            var body = html.Remove(0, html.IndexOf("<body"));
            body = body.Remove(body.IndexOf("</body>") + "</body>".Length - 1);
            try
            {
                var description = findTag(head, "meta", "name", "description", "content");
                if (string.IsNullOrWhiteSpace(description))
                    throw new ArgumentNullException(nameof(description));
                description = description.Split("\r\n").FirstOrDefault().Trim();
                description = description.Split("|").FirstOrDefault().Trim();
                var title = findTag(head, "meta", "name", "twitter:title", "content");
                if (string.IsNullOrWhiteSpace(title))
                    title = findTag(head, "meta", "name", "og:title", "content");
                if (string.IsNullOrWhiteSpace(title))
                    findTag(body, "h1", "class", "big-headline", string.Empty);
                if (string.IsNullOrWhiteSpace(title))
                    throw new ArgumentNullException(nameof(title));
                var imageURL = findTag(head, "meta", "name", "twitter:image", "content");
                if (string.IsNullOrWhiteSpace(imageURL))
                    imageURL = findTag(head, "meta", "name", "og:image", "content");

                Plot = description;
                Title = title;
                ImageURL = imageURL;
                var episodeInfo = findTag(body, "span", "class", "teaser-cat", string.Empty).Trim().Split(',');
                if (episodeInfo.Length == 2)
                {
                    var season = episodeInfo[0].Split(' ').Last();
                    var episode = episodeInfo[1].Split(' ').Last();
                    SeasonNo = Convert.ToInt32(season);
                    EpisodeNo = Convert.ToInt32(episode);
                    Type = VideoType.TVShow;
                }
                else
                    Type = VideoType.Movie;
            }
            catch (Exception ex) { }
        }

    }
}
