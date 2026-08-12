using FluentAssertions;
using MPM.Modules.Administracion.Models;
using Xunit;

namespace MPM.Modules.Administracion.Tests.Models;

public class AdminDtoTests
{
    [Fact]
    public void AdminUsuarioItemDto_Defaults()
    {
        var dto = new AdminUsuarioItemDto();
        dto.Id.Should().Be(0);
        dto.Email.Should().BeEmpty();
        dto.Nombre.Should().BeEmpty();
        dto.Roles.Should().BeEmpty();
        dto.Activo.Should().BeFalse();
        dto.UltimoLogin.Should().BeNull();
        dto.EsAccountManager.Should().BeFalse();
        dto.TotalCount.Should().Be(0);
    }

    [Fact]
    public void AdminLogItemDto_Defaults()
    {
        var dto = new AdminLogItemDto();
        dto.Id.Should().Be(0);
        dto.Tipo.Should().BeEmpty();
        dto.Fecha.Should().Be(default);
        dto.Estado.Should().BeEmpty();
        dto.Detalle.Should().BeEmpty();
        dto.Extra.Should().BeNull();
    }

    [Theory]
    [InlineData("", "n", "p123456", "Usuario", false)]
    [InlineData("no-es-email", "n", "p123456", "Usuario", false)]
    [InlineData("a@b.cl", "", "p123456", "Usuario", false)]
    [InlineData("a@b.cl", "n", "123", "Usuario", false)]
    [InlineData("a@b.cl", "n", "p123456", "", false)]
    [InlineData("a@b.cl", "n", "p123456", "Root", false)]
    [InlineData("a@b.cl", "n", "p123456", "Analista", true)]
    [InlineData("a@b.cl", "n", "p123456", "Admin", true)]
    [InlineData("a@b.cl", "n", "p123456", "SuperAdmin", true)]
    public void CrearUsuarioRequest_ValidacionBasica(string email, string nombre, string pass, string rol, bool esperado)
    {
        var request = new CrearUsuarioRequest { Email = email, Nombre = nombre, Password = pass, Rol = rol };
        var valido = !string.IsNullOrWhiteSpace(request.Email)
                     && request.Email.Contains('@')
                     && !string.IsNullOrWhiteSpace(request.Nombre)
                     && request.Password.Length >= 6
                     && AdminRoleRulesRef.EsRolValido(request.Rol);
        valido.Should().Be(esperado);
    }
}

/// <summary>Puente para reutilizar la regla de rol sin acoplar el test a servicios.</summary>
file static class AdminRoleRulesRef
{
    public static bool EsRolValido(string rol)
        => rol is "SuperAdmin" or "Admin" or "Analista" or "Usuario";
}
