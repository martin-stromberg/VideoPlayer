namespace VideoPlayer.Services.MediaLibrary.Scanner.Events
{
    public class FolderScanEventArgs : EventArgs
    {

        public FolderScanEventArgs(string value)
            : base()
        {
            Value = value;
        }

        public string Value { get; }

        public bool ScanFiles { get; set; }

        public bool ScanFolders { get; set; }
        public bool Success { get; internal set; }
    }
}
