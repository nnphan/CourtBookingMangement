using CourtBookingManagement.Domain.Bookings;
using Dapper;
using System.Data;

namespace CourtBookingManagement.Infrastructure.Persistence.DapperHandlers;

public sealed class PaymentMethodTypeHandler : SqlMapper.TypeHandler<PaymentMethod>
{
    public override void SetValue(IDbDataParameter parameter, PaymentMethod value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }

    public override PaymentMethod Parse(object value) =>
        Enum.TryParse<PaymentMethod>(value.ToString(), true, out var result)
            ? result
            : throw new ArgumentException($"Unable to map database value '{value}' to PaymentMethod.");
}