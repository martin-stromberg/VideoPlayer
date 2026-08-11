using Xunit;

namespace VideoWebPlayer.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public class MediaSourceClassifierCollection
{
    public const string Name = "MediaSourceClassifier";
}
