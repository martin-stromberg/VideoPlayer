namespace VideoWebPlayer.Data
{
    public class BlockedLoginIp
    {
        public string Ip { get; set; } = "";
        public DateTime BlockedAtUtc { get; set; }
        public int Failures { get; set; }
    }
}