namespace ProHair.NL.Services
{
    public interface IAvailabilityService
    {
        Task<bool> IsSlotBookable(DateTimeOffset startUtc);
    }
}
