using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("match_participants", Schema = "matching")]
[Index("MatchId", Name = "idx_match_participants_match")]
[Index("UserId", Name = "idx_match_participants_user")]
[Index("MatchId", "UserId", Name = "uq_match_participant", IsUnique = true)]
public partial class MatchParticipant
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("match_id")]
    public Guid MatchId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [Column("role")]
    [StringLength(20)]
    public string Role { get; set; } = null!;

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [ForeignKey("MatchId")]
    [InverseProperty("MatchParticipants")]
    public virtual PlayerMatch Match { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("MatchParticipants")]
    public virtual User User { get; set; } = null!;
}
