using System;
using System.Linq;
using System.Xml;

namespace Mediathek.Extensions
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

        public static IEnumerable<XmlNode> FindChildren(this XmlNode node, string path)
        {
            string[] parts = path.Split('/');
            for (var idx = 0; idx < parts.Length; idx++)
            {
                var part = parts[idx];
                var currPart = part.ToLower();
                var childNodes = node.ChildNodes.Cast<XmlNode>().Where(n => n.Name.ToLower() == currPart).ToArray();
                if (!childNodes.Any())
                    break;

                foreach (var  child in childNodes)
                {
                    var subPath = string.Join('/',
                                              parts.Skip(idx + 1));
                    if (string.IsNullOrWhiteSpace(subPath))
                        yield return child;
                    else
                    {
                        var subChildren = child.FindChildren(subPath);
                        foreach (var subChild in subChildren)
                            yield return subChild;
                    }
                }
            }
        }

    }
}
