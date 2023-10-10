using SQLite;
using System;
using System.Linq;

namespace VideoPlayerLib.Services.Database.Models
{
    public class BaseDataModel
    {

        public BaseDataModel() { }

        [PrimaryKey]
        [AutoIncrement]
        public long Id { get; set; }

        public string Name { get; set; }

        public bool IsRecord(BaseDataModel model)
        {
            return (Id == model.Id) && (Id != 0);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return this == null;
            if (obj.GetType() != GetType())
                return false;
            foreach (var prop in GetType()
                                 .GetProperties()
                                 .Where(p => p.GetCustomAttributes(typeof(AffectsEqualAttribute), true).Any()))
            {
                var ownValue = prop.GetValue(this);
                var compareValue = prop.GetValue(obj);
                if ((ownValue != null) && (compareValue == null))
                    return false;
                if ((ownValue == null) && (compareValue != null))
                    return false;
                if (!ownValue.Equals(compareValue))
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            unchecked // Allow arithmetic overflow, numbers will just "wrap around"
            {
                int hashcode = 1430287;
                foreach (var prop in GetType()
                                     .GetProperties()
                                     .Where(p => p.CanRead)
                                     .Where(p => p.GetCustomAttributes(typeof(AffectsEqualAttribute), true).Any()))
                {
                    var value = prop.GetValue(this);
                    hashcode = hashcode * 7302013 ^ value.GetHashCode();
                }
                return hashcode;
            }
        }

        public virtual void Update(BaseDataModel source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source.GetType() != GetType())
                throw new ArgumentException(nameof(source));
            foreach (var prop in GetType()
                                 .GetProperties()
                                 .Where(p => !p.GetCustomAttributes(typeof(AffectsEqualAttribute), true).Any())
                                 .Where(p => !p.GetCustomAttributes(typeof(PrimaryKeyAttribute), true).Any())
                                 .Where(p => p.CanRead && p.CanWrite))
            {
                var sourceValue = prop.GetValue(source);
                prop.SetValue(this, sourceValue);
            }
        }

    }
}
