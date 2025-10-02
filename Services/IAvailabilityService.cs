using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProHair.NL.Services
{
    public interface IAvailabilityService
    {
        /// <summary>
        /// Business rules (open hours, blackout, min notice) check for a given UTC start time.
        /// </summary>
        Task<bool> IsSlotBookable(DateTimeOffset startUtc);

        /// <summary>
        /// Database overlap check for a stylist, considering confirmed appointments
        /// and active holds (HoldUntilUtc > now).
        /// </summary>
        Task<bool> IsSlotFreeAsync(
            int stylistId,
            DateTimeOffset startUtc,
            int durationMinutes,
            CancellationToken ct = default);
    }
}
