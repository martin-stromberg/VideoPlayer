using Newtonsoft.Json;
using System;
using System.Linq;

namespace VideoPlayer.Service.Library.Models.MediaInformation
{
    public class MediaInformation: IComparable
    {

        public string Title { get; set; }

        public string OriginalTitle { get; set; }

        public string Language { get; set; }

        public DateTime LastUpdate { get; set; }

        public string[] Studios { get; set; }
        public ActorInformation[] Actors { get; set; }

        public virtual int CompareTo(object obj)
        {
            if (obj is null) 
                return -1;            
            if (!(obj is MediaInformation))
                return 1;
            if (obj.GetType().FullName != GetType().FullName)
                return 1;

            var own = ToString();
            var compare = (obj as MediaInformation).ToString();
            return own.CompareTo(compare);
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override bool Equals(object obj)
        {
            return CompareTo(obj) == 0;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}
