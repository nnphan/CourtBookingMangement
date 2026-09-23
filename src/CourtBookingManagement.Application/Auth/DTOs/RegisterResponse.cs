using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CourtBookingManagement.Application.Auth.DTOs
{
    public sealed class RegisterResponse
    {
        public CurrentUserResponse User { get; init; } = new();
    }
}
