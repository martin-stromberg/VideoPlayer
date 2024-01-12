namespace Mediathek.Models
{
    public class BaseModelEventArgs: EventArgs
    {

        public BaseModelEventArgs(BaseModel modelObj)
        {
            Element = modelObj;
        }

        public BaseModel Element { get; }

    }
}
