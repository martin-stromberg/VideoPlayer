using System;
using System.Linq;

namespace MyVideoPlayer.ViewModels.Navigation.Sources
{
    [AttributeUsage(AttributeTargets.All)]
    public class TranslationAttribute: Attribute
    {

        public TranslationAttribute() { }

        public string LanguageCode { get; set; }

        public string Name { get; set; }

    }
}
