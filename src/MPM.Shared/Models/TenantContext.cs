namespace MPM.Shared.Models
{
    public class TenantContext
    {
        public string TenantId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string[] Roles { get; set; } = [];
        public string TenantName { get; set; } = string.Empty;
    }
}
