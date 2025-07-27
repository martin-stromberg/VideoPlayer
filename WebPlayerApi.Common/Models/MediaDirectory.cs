namespace WebPlayerApi.Models
{
    public class BaseDataModel
    {
        public string Id { get; set; }
        public DateTime LastUpdate { get; set; }
    }
    public class MediaSource: BaseDataModel
    {
        public string Name { get; set; }
        public string Icon { get; set; }
    }
    public class MediaDirectory: MediaSource
    {        
        public string Path { get; set; }
        public string Servername { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public DateTime LastScan { get; set; }
    }

}
