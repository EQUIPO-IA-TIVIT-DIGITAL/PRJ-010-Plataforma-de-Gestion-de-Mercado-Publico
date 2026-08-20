using System.Text.Json;
using Microsoft.Extensions.Logging;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using MPM.Modules.Propuestas.Data;
using MPM.Modules.Propuestas.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Propuestas.Services;

public interface IProposalLicitacionLookup
{
    Task<LicitacionDetalleDto?> ObtenerPorCodigoAsync(string codigoExterno, CancellationToken ct = default);
}

public sealed class ProposalLicitacionLookup(LicitacionService service) : IProposalLicitacionLookup
{
    public Task<LicitacionDetalleDto?> ObtenerPorCodigoAsync(string codigoExterno, CancellationToken ct = default)
        => service.ObtenerPorCodigoAsync(codigoExterno, ct);
}

public interface IProposalSummaryProvider
{
    Task<string?> ObtenerResumenAsync(long licitacionId, CancellationToken ct = default);
}

public sealed class AnalisisProposalSummaryProvider(AnalisisComercialHandler handler) : IProposalSummaryProvider
{
    public async Task<string?> ObtenerResumenAsync(long licitacionId, CancellationToken ct = default)
        => (await handler.ObtenerUltimoAsync(licitacionId, ct))?.ResumenEjecutivo;
}

public interface ICertificationFileProvider
{
    Task<byte[]> DownloadAsync(string fileId, CancellationToken ct = default);
}

public sealed class CensusCertificationFileProvider(MPM.Modules.Censo.Services.CensusClient client) : ICertificationFileProvider
{
    public Task<byte[]> DownloadAsync(string fileId, CancellationToken ct = default)
        => client.DownloadCertificationFileAsync(fileId, ct);
}

public interface IPropuestaService
{
    Task<GenerarPropuestaResponse> GenerarAsync(string codigoExterno, GenerarPropuestaRequest request, string generadoPor, CancellationToken ct = default);
    Task<CatalogoPage<PropuestaHistorialDto>> ListarAsync(string codigoExterno, string? estado, int page, int size, CancellationToken ct = default);
    Task<(LicitacionDetalleDto Licitacion, PropuestaRow Propuesta)> ObtenerArchivoAsync(string codigoExterno, long propuestaId, CancellationToken ct = default);
    Task<Stream?> DownloadStoredAsync(string storedPath, CancellationToken ct = default);
    Task<PropuestaHistorialDto> ActualizarEstadoAsync(string codigoExterno, long propuestaId, string estado, CancellationToken ct = default);
    Task<ExportarDriveResponse> ExportarDriveAsync(string codigoExterno, long propuestaId, CancellationToken ct = default);
}

