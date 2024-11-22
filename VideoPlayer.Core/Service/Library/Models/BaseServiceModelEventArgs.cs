namespace VideoPlayer.Service.Library.Models
{
    public class BaseServiceModelEventArgs: EventArgs
    {

        public BaseServiceModelEventArgs(BaseServiceModel modelObject)
        {
            ModelObject = modelObject;
        }

        public BaseServiceModel ModelObject { get; set; }

    }
}
