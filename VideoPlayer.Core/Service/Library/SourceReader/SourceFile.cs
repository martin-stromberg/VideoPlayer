namespace VideoPlayer.Service.Library.SourceReader
{
    public class SourceFile
    {

        public string Name { get; set; }

        public string FullPath { get; set; }

        public string Path { get; set; }

        public DateTime LastWriteTime { get; set; }
        public override string ToString()
        {
            return Path;
        }
    }
}
