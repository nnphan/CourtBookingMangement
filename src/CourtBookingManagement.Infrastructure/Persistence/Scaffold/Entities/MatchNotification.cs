using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Scaffold.Entities;

[Table("match_notifications", Schema = "matching")]
[Index("UserId", "IsRead", Name = "idx_notifications_unread")]
[Index("UserId", Name = "idx_notifications_user")]
public partial class MatchNotification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("match_id")]
    public Guid? MatchId { get; set; }

    [Column("type")]
    [StringLength(50)]
    public string Type { get; set; } = null!;

    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Column("message")]
    public string Message { get; set; } = null!;

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [ForeignKey("MatchId")]
    [InverseProperty("MatchNotifications")]
    public virtual PlayerMatch? Match { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("MatchNotifications")]
    public virtual User User { get; set; } = null!;
}
