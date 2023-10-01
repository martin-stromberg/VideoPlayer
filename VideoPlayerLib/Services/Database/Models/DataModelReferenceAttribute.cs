namespace VideoPlayerLib.Services.Database.Models
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DataModelReferenceAttribute : Attribute
    {
        public DataModelReferenceAttribute(Type dataModelType)
        {
            if (!dataModelType.IsAssignableTo(typeof(BaseDataModel)))
                throw new ArgumentException(nameof(dataModelType));
            DataModelType = dataModelType;
        }

        public Type DataModelType { get; }
        public string FilterPropertyName { get; set; }
        public string FilterPropertyValue { get; set; }
    }
}
