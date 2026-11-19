using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CourtBookingManagement.Application.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("player_matches", Schema = "matching")]
[Index("BranchId", Name = "idx_player_matches_branch")]
[Index("MatchDate", Name = "idx_player_matches_date")]
[Index("Status", Name = "idx_player_matches_status")]
public partial class PlayerMatch
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("court_id")]
    public Guid? CourtId { get; set; }

    [Column("booking_id")]
    public Guid? BookingId { get; set; }

    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("room_code")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public string RoomCode { get; set; } = null!;

    [Column("match_date")]
    public DateOnly? MatchDate { get; set; }

    [Column("start_time")]
    public TimeOnly? StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly? EndTime { get; set; }

    [Column("game_type")]
    [StringLength(10)]
    public string? GameType { get; set; }

    [Column("minimum_rating")]
    public int? MinimumRating { get; set; }

    [Column("maximum_rating")]
    public int? MaximumRating { get; set; }

    [Column("gender_policy")]
    [StringLength(10)]
    public string? GenderPolicy { get; set; }

    [Column("required_gender")]
    [StringLength(10)]
    public string? RequiredGender { get; set; }

    [Column("approval_mode")]
    [StringLength(20)]
    public string ApprovalMode { get; set; } = "MANUAL";

    [Column("visibility")]
    [StringLength(20)]
    public string Visibility { get; set; } = "PUBLIC";

    [Column("skill_level")]
    [StringLength(30)]
    public string SkillLevel { get; set; } = null!;

    [Column("gender_preference")]
    [StringLength(20)]
    public string GenderPreference { get; set; } = GenderPreferences.Both;

    [Column("max_players")]
    public int MaxPlayers { get; set; }

    [Column("current_players")]
    public int CurrentPlayers { get; set; }

    [Column("fee_per_player")]
    [Precision(12, 2)]
    public decimal? FeePerPlayer { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("cancelled_by")]
    public Guid? CancelledBy { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("PlayerMatches")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("CourtId")]
    [InverseProperty("PlayerMatches")]
    public virtual Court? Court { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("PlayerMatches")]
    public virtual User CreatedByNavigation { get; set; } = null!;

    [InverseProperty("Match")]
    public virtual ICollection<MatchJoinRequest> MatchJoinRequests { get; set; } = new List<MatchJoinRequest>();

    [InverseProperty("Match")]
    public virtual ICollection<MatchNotification> MatchNotifications { get; set; } = new List<MatchNotification>();

    [InverseProperty("Match")]
    public virtual ICollection<MatchParticipant> MatchParticipants { get; set; } = new List<MatchParticipant>();
}
