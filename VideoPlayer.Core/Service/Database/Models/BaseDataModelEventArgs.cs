namespace VideoPlayer.Service.Database.Models
{
    public class BaseDataModelEventArgs: EventArgs
    {

        public BaseDataModelEventArgs(BaseDataModel dataModel)
        {
            DataModel = dataModel;
        }

        public BaseDataModel DataModel { get; private set; }

    }
}
