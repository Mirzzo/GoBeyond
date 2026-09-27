using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.Entities;

namespace GoBeyond.Infrastructure.Services.Users;

/// <summary>Primjena (već validiranih) izmjena na entitete korisnika i profila; dijele ga profil i admin.</summary>
public static class ProfileUpdater
{
    public static void ApplyAccount(User user, AccountFieldsRequest request)
    {
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.GenderId = request.GenderId;
    }

    /// <summary>Izmjena mentorskih podataka ne mijenja status odobrenja.</summary>
    public static void ApplyMentor(MentorProfile mentor, MentorProfileRequest request)
    {
        mentor.TrainingTypeId = request.TrainingTypeId;
        mentor.Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? null : request.Nickname.Trim();
        mentor.Bio = request.Bio.Trim();
        mentor.YearsOfExperience = request.YearsOfExperience;
        mentor.MonthlyPrice = request.MonthlyPrice;

        var wanted = request.SpecializationIds.Distinct().ToHashSet();
        foreach (var existing in mentor.Specializations.Where(x => !wanted.Contains(x.FitnessGoalId)).ToList())
            mentor.Specializations.Remove(existing);
        foreach (var goalId in wanted.Where(id => mentor.Specializations.All(x => x.FitnessGoalId != id)))
            mentor.Specializations.Add(new MentorSpecialization { FitnessGoalId = goalId });
    }

    public static void ApplyClient(ClientProfile client, ClientProfileRequest request)
    {
        client.WeightKg = request.WeightKg;
        client.HeightCm = request.HeightCm;
        client.FitnessLevelId = request.FitnessLevelId;
        client.TrainingExperienceYears = request.TrainingExperienceYears;
        client.FitnessGoalId = request.FitnessGoalId;
        client.GoalDescription = string.IsNullOrWhiteSpace(request.GoalDescription) ? null : request.GoalDescription.Trim();
        client.PreferredTrainingTypeId = request.PreferredTrainingTypeId;
    }
}
