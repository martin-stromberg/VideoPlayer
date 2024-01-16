using System;
using System.Linq;

namespace Mediathek.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class PathAttribute: Attribute
    {

        public PathAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        public string PropertyName { get; }

    }
}
