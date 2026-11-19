using TheSplashLab.Core.Enums;

namespace TheSplashLab.Core.Entities;

public class DemoUser
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
