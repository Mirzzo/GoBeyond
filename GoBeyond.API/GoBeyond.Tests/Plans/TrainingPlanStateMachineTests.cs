using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;

namespace GoBeyond.Tests.Plans;

public class TrainingPlanStateMachineTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);

    private readonly TrainingPlanStateFactory _factory = new(
        [new DraftTrainingPlanState(), new PublishedTrainingPlanState(), new ArchivedTrainingPlanState()]);

    private static UpsertDayPlanRequest Day(string training = "Čučanj 4x8, bench press 4x8, veslanje 3x10.") => new()
    {
        TrainingDurationMinutes = 60,
        TrainingDescription = training,
        NutritionDurationMinutes = 30,
        NutritionDescription = "Doručak: zobene pahuljice 80 g, jaja 3 kom."
    };

    private static TrainingPlan Plan(TrainingPlanStatus status, int filledDays)
    {
        var plan = new TrainingPlan { Status = status, Version = 1 };
        for (var day = 1; day <= filledDays; day++)
            plan.Days.Add(new DayPlan { DayOfWeek = day, TrainingDescription = "Trening dana", NutritionDescription = "Ishrana dana" });
        return plan;
    }

    private BaseTrainingPlanState State(TrainingPlan plan) => _factory.GetState(plan.Status);

    [Theory]
    [InlineData(TrainingPlanStatus.Draft, typeof(DraftTrainingPlanState))]
    [InlineData(TrainingPlanStatus.Published, typeof(PublishedTrainingPlanState))]
    [InlineData(TrainingPlanStatus.Archived, typeof(ArchivedTrainingPlanState))]
    public void Factory_ResolvesStateForEveryStatus(TrainingPlanStatus status, Type expected)
    {
        Assert.IsType(expected, _factory.GetState(status));
    }

    [Fact]
    public void Draft_Publish_WithAllSevenDays_PublishesPlan()
    {
        var plan = Plan(TrainingPlanStatus.Draft, 7);

        State(plan).Publish(plan, Now);

        Assert.Equal(TrainingPlanStatus.Published, plan.Status);
        Assert.Equal(Now, plan.PublishedAt);
        Assert.Equal(1, plan.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void Draft_Publish_WithoutAllSevenDays_IsRejected(int filledDays)
    {
        var plan = Plan(TrainingPlanStatus.Draft, filledDays);

        var error = Assert.Throws<ValidationException>(() => State(plan).Publish(plan, Now));

        Assert.Equal("Plan mora imati popunjenih svih 7 dana prije objave.", error.Message);
        Assert.Equal(TrainingPlanStatus.Draft, plan.Status);
    }

    [Fact]
    public void Draft_UpsertDay_AddsNewDayAndUpdatesExistingOne()
    {
        var plan = Plan(TrainingPlanStatus.Draft, 1);

        State(plan).UpsertDay(plan, 2, Day(), Now);
        State(plan).UpsertDay(plan, 1, Day("Mrtvo dizanje 5x5, zgibovi 4x6."), Now);

        Assert.Equal(2, plan.Days.Count);
        Assert.Equal("Mrtvo dizanje 5x5, zgibovi 4x6.", plan.Days.Single(x => x.DayOfWeek == 1).TrainingDescription);
        Assert.Equal(1, plan.Version);
    }

    [Fact]
    public void Draft_RemoveDay_RemovesIt()
    {
        var plan = Plan(TrainingPlanStatus.Draft, 3);

        State(plan).RemoveDay(plan, 2, Now);

        Assert.DoesNotContain(plan.Days, x => x.DayOfWeek == 2);
    }

    [Fact]
    public void Draft_Archive_IsNotAllowed()
    {
        var plan = Plan(TrainingPlanStatus.Draft, 7);
        Assert.Throws<ValidationException>(() => State(plan).Archive(plan, Now));
    }

    [Fact]
    public void Published_UpsertDay_IncrementsVersion()
    {
        var plan = Plan(TrainingPlanStatus.Published, 7);

        State(plan).UpsertDay(plan, 3, Day(), Now);

        Assert.Equal(2, plan.Version);
        Assert.Equal(TrainingPlanStatus.Published, plan.Status);
    }

    [Fact]
    public void Published_RemoveDay_IsNotAllowed()
    {
        var plan = Plan(TrainingPlanStatus.Published, 7);
        Assert.Throws<ValidationException>(() => State(plan).RemoveDay(plan, 1, Now));
        Assert.Equal(7, plan.Days.Count);
    }

    [Fact]
    public void Published_PublishAgain_IsNotAllowed()
    {
        var plan = Plan(TrainingPlanStatus.Published, 7);
        Assert.Throws<ValidationException>(() => State(plan).Publish(plan, Now));
    }

    [Fact]
    public void Published_Archive_ArchivesPlan()
    {
        var plan = Plan(TrainingPlanStatus.Published, 7);

        State(plan).Archive(plan, Now);

        Assert.Equal(TrainingPlanStatus.Archived, plan.Status);
    }

    [Fact]
    public void Archived_Publish_RepublishesAndIncrementsVersion()
    {
        var plan = Plan(TrainingPlanStatus.Archived, 7);

        State(plan).Publish(plan, Now);

        Assert.Equal(TrainingPlanStatus.Published, plan.Status);
        Assert.Equal(2, plan.Version);
        Assert.Equal(Now, plan.PublishedAt);
    }

    [Fact]
    public void Archived_Archive_IsNotAllowed()
    {
        var plan = Plan(TrainingPlanStatus.Archived, 7);
        Assert.Throws<ValidationException>(() => State(plan).Archive(plan, Now));
    }

    [Fact]
    public void NotAllowedAction_MessageNamesTheStatusInBosnian()
    {
        var plan = Plan(TrainingPlanStatus.Archived, 7);
        var error = Assert.Throws<ValidationException>(() => State(plan).RemoveDay(plan, 1, Now));
        Assert.Contains("Arhiviran", error.Message);
    }
}
