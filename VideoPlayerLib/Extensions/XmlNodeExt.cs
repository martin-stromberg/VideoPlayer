using System;
using System.Linq;
using System.Xml;

namespace VideoPlayerLib.Extensions
{
    public static class XmlNodeExt
    {
        public static XmlNode FindChild(this XmlNode node, string path, bool createIfNotExists = false)
        {
            string[] parts = path.Split('/');
            foreach (var part in parts)
            {
                var currPart = part.ToLower();
                var childNode = node.ChildNodes.Cast<XmlNode>().FirstOrDefault(n => n.Name.ToLower() == currPart);
                if (childNode == null)
                {
                    if (!createIfNotExists)
                        return null;
                    childNode = node.AppendChild(node.OwnerDocument.CreateElement(part));
                }
                node = childNode;
            }
            return node;
        }
    }
}
