using System;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(DataSetup))]
    public class Setup: BaseServiceModel
    {

        public Setup()
            : this(null) { }

        public Setup(DataSetup dataModel)
            : base(dataModel) { }

    }
}
