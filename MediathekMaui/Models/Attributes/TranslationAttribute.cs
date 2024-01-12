using System;
using System.Linq;

namespace Mediathek.Models.Attributes
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class TranslationAttribute: Attribute
    {

        public TranslationAttribute(string languageName, string translationValue)
        {
            LanguageName = languageName;
            TranslationValue = translationValue;
        }

        public string TranslationValue { get; private set; }

        public string LanguageName { get; private set; }

    }
}
