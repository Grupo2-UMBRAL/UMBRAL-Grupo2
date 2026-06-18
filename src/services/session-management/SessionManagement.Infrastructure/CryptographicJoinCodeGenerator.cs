using System.Security.Cryptography;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Infrastructure;

public sealed class CryptographicJoinCodeGenerator : IJoinCodeGenerator
{
    public JoinCode Generate()
    {
        Span<char> characters = stackalloc char[JoinCode.Length];
        for (var index = 0; index < characters.Length; index++)
        {
            var alphabetIndex = RandomNumberGenerator.GetInt32(JoinCode.AllowedCharacters.Length);
            characters[index] = JoinCode.AllowedCharacters[alphabetIndex];
        }

        return JoinCode.Parse(new string(characters));
    }
}
