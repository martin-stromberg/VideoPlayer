namespace WebPlayerApi.Models
{
    public class PagedResult<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<T> Items { get; set; } = new();
        public bool Loading { get; set; }
    }

    public class CardResult<T>
    {
        public T Item { get; set; }
        public bool Loading { get; set; }
    }
}
