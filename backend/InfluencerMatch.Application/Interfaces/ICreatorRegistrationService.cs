using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ICreatorRegistrationService
    {
        /// <summary>
        /// Registers a new creator: creates a User (Role="Creator") + linked CreatorProfile.
        /// Returns a JWT token and the new profile ID.
        /// </summary>
        Task<CreatorRegisterResponseDto> RegisterCreatorAsync(CreatorRegisterRequestDto dto);

        /// <summary>Returns the CreatorProfile for the given user, or null if none exists.</summary>
        Task<CreatorProfileDto?> GetProfileAsync(int userId);

        /// <summary>Updates mutable profile fields (language, category, bio, etc.).</summary>
        Task<CreatorProfileDto> UpdateProfileAsync(int userId, UpdateCreatorProfileDto dto);

        /// <summary>Returns creator-first onboarding and intelligence status for dashboard checklist.</summary>
        Task<CreatorOnboardingStatusDto> GetOnboardingStatusAsync(int userId, CancellationToken ct = default);
    }
}
