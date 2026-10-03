You are a Principal .NET Solution Architect with 15+ years of experience in:

- .NET 8
- ASP.NET Core Web API
- PostgreSQL
- Dapper
- SOLID Principles
- Clean Architecture
- REST API Design
- Enterprise Software Development
- High Performance Back-end Systems

I am building a Badminton Court Booking Management System.
I am implementing court booking API for guest customer who did not register account.
Suggest workflow:

Implement using:

React UI
↓
POST /api/court-bookings
↓
CourtBookingController
↓
CourtBookingService
↓
CourtBookingRepository (Dapper)
↓
PostgreSQL Function
↓
Database

There are existing Enitities: Booking, BookingDetail in CourtBookingManagement.Infrastructure\Persistence\Entities

1. Business Workflow

Khách hàng:
B1. Chọn chi nhánh
B2. Chọn sân
B3. Chọn ngày
B4. Chọn khung giờ
B5. Nhập:

- Họ tên
- Số điện thoại
- Ghi chú
  B6. Đặt lịch

B7. Hệ thống kiểm tra:
System validates:

✓ Branch exists

✓ Court exists

✓ Court belongs to branch

✓ Court is active

✓ Court is not blocked

✓ Booking date must be today or future date

✓ StartTime < EndTime

✓ Booking time is within operating hours

✓ No overlapping booking

✓ Phone number format is valid

B8: Chọn hình thức thanh toán: Tiền mặt (default), chức năng thanh toán online qua QR code sẽ triển kahi sau.

B8. Tạo booking

B9. Trả booking code, Payment status ("Chưa thanh toán") .

2. API Design
   Endpoint
   POST /api/court-bookings

Request DTO

```c#
public sealed record CreateCourtBookingRequest
{
public Guid BranchId { get; init; }

    public Guid CourtId { get; init; }

    public DateOnly BookingDate { get; init; }

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public string CustomerName { get; init; }
        = string.Empty;

    public string PhoneNumber { get; init; }
        = string.Empty;

    public string? Note { get; init; }

    public string PaymentMethod { get; init; }
        = "Cash";
}
```

Example

```json
{
  "branchId": "ef5cfc2e-4ef1-4c0f-8ac1-f1cfa6d6d001",
  "courtId": "ef5cfc2e-4ef1-4c0f-8ac1-f1cfa6d6d011",
  "bookingDate": "2026-09-24",
  "startTime": "18:00",
  "endTime": "20:00",
  "customerName": "Nguyen Van A",
  "phoneNumber": "0903757746",
  "note": "Booking from website",
  "paymentMethod": "Cash"
}
```

Define the PaymentMethodEnum in the Domain Layer

```c#
public enum PaymentMethod
{
Cash,
BankTransfer,
MoMo
}
```

PaymentMethod as string in PostgreSQL.
Configure Dapper Type Handler for String Enums (Infrastructure Layer)
Because Dapper doesn't automatically convert C# enums to their string representations for PostgreSQL by default, you need a custom Type Handler in your Infrastructure layer.
This handler converts the C# Enum to its string name when saving to the database, and parses the string back into the C# Enum when reading.

```c#
// Infrastructure/Persistence/DapperHandlers/EnumTypeHandlers.cs

public class PaymentMethodTypeHandler : SqlMapper.TypeHandler<PaymentMethod>
{
    public override void SetValue(IDbDataParameter parameter, PaymentMethod value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString(); // Converts Enum to String (e.g., "VNPay")
    }

    public override PaymentMethod Parse(object value)
    {
        return value switch
        {
            string s when Enum.TryParse<PaymentMethod>(s, true, out var result) => result,
            _ => throw new ArgumentException($"Unable to map database value '{value}' to PaymentMethod enum.")
        };
    }
}

```

Register the Handler in Infrastructure

```c#
// Infrastructure/DependencyInjection.cs

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register the string type handler for PaymentMethod
        SqlMapper.AddTypeHandler(new PaymentMethodTypeHandler());

        // ... register other handlers and services
        return services;
    }
}
```

Response DTO

```c#
public sealed record CreateCourtBookingResponse
{
    public Guid BookingId { get; init; }

    public string BookingCode { get; init; }
        = string.Empty;

    public string BookingStatus { get; init; }
        = string.Empty;

    public string CustomerName { get; init; }
        = string.Empty;

    public decimal TotalAmount { get; init; }

    public DateOnly BookingDate { get; init; }

    public string BranchName { get; init; }
        = string.Empty;

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public string PaymentMethod { get; init; }
        = "Cash";

    public DateTime CreatedAt { get; init; }

}
```

