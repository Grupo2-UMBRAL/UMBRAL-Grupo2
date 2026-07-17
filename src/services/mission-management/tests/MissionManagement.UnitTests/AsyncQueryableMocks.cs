using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query;
using Moq;
using Microsoft.EntityFrameworkCore;

namespace MissionManagement.UnitTests;

internal sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = ((IQueryProvider)this)
            .GetType()
            .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
            .MakeGenericMethod(expectedResultType)
            .Invoke(this, [expression]);

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, [executionResult])!;
    }
}

internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    {
    }

    public TestAsyncEnumerable(Expression expression)
        : base(expression)
    {
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }
}

public static class DbSetMockExtensions
{
    public static Mock<DbSet<T>> SetupDbSetMock<T>(this IEnumerable<T> source) where T : class
    {
        var queryable = new TestAsyncEnumerable<T>(source);
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(((IQueryable<T>)queryable).Provider);
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(((IQueryable<T>)queryable).Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(((IQueryable<T>)queryable).ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => ((IEnumerable<T>)queryable).GetEnumerator());
        mockSet.As<IAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(() => queryable.GetAsyncEnumerator());
        return mockSet;
    }
}
