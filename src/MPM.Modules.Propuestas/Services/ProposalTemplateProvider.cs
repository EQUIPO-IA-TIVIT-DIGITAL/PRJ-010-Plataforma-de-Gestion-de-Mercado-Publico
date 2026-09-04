namespace MPM.Modules.Propuestas.Services;

public sealed class ProposalTemplateProvider
{
    public const string FileName = "tivit_proposal_template.docx";

    private readonly string? _configuredPath;

    public ProposalTemplateProvider(string? configuredPath = null)
    {
        _configuredPath = configuredPath;
    }

    public string ResolvePath()
    {
        var path = _configuredPath ?? Path.Combine(AppContext.BaseDirectory, "Templates", FileName);
        if (!File.Exists(path))
            throw new ProposalGenerationException("PRO_010", "La plantilla corporativa no está disponible");
        return path;
    }

    public Stream OpenRead()
    {
        var path = ResolvePath();
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ProposalGenerationException("PRO_010", "No se pudo leer la plantilla corporativa", ex);
        }
    }
}

public sealed class ProposalGenerationException : Exception
{
    public ProposalGenerationException(string code, string message, Exception? inner = null)
        : base(message, inner) => Code = code;

    public string Code { get; }
}
