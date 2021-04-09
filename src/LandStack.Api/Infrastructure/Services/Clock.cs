using System;

namespace LandStack.Api.Infrastructure.Services
{
    public interface IClock
    {
        DateTime Now { get; }
    }

    public class Clock : IClock
    {
        public DateTime Now => new DateTime(DateTime.UtcNow.Ticks);
    }
}