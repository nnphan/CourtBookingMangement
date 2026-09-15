using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("refresh_tokens", Schema = "auth")]
[Index("TokenHash", Name = "ux_refresh_tokens_hash", IsUnique = true)]
public partial class RefreshToken
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("token_hash")]
    public string TokenHash { get; set; } = null!;

    [Column("issued_at")]
    public DateTime IssuedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("replaced_by_token")]
    public Guid? ReplacedByToken { get; set; }

    [Column("device_info")]
    public string? DeviceInfo { get; set; }

    [InverseProperty("ReplacedByTokenNavigation")]
    public virtual ICollection<RefreshToken> InverseReplacedByTokenNavigation { get; set; } = new List<RefreshToken>();

    [ForeignKey("ReplacedByToken")]
    [InverseProperty("InverseReplacedByTokenNavigation")]
    public virtual RefreshToken? ReplacedByTokenNavigation { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("RefreshTokens")]
    public virtual User User { get; set; } = null!;
}