public sealed class PropuestaService(
    PropuestasHandler handler,
    IProposalLicitacionLookup licitacionLookup,
    IProposalSummaryProvider summaryProvider,
    ICertificationFileProvider certificationFileProvider,
    ProposalTemplateProvider templateProvider,
    DocxProposalGenerator generator,
    IStorageService storage,
    IGoogleDriveService driveService,
    ILogger<PropuestaService> logger) : IPropuestaService
{
    public sealed class PropuestaException(string code, string message, Exception? inner = null) : Exception(message, inner)
    {
        public string Code { get; } = code;
    }

    public async Task<GenerarPropuestaResponse> GenerarAsync(
        string codigoExterno, GenerarPropuestaRequest request, string generadoPor, CancellationToken ct = default)
    {
        ValidateCodigo(codigoExterno);
        var licitacion = await licitacionLookup.ObtenerPorCodigoAsync(codigoExterno, ct)
            ?? throw new PropuestaException("LIC_001", "Licitación no encontrada");

        var decision = await handler.ObtenerDecisionAsync(licitacion.Id, ct);
        if (!string.Equals(decision?.Decision, "go", StringComparison.OrdinalIgnoreCase))
            throw new PropuestaException("PRO_003", "La propuesta sólo puede generarse con una decisión GO vigente");

        var chaptersPage = await handler.ListarCapitulosActivosAsync(ct);
        var certificationsPage = await handler.ListarCertificacionesActivasAsync(ct);
        var experiencesPage = await handler.ListarExperienciasActivasAsync(ct);

        if (request.CertificacionesIds is { Count: > 0 } && certificationsPage.Items.Count == 0
            || request.ExperienciasIds is { Count: > 0 } && experiencesPage.Items.Count == 0)
            throw new PropuestaException("PRO_006", "El catálogo seleccionado no tiene elementos activos");

        var chapters = SelectChapters(request.CapitulosIds, chaptersPage.Items);
        var certifications = SelectCertifications(request.CertificacionesIds, certificationsPage.Items);
        var experiences = SelectExperiences(request.ExperienciasIds, experiencesPage.Items);

        var certificationDocuments = await DownloadCertificationFilesAsync(certifications, ct);
        var summary = await summaryProvider.ObtenerResumenAsync(licitacion.Id, ct);
        var usuario = await handler.ObtenerUsuarioAsync(generadoPor, ct);
        var contactName = !string.IsNullOrWhiteSpace(usuario?.Nombre) ? usuario.Nombre : (generadoPor.Contains('@') ? "Equipo Comercial TIVIT" : generadoPor);
        var contactEmail = !string.IsNullOrWhiteSpace(usuario?.Email) ? usuario.Email : (generadoPor.Contains('@') ? generadoPor : "comercial.chile@tivit.com");

        byte[] bytes;
        try
        {
            // Resolve explicitly before rendering so an absent file is PRO_010, not PRO_009.
            _ = templateProvider.ResolvePath();
            bytes = generator.Generate(new ProposalDocumentInput(
                Chapters: chapters,
                Certifications: certificationDocuments,
                Experiences: experiences,
                ExecutiveSummary: summary,
                LicitacionTitulo: licitacion.Nombre,
                LicitacionCodigo: licitacion.CodigoExterno,
                OrganismoComprador: licitacion.Organismo,
                ContactName: contactName,
                ContactEmail: contactEmail));
        }
        catch (ProposalGenerationException ex)
        {
            throw new PropuestaException(ex.Code, ex.Message, ex);
        }

        var safeCode = SanitizePathSegment(codigoExterno);
        var storagePath = $"licitaciones/{safeCode}/propuestas";
        var fileName = $"Propuesta_{safeCode}_{Guid.NewGuid():N}.docx";
        string storedPath;
        try
        {
            await using var content = new MemoryStream(bytes, writable: false);
            storedPath = await storage.UploadAsync(storagePath, fileName, content,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ct);
        }
        catch (Exception ex)
        {
            throw new PropuestaException("PRO_009", "No se pudo almacenar el documento generado", ex);
        }

        ProposalMutationResult mutation;
        try
        {
            mutation = await handler.GenerarPropuestaAsync(
                licitacion.Id,
                JsonSerializer.Serialize(chapters.Select(c => c.Id)),
                JsonSerializer.Serialize(certifications.Select(c => c.Id)),
                JsonSerializer.Serialize(experiences.Select(e => e.Id)),
                storedPath,
                generadoPor,
                ct);
        }
        catch
        {
            await TryDeleteAsync(storedPath, ct);
            throw;
        }

        var generatedAt = DateTime.UtcNow;
        var storageKind = storedPath.StartsWith("gs://", StringComparison.OrdinalIgnoreCase) ? "GCS" : "local";
        return new GenerarPropuestaResponse
        {
            PropuestaId = mutation.Id,
            Version = mutation.Version,
            Estado = "generada",
            RutaDescarga = $"/api/v1/licitaciones/{Uri.EscapeDataString(codigoExterno)}/propuestas/{mutation.Id}/archivo",
            GeneradoPor = generadoPor,
            GeneradoAt = generatedAt,
            Resumen = new PropuestaResumenDto
            {
                Capitulos = chapters.Count,
                Certificaciones = certifications.Count,
                CertificacionesSinPdf = certificationDocuments.Count(c => c.PdfBytes == null),
                Experiencias = experiences.Count,
                ArchivosStorage = storageKind,
            },
        };
    }

    public async Task<CatalogoPage<PropuestaHistorialDto>> ListarAsync(
        string codigoExterno, string? estado, int page, int size, CancellationToken ct = default)
    {
        ValidateCodigo(codigoExterno);
        ValidatePage(page, size);
        var licitacion = await GetLicitacionAsync(codigoExterno, ct);
        var result = await handler.ListarPropuestasAsync(licitacion.Id, CleanEstado(estado), page, size, ct);
        foreach (var item in result.Items)
            item.RutaDescarga = $"/api/v1/licitaciones/{Uri.EscapeDataString(codigoExterno)}/propuestas/{item.PropuestaId}/archivo";
        return result;
    }

    public async Task<(LicitacionDetalleDto Licitacion, PropuestaRow Propuesta)> ObtenerArchivoAsync(
        string codigoExterno, long propuestaId, CancellationToken ct = default)
    {
        ValidateCodigo(codigoExterno);
        if (propuestaId <= 0) throw new PropuestaException("VAL_007", "propuestaId debe ser positivo");
        var licitacion = await GetLicitacionAsync(codigoExterno, ct);
        var proposal = await handler.ObtenerPropuestaAsync(propuestaId, ct);
        if (proposal == null || proposal.LicitacionId != licitacion.Id || string.IsNullOrWhiteSpace(proposal.RutaArchivo))
            throw new PropuestaException("PRO_001", "Propuesta no encontrada o sin archivo");
        return (licitacion, proposal);
    }

    public async Task<PropuestaHistorialDto> ActualizarEstadoAsync(
        string codigoExterno, long propuestaId, string estado, CancellationToken ct = default)
    {
        ValidateCodigo(codigoExterno);
        var licitacion = await GetLicitacionAsync(codigoExterno, ct);
        var proposal = await handler.ObtenerPropuestaAsync(propuestaId, ct);
        if (proposal == null || proposal.LicitacionId != licitacion.Id)
            throw new PropuestaException("PRO_001", "Propuesta no encontrada");

        var normalizedState = CleanEstado(estado) ?? throw new PropuestaException("VAL_001", "estado es obligatorio");
        if (!((proposal.Estado == "generada" && normalizedState is "enviada" or "descartada")
              || (proposal.Estado == "enviada" && normalizedState == "descartada")))
            throw new PropuestaException("PRO_008", "Transición de estado inválida");

        await handler.ActualizarEstadoPropuestaAsync(propuestaId, normalizedState, ct);
        return new PropuestaHistorialDto
        {
            PropuestaId = proposal.Id,
            Version = proposal.Version,
            Estado = normalizedState,
            Capitulos = CountJson(proposal.CapitulosSeleccionados),
            Certificaciones = CountJson(proposal.CertificacionesIds),
            Experiencias = CountJson(proposal.ExperienciasIds),
            GeneradoPor = proposal.GeneradoPor,
            GeneradoAt = proposal.GeneradoAt,
        };
    }

    public async Task<Stream?> DownloadStoredAsync(string storedPath, CancellationToken ct = default)
        => await storage.DownloadAsync(storedPath, ct);

    public async Task<ExportarDriveResponse> ExportarDriveAsync(
        string codigoExterno, long propuestaId, CancellationToken ct = default)
    {
        ValidateCodigo(codigoExterno);
        var (licitacion, proposal) = await ObtenerArchivoAsync(codigoExterno, propuestaId, ct);
        var stream = await DownloadStoredAsync(proposal.RutaArchivo!, ct)
            ?? throw new PropuestaException("PRO_001", "No se pudo leer el archivo de la propuesta");

        var fileName = $"Propuesta_{SanitizePathSegment(codigoExterno)}_v{proposal.Version}.docx";
        return await driveService.ExportarArchivoAsync(
            codigoExterno, fileName, stream,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ct);
    }

    private async Task<LicitacionDetalleDto> GetLicitacionAsync(string codigoExterno, CancellationToken ct)
        => await licitacionLookup.ObtenerPorCodigoAsync(codigoExterno, ct)
           ?? throw new PropuestaException("LIC_001", "Licitación no encontrada");

    private async Task<List<CertificationDocument>> DownloadCertificationFilesAsync(
        IReadOnlyList<CertificacionCatalogoDto> certifications, CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(4, 4);
        var tasks = certifications.Select(async certification =>
        {
            if (string.IsNullOrWhiteSpace(certification.FileIdCensus))
                return new CertificationDocument(certification, null, "No existe PDF asociado");

            await semaphore.WaitAsync(ct);
            try
            {
                var bytes = await certificationFileProvider.DownloadAsync(certification.FileIdCensus, ct);
                if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
                    return new CertificationDocument(certification, null, "La respuesta no era un PDF válido");
                return new CertificationDocument(certification, bytes, null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "No se pudo descargar el PDF de una certificación; se conserva el texto");
                return new CertificationDocument(certification, null, "No se pudo descargar el PDF");
            }
            finally
            {
                semaphore.Release();
            }
        });
        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task TryDeleteAsync(string storedPath, CancellationToken ct)
    {
        try { await storage.DeleteAsync(storedPath, ct); }
        catch (Exception ex) { logger.LogError(ex, "No se pudo limpiar el objeto de propuesta tras fallo de persistencia"); }
    }

    private static List<CapituloCatalogoDto> SelectChapters(List<long>? ids, IReadOnlyList<CapituloCatalogoDto> active)
    {
        if (ids == null || ids.Count == 0) return active.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
        return SelectById(ids, active, "capítulos").OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
    }

    private static List<CertificacionCatalogoDto> SelectCertifications(List<long>? ids, IReadOnlyList<CertificacionCatalogoDto> active)
        => ids == null ? [] : SelectById(ids, active, "certificaciones");

    private static List<ExperienciaCatalogoDto> SelectExperiences(List<long>? ids, IReadOnlyList<ExperienciaCatalogoDto> active)
        => ids == null ? [] : SelectById(ids, active, "experiencias");

    private static List<T> SelectById<T>(IEnumerable<long> ids, IReadOnlyList<T> active, string label) where T : class
    {
        var requested = ids.Distinct().ToList();
        var byId = active.ToDictionary(item => GetId(item));
        var missing = requested.Where(id => !byId.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new PropuestaException("PRO_002", $"Hay {label} inexistentes o inactivas");
        return requested.Select(id => byId[id]).ToList();
    }

    private static long GetId<T>(T item) where T : class => item switch
    {
        CapituloCatalogoDto chapter => chapter.Id,
        CertificacionCatalogoDto certification => certification.Id,
        ExperienciaCatalogoDto experience => experience.Id,
        _ => throw new InvalidOperationException("Tipo de catálogo no soportado"),
    };

    private static int CountJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try { using var doc = JsonDocument.Parse(json); return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0; }
        catch (JsonException) { return 0; }
    }

    private static void ValidateCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Contains("..", StringComparison.Ordinal) || codigo.Contains('/') || codigo.Contains('\\'))
            throw new PropuestaException("VAL_001", "codigoExterno inválido");
    }

    private static string SanitizePathSegment(string value)
        => string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

    private static string? CleanEstado(string? estado)
        => string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToLowerInvariant();

    private static void ValidatePage(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100)
            throw new PropuestaException("VAL_001", "Paginación inválida");
    }
}
