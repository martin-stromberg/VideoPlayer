namespace VideoPlayer.Service.Library.Models
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ServiceModelReferenceAttribute: Attribute
    {

        public ServiceModelReferenceAttribute(Type serviceModelType)
        {
            ServiceModelType = serviceModelType;
        }

        public Type ServiceModelType { get; private set; }

    }
}
