using System;
using System.Linq;

namespace VideoPlayer.Service.Database.Models
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class DataModelReferenceAttribute: Attribute
    {

        public DataModelReferenceAttribute(Type dataModelType)
        {
            DataModelType = dataModelType;
        }

        public Type DataModelType { get; private set; }

    }
}
