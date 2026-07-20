using FluentAssertions;
using Npgsql;
using Xunit;

namespace MPM.Modules.Licitaciones.Tests.Data;

/// <summary>Cubre 029-fix-hallazgos-code-review-competidores-alertas FR-001/US1 (y el hallazgo
/// de code review original): antes de V109, un sync con <c>codigo_estado</c> no reconocido por
/// <c>estados_licitacion</c> reseteaba el estado de una licitación ya válida al código legado 1
/// (<c>COALESCE(..., 1::SMALLINT)</c> en el UPDATE de <c>usp_SyncEngine_MergeLicitaciones</c>).
/// Corre contra el Postgres real de docker-compose (localhost:5433) -- requiere V109 aplicada,
/// mismo patrón que <see cref="LicitacionSearchTests"/>.</summary>
public class SyncEngineMergeLicitacionesTests
{
    private const string TestConnectionString =
        "Host=localhost;Port=5433;Database=mpm;Username=mpm;Password=mpm_password";

    [Fact]
    public async Task MergeLicitaciones_CodigoEstadoNoReconocido_PreservaElEstadoExistente()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        var codigoExterno = $"TEST-029-US1-{Guid.NewGuid():N}";
        try
        {
            // Estado inicial válido: 8 = Adjudicada (ver V086/V108).
            await using (var insert = new NpgsqlCommand(
                """
                INSERT INTO licitaciones (codigo_externo, nombre, descripcion, codigo_estado, tipo,
                                           organismo, unidad_tecnica, moneda, monto_estimado,
                                           fecha_publicacion, fecha_cierre, link, raw_data)
                VALUES (@codigo, 'Licitación de prueba US1', '', 8, 'LE', '', '', 'CLP', NULL,
                        NOW(), NOW(), 'https://example.test', '{}'::JSONB)
                """, conn))
            {
                insert.Parameters.AddWithValue("codigo", codigoExterno);
                await insert.ExecuteNonQueryAsync();
            }

            // Sync entrante con un codigo_estado que NO existe en estados_licitacion (99).
            var payload = $$"""
                [{"codigo_externo":"{{codigoExterno}}","nombre":"Licitación de prueba US1","descripcion":"",
                  "codigo_estado":99,"tipo":"LE","organismo":"","unidad_tecnica":"","moneda":"CLP",
                  "monto_estimado":null,"fecha_publicacion":"2025-05-20","fecha_cierre":"2025-04-30",
                  "link":"https://example.test","raw_data":{},"items":[]}]
                """;

            string? errorMsg;
            await using (var call = new NpgsqlCommand(
                "CALL usp_SyncEngine_MergeLicitaciones(@p_datos, @p_creados, @p_actualizados, @p_error_msg)", conn))
            {
                call.Parameters.AddWithValue("p_datos", payload);
                call.Parameters.Add(new NpgsqlParameter("p_creados", NpgsqlTypes.NpgsqlDbType.Integer) { Direction = System.Data.ParameterDirection.InputOutput, Value = 0 });
                call.Parameters.Add(new NpgsqlParameter("p_actualizados", NpgsqlTypes.NpgsqlDbType.Integer) { Direction = System.Data.ParameterDirection.InputOutput, Value = 0 });
                call.Parameters.Add(new NpgsqlParameter("p_error_msg", NpgsqlTypes.NpgsqlDbType.Text) { Direction = System.Data.ParameterDirection.InputOutput, Value = DBNull.Value });
                await call.ExecuteNonQueryAsync();
                errorMsg = call.Parameters["p_error_msg"].Value as string;
            }

            var codigoEstadoFinal = (short)(await new NpgsqlCommand(
                "SELECT codigo_estado FROM licitaciones WHERE codigo_externo = @codigo", conn)
            {
                Parameters = { new NpgsqlParameter("codigo", codigoExterno) }
            }.ExecuteScalarAsync())!;

            codigoEstadoFinal.Should().Be((short)8,
                "un codigo_estado entrante no reconocido no debe pisar un estado ya válido");
            errorMsg.Should().Contain(codigoExterno).And.Contain("no reconocido",
                "el intento debe quedar auditado en p_error_msg (SyncEngineHandler.cs lo loguea como warning)");
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("DELETE FROM licitaciones WHERE codigo_externo = @codigo", conn);
            cleanup.Parameters.AddWithValue("codigo", codigoExterno);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task MergeLicitaciones_CodigoEstadoReconocido_ActualizaNormalmente()
    {
        await using var conn = new NpgsqlConnection(TestConnectionString);
        await conn.OpenAsync();

        var codigoExterno = $"TEST-029-US1-{Guid.NewGuid():N}";
        try
        {
            await using (var insert = new NpgsqlCommand(
                """
                INSERT INTO licitaciones (codigo_externo, nombre, descripcion, codigo_estado, tipo,
                                           organismo, unidad_tecnica, moneda, monto_estimado,
                                           fecha_publicacion, fecha_cierre, link, raw_data)
                VALUES (@codigo, 'Licitación de prueba US1', '', 5, 'LE', '', '', 'CLP', NULL,
                        NOW(), NOW(), 'https://example.test', '{}'::JSONB)
                """, conn))
            {
                insert.Parameters.AddWithValue("codigo", codigoExterno);
                await insert.ExecuteNonQueryAsync();
            }

            // codigo_estado = 8 (Adjudicada) SÍ existe en el catálogo -- debe aplicarse normalmente.
            var payload = $$"""
                [{"codigo_externo":"{{codigoExterno}}","nombre":"Licitación de prueba US1","descripcion":"",
                  "codigo_estado":8,"tipo":"LE","organismo":"","unidad_tecnica":"","moneda":"CLP",
                  "monto_estimado":null,"fecha_publicacion":"2025-05-20","fecha_cierre":"2025-04-30",
                  "link":"https://example.test","raw_data":{},"items":[]}]
                """;

            await using (var call = new NpgsqlCommand(
                "CALL usp_SyncEngine_MergeLicitaciones(@p_datos, @p_creados, @p_actualizados, @p_error_msg)", conn))
            {
                call.Parameters.AddWithValue("p_datos", payload);
                call.Parameters.Add(new NpgsqlParameter("p_creados", NpgsqlTypes.NpgsqlDbType.Integer) { Direction = System.Data.ParameterDirection.InputOutput, Value = 0 });
                call.Parameters.Add(new NpgsqlParameter("p_actualizados", NpgsqlTypes.NpgsqlDbType.Integer) { Direction = System.Data.ParameterDirection.InputOutput, Value = 0 });
                call.Parameters.Add(new NpgsqlParameter("p_error_msg", NpgsqlTypes.NpgsqlDbType.Text) { Direction = System.Data.ParameterDirection.InputOutput, Value = DBNull.Value });
                await call.ExecuteNonQueryAsync();
            }

            var codigoEstadoFinal = (short)(await new NpgsqlCommand(
                "SELECT codigo_estado FROM licitaciones WHERE codigo_externo = @codigo", conn)
            {
                Parameters = { new NpgsqlParameter("codigo", codigoExterno) }
            }.ExecuteScalarAsync())!;

            codigoEstadoFinal.Should().Be((short)8, "un codigo_estado válido sí debe aplicarse normalmente");
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("DELETE FROM licitaciones WHERE codigo_externo = @codigo", conn);
            cleanup.Parameters.AddWithValue("codigo", codigoExterno);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
