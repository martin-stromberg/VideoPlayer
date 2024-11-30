using Microsoft.Extensions.Logging;
using System.Reflection;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Sources;
using VideoPlayer.Service.Library.SourceReader;

namespace VideoPlayer.Service.BaseServices
{
    public abstract class SourceTimerService : TimerService
    {

        private Dictionary<string, ISourceReader> _SourceReaderTypes = new Dictionary<string, ISourceReader>();

        protected SourceTimerService(ILogger logger) : base(logger)
        {
        }

        protected ISourceReader CreateReader(MediaSource source)
        {
            var sourceType = source.GetType();
            var destType = typeof(ISourceReader);
            var key = $"{sourceType.FullName}-{source.Id}";
            if (!_SourceReaderTypes.ContainsKey(key))
            {
                destType = sourceType.Assembly
                                     .GetTypes()
                                     .Where(t => !t.IsAbstract)
                                     .Where(t => t.IsAssignableTo(destType))
                                     .Where(t =>
                                     {
                                         var attr = t.GetCustomAttribute(typeof(ServiceModelReferenceAttribute)) as ServiceModelReferenceAttribute;
                                         if (attr is null)
                                             return false;
                                         if (attr.ServiceModelType != sourceType)
                                             return false;
                                         return true;
                                     })
                                     .FirstOrDefault();
                if (destType is null)
                    return null;
                var e = new SourceReaderEventArgs() { Source = source };
                CreatingSourceReader?.Invoke(this, e);

                ISourceReader reader = e.Reader ?? Activator.CreateInstance(destType, source) as ISourceReader;
                _SourceReaderTypes.Add(key, reader);
            }
            return _SourceReaderTypes[key];
        }

        public event EventHandler<SourceReaderEventArgs> CreatingSourceReader;

    }
}
