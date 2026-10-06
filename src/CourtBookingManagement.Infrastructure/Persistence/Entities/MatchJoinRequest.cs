using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("match_join_requests", Schema = "matching")]
[Index("MatchId", Name = "idx_join_requests_match")]
[Index("MatchId", "RequesterId", Name = "uq_join_request", IsUnique = true)]
public partial class MatchJoinRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("match_id")]
    public Guid MatchId { get; set; }

    [Column("requester_id")]
    public Guid RequesterId { get; set; }

    [Column("note")]
    [StringLength(500)]
    public string? Note { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [ForeignKey("MatchId")]
    [InverseProperty("MatchJoinRequests")]
    public virtual PlayerMatch Match { get; set; } = null!;

    [ForeignKey("RequesterId")]
    [InverseProperty("MatchJoinRequestRequesters")]
    public virtual User Requester { get; set; } = null!;

    [ForeignKey("ReviewedBy")]
    [InverseProperty("MatchJoinRequestReviewedByNavigations")]
    public virtual User? ReviewedByNavigation { get; set; }
}
