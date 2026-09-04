using FluentAssertions;
using MPM.Modules.Administracion.Services;
using Xunit;

namespace MPM.Modules.Administracion.Tests.Services;

public class AdminRoleRulesTests
{
    [Fact]
    public void EsRolValido_ReconoceLosCuatroRoles()
    {
        foreach (var rol in new[] { "SuperAdmin", "Admin", "Analista", "Usuario" })
            AdminRoleRules.EsRolValido(rol).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("Root")]
    [InlineData("SuperAdministrator")]
    public void EsRolValido_RechazaRolesInvalidos(string? rol)
    {
        AdminRoleRules.EsRolValido(rol).Should().BeFalse();
    }

    [Theory]
    [InlineData("SuperAdmin", "SuperAdmin", true)]
    [InlineData("SuperAdmin", "Admin", true)]
    [InlineData("SuperAdmin", "Analista", true)]
    [InlineData("SuperAdmin", "Usuario", true)]
    [InlineData("Admin", "Analista", true)]
    [InlineData("Admin", "Usuario", true)]
    [InlineData("Admin", "Admin", false)]
    [InlineData("Admin", "SuperAdmin", false)]
    [InlineData("Analista", "Usuario", false)]
    [InlineData("Usuario", "Analista", false)]
    [InlineData("SuperAdmin", "Root", false)]
    public void PuedeGestionarRol_RespetaLaJerarquia(string actorRol, string rolObjetivo, bool esperado)
    {
        AdminRoleRules.PuedeGestionarRol([actorRol], rolObjetivo).Should().Be(esperado);
    }

    [Fact]
    public void PuedeGestionarUsuario_SuperAdminGestionaTodo()
    {
        AdminRoleRules.PuedeGestionarUsuario(["SuperAdmin"], ["SuperAdmin"]).Should().BeTrue();
        AdminRoleRules.PuedeGestionarUsuario(["SuperAdmin"], ["Admin"]).Should().BeTrue();
        AdminRoleRules.PuedeGestionarUsuario(["SuperAdmin"], ["Analista", "Usuario"]).Should().BeTrue();
    }

    [Fact]
    public void PuedeGestionarUsuario_AdminNoTocaRolesPrivilegiados()
    {
        AdminRoleRules.PuedeGestionarUsuario(["Admin"], ["Analista"]).Should().BeTrue();
        AdminRoleRules.PuedeGestionarUsuario(["Admin"], ["Usuario"]).Should().BeTrue();
        AdminRoleRules.PuedeGestionarUsuario(["Admin"], ["Admin"]).Should().BeFalse();
        AdminRoleRules.PuedeGestionarUsuario(["Admin"], ["SuperAdmin"]).Should().BeFalse();
        AdminRoleRules.PuedeGestionarUsuario(["Admin"], ["Admin", "Analista"]).Should().BeFalse();
    }

    [Fact]
    public void PuedeGestionarUsuario_RolesNoAdministrativosNoPueden()
    {
        AdminRoleRules.PuedeGestionarUsuario(["Analista"], ["Usuario"]).Should().BeFalse();
        AdminRoleRules.PuedeGestionarUsuario(["Usuario"], ["Analista"]).Should().BeFalse();
    }

    [Fact]
    public void RolPorDefecto_AdminSoloGestionaAnalistaYUsuario()
    {
        AdminRoleRules.AdminManagedRoles.Should().Contain("Analista");
        AdminRoleRules.AdminManagedRoles.Should().Contain("Usuario");
        AdminRoleRules.AdminManagedRoles.Should().NotContain("Admin");
        AdminRoleRules.AdminManagedRoles.Should().NotContain("SuperAdmin");
    }
}
