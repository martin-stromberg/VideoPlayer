using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reflection;
using VideoPlayer.Properties;
using VideoPlayer.Service.Export;

namespace VideoPlayer.Services.Registrations
{
    internal class DataExporterRegistration: IDataExporterRegistration
    {

        public string LicenseKey => secrets.Syncfusion;

    }
}
