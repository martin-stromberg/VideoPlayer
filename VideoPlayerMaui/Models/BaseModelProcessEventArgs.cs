namespace VideoPlayer.Models
{
    public class BaseModelProcessEventArgs : BaseModelEventArgs
    {
        public BaseModelProcessEventArgs(BaseModel modelObj) : base(modelObj)
        {
        }
        public bool Continue { get; set; } = true;
    }
}
