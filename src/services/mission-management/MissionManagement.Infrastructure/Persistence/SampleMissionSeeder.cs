using Microsoft.EntityFrameworkCore;
using MissionManagement.Application.Features.Missions;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence;

/// <summary>
/// Dev-only seeder that loads one ready-to-play Mission of each shape — Trivia, Treasure Hunt and a
/// Mixed mission combining both — so a game can be created and played end to end without authoring
/// content by hand. Missions are built through the domain factories (so all invariants hold) and left
/// <see cref="Mission.Activate">activated</see> and eligible for a LiveSession. Idempotent: missions
/// already present (matched by Name) are skipped, so it is safe to run on every startup.
/// </summary>
/// <remarks>
/// Treasure-hunt QR codes are matched as plain case-insensitive text (no hashing), so the
/// <c>ExpectedQrHash</c> values below are memorable strings: encode them in any QR generator and scan
/// from the mobile app to complete the stage.
/// </remarks>
public static class SampleMissionSeeder
{
    public static async Task SeedAsync(MissionManagementDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var existingNames = await dbContext.Missions
            .Select(mission => mission.Name)
            .ToListAsync(cancellationToken);
        var existing = existingNames.ToHashSet(StringComparer.Ordinal);

        var builders = new Func<Mission>[] { BuildTrivia, BuildTreasureHunt, BuildMixed };

        var added = false;
        foreach (var build in builders)
        {
            var mission = build();
            if (existing.Contains(mission.Name))
            {
                continue;
            }

            mission.Activate();
            dbContext.Missions.Add(mission);
            MissionLoader.AddItems(dbContext, mission);
            added = true;
        }

        if (added)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static Mission BuildTrivia()
    {
        var missionId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();

        var challenge = Challenge.Create(
            challengeId, missionId, null, 1, "Preguntas de cultura general",
            MissionGameType.Trivia, Difficulty.Easy, 5, true,
            new Play[]
            {
                MultipleChoice(challengeId, 1, "¿Cuál es la capital de Venezuela?",
                    ("Caracas", true), ("Maracaibo", false), ("Valencia", false)),
                MultipleChoice(challengeId, 2, "¿Cuántos planetas tiene el sistema solar?",
                    ("8", true), ("9", false), ("7", false)),
                MultipleChoice(challengeId, 3, "¿Qué lenguaje corre el backend de UMBRAL?",
                    ("C#", true), ("Java", false), ("Python", false))
            });

        return Mission.Create(missionId, "Trivia: Cultura General",
            "Misión de prueba de tipo Trivia con tres preguntas de opción múltiple.", 15,
            new PathItem[] { challenge });
    }

    private static Mission BuildTreasureHunt()
    {
        var missionId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var firstSearchId = Guid.NewGuid();

        var challenge = Challenge.Create(
            challengeId, missionId, null, 1, "Recorrido por el campus",
            MissionGameType.TreasureHunt, Difficulty.Medium, 20, true,
            new Play[]
            {
                Search.Create(firstSearchId, challengeId, 1,
                    "Escaneá el código de la entrada principal.", "UMBRAL-TESORO-1", null, null,
                    new[]
                    {
                        Hint.Create(Guid.NewGuid(), firstSearchId, 1, "Está junto al portón de acceso.", false, 10.5, -66.9)
                    }),
                Search.Create(Guid.NewGuid(), challengeId, 2,
                    "Buscá la fuente del patio central y escaneá su código.", "UMBRAL-TESORO-2", null, null, null),
                Search.Create(Guid.NewGuid(), challengeId, 3,
                    "El tesoro final está en la biblioteca. Escaneá el código.", "UMBRAL-TESORO-3", null, null, null)
            });

        return Mission.Create(missionId, "Búsqueda del Tesoro: Campus",
            "Misión de prueba de tipo Búsqueda del Tesoro. Escaneá los códigos UMBRAL-TESORO-1..3 desde el móvil.", 60,
            new PathItem[] { challenge });
    }

    private static Mission BuildMixed()
    {
        var missionId = Guid.NewGuid();

        var sectionId = Guid.NewGuid();
        var huntId = Guid.NewGuid();
        var firstSearchId = Guid.NewGuid();
        var hunt = Challenge.Create(
            huntId, missionId, sectionId, 1, "Pistas en el terreno",
            MissionGameType.TreasureHunt, Difficulty.Easy, 15, true,
            new Play[]
            {
                Search.Create(firstSearchId, huntId, 1,
                    "Encontrá la estatua de la plaza y escaneá su código.", "UMBRAL-MIXTA-1", null, null,
                    new[]
                    {
                        Hint.Create(Guid.NewGuid(), firstSearchId, 1, "Cerca de la plaza principal.", false, 10.5, -66.9)
                    }),
                Search.Create(Guid.NewGuid(), huntId, 2,
                    "Escaneá el código del mural histórico.", "UMBRAL-MIXTA-2", null, null, null)
            });
        var section = Section.Create(sectionId, missionId, null, 1, "Etapa 1: Búsqueda",
            new PathItem[] { hunt });

        var triviaId = Guid.NewGuid();
        var trivia = Challenge.Create(
            triviaId, missionId, null, 2, "Etapa 2: Preguntas",
            MissionGameType.Trivia, Difficulty.Medium, 10, true,
            new Play[]
            {
                MultipleChoice(triviaId, 1, "¿En qué año fue fundada la ciudad? (pista del mural)",
                    ("1567", true), ("1492", false), ("1810", false)),
                MultipleChoice(triviaId, 2, "¿De qué material es la estatua de la plaza?",
                    ("Bronce", true), ("Mármol", false), ("Madera", false))
            });

        return Mission.Create(missionId, "Misión Mixta: Aventura Completa",
            "Misión de prueba mixta: una sección de Búsqueda del Tesoro seguida de un desafío de Trivia.", 90,
            new PathItem[] { section, trivia });
    }

    private static Question MultipleChoice(
        Guid challengeId,
        int order,
        string text,
        params (string Text, bool IsCorrect)[] choices)
    {
        var questionId = Guid.NewGuid();
        var built = choices
            .Select((choice, index) => Choice.Create(Guid.NewGuid(), questionId, index + 1, choice.Text, choice.IsCorrect))
            .ToArray();

        return Question.Create(questionId, challengeId, order, text, null, null, built);
    }
}
