namespace CritterCrush.Profiles;

public record DogProfileCreated(Guid DogProfileId, string Name, string Breed, int AgeInMonths, Guid OwnerId);

/// <summary>
/// The Profiles write model — a self-aggregating fold over its own stream. Create/Apply are the
/// only mutators, owned by Marten; registered as an Inline snapshot so a caller's next GET sees
/// their own write.
/// </summary>
public class DogProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public int AgeInMonths { get; set; }
    public Guid OwnerId { get; set; }

    public static DogProfile Create(DogProfileCreated created) => new()
    {
        Id = created.DogProfileId,
        Name = created.Name,
        Breed = created.Breed,
        AgeInMonths = created.AgeInMonths,
        OwnerId = created.OwnerId,
    };
}
