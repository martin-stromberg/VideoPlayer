namespace VideoPlayer.Services.Export
{
    public interface IDatabaseExporter
    {

        Task<string> CreateExportFile();

    }
}
