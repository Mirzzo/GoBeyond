using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Users;

/// <summary>
/// Poslovne provjere korisničkih podataka koje DataAnnotations ne mogu uraditi:
/// jedinstvenost korisničkog imena/emaila i postojanje odabranih stavki iz šifarnika.
/// </summary>
public interface IUserAccountValidator
{
    Task ValidateAccountAsync(ValidationErrorCollector errors, AccountFieldsRequest request, int? currentUserId, CancellationToken cancellationToken);

    Task ValidateMentorAsync(ValidationErrorCollector errors, int trainingTypeId, IReadOnlyCollection<int> specializationIds, string prefix, CancellationToken cancellationToken);

    Task ValidateClientAsync(ValidationErrorCollector errors, int fitnessLevelId, int fitnessGoalId, int? preferredTrainingTypeId, string prefix, CancellationToken cancellationToken);
}

public sealed class UserAccountValidator(GoBeyondDbContext db) : IUserAccountValidator
{
    public async Task ValidateAccountAsync(ValidationErrorCollector errors, AccountFieldsRequest request, int? currentUserId,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        errors.Require(!await db.Users.AnyAsync(x => x.Username == username && x.Id != currentUserId, cancellationToken),
            "username", "Korisničko ime je već zauzeto.");
        errors.Require(!await db.Users.AnyAsync(x => x.Email == email && x.Id != currentUserId, cancellationToken),
            "email", "Email adresa je već registrovana.");
        errors.Require(await db.Genders.AnyAsync(x => x.Id == request.GenderId, cancellationToken),
            "genderId", "Odabrani spol ne postoji.");
    }

    public async Task ValidateMentorAsync(ValidationErrorCollector errors, int trainingTypeId, IReadOnlyCollection<int> specializationIds,
        string prefix, CancellationToken cancellationToken)
    {
        errors.Require(await db.TrainingTypes.AnyAsync(x => x.Id == trainingTypeId, cancellationToken),
            prefix + "trainingTypeId", "Odabrana vrsta treninga ne postoji.");

        var distinct = specializationIds.Distinct().ToList();
        var existing = await db.FitnessGoals.CountAsync(x => distinct.Contains(x.Id), cancellationToken);
        errors.Require(existing == distinct.Count, prefix + "specializationIds", "Odabrana specijalizacija ne postoji.");
    }

    public async Task ValidateClientAsync(ValidationErrorCollector errors, int fitnessLevelId, int fitnessGoalId,
        int? preferredTrainingTypeId, string prefix, CancellationToken cancellationToken)
    {
        errors.Require(await db.FitnessLevels.AnyAsync(x => x.Id == fitnessLevelId, cancellationToken),
            prefix + "fitnessLevelId", "Odabrani nivo fizičke spreme ne postoji.");
        errors.Require(await db.FitnessGoals.AnyAsync(x => x.Id == fitnessGoalId, cancellationToken),
            prefix + "fitnessGoalId", "Odabrani fitness cilj ne postoji.");
        if (preferredTrainingTypeId is { } typeId)
            errors.Require(await db.TrainingTypes.AnyAsync(x => x.Id == typeId, cancellationToken),
                prefix + "preferredTrainingTypeId", "Odabrana vrsta treninga ne postoji.");
    }
}
