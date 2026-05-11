using Playgo.Domain.Common;
using Playgo.Domain.Enums;

namespace Playgo.Domain.Entities;

public class UserPreferences : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public PreferredLanguage Language { get; set; } = PreferredLanguage.En;
    public PreferredQuality Quality { get; set; } = PreferredQuality.Auto;

    public bool Autoplay { get; set; } = true;
    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = false;
}
