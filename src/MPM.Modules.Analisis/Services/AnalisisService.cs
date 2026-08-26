using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MPM.Modules.Analisis.Data;
using MPM.Modules.Analisis.Models;
using MPM.Shared.Services;

namespace MPM.Modules.Analisis.Services;

public class AnalisisService(
    AnalisisHandler handler,
    GeminiService geminiService,
    IStorageService storageService,
    IAnalisisBackgroundService backgroundService,
    ILogger<AnalisisService>? logger = null,
    IServiceProvider? serviceProvider = null)
{
    private readonly AnalisisHandler _handler = handler;
    private readonly GeminiService _geminiService = geminiService;
    private readonly IStorageService _storageService = storageService;
    private readonly IAnalisisBackgroundService _backgroundService = backgroundService;
    private readonly ILogger<AnalisisService> _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AnalisisService>.Instance;
    private readonly IServiceProvider? _serviceProvider = serviceProvider;

    public async Task<(PaginatedResult<WorkspaceItemDto>? Result, string? Error)> ListarWorkspacesAsync(
        int page, int pageSize, string? search, string? estado,
        DateOnly? fechaDesde = null, DateOnly? fechaHasta = null, CancellationToken ct = default)
    {
        var (items, total) = await _handler.ListarWorkspacesAsync(page, pageSize, search, estado, fechaDesde, fechaHasta, ct);
        var list = items.ToList();
        return (new PaginatedResult<WorkspaceItemDto>
        {
            Items = list,
            Page = page,
            PageSize = pageSize,
            TotalRecords = (int)total,
            TotalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0
        }, null);
    }

    public async Task<(WorkspaceDetalleDto? Workspace, string? Error)> ObtenerWorkspaceAsync(long id, CancellationToken ct = default)
    {
        var ws = await _handler.ObtenerWorkspaceAsync(id, ct);
        if (ws == null) return (null, "ANA_001:Workspace no encontrado");
        return (ws, null);
    }

    public async Task<(WorkspaceDetalleDto? Workspace, string? Error)> CrearWorkspaceAsync(long? licitacionId, string nombre, string userId, CancellationToken ct = default)
    {
        var (id, error) = await _handler.CrearWorkspaceAsync(licitacionId, nombre, userId, ct);
        if (error != null) return (null, error);
        var ws = await _handler.ObtenerWorkspaceAsync(id, ct);
        return (ws, null);
    }

    public async Task<(bool Success, string? Error)> EliminarWorkspaceAsync(long id, CancellationToken ct = default)
    {
        var error = await _handler.EliminarWorkspaceAsync(id, ct);
        if (error != null) return (false, error);
        return (true, null);
    }

    public async Task<(DocumentoDetalleDto? Documento, string? Error)> SubirDocumentoAsync(
        long workspaceId, string nombreArchivo, string mimeType, long tamanioBytes, Stream fileStream, CancellationToken ct = default)
    {
        var storagePath = $"analisis/{workspaceId}";
        var fileName = $"{Guid.NewGuid()}_{nombreArchivo}";
        var rutaStorage = await _storageService.UploadAsync(storagePath, fileName, fileStream, mimeType, ct);

        var (id, error) = await _handler.CrearDocumentoAsync(workspaceId, nombreArchivo, mimeType, tamanioBytes, rutaStorage, ct);
        if (error != null) return (null, error);

        var doc = await _handler.ObtenerDocumentoAsync(id, ct);
        return (doc, null);
    }

    public async Task<(IEnumerable<DocumentoItemDto> Documentos, string? Error)> ListarDocumentosAsync(long workspaceId, CancellationToken ct = default)
    {
        var docs = await _handler.ListarDocumentosAsync(workspaceId, ct);
        return (docs, null);
    }

    public async Task<(bool Success, string? Error)> EliminarDocumentoAsync(long id, long workspaceId, CancellationToken ct = default)
    {
        var doc = await _handler.ObtenerDocumentoAsync(id, ct);
        if (doc == null || doc.WorkspaceId != workspaceId)
            return (false, "ANA_006:Documento no encontrado");

        var error = await _handler.EliminarDocumentoAsync(id, workspaceId, ct);
        if (error != null) return (false, error);

        try
        {
            await _storageService.DeleteAsync(doc.RutaStorage, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeleteAsync fallback ws {WorkspaceId} doc {DocId}", workspaceId, id);
        }

        return (true, null);
    }

    public async Task<(AnalisisResumenDto? Resultado, string? Error)> AnalizarAsync(
        long workspaceId, long? documentoId, CancellationToken ct = default)
    {
        var ws = await _handler.ObtenerWorkspaceAsync(workspaceId, ct);
        if (ws == null) return (null, "ANA_001:Workspace no encontrado");

        if (ws.Estado == "analizando")
            return (null, "ANA_002:El workspace ya tiene un análisis en progreso");

        List<DocumentoDetalleDto> documentos;
        if (documentoId.HasValue)
        {
            var doc = await _handler.ObtenerDocumentoAsync(documentoId.Value, ct);
            if (doc == null) return (null, "ANA_001:Documento no encontrado");
            documentos = [doc];
        }
        else
        {
            // 029-fix-hallazgos-code-review-competidores-alertas (FR-011/QA BUG-005): "Analizar
            // todo" antes solo tomaba docList.First() -- un único documento, sin advertir que
            // el resto del workspace quedaba sin analizar. Ahora se envían todos los documentos
            // del workspace a Gemini en una sola llamada (ver GeminiService.AnalyzeDocumentosAsync).
            var docs = await _handler.ListarDocumentosAsync(workspaceId, ct);
            var docList = docs.ToList();
            if (docList.Count == 0) return (null, "ANA_003:No hay documentos para analizar en este workspace");

            documentos = [];
            foreach (var item in docList)
            {
                var detalle = await _handler.ObtenerDocumentoAsync(item.Id, ct);
                if (detalle != null) documentos.Add(detalle);
            }
            if (documentos.Count == 0) return (null, "ANA_003:No hay documentos para analizar en este workspace");
        }

        var estadoError = await _handler.ActualizarEstadoAsync(workspaceId, "analizando", ct);
        if (estadoError != null) return (null, estadoError);

        // El resultado se asocia (FK documento_id) al documento más reciente del conjunto como
        // representante -- el contenido analizado por Gemini sí incluye TODOS los documentos
        // (ver AnalisisBackgroundService), esto solo decide qué fila referenciar para el join
        // existente de usp_AnalisisResultados_ObtenerPorLicitacion.
        var documentoRepresentativo = documentos.OrderByDescending(d => d.CreatedAt).First();

        _backgroundService.EnqueueAnalisis(
            workspaceId,
            documentoRepresentativo.Id,
            documentos.Select(d => (d.Id, d.NombreArchivo, d.RutaStorage)).ToList());

        return (new AnalisisResumenDto
        {
            Id = 0,
            Estado = "analizando",
            // 033-migracion-qwen-g4: el modelo del proveedor activo (ya no una constante
            // Gemini); el análisis encolado persiste el modelo real al ejecutarse.
            ModeloUsado = await _geminiService.GetModelNameAsync(ct),
            CreatedAt = DateTime.UtcNow
        }, null);
    }

    public async Task<(ResultadoDto? Resultado, string? Error)> ObtenerDashboardAsync(long workspaceId, CancellationToken ct = default)
    {
        var ws = await _handler.ObtenerWorkspaceAsync(workspaceId, ct);
        if (ws == null) return (null, "ANA_001:Workspace no encontrado");

        var resultado = await _handler.ObtenerResultadoPorWorkspaceAsync(workspaceId, ct);
        if (resultado == null) return (null, "ANA_004:No hay análisis completado para este workspace");

        return (resultado, null);
    }

    public async Task<(ChatResponseDto? Response, string? Error)> ChatAsync(
        long workspaceId, string mensaje, CancellationToken ct = default)
    {
        var resultado = await _handler.ObtenerResultadoPorWorkspaceAsync(workspaceId, ct);
        if (resultado == null) return (null, "ANA_004:No hay análisis completado para este workspace");

        var (convId, convError) = await _handler.ObtenerOCrearChatConversacionAsync(workspaceId, ct);
        if (convError != null) return (null, convError);

        var (_, msgError) = await _handler.CrearMensajeChatAsync(convId, "user", mensaje, ct);
        if (msgError != null) return (null, msgError);

        var historial = await _handler.ObtenerHistorialChatAsync(convId, 50, ct);

        var chatHistory = historial
            .Where(h => h.Id > 0) 
            .Select(h => new ChatHistoryItem { Rol = h.Rol, Contenido = h.Contenido })
            .ToList();

        var chatResponse = await _geminiService.ChatAsync(mensaje, resultado.ContenidoJson ?? "{}", chatHistory, ct);

        var (_, respError) = await _handler.CrearMensajeChatAsync(convId, "assistant", chatResponse.Text, ct);
        if (respError != null) return (null, respError);

        var mensajesActualizados = await _handler.ObtenerHistorialChatAsync(convId, 50, ct);

        return (new ChatResponseDto
        {
            Respuesta = chatResponse.Text,
            ConversacionId = convId,
            Mensajes = mensajesActualizados.ToList()
        }, null);
    }

    public async Task<(ChatHistorialDto? Historial, string? Error)> ObtenerChatHistorialAsync(long workspaceId, CancellationToken ct = default)
    {
        var (convId, convError) = await _handler.ObtenerOCrearChatConversacionAsync(workspaceId, ct);
        if (convError != null) return (null, convError);

        var mensajes = await _handler.ObtenerHistorialChatAsync(convId, 50, ct);

        return (new ChatHistorialDto
        {
            ConversacionId = convId,
            Mensajes = mensajes.ToList()
        }, null);
    }

    public async Task<(DashboardEjecutivoDto? Dashboard, string? Error)> GetDashboardEjecutivoAsync(int? anio, CancellationToken ct = default)
    {
        var resultados = (await _handler.ObtenerResultadosCompletosAsync(anio, ct)).ToList();

        if (resultados.Count == 0)
        {
            ComparacionAnualDto? comparacionVacia = null;
            if (anio.HasValue)
            {
                var anioAnteriorVacio = anio.Value - 1;
                var (montoAnteriorVacio, tieneDatosVacio) = await CalcularTotalesGanadasAsync(anioAnteriorVacio, ct);
                double? variacionVacia = null;
                if (tieneDatosVacio && montoAnteriorVacio != 0)
                {
                    variacionVacia = Math.Round((double)((0m - montoAnteriorVacio) / montoAnteriorVacio * 100m), 1);
                }
                _logger.LogDebug(new EventId(1300, "YoYCalc"), "YoY {AnioActual} vs {AnioAnterior} var {Var}% monto {MontoActual}/{MontoAnterior}", anio.Value, anioAnteriorVacio, variacionVacia, 0m, montoAnteriorVacio);

                comparacionVacia = new ComparacionAnualDto
                {
                    AnioActual = anio.Value,
                    AnioAnterior = anioAnteriorVacio,
                    MontoActual = 0m,
                    MontoAnterior = montoAnteriorVacio,
                    VariacionPorcentaje = variacionVacia,
                    TieneDatosAnioAnterior = tieneDatosVacio
                };
            }

            // Track2 ligero — CM cache (mserv) hook: si hay año filtrado intenta sumar Convenio Marco
            decimal montoCmVacio = 0m;
            try
            {
                if (_serviceProvider != null && anio.HasValue)
                {
                    var cmHandler = _serviceProvider.GetService<ICmResumenHandler>();
                    if (cmHandler != null)
                        montoCmVacio = await cmHandler.ObtenerMontoAnualAsync("76.130.712-6", anio.Value, ct);
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "CM cache fallback anio {Anio}", anio); }

            return (new DashboardEjecutivoDto
            {
                AniosDisponibles = [],
                ComparacionAnual = comparacionVacia,
                MontoConvenioMarco = montoCmVacio,
                MontoTotalGanadoConCm = montoCmVacio
            }, null);
        }

        var licitaciones = new List<LicitacionResumenEjecutivoDto>();
        var competidores = new Dictionary<string, CompetidorRankingDto>(StringComparer.OrdinalIgnoreCase);
        var todosLosAnios = new HashSet<int>();
        var debilidades = new List<string>();
        var licitacionesVistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in resultados)
        {
            if (string.IsNullOrWhiteSpace(r.ContenidoJson))
            {
                // Sin contenido que parsear -- no hay fecha real disponible, se usa CreadoEn
                // como único dato que existe para esta fila (mejor que omitirla del todo).
                todosLosAnios.Add(r.CreadoEn.Year);
                continue;
            }

            JsonElement root;
            try { root = JsonDocument.Parse(r.ContenidoJson).RootElement; }
            catch (Exception ex) { _logger.LogDebug(ex, "Parse JSON fallback anioReal ws {WorkspaceId}", r.WorkspaceId); todosLosAnios.Add(r.CreadoEn.Year); continue; }
            if (root.ValueKind != JsonValueKind.Object) { todosLosAnios.Add(r.CreadoEn.Year); continue; }

            // 029-fix-hallazgos-code-review-competidores-alertas (FR-018/US14, QA BUG-011): antes
            // se usaba r.CreadoEn.Year (fecha de creación del registro de análisis) para el filtro
            // de año -- una licitación de 2025 analizada recién en 2026 nunca aparecía bajo "2025".
            // Se usa la fecha real de la licitación (adjudicación si existe, si no publicación) ya
            // extraída por Gemini en el propio contenido_json, con CreadoEn solo como último
            // fallback si el JSON no trae ninguna fecha real utilizable.
            var anioReal = ExtraerAnioRealLicitacion(root) ?? r.CreadoEn.Year;
            todosLosAnios.Add(anioReal);

            if (anio.HasValue && anioReal != anio.Value)
                continue;

            // Deduplicación por licitación (o por workspace si no está asociada a una licitación)
            var deduplicacionKey = r.LicitacionId.HasValue && r.LicitacionId.Value > 0
                ? $"lic_{r.LicitacionId.Value}"
                : $"ws_{r.WorkspaceId}";

            if (!licitacionesVistas.Add(deduplicacionKey))
                continue;

            var tivitGano = false;
            string resultadoTivit = "Desconocido";
            double? puntajeTivit = null, puntajeGanador = null, puntajeMaximo = null;
            decimal? montoTivit = null;

            if (root.TryGetProperty("analisis_tivit", out var at) && at.ValueKind == JsonValueKind.Object)
            {
                tivitGano = at.TryGetProperty("es_ganador", out var eg) && eg.ValueKind == JsonValueKind.True;
                resultadoTivit = at.TryGetProperty("resultado", out var rt) ? rt.GetString() ?? "Desconocido" : "Desconocido";
                puntajeTivit = at.TryGetProperty("puntaje_obtenido", out var pt) && pt.ValueKind == JsonValueKind.Number ? pt.GetDouble() : null;
                puntajeMaximo = at.TryGetProperty("puntaje_maximo_posible", out var pm) && pm.ValueKind == JsonValueKind.Number ? pm.GetDouble() : null;
                montoTivit = at.TryGetProperty("monto_ofertado", out var mo) && mo.ValueKind == JsonValueKind.Number ? (decimal?)mo.GetDecimal() : null;

                if (at.TryGetProperty("debilidades", out var deb) && deb.ValueKind == JsonValueKind.Array)
                    foreach (var d in deb.EnumerateArray())
                        if (d.ValueKind == JsonValueKind.String && d.GetString() is string s && !string.IsNullOrWhiteSpace(s))
                            debilidades.Add(s);
            }

            string? adjudicatarioNombre = null, adjudicatarioRut = null;
            decimal? montoAdj = null;
            var competidoresNombres = new List<string>();

            if (root.TryGetProperty("adjudicacion", out var adj) && adj.ValueKind == JsonValueKind.Object)
            {
                // "adjudicatario" puede existir en el JSON con valor null (licitacion sin un
                // unico adjudicatario claro) en vez de estar ausente -- TryGetProperty sobre
                // ese elemento Null revienta con "requires an element of type Object" si no se
                // valida el ValueKind primero.
                if (adj.TryGetProperty("adjudicatario", out var adjt) && adjt.ValueKind == JsonValueKind.Object)
                {
                    adjudicatarioNombre = adjt.TryGetProperty("nombre", out var an) ? an.GetString() : null;
                    adjudicatarioRut = adjt.TryGetProperty("rut", out var ar) ? ar.GetString() : null;
                    montoAdj = adjt.TryGetProperty("monto_adjudicado", out var ma) && ma.ValueKind == JsonValueKind.Number ? (decimal?)ma.GetDecimal() : null;
                }

                if (adj.TryGetProperty("ofertantes", out var ofs) && ofs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var of in ofs.EnumerateArray())
                    {
                        if (of.ValueKind != JsonValueKind.Object) continue;

                        var nombre = of.TryGetProperty("nombre", out var on) ? on.GetString() ?? "" : "";
                        var rut = of.TryGetProperty("rut", out var or) ? or.GetString() ?? "" : "";
                        var resultado = of.TryGetProperty("resultado", out var ores) ? ores.GetString() ?? "" : "";
                        var puntaje = of.TryGetProperty("puntaje_total", out var op) && op.ValueKind == JsonValueKind.Number ? op.GetDouble() : 0;
                        var monto = of.TryGetProperty("monto_ofertado", out var om) && om.ValueKind == JsonValueKind.Number ? om.GetDecimal() : 0;

                        if (string.IsNullOrWhiteSpace(nombre)) continue;

                        var ganoOfertante = EsResultadoGanador(resultado, nombre, rut, adjudicatarioNombre, adjudicatarioRut);

                        // track puntaje ganador
                        if (ganoOfertante)
                            puntajeGanador = puntaje;

                        // skip TIVIT from competitor ranking
                        if (nombre.Contains("Tivit", StringComparison.OrdinalIgnoreCase)) continue;

                        competidoresNombres.Add(nombre);

                        var key = string.IsNullOrWhiteSpace(rut) ? nombre : rut;
                        if (!competidores.TryGetValue(key, out var comp))
                        {
                            comp = new CompetidorRankingDto { Nombre = nombre, Rut = rut, Licitaciones = [] };
                            competidores[key] = comp;
                        }
                        comp.VecesCompetidor++;
                        if (ganoOfertante)
                        {
                            comp.VecesGanador++;
                            comp.MontoTotalAdjudicado += (monto > 0 ? monto : (montoAdj ?? 0));
                        }
                        comp.Licitaciones.Add(new LicitacionResumenEjecutivoDto
                        {
                            WorkspaceId = r.WorkspaceId,
                            Nombre = r.WorkspaceNombre,
                            TivitGano = tivitGano,
                            ResultadoTivit = resultadoTivit,
                            MontoAdjudicado = montoAdj,
                            MontoTivit = montoTivit,
                            Adjudicatario = adjudicatarioNombre,
                            AdjudicatarioRut = adjudicatarioRut,
                            PuntajeTivit = puntajeTivit,
                            PuntajeGanador = puntajeGanador,
                            PuntajeMaximo = puntajeMaximo,
                            FechaAnalisis = r.CreadoEn,
                            CompetidoresNombres = competidoresNombres,
                            CompetidorGano = ganoOfertante,
                            ResultadoCompetidor = string.IsNullOrWhiteSpace(resultado) ? (ganoOfertante ? "Adjudicado" : "No adjudicado") : resultado,
                            MontoCompetidor = monto > 0 ? monto : montoAdj
                        });
                    }
                }
            }

            licitaciones.Add(new LicitacionResumenEjecutivoDto
            {
                WorkspaceId = r.WorkspaceId,
                Nombre = r.WorkspaceNombre,
                TivitGano = tivitGano,
                ResultadoTivit = resultadoTivit,
                MontoAdjudicado = montoAdj,
                MontoTivit = montoTivit,
                Adjudicatario = adjudicatarioNombre,
                AdjudicatarioRut = adjudicatarioRut,
                PuntajeTivit = puntajeTivit,
                PuntajeGanador = puntajeGanador,
                PuntajeMaximo = puntajeMaximo,
                FechaAnalisis = r.CreadoEn,
                CompetidoresNombres = competidoresNombres
            });
        }

        var ganadas = licitaciones.Where(l => l.TivitGano).ToList();
        var perdidas = licitaciones.Where(l => !l.TivitGano).ToList();

        var puntajePromTivit = licitaciones.Where(l => l.PuntajeTivit.HasValue).Select(l => l.PuntajeTivit!.Value).DefaultIfEmpty(0).Average();
        var puntajePromGanador = licitaciones.Where(l => l.PuntajeGanador.HasValue).Select(l => l.PuntajeGanador!.Value).DefaultIfEmpty(0).Average();

        var rankingCompetidores = competidores.Values
            .OrderByDescending(c => c.VecesGanador)
            .ThenByDescending(c => c.MontoTotalAdjudicado)
            .ToList();

        // Top factores de pérdida: frecuencia de palabras clave en debilidades
        var factoresFrecuentes = debilidades
            .GroupBy(d => d)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var montoTotalGanado = ganadas.Sum(l => l.MontoAdjudicado ?? 0);
        var montoTotalPerdido = perdidas.Sum(l => l.MontoAdjudicado ?? 0);

        // Track2 ligero — CM Convenio Marco desde cache mserv (idModalidad=5)
        decimal montoConvenioMarco = 0m;
        try
        {
            if (_serviceProvider != null && anio.HasValue)
            {
                var cmHandler = _serviceProvider.GetService<ICmResumenHandler>();
                if (cmHandler != null)
                    montoConvenioMarco = await cmHandler.ObtenerMontoAnualAsync("76.130.712-6", anio.Value, ct);
            }
        }
        catch (Exception ex) { _logger.LogDebug(ex, "CM cache fallback anio {Anio}", anio); }

        var montoTotalGanadoConCm = montoTotalGanado + montoConvenioMarco;

        ComparacionAnualDto? comparacionAnual = null;
        if (anio.HasValue)
        {
            var anioActual = anio.Value;
            var anioAnterior = anioActual - 1;
            var (montoAnterior, tieneDatosAnterior) = await CalcularTotalesGanadasAsync(anioAnterior, ct);
            double? variacionPorcentaje = null;
            if (tieneDatosAnterior && montoAnterior != 0)
            {
                variacionPorcentaje = Math.Round((double)((montoTotalGanado - montoAnterior) / montoAnterior * 100m), 1);
            }
            _logger.LogDebug(new EventId(1300, "YoYCalc"), "YoY {AnioActual} vs {AnioAnterior} var {Var}% monto {MontoActual}/{MontoAnterior}", anioActual, anioAnterior, variacionPorcentaje, montoTotalGanado, montoAnterior);

            comparacionAnual = new ComparacionAnualDto
            {
                AnioActual = anioActual,
                AnioAnterior = anioAnterior,
                MontoActual = montoTotalGanado,
                MontoAnterior = montoAnterior,
                VariacionPorcentaje = variacionPorcentaje,
                TieneDatosAnioAnterior = tieneDatosAnterior
            };
        }

        return (new DashboardEjecutivoDto
        {
            TotalAnalizadas = licitaciones.Count,
            TotalGanadas = ganadas.Count,
            TotalPerdidas = perdidas.Count,
            MontoTotalGanado = montoTotalGanado,
            MontoTotalPerdido = montoTotalPerdido,
            PuntajePromedioTivit = puntajePromTivit > 0 ? puntajePromTivit : null,
            PuntajePromedioGanador = puntajePromGanador > 0 ? puntajePromGanador : null,
            RankingCompetidores = rankingCompetidores,
            FactoresPerdidaFrecuentes = factoresFrecuentes,
            Licitaciones = licitaciones.OrderByDescending(l => l.FechaAnalisis).ToList(),
            AniosDisponibles = todosLosAnios.OrderDescending().ToList(),
            ComparacionAnual = comparacionAnual,
            MontoConvenioMarco = montoConvenioMarco,
            MontoTotalGanadoConCm = montoTotalGanadoConCm
        }, null);
    }

    // 029-fix-hallazgos-code-review-competidores-alertas (FR-018/US14): año real de la licitación
    // desde licitacion.fechas.adjudicacion (preferida) o licitacion.fechas.publicacion, tal como
    // Gemini las devuelve en el schema de GeminiService.GetAnalisisPrompt. Null si el JSON no trae
    // ninguna fecha real parseable.
    private static int? ExtraerAnioRealLicitacion(JsonElement root)
    {
        if (!root.TryGetProperty("licitacion", out var lic) || lic.ValueKind != JsonValueKind.Object)
            return null;
        if (!lic.TryGetProperty("fechas", out var fechas) || fechas.ValueKind != JsonValueKind.Object)
            return null;

        if (fechas.TryGetProperty("adjudicacion", out var fa) && fa.ValueKind == JsonValueKind.String
            && DateTime.TryParse(fa.GetString(), out var fechaAdj))
            return fechaAdj.Year;

        if (fechas.TryGetProperty("publicacion", out var fp) && fp.ValueKind == JsonValueKind.String
            && DateTime.TryParse(fp.GetString(), out var fechaPub))
            return fechaPub.Year;

        return null;
    }

    private async Task<(decimal montoTotalGanado, bool tieneDatos)> CalcularTotalesGanadasAsync(int anioObjetivo, CancellationToken ct)
    {
        var resultadosAnio = (await _handler.ObtenerResultadosCompletosAsync(anioObjetivo, ct)).ToList();
        if (resultadosAnio.Count == 0)
            return (0m, false);

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal montoGanado = 0m;
        int totalDistintas = 0;

        foreach (var r in resultadosAnio)
        {
            if (string.IsNullOrWhiteSpace(r.ContenidoJson))
                continue;

            JsonElement root;
            try { root = JsonDocument.Parse(r.ContenidoJson).RootElement; }
            catch (Exception ex) { _logger.LogDebug(ex, "Parse JSON fallback anioReal ws {WorkspaceId}", r.WorkspaceId); continue; }
            if (root.ValueKind != JsonValueKind.Object) continue;

            var anioReal = ExtraerAnioRealLicitacion(root) ?? r.CreadoEn.Year;
            if (anioReal != anioObjetivo)
                continue;

            var key = r.LicitacionId.HasValue && r.LicitacionId.Value > 0
                ? $"lic_{r.LicitacionId.Value}"
                : $"ws_{r.WorkspaceId}";
            if (!vistos.Add(key))
                continue;

            totalDistintas++;

            bool tivitGano = false;
            decimal? montoAdj = null;

            if (root.TryGetProperty("analisis_tivit", out var at) && at.ValueKind == JsonValueKind.Object)
            {
                tivitGano = at.TryGetProperty("es_ganador", out var eg) && eg.ValueKind == JsonValueKind.True;
            }

            if (root.TryGetProperty("adjudicacion", out var adj) && adj.ValueKind == JsonValueKind.Object)
            {
                if (adj.TryGetProperty("adjudicatario", out var adjt) && adjt.ValueKind == JsonValueKind.Object)
                {
                    montoAdj = adjt.TryGetProperty("monto_adjudicado", out var ma) && ma.ValueKind == JsonValueKind.Number
                        ? (decimal?)ma.GetDecimal()
                        : null;
                }
            }

            if (tivitGano)
                montoGanado += montoAdj ?? 0m;
        }

        return (montoGanado, totalDistintas > 0);
    }

    private static bool EsResultadoGanador(string? resultado, string nombre, string? rut, string? adjNombre, string? adjRut)
    {
        if (!string.IsNullOrWhiteSpace(resultado))
        {
            var r = resultado.Trim();
            if (r.StartsWith("no ", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("no adjudicad", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("inadmisible", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("rechazad", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("descartad", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("desiert", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (r.Contains("adjudicado", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("adjudicada", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("ganador", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(adjRut) && !string.IsNullOrWhiteSpace(rut) &&
            string.Equals(adjRut.Trim(), rut.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(adjNombre) && !string.IsNullOrWhiteSpace(nombre) &&
            (adjNombre.Contains(nombre, StringComparison.OrdinalIgnoreCase) || nombre.Contains(adjNombre, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
