namespace Mediathek.Services.Export
{
    public interface IDatabaseExporter
    {

        Task<string> CreateExportFile();

    }
}
