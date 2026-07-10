using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.UnitTests;

// In-memory IRepository<T> whose Provider is an IAsyncQueryProvider. The session
// handlers query through EntityFrameworkQueryableExtensions: `.Include(...)` is a
// no-op passthrough on a non-EF provider, and `.SingleOrDefaultAsync(...)` executes
// against this async provider — so the full handler path runs without a DbContext.
internal class FakeRepository<T>(IEnumerable<T> items) : IRepository<T>
    where T : class
{
    private readonly List<T> _items = items.ToList();
    private TestAsyncEnumerable<T> Query => new(_items);

    public Type ElementType => ((IQueryable<T>)Query).ElementType;

    public Expression Expression => ((IQueryable<T>)Query).Expression;

    public IQueryProvider Provider => ((IQueryable<T>)Query).Provider;

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(null);

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<T>>(_items);

    public Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.AsQueryable().SingleOrDefault(predicate));

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.AsQueryable().FirstOrDefault(predicate));

    public Task<IReadOnlyList<T>> GetAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<T>>(_items.AsQueryable().Where(predicate).ToList());

    public void Add(T entity) => _items.Add(entity);

    public void Update(T entity)
    {
    }

    public void Remove(T entity) => _items.Remove(entity);
}

// Canonical EF Core async test-double trio (Microsoft docs "Testing with a mocking
// framework") — lets the EF async LINQ extensions run over an in-memory sequence.
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

// Minimal TimeProvider returning a fixed instant for deterministic handler tests.
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeLiveSessionRepository(IEnumerable<SessionManagement.Domain.LiveSessions.LiveSession> items) 
    : FakeRepository<SessionManagement.Domain.LiveSessions.LiveSession>(items), ILiveSessionRepository
{
    public Task<SessionManagement.Domain.LiveSessions.LiveSession?> GetBySessionTeamIdWithEvidenceSubmissionsAsync(
        Guid sessionTeamId, 
        CancellationToken cancellationToken = default)
    {
        var session = this.FirstOrDefault(s => s.SessionTeams.Any(t => t.Id == sessionTeamId));
        return Task.FromResult(session);
    }

    public Task<SessionManagement.Domain.LiveSessions.LiveSession?> GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync(
        Guid submissionId, 
        CancellationToken cancellationToken = default)
    {
        var session = this.FirstOrDefault(s => s.EvidenceSubmissions.Any(e => e.Id == submissionId));
        return Task.FromResult(session);
    }
}
