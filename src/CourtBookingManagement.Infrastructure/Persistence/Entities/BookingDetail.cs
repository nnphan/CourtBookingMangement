using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("booking_details", Schema = "booking")]
[Index("BookingId", Name = "ix_booking_details_booking")]
[Index("CourtId", "BookingDate", Name = "ix_booking_details_court_date")]
public partial class BookingDetail
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("booking_id")]
    public Guid BookingId { get; set; }

    [Column("court_id")]
    public Guid CourtId { get; set; }

    [Column("booking_date")]
    public DateOnly BookingDate { get; set; }

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; }

    [Column("slot_range")]
    public NpgsqlRange<DateTime> SlotRange { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [ForeignKey("BookingId")]
    [InverseProperty("BookingDetails")]
    public virtual Booking Booking { get; set; } = null!;

    [Column("price_charged")]
    [Precision(10, 2)]
    public decimal PriceCharged { get; set; }

    [ForeignKey("CourtId")]
    [InverseProperty("BookingDetails")]
    public virtual Court Court { get; set; } = null!;
}
