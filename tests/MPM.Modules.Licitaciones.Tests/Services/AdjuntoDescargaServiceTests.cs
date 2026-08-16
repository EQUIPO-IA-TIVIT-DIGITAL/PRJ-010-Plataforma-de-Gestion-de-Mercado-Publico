using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPM.Core.Data;
using MPM.Modules.Licitaciones.Data;
using MPM.Modules.Licitaciones.Models;
using MPM.Modules.Licitaciones.Services;
using MPM.Shared.Services;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Services;

/// <summary>
/// 036-flujo-comercial-ofertas (Fase 1): contrato de AdjuntoDescargaService — derivación de
/// estado del conjunto, cálculo del conjuntoHash y ciclo de vida de la descarga bajo demanda.
/// No monta un proceso Node real; verifica el contrato del handler y las transiciones de estado.
/// </summary>
public class AdjuntoDescargaServiceTests
{
    private readonly Mock<AdjuntoDocumentosHandler> _handlerMock;
    private readonly Mock<IStorageService> _storageMock;
    private readonly Mock<ExtraccionLogHandler> _logMock;
    private readonly AdjuntoDescargaService _service;

    public AdjuntoDescargaServiceTests()
    {
        var dbFactory = new DbConnectionFactory("Host=localhost;Database=unused");
        _handlerMock = new Mock<AdjuntoDocumentosHandler>(dbFactory);
        _storageMock = new Mock<IStorageService>();
        _logMock = new Mock<ExtraccionLogHandler>(dbFactory);
        _service = new AdjuntoDescargaService(
            NullLogger<AdjuntoDescargaService>.Instance,
            new ConfigurationBuilder().Build(),
            _handlerMock.Object,
            _logMock.Object,
            _storageMock.Object);
    }

    private static AdjuntoDocumentosHandler.AdjuntoDocumentoFila Fila(
        string nombre, string? hash, string estado = "completado", int version = 1) => new()
    {
        Id = 1,
        LicitacionId = 10,
        Tipo = "anexo",
        NombreArchivo = nombre,
        RutaStorage = $"licitaciones/X/adjuntos/{nombre}",
        TamanioBytes = 1024,
        MimeType = "application/pdf",
        Sha256Hash = hash,
        Version = version,
        DescargaEstado = estado,
    };

    [Fact]
    public async Task ObtenerEstadoAsync_SinDocumentos_EstadoPendiente()
    {
        _handlerMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila>());

        var estado = await _service.ObtenerEstadoAsync(10);

        estado.EstadoConjunto.Should().Be("pendiente");
        estado.ConjuntoHash.Should().BeNull();
        estado.Documentos.Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerEstadoAsync_Descargando_EstadoDescargando()
    {
        _handlerMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila>
            {
                Fila("bases.pdf", "abc", estado: "descargando"),
            });

        var estado = await _service.ObtenerEstadoAsync(10);

        estado.EstadoConjunto.Should().Be("descargando");
    }

    [Fact]
    public async Task ObtenerEstadoAsync_ConError_DevuelveEstadoYMotivo()
    {
        var fila = Fila("bases.pdf", null, estado: "error");
        fila.DescargaError = "cupo de Ver Adjuntos agotado, reintente mas tarde";

        _handlerMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila> { fila });

        var estado = await _service.ObtenerEstadoAsync(10);

        estado.EstadoConjunto.Should().Be("error");
        estado.DescargaError.Should().Be("cupo de Ver Adjuntos agotado, reintente mas tarde");
    }

    [Fact]
    public async Task ObtenerEstadoAsync_Completado_CalculaConjuntoHash()
    {
        _handlerMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila>
            {
                Fila("z.pdf", "hash-b"),
                Fila("a.pdf", "hash-a"),
            });

        var estado = await _service.ObtenerEstadoAsync(10);

        estado.EstadoConjunto.Should().Be("completado");
        estado.ConjuntoHash.Should().NotBeNullOrWhiteSpace();
        estado.Documentos.Should().HaveCount(2);
        estado.Documentos.Should().Contain(d => d.Sha256Hash == "hash-a");
        estado.Documentos.Should().Contain(d => d.Sha256Hash == "hash-b");
    }

    [Fact]
    public async Task ObtenerEstadoAsync_HashParcial_ConjuntoHashNull()
    {
        _handlerMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila>
            {
                Fila("a.pdf", "hash-a"),
                Fila("b.pdf", null),
            });

        var estado = await _service.ObtenerEstadoAsync(10);

        estado.EstadoConjunto.Should().Be("completado");
        estado.ConjuntoHash.Should().BeNull("si algún documento no tiene hash, el cache del conjunto no es fiable");
    }

    [Fact]
    public async Task IniciarDescargaAsync_DescargaViva_NoRedisparaYDevuelveYaEnCurso()
    {
        _handlerMock.Setup(h => h.ExistenDescargasVivasAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _handlerMock.Setup(h => h.ListarAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdjuntoDocumentosHandler.AdjuntoDocumentoFila>
            {
                Fila("bases.pdf", null, estado: "descargando"),
            });

        var resultado = await _service.IniciarDescargaAsync(10, "729-134-LE26", "usuario@tivit.cl", forzar: false);

        resultado.Accion.Should().Be("ya_en_curso");
        resultado.EstadoConjunto.Should().Be("descargando");
        _handlerMock.Verify(h => h.MarcarDescargaIniciadaAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "si ya hay una extracción viva no se debe volver a disparar (evita gastar cupo y duplicar trabajo)");
    }

    [Fact]
    public async Task IniciarDescargaAsync_SinScriptDisponible_MarcaError()
    {
        _handlerMock.Setup(h => h.ExistenDescargasVivasAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var resultado = await _service.IniciarDescargaAsync(10, "729-134-LE26", "usuario@tivit.cl", forzar: false);

        resultado.EstadoConjunto.Should().Be("error");
        resultado.DescargaError.Should().NotBeNullOrWhiteSpace();
        _handlerMock.Verify(h => h.MarcarDescargaIniciadaAsync(10, "usuario@tivit.cl", It.IsAny<CancellationToken>()), Times.Once);
        _handlerMock.Verify(h => h.MarcarDescargaFinalizadaAsync(10, "error", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once,
            "si el script no está disponible, la licitación debe quedar en 'error' (no 'descargando' para siempre)");
    }
}
