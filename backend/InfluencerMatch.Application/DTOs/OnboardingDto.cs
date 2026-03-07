namespace InfluencerMatch.Application.DTOs
{
    public class OnboardingChecklistItemDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Completed { get; set; }
    }

    public class RoleOnboardingChecklistDto
    {
        public string Role { get; set; } = string.Empty;
        public List<OnboardingChecklistItemDto> Items { get; set; } = new();
    }
}
