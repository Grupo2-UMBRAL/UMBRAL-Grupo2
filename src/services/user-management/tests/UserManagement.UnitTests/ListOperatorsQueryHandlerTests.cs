using Moq;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Queries.ListOperators;

using Xunit;

namespace UserManagement.UnitTests;

public sealed class ListOperatorsQueryHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock = new();
    private readonly ListOperatorsQueryHandler _handler;

    public ListOperatorsQueryHandlerTests()
    {
        _handler = new ListOperatorsQueryHandler(_portMock.Object);
    }

    [Fact]
    public async Task Handle_OrdersActiveFirst_ThenByUsernameCaseInsensitive()
    {
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>
            {
                new("u-zoe", "zoe", "zoe@example.com", "Z", "Z", false),
                new("u-bravo", "Bravo", "bravo@example.com", "B", "B", true),
                new("u-alpha", "alpha", "alpha@example.com", "A", "A", true),
                new("u-yan", "Yan", "yan@example.com", "Y", "Y", false)
            });

        var result = await _handler.Handle(new ListOperatorsQuery(), CancellationToken.None);

        // Active group first (alpha, Bravo), inactive group second (Yan, zoe); username compare is case-insensitive.
        Assert.Equal(
            new[] { "alpha", "Bravo", "Yan", "zoe" },
            result.Select(OperatorDto => OperatorDto.Username).ToArray());
    }

    [Fact]
    public async Task Handle_EmptyPort_ReturnsEmpty()
    {
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());

        var result = await _handler.Handle(new ListOperatorsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }
}
