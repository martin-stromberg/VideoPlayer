
namespace Mediathek.Services.Database
{
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
