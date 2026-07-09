using MPM.Modules.Mensajeria.Data;
using FluentAssertions;
using Xunit;

namespace MPM.Modules.Mensajeria.Tests.Data;

// QA BUG-014: crear una conversación nueva (directa o grupal) devolvía siempre 400 en
// producción. Causa real: 42883 "procedure usp_conversaciones_crear(...) does not exist" —
// un asunto/licitacionId NULL sin dbType explícito viaja como parámetro "unknown", y
// participanteIds viaja como texto plano en vez de jsonb; Postgres no logra resolver ninguna
// sobrecarga del procedure con CALL (más estricto que una función con casts implícitos).
// Guarda de regresión source-level (sin conexión Postgres real en este test project).
public class ConversacionHandlerCrearAsyncTests
{
    [Fact]
    public void CrearConversacion_CasteaParticipanteIdsAJsonb()
    {
        MensajeriaStoredProcedures.CrearConversacion.Should().Contain("@p_participante_ids::jsonb",
            "sin el cast explícito, Npgsql manda el JSON como texto/unknown y Postgres no encuentra la sobrecarga del procedure (QA BUG-014)");
    }

    [Fact]
    public void CrearAsync_EspecificaDbTypeParaAsuntoYLicitacionId()
    {
        var source = File.ReadAllText(FindSourceFile("ConversacionHandler.cs")).Replace("\r\n", "\n");

        var inicioMetodo = source.IndexOf("public async Task<(long Id, string? Error)> CrearAsync(", StringComparison.Ordinal);
        inicioMetodo.Should().BeGreaterThanOrEqualTo(0, "CrearAsync debe existir en ConversacionHandler");
        var cuerpoMetodo = source[inicioMetodo..(inicioMetodo + 1400)];

        cuerpoMetodo.Should().Contain("\"p_asunto\", asunto, dbType: DbType.String",
            "un p_asunto NULL sin dbType explícito viaja como parámetro 'unknown' y rompe la resolución de la sobrecarga del procedure (QA BUG-014)");
        cuerpoMetodo.Should().Contain("\"p_licitacion_id\", licitacionId, dbType: DbType.Int64",
            "mismo problema para p_licitacion_id cuando es NULL (conversación sin licitación vinculada)");
    }

    private static string FindSourceFile(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MPM.sln")))
            dir = dir.Parent;

        if (dir == null) throw new FileNotFoundException("No se encontró MPM.sln subiendo desde el directorio de test.");

        return Directory.GetFiles(dir.FullName, fileName, SearchOption.AllDirectories)
            .Single(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
    }
}
