namespace WebPlayer.Data
{
    public interface IApplicationUser
    {
        string Id { get; }
        string? UserName { get;  }
        string Sources { get; set; }
    }

}
