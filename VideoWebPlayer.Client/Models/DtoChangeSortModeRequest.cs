namespace VideoWebPlayer.Client.Models
{
    public class DtoChangeSortModeRequest
    {
        public string NewSortMode { get; set; } = string.Empty;
        public bool? ConfirmLossOfManualOrder { get; set; }
    }
}
