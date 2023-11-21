using VideoPlayer.Services.Database.Models;

namespace VideoPlayer.Services.Database
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DataModelReferenceAttribute: Attribute
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

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FieldModelReferenceAttribute: Attribute
    {

        public FieldModelReferenceAttribute(string fieldName, string referenceFieldName)
        {
            FieldName = fieldName;
            ReferenceFieldName = referenceFieldName;
        }

        public string FieldName { get; set; }

        public string ReferenceFieldName { get; set; }

    }
}