Example

```json
{
  "bookingId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "bookingCode": "BK-20260607-042",
  "bookingStatus": "Pending",
  "customerName": "Nguyen Van A",
  "totalAmount": 150000.0,
  "bookingDate": "2026-06-07",
  "branchName": "Badminton Arena - District 7",
  "startTime": "14:00:00",
  "endTime": "16:00:00",
  "paymentMethod": "MoMo",
  "createdAt": "2026-06-07T07:15:30.1234567Z"
}
```

Define the BookingStatusEnum in the Domain Layer
BookingStatus stored as string in PostgreSQL.

```c#
public enum BookingStatus
{
    Pending = 0, // Created, waiting for payment or confirmation
    Confirmed = 1, // Payment successful / Booked successfully
    CheckedIn = 2, // Player arrived and started playing
    Completed = 3, // Session finished successfully
    Cancelled = 4, // Cancelled by user or admin
    Refunded = 5 // Cancelled and money returned
}
```

There are existing PostgreSQL Function Validate Court Availability and Create Booking

Validate Court Availability

```sql
   CREATE OR REPLACE FUNCTION booking.fn_validate_court_availability
   (
    p_court_id UUID
    ,p_booking_date DATE
    ,p_start_time TIME
    ,p_end_time TIME
   )
   RETURNS BOOLEAN

```

Create Booking

```sql
CREATE OR REPLACE FUNCTION booking.fn_create_guest_booking
(
    p_branch_id UUID,
    p_court_id UUID,
    p_booking_date DATE,
    p_start_time TIME,
    p_end_time TIME,
    p_customer_name VARCHAR,
    p_phone_number VARCHAR,
    p_note TEXT,
    p_payment_method VARCHAR
)
RETURNS TABLE
(
    booking_id UUID,
    booking_code VARCHAR,
    booking_status VARCHAR,
    customer_name VARCHAR,
    total_amount NUMERIC,
    payment_method VARCHAR,
    booking_date DATE,
    branch_name VARCHAR,
    start_time TIME,
    end_time TIME,
    created_at TIMESTAMPTZ
)
```

API bổ sung nên triển khai

Kiểm tra sân trống
POST /api/court-bookings/check-availability
Request:

```json
{
  "courtId": "xxx",
  "bookingDate": "2026-09-24",
  "startTime": "18:00",
  "endTime": "20:00"
}
```

Tra cứu booking bằng SĐT
POST /api/court-bookings/search

Request:

```json
{
  "phoneNumber": "0903757746"
}
```

Hủy booking
POST /api/court-bookings/cancel

Request:

```json
{
  "bookingCode": "BK202609240001",
  "phoneNumber": "0903757746"
}
```

=================================================
APPLICATION LAYER
=================================================

Generate:

CreateCourtBookingRequest

CreateCourtBookingResponse

ICourtBookingService

CourtBookingService

Validation Rules

Domain Constants

=================================================
INFRASTRUCTURE LAYER
=================================================

Generate:

ICourtBookingRepository

CourtBookingRepository

Dapper implementation

Connection factory

Database function execution

=================================================
API LAYER
=================================================

Generate:

CourtBookingController

Endpoint:

POST /api/court-bookings

Use:

[AllowAnonymous]

=================================================
VALIDATION
=================================================

Use FluentValidation.

Rules:

BranchId:

- Required

CourtId:

- Required

BookingDate:

- Must not be in the past

CustomerName:

- Required
- Max 200 characters

PhoneNumber:

- Required
- Vietnam phone format

StartTime:

- Required

EndTime:

- Required
- Must be greater than StartTime

Note:

- Optional
- Max 1000 characters

=================================================
ERROR HANDLING
=================================================

Return common API response.

Court does not exist:

{
"success": false,
"message": "Court not found"
}

Court already booked:

{
"success": false,
"message": "Court is already booked in selected time range"
}

Court blocked:

{
"success": false,
"message": "Court is blocked"
}

Outside operating hours:

{
"success": false,
"message": "Booking time is outside operating hours"
}

=================================================
API RESPONSE WRAPPER
=================================================

{
"success": true,
"message": "Booking created successfully",
"data": {}
}

=================================================
PRODUCTION REQUIREMENTS
=================================================

Follow:

- SOLID Principles
- Clean Code
- Repository Pattern
- Dependency Injection
- Dapper Best Practices
- PostgreSQL Best Practices
- Async/Await
- CancellationToken
- Structured Logging
- Correlation Id Support
