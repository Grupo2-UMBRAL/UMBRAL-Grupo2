using MissionManagement.Domain.Missions;

namespace MissionManagement.UnitTests;

/// <summary>Reusable sample aggregates for the new linear-path model.</summary>
public static class SampleMissions
{
    public static Mission SingleTreasureHunt(Guid missionId, string name)
    {
        var challengeId = Guid.NewGuid();
        var search = Search.Create(Guid.NewGuid(), challengeId, 1, "Scan the opening gate", "qr-hash-1", null, null, null);
        var challenge = Challenge.Create(
            challengeId, missionId, null, 1, "Gate Hunt", MissionGameType.TreasureHunt, Difficulty.Easy, 20, true,
            new Play[] { search });

        return Mission.Create(missionId, name, "Description.", 25, new PathItem[] { challenge });
    }

    public static Mission SingleTrivia(Guid missionId, string name)
    {
        var challengeId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var question = Question.Create(
            questionId, challengeId, 1, "Capital of Venezuela?", null, null,
            new[]
            {
                Choice.Create(Guid.NewGuid(), questionId, 1, "Caracas", true),
                Choice.Create(Guid.NewGuid(), questionId, 2, "Maracaibo", false)
            });
        var challenge = Challenge.Create(
            challengeId, missionId, null, 1, "Quiz", MissionGameType.Trivia, Difficulty.Medium, 10, true,
            new Play[] { question });

        return Mission.Create(missionId, name, "Description.", 30, new PathItem[] { challenge });
    }

    /// <summary>
    /// Root order 1 = a Section containing a treasure-hunt Challenge (2 searches);
    /// root order 2 = an active trivia Challenge (1 question, Hard override);
    /// root order 3 = an INACTIVE trivia Challenge (skipped by the flatten).
    /// </summary>
    public static Mission Mixed(Guid missionId)
    {
        var sectionId = Guid.NewGuid();

        var huntId = Guid.NewGuid();
        var firstSearchId = Guid.NewGuid();
        var hunt = Challenge.Create(
            huntId, missionId, sectionId, 1, "Hunt", MissionGameType.TreasureHunt, Difficulty.Easy, 15, true,
            new Play[]
            {
                Search.Create(firstSearchId, huntId, 1, "Find the fountain", "qr-1", null, null,
                    new[] { Hint.Create(Guid.NewGuid(), firstSearchId, 1, "Near the plaza", false, 10.5, -66.9) }),
                Search.Create(Guid.NewGuid(), huntId, 2, "Find the statue", "qr-2", null, null, null)
            });
        var section = Section.Create(sectionId, missionId, null, 1, "Downtown", new PathItem[] { hunt });

        var triviaId = Guid.NewGuid();
        var triviaQuestionId = Guid.NewGuid();
        var trivia = Challenge.Create(
            triviaId, missionId, null, 2, "Quiz", MissionGameType.Trivia, Difficulty.Easy, 10, true,
            new Play[]
            {
                Question.Create(triviaQuestionId, triviaId, 1, "Capital of Venezuela?", Difficulty.Hard, null,
                    new[]
                    {
                        Choice.Create(Guid.NewGuid(), triviaQuestionId, 1, "Caracas", true),
                        Choice.Create(Guid.NewGuid(), triviaQuestionId, 2, "Valencia", false)
                    })
            });

        var inactiveId = Guid.NewGuid();
        var inactiveQuestionId = Guid.NewGuid();
        var inactive = Challenge.Create(
            inactiveId, missionId, null, 3, "Skipped", MissionGameType.Trivia, Difficulty.Easy, 10, isActive: false,
            new Play[]
            {
                Question.Create(inactiveQuestionId, inactiveId, 1, "Skipped?", null, null,
                    new[]
                    {
                        Choice.Create(Guid.NewGuid(), inactiveQuestionId, 1, "Yes", true),
                        Choice.Create(Guid.NewGuid(), inactiveQuestionId, 2, "No", false)
                    })
            });

        return Mission.Create(missionId, "Tree Mission", "Ready for session.", 60,
            new PathItem[] { section, trivia, inactive });
    }
}
