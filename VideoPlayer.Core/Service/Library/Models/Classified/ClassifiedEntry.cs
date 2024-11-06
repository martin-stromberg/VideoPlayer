using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{
    public class ClassifiedEntry: BaseServiceModel
    {

        public ClassifiedEntry(DataClassifiedEntry dataModel, EntryType type)
            : base(dataModel)
        {
            if (DataModel is not null)
            {
                Enabled = ((DataClassifiedEntry)DataModel).Enabled;
                Visible = ((DataClassifiedEntry)DataModel).Visible;
                Type = (EntryType)((DataClassifiedEntry)DataModel).Type;
                ReleaseDate = ((DataClassifiedEntry)DataModel).ReleaseDate;
                PremieredAt = ((DataClassifiedEntry)DataModel).PremieredAt;
                if (Type != type)
                    throw new ArgumentException(nameof(type));
            }
            else
            {
                Type = type;
                Enabled = true;
            }
        }

        public EntryType Type
        {
            get
            {
                return GetProperty<EntryType>();
            }
            set
            {
                SetProperty(value);
            }
        }

        public bool Enabled
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }
        public bool Visible
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public DateTime ReleaseDate
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        public DateTime PremieredAt
        {
            get
            {
                return GetProperty<DateTime>();
            }
            set
            {
                SetProperty<DateTime>(value);
            }
        }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is null) return;
            ((DataClassifiedEntry)DataModel).Enabled = Enabled;
            ((DataClassifiedEntry)DataModel).Visible = Visible;
            ((DataClassifiedEntry)DataModel).PremieredAt = PremieredAt;
            ((DataClassifiedEntry)DataModel).ReleaseDate = ReleaseDate;
            ((DataClassifiedEntry)DataModel).Type = (DataEntryType)Type;
        }

        public override string ToString()
        {
            return $"{Name}";
        }
    }
}
