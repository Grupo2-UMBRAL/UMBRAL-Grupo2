using System;
using System.Linq;
using System.Runtime.Serialization;
using MissionManagement.Domain.Missions;
using Xunit;

namespace MissionManagement.UnitTests;

public class MissionRehydrateTests
{
    [Fact]
    public void Rehydrate_WithValidFlatHierarchy_ReconstructsTreeProperly()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var searchId = Guid.NewGuid();
        var choiceId1 = Guid.NewGuid();
        var choiceId2 = Guid.NewGuid();
        var hintId = Guid.NewGuid();

        var section = (Section)Activator.CreateInstance(typeof(Section), true)!;
        SetProperty(section, "Id", sectionId);
        SetProperty(section, "Order", 1);
        SetProperty(section, "ParentSectionId", (Guid?)null);

        var challenge = (Challenge)Activator.CreateInstance(typeof(Challenge), true)!;
        SetProperty(challenge, "Id", challengeId);
        SetProperty(challenge, "Order", 1);
        SetProperty(challenge, "ParentSectionId", sectionId);
        SetProperty(challenge, "IsActive", true);
        SetProperty(challenge, "GameType", "trivia");
        SetProperty(challenge, "DefaultDifficulty", "medium");
        SetProperty(challenge, "DefaultTimeLimitMinutes", 5);

        var question = (Question)Activator.CreateInstance(typeof(Question), true)!;
        SetProperty(question, "Id", questionId);
        SetProperty(question, "Order", 1);
        SetProperty(question, "ChallengeId", challengeId);
        SetProperty(question, "Prompt", "What is 2+2?");

        var search = (Search)Activator.CreateInstance(typeof(Search), true)!;
        SetProperty(search, "Id", searchId);
        SetProperty(search, "Order", 2);
        SetProperty(search, "ChallengeId", challengeId);
        SetProperty(search, "Prompt", "Find the statue");

        var choice1 = (Choice)Activator.CreateInstance(typeof(Choice), true)!;
        SetProperty(choice1, "Id", choiceId1);
        SetProperty(choice1, "QuestionId", questionId);
        SetProperty(choice1, "Order", 1);
        SetProperty(choice1, "IsCorrect", false);

        var choice2 = (Choice)Activator.CreateInstance(typeof(Choice), true)!;
        SetProperty(choice2, "Id", choiceId2);
        SetProperty(choice2, "QuestionId", questionId);
        SetProperty(choice2, "Order", 2);
        SetProperty(choice2, "IsCorrect", true);

        var hint = (Hint)Activator.CreateInstance(typeof(Hint), true)!;
        SetProperty(hint, "Id", hintId);
        SetProperty(hint, "SearchId", searchId);
        SetProperty(hint, "Order", 1);

        // Act
        var mission = Mission.Rehydrate(
            id: missionId,
            name: "Rehydrated Mission",
            description: "Test",
            maximumDurationMinutes: 60,
            isActive: true,
            sections: new[] { section },
            challenges: new[] { challenge },
            plays: new Play[] { question, search },
            choices: new[] { choice1, choice2 },
            hints: new[] { hint }
        );

        // Assert
        Assert.NotNull(mission);
        Assert.Equal(missionId, mission.Id);
        Assert.Equal("Rehydrated Mission", mission.Name);
        Assert.Single(mission.RootItems);
        
        var rootSection = Assert.IsType<Section>(mission.RootItems.Single());
        Assert.Equal(sectionId, rootSection.Id);
        Assert.Single(rootSection.Children);

        var loadedChallenge = Assert.IsType<Challenge>(rootSection.Children.Single());
        Assert.Equal(challengeId, loadedChallenge.Id);
        Assert.Equal(2, loadedChallenge.Plays.Count);

        var loadedQuestion = Assert.IsType<Question>(loadedChallenge.Plays.First());
        Assert.Equal(2, loadedQuestion.Choices.Count);
        Assert.True(loadedQuestion.Choices[1].IsCorrect);

        var loadedSearch = Assert.IsType<Search>(loadedChallenge.Plays.Last());
        Assert.Single(loadedSearch.Hints);
    }

    [Fact]
    public void Rehydrate_WithNullArguments_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Mission.Rehydrate(Guid.NewGuid(), "N", "D", 60, true,
            null!, Array.Empty<Challenge>(), Array.Empty<Play>(), Array.Empty<Choice>(), Array.Empty<Hint>()));

        Assert.Throws<ArgumentNullException>(() => Mission.Rehydrate(Guid.NewGuid(), "N", "D", 60, true,
            Array.Empty<Section>(), null!, Array.Empty<Play>(), Array.Empty<Choice>(), Array.Empty<Hint>()));

        Assert.Throws<ArgumentNullException>(() => Mission.Rehydrate(Guid.NewGuid(), "N", "D", 60, true,
            Array.Empty<Section>(), Array.Empty<Challenge>(), null!, Array.Empty<Choice>(), Array.Empty<Hint>()));

        Assert.Throws<ArgumentNullException>(() => Mission.Rehydrate(Guid.NewGuid(), "N", "D", 60, true,
            Array.Empty<Section>(), Array.Empty<Challenge>(), Array.Empty<Play>(), null!, Array.Empty<Hint>()));

        Assert.Throws<ArgumentNullException>(() => Mission.Rehydrate(Guid.NewGuid(), "N", "D", 60, true,
            Array.Empty<Section>(), Array.Empty<Challenge>(), Array.Empty<Play>(), Array.Empty<Choice>(), null!));
    }

    [Fact]
    public void Rehydrate_EmptyCollections_ReturnsEmptyMission()
    {
        // Arrange
        var missionId = Guid.NewGuid();

        // Act
        var mission = Mission.Rehydrate(
            id: missionId,
            name: "Empty",
            description: "Empty Test",
            maximumDurationMinutes: 60,
            isActive: false,
            sections: Array.Empty<Section>(),
            challenges: Array.Empty<Challenge>(),
            plays: Array.Empty<Play>(),
            choices: Array.Empty<Choice>(),
            hints: Array.Empty<Hint>()
        );

        // Assert
        Assert.Empty(mission.RootItems);
    }

    private static void SetProperty(object obj, string propertyName, object? value)
    {
        var prop = obj.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
        }
        else
        {
            var field = obj.GetType().GetField($"<{propertyName}>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }
    }
}
